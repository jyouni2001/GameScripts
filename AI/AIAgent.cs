using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JY;
using UnityEngine.Animations;

namespace JY
{
public interface IRoomDetector
{
    GameObject[] GetDetectedRooms();
    void DetectRooms();
}

public class AIAgent : MonoBehaviour
{
    #region 비공개 변수
    private NavMeshAgent agent;                    // AI 이동 제어를 위한 네비메시 에이전트
    private RoomManager roomManager;               // 룸 매니저 참조
    private Transform counterPosition;             // 카운터 위치
    private static List<RoomInfo> roomList = new List<RoomInfo>();  // 동적 룸 정보 리스트
    private Transform spawnPoint;                  // AI 생성/소멸 지점
    private int currentRoomIndex = -1;            // 현재 사용 중인 방 인덱스 (-1은 미사용)
    private AISpawner spawner;                    // AI 스포너 참조
    private float arrivalDistance = 0.5f;         // 도착 판정 거리

    private bool isInQueue = false;               // 대기열에 있는지 여부
    private Vector3 targetQueuePosition;          // 대기열 목표 위치
    private Quaternion targetQueueRotation;       // 대기열 목표 방향
    private bool isWaitingForService = false;     // 서비스 대기 중인지 여부

    private AIState currentState = AIState.MovingToQueue;  // 현재 AI 상태
    private string currentDestination = "대기열로 이동 중";  // 현재 목적지 (UI 표시용)

    private static readonly object lockObject = new object();  // 스레드 동기화용 잠금 객체
    private Coroutine wanderingCoroutine;         // 배회 코루틴 참조
    private Coroutine roomUseCoroutine;           // 방 사용 코루틴 참조
    private Coroutine roomWanderingCoroutine;     // 방 내부 배회 코루틴 참조

    private Coroutine useWanderingCoroutine;  // 방 외부 배회 코루틴 참조
    private Coroutine queueCoroutine;         // 대기열 코루틴 참조
    private int maxRetries = 3;                   // 위치 찾기 최대 시도 횟수

    [SerializeField] private CounterManager counterManager; // CounterManager 참조
    private TimeSystem timeSystem;                // 시간 시스템 참조
    private int lastBehaviorUpdateHour = -1;      // 마지막 행동 업데이트 시간
    private bool isScheduledForDespawn = false;   // 11시 디스폰 예정인지 여부
    
    // 침대 관련 변수들
    private Transform currentBedTransform;        // 현재 사용 중인 침대 Transform
    private Vector3 preSleepPosition;            // 수면 전 위치 저장
    private Quaternion preSleepRotation;         // 수면 전 회전값 저장
    private bool isSleeping = false;             // 수면 중인지 여부
    private Coroutine sleepingCoroutine;         // 수면 코루틴 참조
    private Animator animator;                   // 애니메이터 컴포넌트
    
    // 선베드 관련 변수들
    private Transform currentSunbedTransform;    // 현재 사용 중인 선베드 Transform
    private Vector3 preSunbedPosition;          // 선베드 사용 전 위치 저장
    private Quaternion preSunbedRotation;       // 선베드 사용 전 회전값 저장
    private bool isUsingSunbed = false;         // 선베드 사용 중인지 여부
    private Coroutine sunbedCoroutine;          // 선베드 관련 코루틴 참조
    
    // 식당 관련 변수들
    private Transform currentKitchenTransform;  // 현재 식당 Transform
    private Transform currentChairTransform;    // 현재 사용 중인 의자 Transform
    private Vector3 preChairPosition;           // 의자 사용 전 위치 저장
    private Quaternion preChairRotation;       // 의자 사용 전 회전값 저장
    private bool isEating = false;              // 식사 중인지 여부
    private Coroutine eatingCoroutine;         // 식사 관련 코루틴 참조
    
    // 주방 카운터 관련 변수들
    private KitchenCounter currentKitchenCounter; // 현재 사용 중인 주방 카운터
    private bool isWaitingAtKitchenCounter = false; // 주방 카운터에서 대기 중인지 여부
    
    // 주방 카운터 관련 공개 프로퍼티
    public bool IsWaitingAtKitchenCounter => isWaitingAtKitchenCounter;
    
    [Header("UI 디버그")]
    [Tooltip("모든 AI 머리 위에 행동 상태 텍스트 표시")]
    [SerializeField] private bool debugUIEnabled = true;
    
    // 모든 AI가 공유하는 static 변수
    private static bool globalShowDebugUI = true;
    #endregion

    #region 룸 정보 클래스
    private class RoomInfo
    {
        public Transform transform;               // 룸의 Transform
        public bool isOccupied;                   // 룸 사용 여부
        public bool isBeingCleaned;               // 룸 청소 중 여부 (집사 AI용)
        public float size;                        // 룸 크기
        public GameObject gameObject;             // 룸 게임 오브젝트
        public string roomId;                     // 룸 고유 ID
        public Bounds bounds;                     // 룸의 Bounds
        public Transform bedTransform;            // 침대 Transform (있는 경우)

        public RoomInfo(GameObject roomObj)
        {
            gameObject = roomObj;
            transform = roomObj.transform;
            isOccupied = false;
            isBeingCleaned = false;

            var collider = roomObj.GetComponent<Collider>();
            size = collider != null ? collider.bounds.size.magnitude * 0.3f : 2f;
            var roomContents = roomObj.GetComponent<RoomContents>();
            bounds = roomContents != null ? roomContents.roomBounds : (collider != null ? collider.bounds : new Bounds(transform.position, Vector3.one * 2f));

            // 침대 탐지
            bedTransform = FindBedInRoom(roomObj);

            Vector3 pos = roomObj.transform.position;
            roomId = $"Room_{pos.x:F0}_{pos.z:F0}";
        }

        private Transform FindBedInRoom(GameObject roomObj)
        {
            // 방 내부에서 "Bed" 태그를 가진 오브젝트 찾기
            var allBeds = GameObject.FindGameObjectsWithTag("Bed");
            foreach (var bed in allBeds)
            {
                if (bed != null && bounds.Contains(bed.transform.position))
                {
                    return bed.transform;
                }
            }
            return null;
        }
    }
    #endregion

    #region AI 상태 열거형
    private enum AIState
    {
        Wandering,           // 외부 배회
        MovingToQueue,       // 대기열로 이동
        WaitingInQueue,      // 대기열에서 대기
        MovingToRoom,        // 배정된 방으로 이동
        UsingRoom,           // 방 사용
        UseWandering,        // 방 사용 중 배회
        ReportingRoom,       // 방 사용 완료 보고
        ReturningToSpawn,    // 스폰 지점으로 복귀 (디스폰)
        RoomWandering,       // 방 내부 배회
        ReportingRoomQueue,  // 방 사용 완료 보고를 위해 대기열로 이동
        MovingToBed,         // 침대로 이동
        Sleeping,            // 침대에서 수면
        MovingToSunbed,      // 선베드로 이동
        UsingSunbed,         // 선베드 사용 중
        MovingToKitchenCounter, // 주방 카운터로 이동
        WaitingAtKitchenCounter, // 주방 카운터에서 대기
        MovingToChair,       // 의자로 이동
        Eating               // 식사 중
    }
    #endregion

    #region 이벤트
    public delegate void RoomsUpdatedHandler(GameObject[] rooms);
    private static event RoomsUpdatedHandler OnRoomsUpdated;
    #endregion

    #region 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeStatics()
    {
        roomList = new List<RoomInfo>();
        OnRoomsUpdated -= UpdateRoomList;
        OnRoomsUpdated = null;
        globalShowDebugUI = true;
    }

    void Start()
    {
        if (!InitializeComponents()) return;
        InitializeRoomsIfEmpty();
        timeSystem = TimeSystem.Instance;
        
        // Inspector 설정을 전역 설정에 반영
        globalShowDebugUI = debugUIEnabled;
        
        DetermineInitialBehavior();
    }

    private bool InitializeComponents()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        if (agent == null)
        {
            Destroy(gameObject);
            return false;
        }

        // NavMeshAgent 관성 제거 설정
        agent.acceleration = 100f;        // 가속도 증가 (빠르게 가속)
        agent.angularSpeed = 360f;        // 회전 속도 증가 (빠르게 회전)
        agent.stoppingDistance = 0.1f;    // 정지 거리 감소
        agent.autoBraking = true;         // 자동 브레이킹 활성화

        GameObject spawn = GameObject.FindGameObjectWithTag("Spawn");
        if (spawn == null)
        {
            Destroy(gameObject);
            return false;
        }

        roomManager = FindFirstObjectByType<RoomManager>();
        spawnPoint = spawn.transform;

        GameObject counter = GameObject.FindGameObjectWithTag("Counter");
        counterPosition = counter != null ? counter.transform : null;

        if (counterManager == null)
        {
            counterManager = FindFirstObjectByType<CounterManager>();
        }

        return true;
    }

    private void InitializeRoomsIfEmpty()
    {
        lock (lockObject)
        {
            if (roomList.Count == 0)
            {
                InitializeRooms();
                if (OnRoomsUpdated == null)
                {
                    OnRoomsUpdated += UpdateRoomList;
                }
            }
        }
    }

    private void DetermineInitialBehavior()
    {
        DetermineBehaviorByTime();
    }
    #endregion

    #region 룸 관리
    private void InitializeRooms()
    {
        roomList.Clear();

        var roomDetectors = GameObject.FindObjectsByType<RoomDetector>(FindObjectsSortMode.None);
        
        if (roomDetectors.Length > 0)
        {
            foreach (var detector in roomDetectors)
            {
                detector.ScanForRooms();
                detector.OnRoomsUpdated += rooms =>
                {
                    if (rooms != null && rooms.Length > 0)
                    {
                        UpdateRoomList(rooms);
                    }
                };
            }
        }
        else
        {
            GameObject[] taggedRooms = GameObject.FindGameObjectsWithTag("Room");
            
            foreach (GameObject room in taggedRooms)
            {
                if (!roomList.Any(r => r.gameObject == room))
                {
                    var roomInfo = new RoomInfo(room);
                    roomList.Add(roomInfo);
                }
            }
        }

        if (roomList.Count == 0)
        {
        }
    }

    public static void UpdateRoomList(GameObject[] newRooms)
    {
        if (newRooms == null || newRooms.Length == 0) return;

        lock (lockObject)
        {
            HashSet<string> processedRoomIds = new HashSet<string>();
            List<RoomInfo> updatedRoomList = new List<RoomInfo>();

            foreach (GameObject room in newRooms)
            {
                if (room != null)
                {
                    RoomInfo newRoom = new RoomInfo(room);
                    if (!processedRoomIds.Contains(newRoom.roomId))
                    {
                        processedRoomIds.Add(newRoom.roomId);
                        var existingRoom = roomList.FirstOrDefault(r => r.roomId == newRoom.roomId);
                        if (existingRoom != null)
                        {
                            newRoom.isOccupied = existingRoom.isOccupied;
                            updatedRoomList.Add(newRoom);
                        }
                        else
                        {
                            updatedRoomList.Add(newRoom);
                        }
                    }
                }
            }

            if (updatedRoomList.Count > 0)
            {
                roomList = updatedRoomList;
            }
        }
    }

    public static void NotifyRoomsUpdated(GameObject[] rooms)
    {
        OnRoomsUpdated?.Invoke(rooms);
    }
    #endregion

    #region 시간 기반 행동 결정
    private void DetermineBehaviorByTime()
    {
        if (timeSystem == null)
        {
            Debug.LogWarning($"[DetermineBehaviorByTime] AI {gameObject.name}: TimeSystem이 null - 대체 행동 실행");
            FallbackBehavior();
            return;
        }

        int hour = timeSystem.CurrentHour;
        int minute = timeSystem.CurrentMinute;
        Debug.Log($"[DetermineBehaviorByTime] AI {gameObject.name}: 시간별 행동 결정 시작 - 시간: {hour:00}:{minute:00}, 방: {currentRoomIndex}, 수면: {isSleeping}, 상태: {currentState}");

        // 17:00에 방 사용 중이 아닌 모든 에이전트 강제 디스폰
        if (hour == 17 && minute == 0)
        {
            Handle17OClockForcedDespawn();
            return;
        }

        if (hour >= 0 && hour < 9)
        {
            // 0:00 ~ 9:00
            Debug.Log($"[0-9시 행동] AI {gameObject.name}: 현재 시간 {hour:00}:{minute:00}, 방 인덱스: {currentRoomIndex}, 수면 중: {isSleeping}, 현재 상태: {currentState}");
            
            if (currentRoomIndex != -1)
            {
                // 0시에 침대로 이동 시작
                if (hour == 0 && !isSleeping && currentState != AIState.MovingToBed && currentState != AIState.Sleeping)
                {
                    if (FindBedInCurrentRoom(out Transform bedTransform))
                    {
                        currentBedTransform = bedTransform;
                        preSleepPosition = transform.position;
                        preSleepRotation = transform.rotation;
                        Debug.Log($"[0-9시 행동] AI {gameObject.name}: 0시 침대 발견, 침대로 이동 시작");
                        TransitionToState(AIState.MovingToBed);
                    }
                    else
                    {
                        Debug.Log($"[0-9시 행동] AI {gameObject.name}: 0시 침대 없음, 방 배회 시작");
                        TransitionToState(AIState.RoomWandering);
                    }
                }
                else if (!isSleeping)
                {
                    Debug.Log($"[0-9시 행동] AI {gameObject.name}: 방 내부 배회 중");
                    TransitionToState(AIState.RoomWandering);
                }
                else
                {
                    Debug.Log($"[0-9시 행동] AI {gameObject.name}: 수면 중");
                }
            }
            else
            {
                Debug.Log($"[0-9시 행동] AI {gameObject.name}: 방 없음, 대체 행동 실행");
                FallbackBehavior();
            }
        }
        else if (hour >= 9 && hour < 11)
        {
            // 9:00 ~ 11:00
            Debug.Log($"[9-11시 행동] AI {gameObject.name}: 현재 시간 {hour:00}:{minute:00}, 방 인덱스: {currentRoomIndex}, 수면 중: {isSleeping}, 현재 상태: {currentState}, 대기열: {isInQueue}");
            
            if (currentRoomIndex != -1)
            {
                // 9시에 수면 중인 AI를 깨움
                if (hour == 9 && isSleeping)
                {
                    Debug.Log($"[9-11시 행동] AI {gameObject.name}: 9시 수면에서 깨어남");
                    WakeUp();
                    // WakeUp()에서 이미 ReportingRoomQueue로 전환하므로 여기서 return
                    return;
                }
                
                // 수면이 아닌 경우 방 사용 완료 보고
                // 단, 이미 보고 중이거나 대기열에 있으면 전환하지 않음
                if (!isSleeping && currentState != AIState.ReportingRoomQueue && 
                    currentState != AIState.ReportingRoom && !isInQueue && !isWaitingForService)
                {
                    Debug.Log($"[9-11시 행동] AI {gameObject.name}: 방 사용 완료 보고 시작 (랜덤 딜레이 후)");
                    // 9시에 AI들이 동시에 몰리지 않도록 랜덤 딜레이 추가
                    StartCoroutine(DelayedReportingRoomQueue());
                }
                else
                {
                    Debug.Log($"[9-11시 행동] AI {gameObject.name}: 이미 보고 진행 중이거나 대기열에 있음");
                }
            }
            else
            {
                Debug.Log($"[9-11시 행동] AI {gameObject.name}: 방 없음, 대체 행동 실행");
                FallbackBehavior();
            }
        }
        else if (hour >= 11 && hour < 17)
        {
            // 11:00 ~ 17:00
            if (currentRoomIndex == -1)
            {
                // 11-16시 방이 없는 AI 행동 확률
                if (hour >= 11 && hour <= 16)
                {
                    float randomValue = Random.value;
                    if (randomValue < 0.10f)
                    {
                        TransitionToState(AIState.MovingToQueue);
                    }
                    else if (randomValue < 0.25f && hour >= 11 && hour <= 15)
                    {
                        if (!TryFindAvailableSunbedRoom())
                        {
                            TransitionToState(AIState.Wandering);
                        }
                    }
                    else if (randomValue < 0.85f)
                    {
                        if (!TryFindAvailableKitchen())
                        {
                            TransitionToState(AIState.Wandering);
                        }
                    }
                    else if (randomValue < 0.95f)
                    {
                        TransitionToState(AIState.Wandering);
                    }
                    else
                    {
                        TransitionToState(AIState.ReturningToSpawn);
                    }
                }
                else
                {
                    // 17시는 기존 로직 유지
                    float randomValue = Random.value;
                    if (randomValue < 0.2f)
                    {
                        TransitionToState(AIState.MovingToQueue);
                    }
                    else if (randomValue < 0.8f)
                    {
                        TransitionToState(AIState.Wandering);
                    }
                    else
                    {
                        TransitionToState(AIState.ReturningToSpawn);
                    }
                }
            }
            else
            {
                // 방이 있는 AI는 선베드를 사용하지 않고 기존 행동만 함
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.UseWandering);
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                }
            }
        }
        else
        {
            // 17:00 ~ 0:00
            if (currentRoomIndex != -1)
            {
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.UseWandering);
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                }
            }
            else
            {
                FallbackBehavior();
            }
        }

        lastBehaviorUpdateHour = hour;
    }

    /// <summary>
    /// 17:00에 방 사용 중이 아닌 모든 AI를 강제로 디스폰시킵니다.
    /// </summary>
    private void Handle17OClockForcedDespawn()
    {
        // 방 사용 중이거나 수면 중인 AI는 디스폰하지 않음 (선베드 사용자는 예외없이 퇴장)
        if (IsInRoomRelatedState() || isSleeping)
        {
            return;
        }

        // 주방 카운터 대기 중이거나 식사 중인 AI는 강제로 종료 후 퇴장
        if (isWaitingAtKitchenCounter || currentState == AIState.MovingToKitchenCounter || currentState == AIState.WaitingAtKitchenCounter)
        {
            ForceFinishKitchenActivity();
        }
        else if (isEating)
        {
            ForceFinishEating();
        }

        // 모든 코루틴 강제 종료
        CleanupCoroutines();

        // 대기열에서 강제 제거
        if (isInQueue && counterManager != null)
        {
            counterManager.LeaveQueue(this);
            isInQueue = false;
            isWaitingForService = false;
            counterManager.ForceCleanupQueue();
        }

        // 강제 디스폰
        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
    }

    /// <summary>
    /// 방 관련 상태인지 확인합니다.
    /// </summary>
    private bool IsInRoomRelatedState()
    {
        return (currentState == AIState.UsingRoom || 
                currentState == AIState.UseWandering || 
                currentState == AIState.RoomWandering ||
                currentState == AIState.MovingToRoom ||
                currentState == AIState.MovingToBed ||
                currentState == AIState.Sleeping) && 
                currentRoomIndex != -1;
    }

    /// <summary>
    /// 11시 체크아웃 완료 후 디스폰을 처리합니다.
    /// </summary>
    private void HandleCheckoutDespawn()
    {
        // 방이 없고 배회 중인 AI들을 11시에 디스폰
        if (currentState == AIState.Wandering && currentRoomIndex == -1)
        {
            TransitionToState(AIState.ReturningToSpawn);
            agent.SetDestination(spawnPoint.position);
        }
    }

    /// <summary>
    /// 중요한 상태인지 확인합니다 (행동 재결정을 방해하면 안 되는 상태).
    /// </summary>
    private bool IsInCriticalState()
    {
        bool isCritical = currentState == AIState.WaitingInQueue || 
               currentState == AIState.MovingToQueue || 
               currentState == AIState.MovingToRoom || 
               currentState == AIState.MovingToBed ||
               currentState == AIState.Sleeping ||
               currentState == AIState.MovingToSunbed ||
               currentState == AIState.UsingSunbed ||
               currentState == AIState.ReportingRoom ||
               currentState == AIState.ReportingRoomQueue ||
               currentState == AIState.ReturningToSpawn ||
               isInQueue || isWaitingForService ||
               isScheduledForDespawn; // 11시 디스폰 예정인 AI는 행동 재결정하지 않음
        
        if (isCritical)
        {
            Debug.Log($"[IsInCriticalState] AI {gameObject.name}: 중요 상태임 - 상태: {currentState}, isInQueue: {isInQueue}, isWaitingForService: {isWaitingForService}");
        }
        
        return isCritical;
    }

    /// <summary>
    /// 방 사용 중인 AI의 시간별 내부/외부 배회를 재결정합니다.
    /// </summary>
    private void RedetermineRoomBehavior()
    {
        if (!IsInRoomRelatedState()) return;

        int hour = timeSystem.CurrentHour;
        
        // 0시에는 무조건 내부 배회
        if (hour == 0)
        {
            if (currentState == AIState.UseWandering)
            {
                TransitionToState(AIState.RoomWandering);
            }
        }
        // 11-17시, 17-24시에는 50/50 확률로 재결정
        else if ((hour >= 11 && hour < 17) || (hour >= 17 && hour < 24))
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                if (currentState != AIState.UseWandering)
                {
                    TransitionToState(AIState.UseWandering);
                }
            }
            else
            {
                if (currentState != AIState.RoomWandering)
                {
                    TransitionToState(AIState.RoomWandering);
                }
            }
        }
    }

    private void FallbackBehavior()
    {
        Debug.Log($"[FallbackBehavior] AI {gameObject.name}: 대체 행동 실행 (counterPosition: {counterPosition != null}, counterManager: {counterManager != null})");
        
        if (counterPosition == null || counterManager == null)
        {
            float randomValue = Random.value;
            Debug.Log($"[FallbackBehavior] AI {gameObject.name}: 카운터 없음 - 랜덤 선택 ({randomValue:F2})");
            if (randomValue < 0.5f)
            {
                Debug.Log($"[FallbackBehavior] AI {gameObject.name}: 배회 선택");
                TransitionToState(AIState.Wandering);
            }
            else
            {
                Debug.Log($"[FallbackBehavior] AI {gameObject.name}: 스폰 지점 복귀 선택");
                TransitionToState(AIState.ReturningToSpawn);
            }
        }
        else
        {
            float randomValue = Random.value;
            Debug.Log($"[FallbackBehavior] AI {gameObject.name}: 카운터 있음 - 랜덤 선택 ({randomValue:F2})");
            if (randomValue < 0.4f)
            {
                Debug.Log($"[FallbackBehavior] AI {gameObject.name}: 배회 선택");
                TransitionToState(AIState.Wandering);
            }
            else
            {
                Debug.Log($"[FallbackBehavior] AI {gameObject.name}: 카운터로 이동 선택");
                TransitionToState(AIState.MovingToQueue);
            }
        }
    }
    #endregion

    #region 업데이트 및 상태 머신
    void Update()
    {
        // 선베드 사용 중이거나 수면 중일 때는 NavMesh 체크 건너뛰기
        if (!isUsingSunbed && !isSleeping && !isEating)
        {
            if (!agent.isOnNavMesh)
            {
                ReturnToPool();
                return;
            }
        }
        
        // Inspector에서 설정이 변경되면 전역 설정 업데이트
        if (globalShowDebugUI != debugUIEnabled)
        {
            globalShowDebugUI = debugUIEnabled;
        }

        // 주기적으로 오브젝트 유효성 검사 (3초마다)
        CheckObjectValidity();

        // 애니메이션 파라미터 제어
        if (animator != null)
        {
            // Moving 파라미터: 이동 중일 때 true (수면 중이나 선베드 사용 중, 식사 중이 아닐 때)
            bool isMoving = agent.velocity.magnitude > 0.1f && !isSleeping && !isUsingSunbed && !isEating;
            animator.SetBool("Moving", isMoving);
            
            // BedTime 파라미터: 수면 중이거나 선베드 사용 중일 때 true
            animator.SetBool("BedTime", isSleeping || isUsingSunbed);
            
            // Eating 파라미터: 식사 중일 때 true
            animator.SetBool("Eating", isEating);
        }

        // 시간 기반 행동 갱신
        if (timeSystem != null)
        {
            int hour = timeSystem.CurrentHour;
            int minute = timeSystem.CurrentMinute;

            // 17:00에 방 사용 중이 아닌 모든 에이전트 강제 디스폰
            if (hour == 17 && minute == 0)
            {
                Handle17OClockForcedDespawn();
                return;
            }

            // 11시 체크아웃 완료 후 디스폰 체크
            if (hour == 11 && minute == 0 && lastBehaviorUpdateHour != hour)
            {
                HandleCheckoutDespawn();
                lastBehaviorUpdateHour = hour;
                return;
            }

            // 0시 특별 처리: 방이 있는 AI는 무조건 침대로 이동 (다른 체크보다 먼저)
            if (hour == 0 && minute == 0 && currentRoomIndex != -1 && !isSleeping && lastBehaviorUpdateHour != hour)
            {
                Debug.Log($"[Update-0시] AI {gameObject.name}: 0시 감지, 방 있음 - 강제로 침대로 이동 (현재 상태: {currentState})");
                DetermineBehaviorByTime();
                lastBehaviorUpdateHour = hour;
                return;
            }

            // 9시에 수면 중인 AI 특별 처리 (다른 체크보다 먼저)
            if (hour == 9 && minute == 0 && isSleeping && lastBehaviorUpdateHour != hour)
            {
                Debug.Log($"[Update-9시] AI {gameObject.name}: 9시 수면 중 감지 - WakeUp 호출");
                WakeUp();
                lastBehaviorUpdateHour = hour;
                return;
            }

            // 매시간 행동 재결정 (모든 AI 포함)
            if (minute == 0 && hour != lastBehaviorUpdateHour)
            {
                Debug.Log($"[Update-매시간] AI {gameObject.name}: {hour}시 정각 감지 - 행동 재결정 체크 (상태: {currentState}, 방: {currentRoomIndex})");
                
                // 디스폰 예정 AI는 행동 재결정하지 않음
                if (isScheduledForDespawn)
                {
                    Debug.Log($"[Update-매시간] AI {gameObject.name}: 디스폰 예정이므로 행동 재결정 건너뜀");
                    lastBehaviorUpdateHour = hour;
                }
                // 중요한 상태가 아닌 경우에만 행동 재결정
                else if (!IsInCriticalState())
                {
                    Debug.Log($"[Update-매시간] AI {gameObject.name}: 중요 상태 아님 - DetermineBehaviorByTime 호출");
                    DetermineBehaviorByTime();
                    lastBehaviorUpdateHour = hour;
                }
                // 방 사용 중인 AI도 매시간 내부/외부 배회 재결정
                else if (IsInRoomRelatedState())
                {
                    Debug.Log($"[Update-매시간] AI {gameObject.name}: 방 관련 상태 - RedetermineRoomBehavior 호출");
                    RedetermineRoomBehavior();
                    lastBehaviorUpdateHour = hour;
                }
                else
                {
                    Debug.Log($"[Update-매시간] AI {gameObject.name}: 중요 상태이므로 행동 재결정 건너뜀 (상태: {currentState})");
                    lastBehaviorUpdateHour = hour;
                }
            }
        }

        switch (currentState)
        {
            case AIState.Wandering:
                break;
            case AIState.MovingToQueue:
            case AIState.WaitingInQueue:
            case AIState.ReportingRoomQueue:
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1 && currentRoomIndex < roomList.Count)
                {
                    Bounds roomBounds = roomList[currentRoomIndex].bounds;
                    if (agent != null && agent.enabled && agent.isOnNavMesh && 
                        !agent.pathPending && agent.remainingDistance < arrivalDistance && roomBounds.Contains(transform.position))
                    {
                        StartCoroutine(UseRoom());
                    }
                }
                break;
            case AIState.MovingToBed:
                // 침대로 이동 중 - MoveToBedBehavior 코루틴에서 처리
                break;
            case AIState.Sleeping:
                // 수면 중 - 별도 처리 불필요
                break;
            case AIState.MovingToSunbed:
                // 선베드로 이동 중 - MoveToSunbedBehavior 코루틴에서 처리
                break;
            case AIState.UsingSunbed:
                // 선베드 사용 중 - 별도 처리 불필요
                break;
            case AIState.MovingToKitchenCounter:
                // 주방 카운터로 이동 중 - MoveToKitchenCounterBehavior 코루틴에서 처리
                break;
            case AIState.WaitingAtKitchenCounter:
                // 주방 카운터에서 대기 중 - 별도 처리 불필요
                break;
            case AIState.MovingToChair:
                // 의자로 이동 중 - MoveToChairBehavior 코루틴에서 처리
                break;
            case AIState.Eating:
                // 식사 중 - 별도 처리 불필요
                break;
            case AIState.UsingRoom:
            case AIState.RoomWandering:
                break;
            case AIState.ReportingRoom:
                break;
            case AIState.ReturningToSpawn:
                if (agent != null && agent.enabled && agent.isOnNavMesh && 
                    !agent.pathPending && agent.remainingDistance < arrivalDistance)
                {
                    ReturnToPool();
                }
                break;
        }
    }
    #endregion

    #region 대기열 동작
    private int queueRetryCount = 0; // 대기열 재시도 횟수
    private const int maxQueueRetries = 5; // 최대 재시도 횟수
    
    /// <summary>
    /// 9시에 AI들이 동시에 몰리지 않도록 랜덤 딜레이 후 ReportingRoomQueue로 전환
    /// </summary>
    private IEnumerator DelayedReportingRoomQueue()
    {
        // 0-10초 사이의 랜덤 딜레이
        float delay = Random.Range(0f, 10f);
        Debug.Log($"[DelayedReportingRoomQueue] AI {gameObject.name}: 방 사용 완료 보고 대기 시작 - 딜레이: {delay:F1}초");
        
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"[DelayedReportingRoomQueue] AI {gameObject.name}: 딜레이 완료 - 상태 체크 (방: {currentRoomIndex}, 현재 상태: {currentState}, isInQueue: {isInQueue})");
        
        // 딜레이 중에 상태가 변경되지 않았는지 확인
        if (currentRoomIndex != -1 && currentState != AIState.ReportingRoomQueue && 
            currentState != AIState.ReportingRoom && !isInQueue && !isWaitingForService)
        {
            Debug.Log($"[DelayedReportingRoomQueue] AI {gameObject.name}: 조건 충족 - ReportingRoomQueue로 전환");
            TransitionToState(AIState.ReportingRoomQueue);
        }
        else
        {
            Debug.LogWarning($"[DelayedReportingRoomQueue] AI {gameObject.name}: 조건 불충족 - 전환 취소 (방: {currentRoomIndex}, 상태: {currentState}, isInQueue: {isInQueue}, isWaitingForService: {isWaitingForService})");
        }
    }
    
    private IEnumerator QueueBehavior()
    {
        Debug.Log($"[QueueBehavior] AI {gameObject.name}: QueueBehavior 시작 (현재 상태: {currentState}, 방 인덱스: {currentRoomIndex})");
        
        if (counterManager == null || counterPosition == null)
        {
            Debug.LogError($"[QueueBehavior] AI {gameObject.name}: counterManager 또는 counterPosition이 null");
            queueRetryCount = 0; // 재시도 횟수 초기화
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
            }
            yield break;
        }

        Debug.Log($"[QueueBehavior] AI {gameObject.name}: 대기열 진입 시도 (재시도 횟수: {queueRetryCount}/{maxQueueRetries})");
        
        if (!counterManager.TryJoinQueue(this))
        {
            queueRetryCount++;
            Debug.LogWarning($"[QueueBehavior] AI {gameObject.name}: 대기열 진입 실패 (재시도 횟수: {queueRetryCount}/{maxQueueRetries})");
            
            // 최대 재시도 횟수 초과 시
            if (queueRetryCount >= maxQueueRetries)
            {
                Debug.LogWarning($"[QueueBehavior] AI {gameObject.name}: 최대 재시도 횟수 초과 - 포기");
                queueRetryCount = 0;
                
                // ReportingRoomQueue 상태였다면 방을 반납하고 배회
                if (currentState == AIState.ReportingRoomQueue)
                {
                    if (currentRoomIndex != -1)
                    {
                        lock (lockObject)
                        {
                            if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                            {
                                roomList[currentRoomIndex].isOccupied = false;
                            }
                            currentRoomIndex = -1;
                        }
                    }
                }
                
                DetermineBehaviorByTime();
                yield break;
            }
            
            // ReportingRoomQueue 상태인 경우 재시도 (점진적으로 대기 시간 증가)
            if (currentState == AIState.ReportingRoomQueue)
            {
                float waitTime = Random.Range(3f, 7f) * queueRetryCount; // 재시도 횟수에 비례하여 대기 시간 증가
                yield return new WaitForSeconds(waitTime);
                queueCoroutine = StartCoroutine(QueueBehavior());
                yield break;
            }
            
            if (currentRoomIndex == -1)
            {
                queueRetryCount = 0;
                float randomValue = Random.value;
                if (randomValue < 0.5f)
                {
                    TransitionToState(AIState.Wandering);
                    wanderingCoroutine = StartCoroutine(WanderingBehavior());
                }
                else
                {
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                }
            }
            else
            {
                float waitTime = Random.Range(2f, 4f) * queueRetryCount;
                yield return new WaitForSeconds(waitTime);
                queueCoroutine = StartCoroutine(QueueBehavior());
            }
            yield break;
        }

        // 대기열 진입 성공 시 재시도 횟수 초기화
        queueRetryCount = 0;

        isInQueue = true;
        Debug.Log($"[QueueBehavior] AI {gameObject.name}: 대기열 진입 성공 - isInQueue=true");
        
        // ⚠️ 여기서 TransitionToState를 호출하면 안됨! 현재 코루틴이 중지되고 재시작되는 무한 루프 발생!
        // 대신 상태만 직접 변경
        if (currentState != AIState.ReportingRoomQueue && currentState != AIState.WaitingInQueue)
        {
            currentState = AIState.WaitingInQueue;
            currentDestination = GetStateDescription(AIState.WaitingInQueue);
            Debug.Log($"[QueueBehavior] AI {gameObject.name}: 상태 직접 변경 → WaitingInQueue");
        }

        while (isInQueue)
        {
            Debug.Log($"[QueueBehavior] AI {gameObject.name}: 대기열 루프 실행 중 (isInQueue={isInQueue})");
            
            // 17시 체크 - 대기열에서도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Handle17OClockForcedDespawn();
                yield break;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && 
                !agent.pathPending && agent.remainingDistance < arrivalDistance)
            {
                // 목적지 도착 시 저장된 방향으로 회전
                if (targetQueueRotation != Quaternion.identity)
                {
                    transform.rotation = targetQueueRotation;
                }
                
                if (counterManager.CanReceiveService(this))
                {
                    counterManager.StartService(this);
                    isWaitingForService = true;

                    while (isWaitingForService)
                    {
                        // 서비스 대기 중에도 17시 체크
                        if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                        {
                            Handle17OClockForcedDespawn();
                            yield break;
                        }
                        yield return new WaitForSeconds(0.1f);
                    }

                    if (currentState == AIState.ReportingRoomQueue)
                    {
                        Debug.Log($"[QueueBehavior] AI {gameObject.name}: 서비스 완료 후 ReportRoomVacancy 호출 시작");
                        // ReportRoomVacancy 코루틴이 완전히 끝날 때까지 대기 (결제 처리 보장)
                        yield return StartCoroutine(ReportRoomVacancy());
                        Debug.Log($"[QueueBehavior] AI {gameObject.name}: ReportRoomVacancy 완료");
                    }
                    else if (currentRoomIndex != -1)
                    {
                        roomList[currentRoomIndex].isOccupied = false;
                        currentRoomIndex = -1;
                        TransitionToState(AIState.ReturningToSpawn);
                        agent.SetDestination(spawnPoint.position);
                    }
                    else
                    {
                        if (TryAssignRoom())
                        {
                            // 방 배정 후 즉시 결제 정보 등록 (방 사용 전에 0시가 되어도 결제되도록)
                            var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
                            var roomManager = FindFirstObjectByType<RoomManager>();
                            if (roomManager != null && room != null)
                            {
                                Debug.Log($"[방 배정] AI {gameObject.name}: 방 배정 완료, 즉시 결제 정보 등록");
                                roomManager.ReportRoomUsage(gameObject.name, room);
                            }
                            
                            TransitionToState(AIState.MovingToRoom);
                            agent.SetDestination(roomList[currentRoomIndex].transform.position);
                        }
                        else
                        {
                            float randomValue = Random.value;
                            if (randomValue < 0.5f)
                            {
                                TransitionToState(AIState.Wandering);
                                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                            }
                            else
                            {
                                TransitionToState(AIState.ReturningToSpawn);
                                agent.SetDestination(spawnPoint.position);
                            }
                        }
                    }
                    
                    // 서비스 완료 후 대기열에서 나가기
                    isInQueue = false;
                    Debug.Log($"[QueueBehavior] AI {gameObject.name}: 대기열에서 나감 - isInQueue=false");
                    if (counterManager != null)
                    {
                        counterManager.LeaveQueue(this);
                    }
                    Debug.Log($"[QueueBehavior] AI {gameObject.name}: QueueBehavior 종료");
                    break;
                }
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private bool TryAssignRoom()
    {
        lock (lockObject)
        {
            var availableRooms = roomList.Select((room, index) => new { room, index })
                                         .Where(r => !r.room.isOccupied && !r.room.isBeingCleaned)
                                         .Select(r => r.index)
                                         .ToList();

            if (availableRooms.Count == 0)
            {
                return false;
            }

            int selectedRoomIndex = availableRooms[Random.Range(0, availableRooms.Count)];
            if (!roomList[selectedRoomIndex].isOccupied && !roomList[selectedRoomIndex].isBeingCleaned)
            {
                roomList[selectedRoomIndex].isOccupied = true;
                currentRoomIndex = selectedRoomIndex;
                return true;
            }

            return false;
        }
    }
    #endregion

    #region 상태 전환
    private void TransitionToState(AIState newState)
    {
        AIState oldState = currentState;
        Debug.Log($"[상태 전환] AI {gameObject.name}: {oldState} → {newState} (방 인덱스: {currentRoomIndex})");
        
        CleanupCoroutines();

        // 이동이 필요한 상태로 전환될 때 NavMeshAgent 활성화 확인
        if (newState == AIState.Wandering || 
            newState == AIState.UseWandering || 
            newState == AIState.RoomWandering ||
            newState == AIState.MovingToQueue ||
            newState == AIState.MovingToRoom ||
            newState == AIState.ReturningToSpawn)
        {
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
                Debug.Log($"[상태 전환] AI {gameObject.name}: NavMeshAgent 재활성화 ({oldState} → {newState})");
            }
        }

        currentState = newState;
        currentDestination = GetStateDescription(newState);

        switch (newState)
        {
            case AIState.Wandering:
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                break;
            case AIState.MovingToQueue:
            case AIState.ReportingRoomQueue:
                queueCoroutine = StartCoroutine(QueueBehavior());
                break;
            case AIState.MovingToRoom:
                if (currentRoomIndex != -1)
                {
                    agent.SetDestination(roomList[currentRoomIndex].transform.position);
                }
                break;
            case AIState.MovingToBed:
                if (currentBedTransform != null)
                {
                    sleepingCoroutine = StartCoroutine(MoveToBedBehavior());
                }
                break;
            case AIState.Sleeping:
                // 수면 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToSunbed:
                if (currentSunbedTransform != null)
                {
                    sunbedCoroutine = StartCoroutine(MoveToSunbedBehavior());
                }
                break;
            case AIState.UsingSunbed:
                // 선베드 사용 상태 - 별도의 코루틴 불필요
                break;
            case AIState.MovingToKitchenCounter:
                if (currentKitchenCounter != null)
                {
                    eatingCoroutine = StartCoroutine(MoveToKitchenCounterBehavior());
                }
                break;
            case AIState.WaitingAtKitchenCounter:
                // 주방 카운터에서 대기 - 별도 처리 불필요
                break;
            case AIState.MovingToChair:
                if (currentChairTransform != null)
                {
                    eatingCoroutine = StartCoroutine(MoveToChairBehavior());
                }
                break;
            case AIState.Eating:
                // 식사 상태 - 별도의 코루틴 불필요
                break;
            case AIState.ReturningToSpawn:
                agent.SetDestination(spawnPoint.position);
                break;
            case AIState.RoomWandering:
                roomWanderingCoroutine = StartCoroutine(RoomWanderingBehavior());
                break;
            case AIState.UseWandering:
                useWanderingCoroutine = StartCoroutine(UseWanderingBehavior());
                break;
        }
    }

    private string GetStateDescription(AIState state)
    {
        return state switch
        {
            AIState.Wandering => "배회 중",
            AIState.MovingToQueue => "대기열로 이동 중",
            AIState.WaitingInQueue => "대기열에서 대기 중",
            AIState.MovingToRoom => $"룸 {currentRoomIndex + 1}번으로 이동 중",
            AIState.UsingRoom => "룸 사용 중",
            AIState.ReportingRoom => "룸 사용 완료 보고 중",
            AIState.ReturningToSpawn => "퇴장 중",
            AIState.RoomWandering => $"룸 {currentRoomIndex + 1}번 내부 배회 중",
            AIState.ReportingRoomQueue => "사용 완료 보고 대기열로 이동 중",
            AIState.UseWandering => $"룸 {currentRoomIndex + 1}번 사용 중 외부 배회",
            AIState.MovingToBed => $"룸 {currentRoomIndex + 1}번 침대로 이동 중",
            AIState.Sleeping => $"룸 {currentRoomIndex + 1}번 침대에서 수면 중",
            AIState.MovingToSunbed => $"룸 {currentRoomIndex + 1}번 선베드로 이동 중",
            AIState.UsingSunbed => $"룸 {currentRoomIndex + 1}번 선베드 사용 중",
            AIState.MovingToKitchenCounter => "주방 카운터로 이동 중",
            AIState.WaitingAtKitchenCounter => "주방 카운터에서 대기 중",
            AIState.MovingToChair => "의자로 이동 중",
            AIState.Eating => "식사 중",
            _ => "알 수 없는 상태"
        };
    }
    #endregion

    #region 룸 사용
    private IEnumerator UseRoom()
    {
        float roomUseTime = Random.Range(25f, 35f);
        float elapsedTime = 0f;

        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            StartCoroutine(ReportRoomVacancy());
            yield break;
        }

        // ⚠️ ReportRoomUsage는 방 배정 시 이미 호출됨 (중복 방지)
        // 방 배정 받자마자 결제 정보가 등록되므로 여기서는 호출하지 않음

        TransitionToState(AIState.UseWandering);

        while (elapsedTime < roomUseTime && agent.isOnNavMesh)
        {
            yield return new WaitForSeconds(Random.Range(2f, 5f));
            elapsedTime += Random.Range(2f, 5f);
        }

        DetermineBehaviorByTime();
    }

    private IEnumerator ReportRoomVacancy()
    {
        Debug.Log($"[ReportRoomVacancy] AI {gameObject.name}: 시작 (방 인덱스: {currentRoomIndex})");
        
        TransitionToState(AIState.ReportingRoom);
        int reportingRoomIndex = currentRoomIndex;

        lock (lockObject)
        {
            if (reportingRoomIndex >= 0 && reportingRoomIndex < roomList.Count)
            {
                roomList[reportingRoomIndex].isOccupied = false;
                Debug.Log($"[ReportRoomVacancy] AI {gameObject.name}: 방 {reportingRoomIndex} 해제");
                currentRoomIndex = -1;
            }
            else
            {
                Debug.LogWarning($"[ReportRoomVacancy] AI {gameObject.name}: 유효하지 않은 방 인덱스 {reportingRoomIndex}");
            }
        }

        var roomManager = FindFirstObjectByType<RoomManager>();
        if (roomManager != null)
        {
            Debug.Log($"[방 사용 완료] AI {gameObject.name}: 카운터에서 방 결제 처리 시작 (보고 중인 방 인덱스: {reportingRoomIndex})");
            int amount = roomManager.ProcessRoomPayment(gameObject.name);
            
            if (amount > 0)
            {
                Debug.Log($"✅ [방 사용 완료 결제] AI {gameObject.name}: 결제 성공! 총 {amount}원 획득");
            }
            else
            {
                Debug.LogWarning($"⚠️ [방 사용 완료 결제] AI {gameObject.name}: 결제 금액이 0원입니다 (보고한 방: {reportingRoomIndex}). PaymentSystem에 이 AI의 미결제 정보가 없을 가능성 있음 - 방 배정 시 ReportRoomUsage 호출 여부 확인 필요");
            }
        }
        else
        {
            Debug.LogError($"❌ [방 사용 완료] AI {gameObject.name}: RoomManager를 찾을 수 없어 결제 처리 실패!");
        }

        // 집사 AI 생성 요청 (컴파일 순서 문제로 인해 문자열로 찾기)
        GameObject butlerSpawnerObj = GameObject.Find("ButlerSpawner");
        if (butlerSpawnerObj != null)
        {
            var butlerSpawner = butlerSpawnerObj.GetComponent<MonoBehaviour>();
            if (butlerSpawner != null)
            {
                // 리플렉션을 사용하여 SpawnButlerForRoom 메서드 호출
                var method = butlerSpawner.GetType().GetMethod("SpawnButlerForRoom");
                if (method != null)
                {
                    method.Invoke(butlerSpawner, new object[] { reportingRoomIndex });
                }
            }
        }

        Debug.Log($"[ReportRoomVacancy] AI {gameObject.name}: 결제 처리 완료, 다음 행동 결정 (현재 시간: {timeSystem.CurrentHour}시)");
        
        if (timeSystem.CurrentHour >= 9 && timeSystem.CurrentHour < 11)
        {
            Debug.Log($"[ReportRoomVacancy] AI {gameObject.name}: 9-11시 - 디스폰 예정으로 배회 시작");
            // 9-11시 체크아웃 후에는 배회하다가 11시에 디스폰 예정
            isScheduledForDespawn = true; // 디스폰 예정 플래그 설정
            TransitionToState(AIState.Wandering);
            wanderingCoroutine = StartCoroutine(WanderingBehaviorWithDespawn());
        }
        else
        {
            Debug.Log($"[ReportRoomVacancy] AI {gameObject.name}: 시간별 행동 결정 호출");
            DetermineBehaviorByTime();
        }

        yield break;
    }
    #endregion

    #region 배회 동작
    private IEnumerator WanderingBehavior()
    {
        // NavMeshAgent 상태 확인 및 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            Debug.Log($"[WanderingBehavior] AI {gameObject.name}: NavMeshAgent 재활성화");
            yield return null; // 한 프레임 대기
        }

        // NavMeshAgent가 NavMesh 위에 있는지 확인
        if (agent == null || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[WanderingBehavior] AI {gameObject.name}: NavMeshAgent가 NavMesh 위에 없음 - 배회 중단");
            yield break;
        }

        float wanderingTime = Random.Range(20f, 40f);
        float elapsedTime = 0f;

        while (currentState == AIState.Wandering && elapsedTime < wanderingTime)
        {
            // 17시 체크 - 배회 중에도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Handle17OClockForcedDespawn();
                yield break;
            }

            Vector3 currentPos = transform.position;
            float wanderDistance = Random.Range(25f, 50f);
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 targetPoint = currentPos + randomDirection * wanderDistance;
            
            int groundMask = NavMesh.GetAreaFromName("Ground");
            if (groundMask == 0) groundMask = NavMesh.AllAreas;
            
            bool foundValidPosition = false;
            
            // 방을 피해서 배회 위치 찾기
            if (TryGetWanderingPositionAvoidingRooms(targetPoint, 15f, groundMask, out Vector3 validPosition))
            {
                agent.SetDestination(validPosition);
                foundValidPosition = true;
            }
            else
            {
                WanderOnGround();
                foundValidPosition = true;
            }
            
            if (foundValidPosition)
            {
                // 목적지까지 이동 대기 (타임아웃 추가)
                float moveTimeout = 15f;
                float moveTimer = 0f;
                
                while (agent != null && agent.enabled && agent.isOnNavMesh && 
                       (agent.pathPending || agent.remainingDistance > arrivalDistance))
                {
                    if (moveTimer >= moveTimeout)
                    {
                        break;
                    }
                    
                    // 17시 체크
                    if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                    {
                        Handle17OClockForcedDespawn();
                        yield break;
                    }
                    
                    yield return new WaitForSeconds(0.5f);
                    moveTimer += 0.5f;
                }
                
                // 도착 후 대기
                float waitTime = Random.Range(5f, 12f);
                
                // 대기 시간을 쪼개서 17시 체크를 더 자주 함
                float remainingWait = waitTime;
                while (remainingWait > 0 && currentState == AIState.Wandering)
                {
                    yield return new WaitForSeconds(1f);
                    remainingWait -= 1f;
                    
                    // 대기 중에도 17시 체크
                    if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                    {
                        Handle17OClockForcedDespawn();
                        yield break;
                    }
                }
                
                elapsedTime += waitTime + moveTimer;
            }
            else
            {
                yield return new WaitForSeconds(2f);
                elapsedTime += 2f;
            }
        }

        // 배회 완료 후에도 17시 체크
        if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
        {
            Handle17OClockForcedDespawn();
            yield break;
        }

        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 9-11시 체크아웃 후 배회하다가 11시에 디스폰하는 특별한 배회
    /// </summary>
    private IEnumerator WanderingBehaviorWithDespawn()
    {
        while (currentState == AIState.Wandering)
        {
            // 11시가 되면 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour >= 11)
            {
                isScheduledForDespawn = false;
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
                yield break;
            }

            // 17시 체크도 유지
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                isScheduledForDespawn = false;
                Handle17OClockForcedDespawn();
                yield break;
            }

            WanderOnGround();
            float waitTime = Random.Range(3f, 7f);
            
            // 대기 시간을 쪼개서 11시와 17시 체크를 더 자주 함
            float remainingWait = waitTime;
            while (remainingWait > 0 && currentState == AIState.Wandering)
            {
                yield return new WaitForSeconds(1f);
                remainingWait -= 1f;
                
                // 11시 체크
                if (timeSystem != null && timeSystem.CurrentHour >= 11)
                {
                    isScheduledForDespawn = false;
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                    yield break;
                }
                
                // 17시 체크
                if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                {
                    isScheduledForDespawn = false;
                    Handle17OClockForcedDespawn();
                    yield break;
                }
            }
        }
    }

    private IEnumerator UseWanderingBehavior()
    {
        // NavMeshAgent 상태 확인 및 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            Debug.Log($"[UseWanderingBehavior] AI {gameObject.name}: NavMeshAgent 재활성화");
            yield return null; // 한 프레임 대기
        }

        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // NavMeshAgent가 NavMesh 위에 있는지 확인
        if (agent == null || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[UseWanderingBehavior] AI {gameObject.name}: NavMeshAgent가 NavMesh 위에 없음 - 배회 중단");
            DetermineBehaviorByTime();
            yield break;
        }

        while (currentState == AIState.UseWandering && agent.isOnNavMesh)
        {
            // 방을 피해서 외부 배회
            Vector3 currentPos = transform.position;
            float wanderDistance = Random.Range(15f, 25f);
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();
            
            Vector3 targetPoint = currentPos + randomDirection * wanderDistance;
            
            int groundMask = NavMesh.GetAreaFromName("Ground");
            if (groundMask == 0) groundMask = NavMesh.AllAreas;
            
            if (TryGetWanderingPositionAvoidingRooms(targetPoint, 10f, groundMask, out Vector3 validPosition))
            {
                agent.SetDestination(validPosition);
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh && 
                                                !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                WanderOnGround();
            }

            float waitTime = Random.Range(4f, 10f);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator RoomWanderingBehavior()
    {
        Debug.Log($"[RoomWanderingBehavior] AI {gameObject.name}: 방 내부 배회 시작 (방 인덱스: {currentRoomIndex})");
        
        // NavMeshAgent 상태 확인 및 활성화
        if (agent != null && !agent.enabled)
        {
            agent.enabled = true;
            Debug.Log($"[RoomWanderingBehavior] AI {gameObject.name}: NavMeshAgent 재활성화");
            yield return null; // 한 프레임 대기
        }
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogWarning($"[RoomWanderingBehavior] AI {gameObject.name}: 유효하지 않은 방 인덱스 - 행동 재결정");
            DetermineBehaviorByTime();
            yield break;
        }

        // NavMeshAgent가 NavMesh 위에 있는지 확인
        if (agent == null || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[RoomWanderingBehavior] AI {gameObject.name}: NavMeshAgent가 NavMesh 위에 없음 - 행동 재결정");
            DetermineBehaviorByTime();
            yield break;
        }

        float wanderingTime = Random.Range(15f, 30f);
        float elapsedTime = 0f;
        Debug.Log($"[RoomWanderingBehavior] AI {gameObject.name}: 배회 시간 설정 - {wanderingTime:F1}초");

        while (currentState == AIState.RoomWandering && elapsedTime < wanderingTime && agent.isOnNavMesh)
        {
            // 방 내부에서만 배회
            if (TryGetRoomWanderingPosition(currentRoomIndex, out Vector3 roomPosition))
            {
                agent.SetDestination(roomPosition);
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh && 
                                                !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                // 방 내부 위치를 찾지 못한 경우 기존 방식 사용
                Vector3 roomCenter = roomList[currentRoomIndex].transform.position;
                float roomSize = roomList[currentRoomIndex].size * 0.5f;
                if (TryGetValidPosition(roomCenter, roomSize, NavMesh.AllAreas, out Vector3 fallbackPos))
                {
                    agent.SetDestination(fallbackPos);
                }
            }

            float waitTime = Random.Range(3f, 6f);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        Debug.Log($"[RoomWanderingBehavior] AI {gameObject.name}: 방 내부 배회 종료 - 행동 재결정 (경과 시간: {elapsedTime:F1}초)");
        DetermineBehaviorByTime();
    }

    private void WanderOnGround()
    {
        float wanderDistance = Random.Range(20f, 40f);
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0;
        randomDirection.Normalize();
        
        Vector3 randomPoint = transform.position + randomDirection * wanderDistance;
        
        int groundMask = NavMesh.GetAreaFromName("Ground");
        if (groundMask == 0)
        {
            return;
        }

        // 방을 피해서 배회하도록 수정
        if (TryGetWanderingPositionAvoidingRooms(randomPoint, 15f, groundMask, out Vector3 validPosition))
        {
            agent.SetDestination(validPosition);
        }
        else
        {
            // 방을 피하지 못한 경우 기본 방식으로 시도
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 15f, groundMask))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
    
    /// <summary>
    /// 방들을 피해서 배회 위치를 찾는 메서드
    /// </summary>
    private bool TryGetWanderingPositionAvoidingRooms(Vector3 targetPoint, float searchRadius, int layerMask, out Vector3 result)
    {
        result = targetPoint;
        
        for (int i = 0; i < maxRetries * 2; i++) // 더 많이 시도
        {
            Vector3 testPoint = targetPoint + Random.insideUnitSphere * searchRadius;
            testPoint.y = targetPoint.y; // Y축 고정
            
            if (NavMesh.SamplePosition(testPoint, out NavMeshHit hit, searchRadius, layerMask))
            {
                // 방과의 거리 체크
                bool tooCloseToRoom = false;
                foreach (var room in roomList)
                {
                    if (room != null && room.gameObject != null)
                    {
                        float distanceToRoom = Vector3.Distance(hit.position, room.transform.position);
                        // 방 크기의 1.5배 이상 떨어져 있어야 함
                        if (distanceToRoom < room.size * 1.5f)
                        {
                            tooCloseToRoom = true;
                            break;
                        }
                    }
                }
                
                if (!tooCloseToRoom)
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 방 내부에서만 배회하는 위치를 찾는 메서드
    /// </summary>
    private bool TryGetRoomWanderingPosition(int roomIndex, out Vector3 result)
    {
        result = Vector3.zero;
        
        if (roomIndex < 0 || roomIndex >= roomList.Count)
            return false;
            
        var room = roomList[roomIndex];
        if (room == null || room.gameObject == null)
            return false;
            
        Bounds roomBounds = room.bounds;
        Vector3 roomCenter = roomBounds.center;
        
        for (int i = 0; i < maxRetries * 3; i++)
        {
            // 방 내부의 랜덤한 위치 생성
            Vector3 randomPoint = new Vector3(
                Random.Range(roomBounds.min.x + 1f, roomBounds.max.x - 1f),
                roomCenter.y,
                Random.Range(roomBounds.min.z + 1f, roomBounds.max.z - 1f)
            );
            
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                // 방 경계 내부인지 확인
                if (roomBounds.Contains(hit.position))
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        
        return false;
    }
    #endregion

    #region 침대 관련 메서드
    /// <summary>
    /// 현재 방에서 침대를 찾습니다.
    /// </summary>
    private bool FindBedInCurrentRoom(out Transform bedTransform)
    {
        bedTransform = null;
        
        Debug.Log($"[FindBedInCurrentRoom] AI {gameObject.name}: 침대 찾기 시작 (방 인덱스: {currentRoomIndex})");
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogWarning($"[FindBedInCurrentRoom] AI {gameObject.name}: 유효하지 않은 방 인덱스 ({currentRoomIndex}/{roomList.Count})");
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.bedTransform == null)
        {
            Debug.LogWarning($"[FindBedInCurrentRoom] AI {gameObject.name}: 방이 null이거나 침대가 없음 (방 null: {room == null}, 침대 null: {room?.bedTransform == null})");
            return false;
        }

        bedTransform = room.bedTransform;
        Debug.Log($"[FindBedInCurrentRoom] AI {gameObject.name}: 침대 찾기 성공 - 위치: {bedTransform.position}");
        return true;
    }

    /// <summary>
    /// 침대로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToBedBehavior()
    {
        Debug.Log($"[MoveToBedBehavior] AI {gameObject.name}: 침대로 이동 시작");
        
        if (currentBedTransform == null)
        {
            Debug.LogWarning($"[MoveToBedBehavior] AI {gameObject.name}: 침대 Transform이 null - 행동 재결정");
            DetermineBehaviorByTime();
            yield break;
        }

        // 침대 위치로 이동
        agent.SetDestination(currentBedTransform.position);
        Debug.Log($"[MoveToBedBehavior] AI {gameObject.name}: 침대 위치로 이동 중 ({currentBedTransform.position})");

        // 침대에 도착할 때까지 대기
        float timeout = 10f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (timer >= timeout)
            {
                Debug.LogWarning($"[MoveToBedBehavior] AI {gameObject.name}: 침대 도착 타임아웃 ({timeout}초) - 행동 재결정");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        Debug.Log($"[MoveToBedBehavior] AI {gameObject.name}: 침대 도착 완료 - 수면 시작");
        // 침대에 도착했으므로 수면 시작
        StartSleeping();
    }

    /// <summary>
    /// 수면을 시작합니다.
    /// </summary>
    private void StartSleeping()
    {
        Debug.Log($"[StartSleeping] AI {gameObject.name}: 수면 시작 시도");
        
        if (currentBedTransform == null)
        {
            Debug.LogWarning($"[StartSleeping] AI {gameObject.name}: 침대 Transform이 null - 수면 불가");
            return;
        }

        // 침대 위치와 각도로 Transform 설정
        transform.position = currentBedTransform.position;
        transform.rotation = currentBedTransform.rotation;
        Debug.Log($"[StartSleeping] AI {gameObject.name}: 침대 위치로 이동 완료 ({currentBedTransform.position})");

        // NavMeshAgent 일시 정지
        if (agent != null)
        {
            agent.enabled = false;
            Debug.Log($"[StartSleeping] AI {gameObject.name}: NavMeshAgent 비활성화");
        }

        // 수면 상태 설정
        isSleeping = true;
        Debug.Log($"[StartSleeping] AI {gameObject.name}: 수면 상태로 전환 (isSleeping=true)");
        TransitionToState(AIState.Sleeping);
    }

    /// <summary>
    /// 수면에서 깨어납니다.
    /// </summary>
    private void WakeUp()
    {
        Debug.Log($"[WakeUp] AI {gameObject.name}: 기상 시도 (isSleeping={isSleeping})");
        
        if (!isSleeping)
        {
            Debug.LogWarning($"[WakeUp] AI {gameObject.name}: 수면 중이 아니므로 기상 불가");
            return;
        }

        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"[WakeUp] AI {gameObject.name}: NavMeshAgent 활성화");
        }

        // 저장된 위치로 복귀
        transform.position = preSleepPosition;
        transform.rotation = preSleepRotation;
        Debug.Log($"[WakeUp] AI {gameObject.name}: 수면 전 위치로 복귀 ({preSleepPosition})");

        // 수면 상태 해제
        isSleeping = false;
        currentBedTransform = null;
        Debug.Log($"[WakeUp] AI {gameObject.name}: 수면 상태 해제 (isSleeping=false)");

        // 9시 기상 시 결제 정보 확인 및 등록 (방 배정 시 등록되지 않은 경우 대비)
        if (currentRoomIndex != -1)
        {
            var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
            var roomManager = FindFirstObjectByType<RoomManager>();
            if (roomManager != null && room != null)
            {
                Debug.Log($"[WakeUp] AI {gameObject.name}: 9시 기상, 결제 정보 등록 (방 인덱스: {currentRoomIndex})");
                roomManager.ReportRoomUsage(gameObject.name, room);
            }
        }

        // 방 사용 완료 보고로 전환
        Debug.Log($"[WakeUp] AI {gameObject.name}: 방 사용 완료 보고를 위해 ReportingRoomQueue로 전환");
        TransitionToState(AIState.ReportingRoomQueue);
    }
    #endregion

    #region 선베드 관련 메서드
    /// <summary>
    /// 현재 방에서 선베드를 찾습니다.
    /// </summary>
    private bool FindSunbedInCurrentRoom(out Transform sunbedTransform)
    {
        sunbedTransform = null;
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return false;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return false;
        }

        // 방 내부에서 "Sunbed" 태그를 가진 오브젝트 찾기
        var allSunbeds = GameObject.FindGameObjectsWithTag("Sunbed");
        
        foreach (var sunbed in allSunbeds)
        {
            if (sunbed != null)
            {
                bool isInRoom = room.bounds.Contains(sunbed.transform.position);
                
                if (isInRoom)
                {
                    sunbedTransform = sunbed.transform;
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// 사용 가능한 선베드 방을 찾아서 배정합니다.
    /// </summary>
    private bool TryFindAvailableSunbedRoom()
    {
        lock (lockObject)
        {
            // 선베드 방 중에서 사용 가능한 방 찾기
            var availableSunbedRooms = roomList.Select((room, index) => new { room, index })
                                             .Where(r => !r.room.isOccupied && IsSunbedRoom(r.room))
                                             .Select(r => r.index)
                                             .ToList();

            if (availableSunbedRooms.Count == 0)
            {
                return false;
            }

            int selectedRoomIndex = availableSunbedRooms[Random.Range(0, availableSunbedRooms.Count)];
            if (!roomList[selectedRoomIndex].isOccupied)
            {
                roomList[selectedRoomIndex].isOccupied = true;
                currentRoomIndex = selectedRoomIndex;
                
                // 선베드로 이동
                if (FindSunbedInCurrentRoom(out Transform sunbedTransform))
                {
                    // 선베드 방 배정 확정 후 결제 정보 등록
                    var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
                    var roomManager = FindFirstObjectByType<RoomManager>();
                    if (roomManager != null && room != null)
                    {
                        Debug.Log($"[선베드 방 배정] AI {gameObject.name}: 선베드 방 배정 완료, 즉시 결제 정보 등록");
                        roomManager.ReportRoomUsage(gameObject.name, room);
                    }
                    
                    currentSunbedTransform = sunbedTransform;
                    // 타이머 바로 시작 (이동할 때부터)
                    sunbedCoroutine = StartCoroutine(SunbedUsageTimer());
                    TransitionToState(AIState.MovingToSunbed);
                    return true;
                }
                else
                {
                    // 선베드를 찾지 못한 경우 방 배정 취소
                    roomList[selectedRoomIndex].isOccupied = false;
                    currentRoomIndex = -1;
                    return false;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 방이 선베드 방인지 확인합니다.
    /// </summary>
    private bool IsSunbedRoom(RoomInfo room)
    {
        if (room == null || room.gameObject == null)
            return false;

        var roomContents = room.gameObject.GetComponent<RoomContents>();
        return roomContents != null && roomContents.isSunbedRoom;
    }

    /// <summary>
    /// 선베드로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToSunbedBehavior()
    {
        if (currentSunbedTransform == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // NavMesh 상태 확인
        if (!agent.isOnNavMesh)
        {
            CleanupSunbedMovement();
            yield break;
        }

        // 선베드 위치로 이동
        bool pathSet = agent.SetDestination(currentSunbedTransform.position);
        
        if (!pathSet || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            CleanupSunbedMovement();
            yield break;
        }

        // 선베드에 도착할 때까지 대기
        float timeout = 30f; // 타임아웃 증가
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            // NavMesh에서 벗어났는지 체크
            if (!agent.isOnNavMesh)
            {
                CleanupSunbedMovement();
                yield break;
            }
            
            if (timer >= timeout)
            {
                CleanupSunbedMovement();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 선베드에 도착했으므로 사용 시작
        StartUsingSunbed();
    }

    /// <summary>
    /// 선베드로 이동 실패 시 리소스 정리
    /// </summary>
    private void CleanupSunbedMovement()
    {
        // 방 배정 해제
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                }
                currentRoomIndex = -1;
            }
        }
        
        // 선베드 관련 변수 정리
        currentSunbedTransform = null;
        preSunbedPosition = Vector3.zero;
        preSunbedRotation = Quaternion.identity;
        
        // 다른 행동으로 전환
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 선베드 사용을 시작합니다.
    /// </summary>
    private void StartUsingSunbed()
    {
        if (currentSunbedTransform == null)
        {
            return;
        }

        // 이미 선베드 사용 중인지 확인
        if (isUsingSunbed)
        {
            return;
        }

        // NavMeshAgent 상태 체크
        if (agent != null && !agent.isOnNavMesh)
        {
            return;
        }

        // 현재 위치 저장 (선베드 위치로 이동하기 전에)
        preSunbedPosition = transform.position;
        preSunbedRotation = transform.rotation;
        
        // 선베드 위치와 각도로 Transform 설정
        transform.position = currentSunbedTransform.position;
        transform.rotation = currentSunbedTransform.rotation;

        // NavMeshAgent는 끄지 않고 이동만 중지
        if (agent != null)
        {
            agent.isStopped = true;
        }

        // 선베드 사용 상태 설정
        isUsingSunbed = true;
        TransitionToState(AIState.UsingSunbed);
        
        // 타이머가 없다면 다시 시작 (안전장치)
        if (sunbedCoroutine == null)
        {
            sunbedCoroutine = StartCoroutine(SunbedUsageTimer());
        }
        
        // BedTime 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("BedTime", true);
        }
    }

    /// <summary>
    /// 선베드 사용 시간 타이머 (게임 시간 50분)
    /// </summary>
    private IEnumerator SunbedUsageTimer()
    {
        // TimeSystem 안전성 체크
        if (timeSystem == null)
        {
            timeSystem = TimeSystem.Instance;
            if (timeSystem == null)
            {
                yield return new WaitForSeconds(50f);
                if (isUsingSunbed) FinishUsingSunbed();
                sunbedCoroutine = null;
                yield break;
            }
        }

        // 게임 시간으로 50분 대기
        int startHour = timeSystem.CurrentHour;
        int startMinute = timeSystem.CurrentMinute;
        int startTotalMinutes = startHour * 60 + startMinute;
        
        // 50분 후의 시간 계산
        int targetTotalMinutes = startTotalMinutes + 50;
        int targetHour = (targetTotalMinutes / 60) % 24;
        int targetMinute = targetTotalMinutes % 60;
        
        // 안전장치: 최대 대기 시간 (실제 시간 10분)
        float maxWaitTime = 600f;
        float elapsedTime = 0f;
        float checkInterval = 0.5f; // 0.5초마다 체크 (빠른 감지)
        
        // 게임 시간이 목표 시간에 도달할 때까지 대기
        while (isUsingSunbed && elapsedTime < maxWaitTime)
        {
            // TimeSystem 재확인
            if (timeSystem == null)
            {
                timeSystem = TimeSystem.Instance;
                if (timeSystem == null)
                {
                    break;
                }
            }
            
            int currentHour = timeSystem.CurrentHour;
            int currentMinute = timeSystem.CurrentMinute;
            int currentTotalMinutes = currentHour * 60 + currentMinute;
            
            // 16시 이후 강제 종료 (15시까지만 선베드 사용 가능)
            if (currentHour >= 16)
            {
                break;
            }
            
            // 목표 시간 도달 체크
            bool timeReached = false;
            
            // 같은 날인 경우 (자정을 넘지 않음)
            if (targetTotalMinutes < 1440) // 1440분 = 24시간
            {
                if (currentTotalMinutes >= targetTotalMinutes)
                {
                    timeReached = true;
                }
            }
            else
            {
                // 다음 날로 넘어가는 경우
                int nextDayTargetMinutes = targetTotalMinutes - 1440;
                if (currentTotalMinutes >= nextDayTargetMinutes || currentTotalMinutes < startTotalMinutes)
                {
                    timeReached = true;
                }
            }
            
            // 시간이 크게 점프한 경우 감지 (게임 시간 빨리 감기)
            int timeDifference = currentTotalMinutes - startTotalMinutes;
            if (timeDifference < 0) timeDifference += 1440; // 하루 넘어간 경우 보정
            
            if (timeDifference >= 50) // 50분 이상 지났으면
            {
                timeReached = true;
            }
            
            if (timeReached)
            {
                break;
            }
            
            yield return new WaitForSeconds(checkInterval);
            elapsedTime += checkInterval;
        }
        
        // 선베드 사용 종료
        if (isUsingSunbed)
        {
            FinishUsingSunbed();
        }
        
        // 코루틴 참조 정리
        sunbedCoroutine = null;
    }

    /// <summary>
    /// 선베드 사용을 종료합니다.
    /// </summary>
    private void FinishUsingSunbed()
    {
        if (!isUsingSunbed)
        {
            return;
        }

        // 1. BedTime 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }

        // 2. 저장된 위치로 복귀
        transform.position = preSunbedPosition;
        transform.rotation = preSunbedRotation;

        // 3. NavMeshAgent 이동 재시작
        if (agent != null)
        {
            agent.isStopped = false;
        }

        // 4. 선베드 사용 상태 해제 (안전하게)
        isUsingSunbed = false;
        currentSunbedTransform = null;

        // 5. 선베드 결제 처리
        ProcessSunbedPayment();
    }

    /// <summary>
    /// GameObject 비활성화 시 코루틴 없이 선베드 결제를 직접 처리합니다.
    /// </summary>
    private void ProcessSunbedPaymentDirectly()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            return;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            return;
        }

        var roomContents = room.gameObject.GetComponent<RoomContents>();
        if (roomContents != null && roomContents.isSunbedRoom)
        {
            // 선베드 방의 고정 가격과 명성도 사용
            int price = roomContents.TotalRoomPrice;
            int reputation = roomContents.TotalRoomReputation;
            
            // PaymentSystem을 통한 실제 결제 처리
            var paymentSystem = PaymentSystem.Instance;
            if (paymentSystem != null)
            {
                // 결제 정보를 PaymentSystem에 추가
                paymentSystem.AddPayment(gameObject.name, price, roomContents.roomID, reputation);
                
                // 결제 처리 실행
                int totalAmount = paymentSystem.ProcessPayment(gameObject.name);
            }
        }

        // 방 사용 완료 처리 (선베드 사용 완료 후 방 반납)
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                }
                // currentRoomIndex를 -1로 설정하여 방 사용 완료 처리
                currentRoomIndex = -1;
            }
        }
    }
    
    /// <summary>
    /// 17시 강제 디스폰 시 선베드 사용을 강제로 종료합니다.
    /// </summary>
    private void ForceFinishUsingSunbed()
    {
        if (!isUsingSunbed)
        {
            return;
        }

        // 1. BedTime 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
        }

        // 2. NavMeshAgent 이동 재시작
        if (agent != null)
        {
            agent.isStopped = false;
        }

        // 3. 저장된 위치로 복귀
        transform.position = preSunbedPosition;
        transform.rotation = preSunbedRotation;

        // 4. 선베드 사용 상태 해제 (안전하게)
        isUsingSunbed = false;
        currentSunbedTransform = null;

        // 5. 선베드 결제 처리 (강제 종료 시에도 결제)
        ProcessSunbedPayment(true);  // 강제 디스폰 플래그 전달
    }

    /// <summary>
    /// 선베드 결제를 처리합니다.
    /// </summary>
    private void ProcessSunbedPayment(bool isForcedDespawn = false)
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            TransitionToState(AIState.Wandering);
            return;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            TransitionToState(AIState.Wandering);
            return;
        }

        var roomContents = room.gameObject.GetComponent<RoomContents>();
        if (roomContents != null && roomContents.isSunbedRoom)
        {
            // 선베드 방의 고정 가격과 명성도 사용
            int price = roomContents.TotalRoomPrice;
            int reputation = roomContents.TotalRoomReputation;
            
            // PaymentSystem을 통한 실제 결제 처리
            var paymentSystem = PaymentSystem.Instance;
            if (paymentSystem != null)
            {
                // 결제 정보를 PaymentSystem에 추가
                paymentSystem.AddPayment(gameObject.name, price, roomContents.roomID, reputation);
                
                // 결제 처리 실행
                int totalAmount = paymentSystem.ProcessPayment(gameObject.name);
            }
        }

        // 방 사용 완료 처리 (선베드 사용 완료 후 방 반납)
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                }
                // currentRoomIndex를 -1로 설정하여 방 사용 완료 처리
                currentRoomIndex = -1;
            }
        }

        // 결제 및 방 반납 완료 후 상태 전환
        if (isForcedDespawn)
        {
            // 17시 강제 디스폰인 경우 디스폰 상태로 전환
            TransitionToState(AIState.ReturningToSpawn);
            agent.SetDestination(spawnPoint.position);
        }
        else
        {
            // 일반적인 경우 배회로 전환
            TransitionToState(AIState.Wandering);
        }
    }
    #endregion

    #region 식당 관련 메서드
    /// <summary>
    /// 사용 가능한 주방 카운터를 찾아서 대기열에 진입합니다.
    /// </summary>
    private bool TryFindAvailableKitchen()
    {
        // KitchenCounter 컴포넌트를 가진 오브젝트들 찾기
        KitchenCounter[] kitchenCounters = FindObjectsByType<KitchenCounter>(FindObjectsSortMode.None);
        
        if (kitchenCounters.Length == 0)
        {
            return false;
        }

        // 가장 가까운 주방 카운터 찾기
        KitchenCounter closestCounter = null;
        float closestDistance = float.MaxValue;

        foreach (KitchenCounter counter in kitchenCounters)
        {
            if (counter == null) continue;

            float distance = Vector3.Distance(transform.position, counter.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestCounter = counter;
            }
        }

        if (closestCounter != null)
        {
            // 주방 카운터 대기열에 진입 시도
            if (closestCounter.TryJoinQueue(this))
            {
                currentKitchenCounter = closestCounter;
                
                // 해당 주방에서 사용 가능한 의자 미리 선택
                if (FindAvailableChairInKitchen(closestCounter, out Transform selectedChair, out Transform kitchen))
                {
                    currentKitchenTransform = kitchen;
                    currentChairTransform = selectedChair;
                    // 위치 저장은 의자로 이동할 때 하도록 변경 (MoveToChairBehavior에서 처리)
                    
                    TransitionToState(AIState.MovingToKitchenCounter);
                    return true;
                }
                else
                {
                    // 의자가 없으면 대기열에서 나가기
                    closestCounter.LeaveQueue(this);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// 주방에서 사용 가능한 의자 찾기 (단순화된 버전)
    /// </summary>
    private bool FindAvailableChairInKitchen(KitchenCounter counter, out Transform selectedChair, out Transform kitchen)
    {
        selectedChair = null;
        kitchen = null;

        // 모든 KitchenChair 태그 오브젝트 직접 찾기
        GameObject[] allChairs = GameObject.FindGameObjectsWithTag("KitchenChair");
        
        if (allChairs.Length == 0)
        {
            return false;
        }

        List<Transform> availableChairs = new List<Transform>();

        foreach (GameObject chairObj in allChairs)
        {
            if (chairObj == null) continue;

            // 카운터와 의자가 너무 멀지 않은지 확인 (50미터 이내)
            float distance = Vector3.Distance(counter.transform.position, chairObj.transform.position);
            if (distance > 50f) 
            {
                continue;
            }

            // 의자가 사용 중인지 확인
            if (!IsChairOccupied(chairObj.transform))
            {
                availableChairs.Add(chairObj.transform);
            }
            else
            {
            }
        }

        if (availableChairs.Count > 0)
        {
            // 랜덤하게 의자 선택
            selectedChair = availableChairs[Random.Range(0, availableChairs.Count)];
            kitchen = selectedChair; // 의자 자체를 kitchen으로 설정 (단순화)
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// 의자가 사용 중인지 확인합니다.
    /// </summary>
    private bool IsChairOccupied(Transform chair)
    {
        // 다른 AI가 같은 의자를 사용 중인지 확인
        AIAgent[] allAgents = FindObjectsByType<AIAgent>(FindObjectsSortMode.None);
        foreach (AIAgent agent in allAgents)
        {
            if (agent != this && agent.currentChairTransform == chair)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 주방 카운터로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToKitchenCounterBehavior()
    {
        if (currentKitchenCounter == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // 대기열 위치에 실제로 도착할 때까지 대기
        float timeout = 15f;
        float timer = 0f;
        
        // NavMeshAgent가 대기열 위치를 설정받을 때까지 잠시 대기
        yield return new WaitForSeconds(0.1f);
        
        while (true)
        {
            // 타임아웃 체크
            if (timer >= timeout)
            {
                // 대기열에서 나가기
                if (currentKitchenCounter != null)
                {
                    currentKitchenCounter.LeaveQueue(this);
                }
                CleanupKitchenVariables();
                DetermineBehaviorByTime();
                yield break;
            }
            
            // NavMeshAgent가 유효하고 경로가 설정되었는지 확인
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                // 경로가 계산 중이 아니고, 목적지에 도착했는지 확인
                if (!agent.pathPending && agent.remainingDistance <= arrivalDistance)
                {
                    break;
                }
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 대기열 위치에 실제로 도착한 후 대기 상태로 전환
        isWaitingAtKitchenCounter = true;
        
        // 주방 카운터에 도착했음을 알림 (서비스 시작 가능)
        if (currentKitchenCounter != null)
        {
            // 카운터의 ProcessCustomerQueue가 자동으로 호출되어 서비스 시작 확인
        }
        
        TransitionToState(AIState.WaitingAtKitchenCounter);
    }

    /// <summary>
    /// 주방 카운터 서비스 완료 시 호출 (KitchenCounter에서 호출)
    /// </summary>
    public void OnKitchenServiceComplete()
    {
        isWaitingAtKitchenCounter = false;
        
        // 주문 결제 처리 (100원, 명성도 50)
        ProcessKitchenOrderPayment();
        
        // 의자로 이동
        if (currentChairTransform != null)
        {
            TransitionToState(AIState.MovingToChair);
        }
        else
        {
            CleanupKitchenVariables();
            DetermineBehaviorByTime();
        }
    }
    
    /// <summary>
    /// 주방 주문 결제 처리
    /// </summary>
    private void ProcessKitchenOrderPayment()
    {
        int orderPrice = 100;      // 주문 가격 100원
        int orderReputation = 50;  // 명성도 50
        string kitchenOrderID = "KITCHEN_ORDER"; // 주방 주문 고유 ID
        
        // PaymentSystem을 통해 결제 처리
        var paymentSystem = PaymentSystem.Instance;
        if (paymentSystem != null)
        {
            // 결제 정보를 PaymentSystem에 추가
            paymentSystem.AddPayment(gameObject.name, orderPrice, kitchenOrderID, orderReputation);
            
            // 결제 처리 실행
            int totalAmount = paymentSystem.ProcessPayment(gameObject.name);
        }
        else
        {
        }
    }

    /// <summary>
    /// 주방 관련 변수들 정리
    /// </summary>
    private void CleanupKitchenVariables()
    {
        currentKitchenCounter = null;
        currentKitchenTransform = null;
        currentChairTransform = null;
        isWaitingAtKitchenCounter = false;
        isEating = false;
        preChairPosition = Vector3.zero;
        preChairRotation = Quaternion.identity;
    }

    /// <summary>
    /// 식당으로 이동하는 코루틴 (사용하지 않음 - 호환성을 위해 유지)
    /// </summary>
    private IEnumerator MoveToKitchenBehavior()
    {
        if (currentKitchenTransform == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // 식당 위치로 이동
        agent.SetDestination(currentKitchenTransform.position);

        // 식당에 도착할 때까지 대기
        float timeout = 15f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (timer >= timeout)
            {
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 식당에 도착했으므로 의자로 이동
        TransitionToState(AIState.MovingToChair);
    }

    /// <summary>
    /// 의자로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToChairBehavior()
    {
        if (currentChairTransform == null)
        {
            DetermineBehaviorByTime();
            yield break;
        }

        // 의자로 이동하기 전 현재 위치 저장 (정확한 위치 기록)
        preChairPosition = transform.position;
        preChairRotation = transform.rotation;

        // 의자 위치로 이동
        agent.SetDestination(currentChairTransform.position);

        // 의자에 도착할 때까지 대기
        float timeout = 10f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (timer >= timeout)
            {
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 의자에 도착했으므로 식사 시작
        StartEating();
    }

    /// <summary>
    /// 식사를 시작합니다.
    /// </summary>
    private void StartEating()
    {
        if (currentChairTransform == null)
        {
            return;
        }

        // 의자 위치와 각도로 Transform 설정 (의자 각도 + 90도)
        transform.position = currentChairTransform.position;
        transform.rotation = currentChairTransform.rotation * Quaternion.Euler(0, 90, 0);

        // NavMeshAgent 비활성화 (침대와 동일하게)
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 식사 상태 설정
        isEating = true;
        TransitionToState(AIState.Eating);
        
        // 10초 후 자동으로 식사 종료
        eatingCoroutine = StartCoroutine(EatingTimer());
    }

    /// <summary>
    /// 식사 시간 타이머 (10초)
    /// </summary>
    private IEnumerator EatingTimer()
    {
        // 10초 대기
        yield return new WaitForSeconds(10f);
        
        // 10초 후 식사 종료
        FinishEating();
    }

    /// <summary>
    /// 식사를 종료합니다.
    /// </summary>
    private void FinishEating()
    {
        if (!isEating)
            return;

        // NavMeshAgent 다시 활성화 (침대와 동일하게)
        if (agent != null)
        {
            agent.enabled = true;
        }

        // 저장된 위치로 복귀
        transform.position = preChairPosition;
        transform.rotation = preChairRotation;

        // 식사 상태 해제
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;

        // 방이 있는 사람은 방 밖 배회, 방 없는 사람은 배회
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.UseWandering);
        }
        else
        {
            TransitionToState(AIState.Wandering);
        }
    }

    /// <summary>
    /// 17시 강제 디스폰 시 식사를 강제로 종료합니다.
    /// </summary>
    /// <summary>
    /// 주방 카운터 관련 활동 강제 종료
    /// </summary>
    private void ForceFinishKitchenActivity()
    {
        // 주방 카운터 대기열에서 제거
        if (currentKitchenCounter != null)
        {
            currentKitchenCounter.LeaveQueue(this);
        }

        // NavMeshAgent 상태 복원
        if (agent != null)
        {
            agent.isStopped = false;
        }

        // 주방 관련 변수들 정리
        CleanupKitchenVariables();

        // 강제 디스폰
        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
    }

    private void ForceFinishEating()
    {
        if (!isEating)
            return;

        // NavMeshAgent 다시 활성화 (침대와 동일하게)
        if (agent != null)
        {
            agent.enabled = true;
        }

        // 저장된 위치로 복귀
        transform.position = preChairPosition;
        transform.rotation = preChairRotation;

        // 식사 상태 해제
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;
    }
    #endregion

    #region 유틸리티 메서드
    private bool TryGetValidPosition(Vector3 center, float radius, int layerMask, out Vector3 result)
    {
        result = center;
        float searchRadius = radius * 0.8f;

        for (int i = 0; i < maxRetries; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            Vector3 randomPoint = center + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, searchRadius, layerMask))
            {
                if (Vector3.Distance(hit.position, center) <= searchRadius)
                {
                    result = hit.position;
                    return true;
                }
            }
        }
        return false;
    }

    public void SetSpawner(AISpawner spawnerRef)
    {
        spawner = spawnerRef;
    }

    private void ReturnToPool()
    {
        CleanupCoroutines();
        CleanupResources();

        if (spawner != null)
        {
            spawner.ReturnToPool(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region 오브젝트 유효성 검사
    
    private float lastObjectCheckTime = 0f;
    
    /// <summary>
    /// 주기적으로 오브젝트 유효성 검사 (3초마다)
    /// </summary>
    private void CheckObjectValidity()
    {
        // 3초마다만 검사 (성능 최적화)
        if (Time.time - lastObjectCheckTime < 3f)
            return;
            
        lastObjectCheckTime = Time.time;
        
        // 카운터 매니저 체크
        if (counterManager != null && counterManager.gameObject == null)
        {
            Debug.LogWarning($"[오브젝트 체크] AI {gameObject.name}: CounterManager가 삭제됨 - 대기열에서 나가고 배회");
            HandleCounterDestroyed();
        }
        
        // 카운터 위치 체크
        if (counterPosition != null && counterPosition.gameObject == null)
        {
            Debug.LogWarning($"[오브젝트 체크] AI {gameObject.name}: Counter 오브젝트가 삭제됨 - 대기열에서 나가고 배회");
            HandleCounterDestroyed();
        }
        
        // 주방 카운터 체크
        if (currentKitchenCounter != null && currentKitchenCounter.gameObject == null)
        {
            Debug.LogWarning($"[오브젝트 체크] AI {gameObject.name}: KitchenCounter가 삭제됨 - 식사 중단하고 배회");
            HandleKitchenCounterDestroyed();
        }
        
        // 의자 체크 (식사 중일 때)
        if (isEating && currentChairTransform != null && currentChairTransform.gameObject == null)
        {
            Debug.LogWarning($"[오브젝트 체크] AI {gameObject.name}: 의자가 삭제됨 - 식사 강제 종료하고 배회");
            HandleChairDestroyed();
        }
    }
    
    /// <summary>
    /// 카운터가 삭제되었을 때 처리
    /// </summary>
    private void HandleCounterDestroyed()
    {
        // 대기열에서 나가기
        if (isInQueue || isWaitingForService)
        {
            isInQueue = false;
            isWaitingForService = false;
        }
        
        // 코루틴 정리
        CleanupCoroutines();
        
        // 배회로 전환
        if (currentState == AIState.MovingToQueue || 
            currentState == AIState.WaitingInQueue || 
            currentState == AIState.ReportingRoomQueue)
        {
            TransitionToState(AIState.Wandering);
        }
    }
    
    /// <summary>
    /// 주방 카운터가 삭제되었을 때 처리
    /// </summary>
    private void HandleKitchenCounterDestroyed()
    {
        // 주방 관련 변수 정리
        CleanupKitchenVariables();
        
        // 코루틴 정리
        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }
        
        // 배회로 전환
        if (currentState == AIState.MovingToKitchenCounter || 
            currentState == AIState.WaitingAtKitchenCounter ||
            currentState == AIState.MovingToChair ||
            currentState == AIState.Eating)
        {
            if (currentRoomIndex != -1)
            {
                TransitionToState(AIState.UseWandering);
            }
            else
            {
                TransitionToState(AIState.Wandering);
            }
        }
    }
    
    /// <summary>
    /// 의자가 삭제되었을 때 처리
    /// </summary>
    private void HandleChairDestroyed()
    {
        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
        }
        
        // 식사 관련 변수 정리
        isEating = false;
        currentChairTransform = null;
        currentKitchenTransform = null;
        
        // 코루틴 정리
        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }
        
        // 배회로 전환
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.UseWandering);
        }
        else
        {
            TransitionToState(AIState.Wandering);
        }
    }
    
    #endregion

    #region 정리
    void OnDisable()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    void OnDestroy()
    {
        CleanupCoroutines();
        CleanupResources();
    }

    private void CleanupCoroutines()
    {
        Debug.Log($"[코루틴 정리] AI {gameObject.name}: 모든 코루틴 정리 시작 (wandering: {wanderingCoroutine != null}, queue: {queueCoroutine != null}, roomUse: {roomUseCoroutine != null}, sleeping: {sleepingCoroutine != null})");
        
        if (wanderingCoroutine != null)
        {
            Debug.Log($"[코루틴 정리] AI {gameObject.name}: wanderingCoroutine 중지");
            StopCoroutine(wanderingCoroutine);
            wanderingCoroutine = null;
        }
        if (queueCoroutine != null)
        {
            Debug.Log($"[코루틴 정리] AI {gameObject.name}: queueCoroutine 중지 ⚠️");
            StopCoroutine(queueCoroutine);
            queueCoroutine = null;
        }
        if (roomUseCoroutine != null)
        {
            StopCoroutine(roomUseCoroutine);
            roomUseCoroutine = null;
        }
        if (roomWanderingCoroutine != null)
        {
            StopCoroutine(roomWanderingCoroutine);
            roomWanderingCoroutine = null;
        }
        if (useWanderingCoroutine != null) // 새로 추가
        {
            StopCoroutine(useWanderingCoroutine);
            useWanderingCoroutine = null;
        }
        if (sleepingCoroutine != null)
        {
            StopCoroutine(sleepingCoroutine);
            sleepingCoroutine = null;
        }
        // sunbedCoroutine은 UsingSunbed 상태에서도 유지되어야 함
        if (sunbedCoroutine != null && currentState != AIState.UsingSunbed)
        {
            StopCoroutine(sunbedCoroutine);
            sunbedCoroutine = null;
        }
        if (eatingCoroutine != null)
        {
            StopCoroutine(eatingCoroutine);
            eatingCoroutine = null;
        }
    }

    private void CleanupResources()
    {
        // 수면 상태 정리
        if (isSleeping)
        {
            WakeUp();
        }
        
        // 선베드 사용 상태 정리 (애니메이션 지연 없이 직접 정리)
        if (isUsingSunbed)
        {
            // 1. BedTime 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("BedTime", false);
            }

            // 2. 저장된 위치로 복귀
            transform.position = preSunbedPosition;
            transform.rotation = preSunbedRotation;

            // 3. NavMeshAgent 이동 재시작
            if (agent != null)
            {
                agent.isStopped = false;
            }

            // 4. 선베드 사용 상태 해제
            isUsingSunbed = false;
            currentSunbedTransform = null;

            // 5. 선베드 결제 처리 (GameObject 비활성화 시에는 코루틴 시작 없이)
            ProcessSunbedPaymentDirectly();
        }
        
        // 식사 상태 정리 (GameObject 비활성화 시에는 코루틴 시작 없이)
        // 주방 카운터 관련 상태 정리
        if (isWaitingAtKitchenCounter || currentKitchenCounter != null)
        {
            // 주방 카운터 대기열에서 제거
            if (currentKitchenCounter != null)
            {
                currentKitchenCounter.LeaveQueue(this);
            }
            
            // NavMeshAgent 다시 활성화 (침대와 동일하게)
            if (agent != null)
            {
                agent.enabled = true;
            }
            
            // 주방 관련 변수들 정리
            CleanupKitchenVariables();
        }
        
        if (isEating)
        {
            // NavMeshAgent 다시 활성화 (침대와 동일하게)
            if (agent != null)
            {
                agent.enabled = true;
            }

            // 저장된 위치로 복귀
            transform.position = preChairPosition;
            transform.rotation = preChairRotation;

            // 식사 상태 해제
            isEating = false;
            currentKitchenTransform = null;
            currentChairTransform = null;
        }
        
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                }
                currentRoomIndex = -1;
            }
        }

        isInQueue = false;
        isWaitingForService = false;
        isScheduledForDespawn = false; // 디스폰 예정 플래그 리셋
        queueRetryCount = 0; // 대기열 재시도 횟수 리셋

        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
        }
    }
    #endregion

    #region UI
    void OnGUI()
    {
        // 디버그 UI가 활성화된 경우에만 표시
        if (!globalShowDebugUI) return;
        
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0)
        {
            Vector2 guiPosition = new Vector2(screenPos.x, Screen.height - screenPos.y);
            GUI.Label(new Rect(guiPosition.x - 50, guiPosition.y - 50, 100, 40), currentDestination);
        }
    }
    #endregion

    #region 공개 메서드
    public void InitializeAI()
    {
        currentState = AIState.MovingToQueue;
        currentDestination = "대기열로 이동 중";
        isInQueue = false;
        isWaitingForService = false;
        currentRoomIndex = -1;
        lastBehaviorUpdateHour = -1;
        queueRetryCount = 0; // 대기열 재시도 횟수 초기화
        
        // 수면 관련 초기화
        isSleeping = false;
        currentBedTransform = null;
        preSleepPosition = Vector3.zero;
        preSleepRotation = Quaternion.identity;
        
        // 선베드 관련 초기화
        isUsingSunbed = false;
        currentSunbedTransform = null;
        preSunbedPosition = Vector3.zero;
        preSunbedRotation = Quaternion.identity;
        
        // 식사 관련 초기화
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;
        preChairPosition = Vector3.zero;
        preChairRotation = Quaternion.identity;
        
        // 주방 카운터 관련 초기화
        currentKitchenCounter = null;
        isWaitingAtKitchenCounter = false;

        if (agent != null)
        {
            agent.ResetPath();
            DetermineInitialBehavior();
        }
    }

    void OnEnable()
    {
        InitializeAI();
    }

    public void SetQueueDestination(Vector3 position)
    {
        targetQueuePosition = position;
        if (agent != null)
        {
            agent.SetDestination(position);
        }
    }

    public void SetQueueDestination(Vector3 position, Quaternion rotation)
    {
        targetQueuePosition = position;
        targetQueueRotation = rotation;
        if (agent != null)
        {
            agent.SetDestination(position);
        }
    }

    public void OnServiceComplete()
    {
        isWaitingForService = false;
        // isInQueue는 여기서 false로 설정하지 않음
        // QueueBehavior 코루틴에서 서비스 완료 후 처리가 끝나면 자동으로 break로 빠져나감
    }
    #endregion
}
}