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
    private bool isWaitingForService = false;     // 서비스 대기 중인지 여부

    private AIState currentState = AIState.MovingToQueue;  // 현재 AI 상태
    private string currentDestination = "대기열로 이동 중";  // 현재 목적지 (UI 표시용)

    private static readonly object lockObject = new object();  // 스레드 동기화용 잠금 객체
    private Coroutine wanderingCoroutine;         // 배회 코루틴 참조
    private Coroutine roomUseCoroutine;           // 방 사용 코루틴 참조
    private Coroutine roomWanderingCoroutine;     // 방 내부 배회 코루틴 참조

    private Coroutine useWanderingCoroutine;  // 방 외부 배회 코루틴 참조
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
            if (collider == null)
            {
                Debug.LogWarning($"룸 {roomObj.name}에 Collider가 없습니다. 기본 크기(2) 사용.");
            }

            // 침대 탐지
            bedTransform = FindBedInRoom(roomObj);
            if (bedTransform != null)
            {
                Debug.Log($"룸 {roomObj.name}에서 침대 발견: {bedTransform.name}");
            }

            Vector3 pos = roomObj.transform.position;
            roomId = $"Room_{pos.x:F0}_{pos.z:F0}";
            Debug.Log($"룸 ID 생성: {roomId} at {pos}, Bounds: {bounds}");
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
            Debug.LogError($"AI {gameObject.name}: NavMeshAgent 컴포넌트가 없습니다.");
            Destroy(gameObject);
            return false;
        }
        
        if (animator == null)
        {
            Debug.LogWarning($"AI {gameObject.name}: Animator 컴포넌트가 없습니다. 애니메이션 기능을 사용할 수 없습니다.");
        }

        GameObject spawn = GameObject.FindGameObjectWithTag("Spawn");
        if (spawn == null)
        {
            Debug.LogError($"AI {gameObject.name}: Spawn 오브젝트를 찾을 수 없습니다.");
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
            if (counterManager == null)
            {
                Debug.LogWarning($"AI {gameObject.name}: CounterManager를 찾을 수 없습니다.");
                counterPosition = null;
            }
        }

        if (NavMesh.GetAreaFromName("Ground") == 0)
        {
            Debug.LogWarning($"AI {gameObject.name}: Ground NavMesh 영역이 설정되지 않았습니다.");
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
        Debug.Log($"AI {gameObject.name}: 룸 초기화 시작");

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
            Debug.Log($"AI {gameObject.name}: RoomDetector로 룸 감지 시작.");
        }
        else
        {
            GameObject[] taggedRooms = GameObject.FindGameObjectsWithTag("Room");
            foreach (GameObject room in taggedRooms)
            {
                if (!roomList.Any(r => r.gameObject == room))
                {
                    roomList.Add(new RoomInfo(room));
                }
            }
            Debug.Log($"AI {gameObject.name}: 태그로 {roomList.Count}개 룸 발견.");
        }

        if (roomList.Count == 0)
        {
            Debug.LogWarning($"AI {gameObject.name}: 룸을 찾을 수 없습니다!");
        }
        else
        {
            Debug.Log($"AI {gameObject.name}: {roomList.Count}개 룸 초기화 완료.");
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
                Debug.Log($"룸 리스트 업데이트 완료. 총 룸 수: {roomList.Count}");
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
            Debug.LogWarning($"AI {gameObject.name}: TimeSystem이 없습니다. 기본 행동으로 전환.");
            FallbackBehavior();
            return;
        }

        int hour = timeSystem.CurrentHour;
        int minute = timeSystem.CurrentMinute;

        // 17:00에 방 사용 중이 아닌 모든 에이전트 강제 디스폰
        if (hour == 17 && minute == 0)
        {
            Handle17OClockForcedDespawn();
            return;
        }

        if (hour >= 0 && hour < 9)
        {
            // 0:00 ~ 9:00
            if (currentRoomIndex != -1)
            {
                // 0시에 침대로 이동 시작
                if (hour == 0 && !isSleeping && currentState != AIState.MovingToBed && currentState != AIState.Sleeping)
                {
                    if (FindBedInCurrentRoom(out Transform bedTransform))
                    {
                        currentBedTransform = bedTransform;
                        // 현재 위치 저장
                        preSleepPosition = transform.position;
                        preSleepRotation = transform.rotation;
                        TransitionToState(AIState.MovingToBed);
                        Debug.Log($"AI {gameObject.name}: 0시, 침대로 이동 시작.");
                    }
                    else
                    {
                        // 침대가 없으면 기존 행동
                        TransitionToState(AIState.RoomWandering);
                        Debug.Log($"AI {gameObject.name}: 0시, 침대 없음, 방 내부 배회.");
                    }
                }
                else if (isSleeping)
                {
                    // 이미 수면 중이면 계속 수면
                    Debug.Log($"AI {gameObject.name}: 0~9시, 이미 수면 중.");
                }
                else
                {
                    // 0시가 아니거나 이미 침대 관련 상태가 아닌 경우
                    TransitionToState(AIState.RoomWandering);
                    Debug.Log($"AI {gameObject.name}: 0~9시, 방 내부 배회.");
                }
            }
            else
            {
                FallbackBehavior();
            }
        }
        else if (hour >= 9 && hour < 11)
        {
            // 9:00 ~ 11:00
            if (currentRoomIndex != -1)
            {
                // 9시에 수면 중인 AI를 깨움
                if (hour == 9 && isSleeping)
                {
                    WakeUp();
                    Debug.Log($"AI {gameObject.name}: 9시, 수면에서 깨어남.");
                }
                
                // 수면이 아닌 경우 방 사용 완료 보고
                if (!isSleeping)
                {
                    TransitionToState(AIState.ReportingRoomQueue);
                    Debug.Log($"AI {gameObject.name}: 9~11시, 방 사용 완료 보고 대기열로 이동.");
                }
            }
            else
            {
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
                        Debug.Log($"AI {gameObject.name}: 11~16시, 방 없음, 대기열로 이동 (10%).");
                    }
                    else if (randomValue < 0.25f && hour >= 11 && hour <= 15)
                    {
                        // 선베드 사용 시도 (11-15시만, 15% 확률)
                        if (TryFindAvailableSunbedRoom())
                        {
                            Debug.Log($"AI {gameObject.name}: 11~15시, 방 없음, 선베드 방으로 이동 (15%).");
                        }
                        else
                        {
                            TransitionToState(AIState.Wandering);
                            Debug.Log($"AI {gameObject.name}: 11~15시, 방 없음, 선베드 없어서 배회 (15%).");
                        }
                    }
                    else if (randomValue < 0.85f)
                    {
                        // 식당으로 이동 시도 (60% 확률) ⭐ 임시 증가
                        if (TryFindAvailableKitchen())
                        {
                            Debug.Log($"AI {gameObject.name}: 11~16시, 방 없음, 식당으로 이동 (60%).");
                        }
                        else
                        {
                            TransitionToState(AIState.Wandering);
                            Debug.Log($"AI {gameObject.name}: 11~16시, 방 없음, 식당 없어서 배회 (60%).");
                        }
                    }
                    else if (randomValue < 0.95f)
                    {
                        TransitionToState(AIState.Wandering);
                        Debug.Log($"AI {gameObject.name}: 11~16시, 방 없음, 외부 배회 (10%).");
                    }
                    else
                    {
                        TransitionToState(AIState.ReturningToSpawn);
                        Debug.Log($"AI {gameObject.name}: 11~16시, 방 없음, 디스폰 (5%).");
                    }
                }
                else
                {
                    // 17시는 기존 로직 유지
                    float randomValue = Random.value;
                    if (randomValue < 0.2f)
                    {
                        TransitionToState(AIState.MovingToQueue);
                        Debug.Log($"AI {gameObject.name}: 17시, 방 없음, 대기열로 이동 (20%).");
                    }
                    else if (randomValue < 0.8f)
                    {
                        TransitionToState(AIState.Wandering);
                        Debug.Log($"AI {gameObject.name}: 17시, 방 없음, 외부 배회 (60%).");
                    }
                    else
                    {
                        TransitionToState(AIState.ReturningToSpawn);
                        Debug.Log($"AI {gameObject.name}: 17시, 방 없음, 디스폰 (20%).");
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
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 있음, 방 외부 배회 (50%).");
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                    Debug.Log($"AI {gameObject.name}: 11~17시, 방 있음, 방 내부 배회 (50%).");
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
                    Debug.Log($"AI {gameObject.name}: 17~0시, 방 있음, 외부 배회 (50%).");
                }
                else
                {
                    TransitionToState(AIState.RoomWandering);
                    Debug.Log($"AI {gameObject.name}: 17~0시, 방 있음, 방 내부 배회 (50%).");
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
            Debug.Log($"AI {gameObject.name}: 17:00, 방 사용 또는 수면 중이므로 디스폰하지 않음 (상태: {currentState}).");
            return;
        }
        
        // 15시까지만 선베드 사용하므로 17시에는 선베드 사용 중인 AI가 없음

        // 주방 카운터 대기 중이거나 식사 중인 AI는 강제로 종료 후 퇴장
        if (isWaitingAtKitchenCounter || currentState == AIState.MovingToKitchenCounter || currentState == AIState.WaitingAtKitchenCounter)
        {
            Debug.Log($"AI {gameObject.name}: 17:00, 주방 카운터 관련 상태이므로 강제 종료 후 퇴장 (상태: {currentState}).");
            ForceFinishKitchenActivity();
        }
        else if (isEating)
        {
            Debug.Log($"AI {gameObject.name}: 17:00, 식사 중이므로 강제 종료 후 퇴장 (상태: {currentState}).");
            ForceFinishEating();
        }

        Debug.Log($"AI {gameObject.name}: 17:00, 강제 디스폰 시작 (현재 상태: {currentState}).");

        // 모든 코루틴 강제 종료
        CleanupCoroutines();

        // 대기열에서 강제 제거
        if (isInQueue && counterManager != null)
        {
            counterManager.LeaveQueue(this);
            isInQueue = false;
            isWaitingForService = false;
            Debug.Log($"AI {gameObject.name}: 17:00, 대기열에서 강제 제거됨.");
            
            // 대기열 강제 정리는 마지막에 한 번만 호출 (중복 호출 방지)
            counterManager.ForceCleanupQueue();
        }

        // 강제 디스폰
        TransitionToState(AIState.ReturningToSpawn);
        agent.SetDestination(spawnPoint.position);
        Debug.Log($"AI {gameObject.name}: 17:00, 강제 디스폰 실행.");
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
            Debug.Log($"AI {gameObject.name}: 11시, 체크아웃 완료 후 디스폰.");
            TransitionToState(AIState.ReturningToSpawn);
            agent.SetDestination(spawnPoint.position);
        }
    }

    /// <summary>
    /// 중요한 상태인지 확인합니다 (행동 재결정을 방해하면 안 되는 상태).
    /// </summary>
    private bool IsInCriticalState()
    {
        return currentState == AIState.WaitingInQueue || 
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
                Debug.Log($"AI {gameObject.name}: 0시, 방 내부 배회로 전환.");
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
                    Debug.Log($"AI {gameObject.name}: {hour}시, 방 외부 배회로 전환 (50%).");
                    TransitionToState(AIState.UseWandering);
                }
            }
            else
            {
                if (currentState != AIState.RoomWandering)
                {
                    Debug.Log($"AI {gameObject.name}: {hour}시, 방 내부 배회로 전환 (50%).");
                    TransitionToState(AIState.RoomWandering);
                }
            }
        }
    }

    private void FallbackBehavior()
    {
        if (counterPosition == null || counterManager == null)
        {
            float randomValue = Random.value;
            if (randomValue < 0.5f)
            {
                TransitionToState(AIState.Wandering);
                Debug.Log($"AI {gameObject.name}: 카운터 없음, 배회 (50%).");
            }
            else
            {
                TransitionToState(AIState.ReturningToSpawn);
                Debug.Log($"AI {gameObject.name}: 카운터 없음, 디스폰 (50%).");
            }
        }
        else
        {
            float randomValue = Random.value;
            if (randomValue < 0.4f)
            {
                TransitionToState(AIState.Wandering);
                Debug.Log($"AI {gameObject.name}: 기본 행동, 배회 (40%).");
            }
            else
            {
                TransitionToState(AIState.MovingToQueue);
                Debug.Log($"AI {gameObject.name}: 기본 행동, 대기열로 이동 (60%).");
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
                Debug.LogWarning($"AI {gameObject.name}: NavMesh 벗어남");
                ReturnToPool();
                return;
            }
        }
        
        // Inspector에서 설정이 변경되면 전역 설정 업데이트
        if (globalShowDebugUI != debugUIEnabled)
        {
            globalShowDebugUI = debugUIEnabled;
        }

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

            // 매시간 행동 재결정 (모든 AI 포함)
            if (minute == 0 && hour != lastBehaviorUpdateHour)
            {
                // 디스폰 예정 AI는 행동 재결정하지 않음
                if (isScheduledForDespawn)
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: 11시 디스폰 예정이므로 행동 재결정 생략");
                    lastBehaviorUpdateHour = hour;
                }
                // 중요한 상태가 아닌 경우에만 행동 재결정
                else if (!IsInCriticalState())
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: {hour}시 행동 재결정 시작");
                    DetermineBehaviorByTime();
                    lastBehaviorUpdateHour = hour;
                }
                // 방 사용 중인 AI도 매시간 내부/외부 배회 재결정
                else if (IsInRoomRelatedState())
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: {hour}시 방 배회 재결정");
                    RedetermineRoomBehavior();
                    lastBehaviorUpdateHour = hour;
                }
                else
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: {hour}시 중요한 상태로 행동 재결정 생략 (상태: {currentState})");
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
                        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 도착.");
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
                    Debug.Log($"AI {gameObject.name}: 스폰 지점 도착, 디스폰.");
                    ReturnToPool();
                }
                break;
        }
    }
    #endregion

    #region 대기열 동작
    private IEnumerator QueueBehavior()
    {
        Debug.Log($"[AIAgent] {gameObject.name}: QueueBehavior 시작 - 상태: {currentState}, 방 인덱스: {currentRoomIndex}");
        
        if (counterManager == null || counterPosition == null)
        {
            Debug.LogWarning($"[AIAgent] {gameObject.name}: CounterManager 또는 CounterPosition이 없음");
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

        Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 시도");
        if (!counterManager.TryJoinQueue(this))
        {
            Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 실패 - 상태: {currentState}");
            
            // ReportingRoomQueue 상태인 경우 재시도
            if (currentState == AIState.ReportingRoomQueue)
            {
                Debug.Log($"[AIAgent] {gameObject.name}: ReportingRoomQueue 상태이므로 재시도");
                yield return new WaitForSeconds(Random.Range(2f, 5f));
                StartCoroutine(QueueBehavior());
                yield break;
            }
            
            if (currentRoomIndex == -1)
            {
                Debug.Log($"[AIAgent] {gameObject.name}: 방 없음, 대안 행동 선택");
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
                Debug.Log($"[AIAgent] {gameObject.name}: 방 있음, 재시도");
                yield return new WaitForSeconds(Random.Range(1f, 3f));
                StartCoroutine(QueueBehavior());
            }
            yield break;
        }

        Debug.Log($"[AIAgent] {gameObject.name}: 대기열 진입 성공");
        isInQueue = true;
        TransitionToState(currentState == AIState.ReportingRoomQueue ? AIState.ReportingRoomQueue : AIState.WaitingInQueue);

        while (isInQueue)
        {
            // 17시 체크 - 대기열에서도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Debug.Log($"AI {gameObject.name}: 대기열 대기 중 17시 감지, 즉시 강제 디스폰.");
                Handle17OClockForcedDespawn();
                yield break;
            }

            if (agent != null && agent.enabled && agent.isOnNavMesh && 
                !agent.pathPending && agent.remainingDistance < arrivalDistance)
            {
                if (counterManager.CanReceiveService(this))
                {
                    Debug.Log($"[AIAgent] {gameObject.name}: 서비스 시작");
                    counterManager.StartService(this);
                    isWaitingForService = true;

                    while (isWaitingForService)
                    {
                        // 서비스 대기 중에도 17시 체크
                        if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                        {
                            Debug.Log($"AI {gameObject.name}: 서비스 대기 중 17시 감지, 즉시 강제 디스폰.");
                            Handle17OClockForcedDespawn();
                            yield break;
                        }
                        yield return new WaitForSeconds(0.1f);
                    }

                    if (currentState == AIState.ReportingRoomQueue)
                    {
                        Debug.Log($"[AIAgent] {gameObject.name}: ReportingRoomQueue 서비스 완료, ReportRoomVacancy 시작");
                        StartCoroutine(ReportRoomVacancy());
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
                Debug.Log($"AI {gameObject.name}: 사용 가능한 룸 없음 (사용 중: {roomList.Count(r => r.isOccupied)}개, 청소 중: {roomList.Count(r => r.isBeingCleaned)}개).");
                return false;
            }

            int selectedRoomIndex = availableRooms[Random.Range(0, availableRooms.Count)];
            if (!roomList[selectedRoomIndex].isOccupied && !roomList[selectedRoomIndex].isBeingCleaned)
            {
                roomList[selectedRoomIndex].isOccupied = true;
                currentRoomIndex = selectedRoomIndex;
                Debug.Log($"AI {gameObject.name}: 룸 {selectedRoomIndex + 1}번 배정됨.");
                return true;
            }

            Debug.Log($"AI {gameObject.name}: 룸 {selectedRoomIndex + 1}번 이미 사용 중이거나 청소 중.");
            return false;
        }
    }
    #endregion

    #region 상태 전환
    private void TransitionToState(AIState newState)
    {
        CleanupCoroutines();

        currentState = newState;
        currentDestination = GetStateDescription(newState);
        Debug.Log($"AI {gameObject.name}: 상태 변경: {newState}");

        switch (newState)
        {
            case AIState.Wandering:
                wanderingCoroutine = StartCoroutine(WanderingBehavior());
                break;
            case AIState.MovingToQueue:
            case AIState.ReportingRoomQueue:
                StartCoroutine(QueueBehavior());
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
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            StartCoroutine(ReportRoomVacancy());
            yield break;
        }

        var room = roomList[currentRoomIndex].gameObject.GetComponent<RoomContents>();
        if (roomManager != null && room != null)
        {
            roomManager.ReportRoomUsage(gameObject.name, room);
        }

        // TransitionToState(AIState.UsingRoom);
        TransitionToState(AIState.UseWandering);

        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 사용 시작.");
        while (elapsedTime < roomUseTime && agent.isOnNavMesh)
        {
            yield return new WaitForSeconds(Random.Range(2f, 5f));
            elapsedTime += Random.Range(2f, 5f);
        }

        Debug.Log($"AI {gameObject.name}: 룸 {currentRoomIndex + 1}번 사용 완료.");
        DetermineBehaviorByTime();
    }

    private IEnumerator ReportRoomVacancy()
    {
        TransitionToState(AIState.ReportingRoom);
        int reportingRoomIndex = currentRoomIndex;
        Debug.Log($"[AIAgent] AI {gameObject.name}: 룸 {reportingRoomIndex + 1}번 사용 완료 보고 시작.");

        lock (lockObject)
        {
            if (reportingRoomIndex >= 0 && reportingRoomIndex < roomList.Count)
            {
                roomList[reportingRoomIndex].isOccupied = false;
                currentRoomIndex = -1;
                Debug.Log($"[AIAgent] 룸 {reportingRoomIndex + 1}번 비워짐.");
            }
        }

        var roomManager = FindFirstObjectByType<RoomManager>();
        if (roomManager != null)
        {
            Debug.Log($"[AIAgent] RoomManager 발견. ProcessRoomPayment 호출 - AI: {gameObject.name}");
            int amount = roomManager.ProcessRoomPayment(gameObject.name);
            Debug.Log($"[AIAgent] AI {gameObject.name}: 룸 결제 완료, 금액: {amount}원");
        }
        else
        {
            Debug.LogError($"[AIAgent] AI {gameObject.name}: RoomManager를 찾을 수 없습니다!");
        }

        // 집사 AI 생성 요청 (컴파일 순서 문제로 인해 문자열로 찾기)
        GameObject butlerSpawnerObj = GameObject.Find("ButlerSpawner");
        if (butlerSpawnerObj != null)
        {
            var butlerSpawner = butlerSpawnerObj.GetComponent<MonoBehaviour>();
            if (butlerSpawner != null)
            {
                Debug.Log($"[AIAgent] AI {gameObject.name}: 룸 {reportingRoomIndex + 1}번 청소를 위해 집사 AI 생성 요청.");
                // 리플렉션을 사용하여 SpawnButlerForRoom 메서드 호출
                var method = butlerSpawner.GetType().GetMethod("SpawnButlerForRoom");
                if (method != null)
                {
                    method.Invoke(butlerSpawner, new object[] { reportingRoomIndex });
                }
            }
        }
        else
        {
            Debug.LogWarning($"[AIAgent] AI {gameObject.name}: ButlerSpawner를 찾을 수 없습니다!");
        }

        if (timeSystem.CurrentHour >= 9 && timeSystem.CurrentHour < 11)
        {
            // 9-11시 체크아웃 후에는 배회하다가 11시에 디스폰 예정
            Debug.Log($"AI {gameObject.name}: 9-11시 체크아웃 완료, 11시 디스폰 예정으로 설정.");
            isScheduledForDespawn = true; // 디스폰 예정 플래그 설정
            TransitionToState(AIState.Wandering);
            wanderingCoroutine = StartCoroutine(WanderingBehaviorWithDespawn());
        }
        else
        {
            DetermineBehaviorByTime();
        }

        yield break;
    }
    #endregion

    #region 배회 동작
    private IEnumerator WanderingBehavior()
    {
        float wanderingTime = Random.Range(20f, 40f); // 배회 시간 증가
        float elapsedTime = 0f;
        
        Debug.Log($"AI {gameObject.name}: 넓은 범위 배회 시작 (시간: {wanderingTime:F1}초)");

        while (currentState == AIState.Wandering && elapsedTime < wanderingTime)
        {
            // 17시 체크 - 배회 중에도 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Debug.Log($"AI {gameObject.name}: 배회 중 17시 감지, 즉시 강제 디스폰.");
                Handle17OClockForcedDespawn();
                yield break;
            }

            // 새로운 넓은 범위 배회
            Vector3 currentPos = transform.position;
            float wanderDistance = Random.Range(25f, 50f); // 더 넓은 범위
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
                Debug.Log($"AI {gameObject.name}: 넓은 범위 배회 성공 - 거리: {Vector3.Distance(currentPos, validPosition):F1}");
            }
            else
            {
                // 방을 피하지 못한 경우 기본 방식으로 시도
                WanderOnGround();
                foundValidPosition = true;
                Debug.Log($"AI {gameObject.name}: 기본 배회 사용");
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
                        Debug.Log($"AI {gameObject.name}: 이동 타임아웃, 새로운 목적지 설정");
                        break;
                    }
                    
                    // 17시 체크
                    if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                    {
                        Debug.Log($"AI {gameObject.name}: 이동 중 17시 감지, 즉시 강제 디스폰.");
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
                        Debug.Log($"AI {gameObject.name}: 배회 대기 중 17시 감지, 즉시 강제 디스폰.");
                        Handle17OClockForcedDespawn();
                        yield break;
                    }
                }
                
                elapsedTime += waitTime + moveTimer;
            }
            else
            {
                // 위치를 찾지 못한 경우 짧게 대기 후 재시도
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

        Debug.Log($"AI {gameObject.name}: 배회 완료, 다음 행동 결정");
        DetermineBehaviorByTime();
    }

    /// <summary>
    /// 9-11시 체크아웃 후 배회하다가 11시에 디스폰하는 특별한 배회
    /// </summary>
    private IEnumerator WanderingBehaviorWithDespawn()
    {
        Debug.Log($"AI {gameObject.name}: 9-11시 체크아웃 후 배회 시작, 11시에 디스폰 예정.");
        
        while (currentState == AIState.Wandering)
        {
            // 11시가 되면 즉시 디스폰
            if (timeSystem != null && timeSystem.CurrentHour >= 11)
            {
                Debug.Log($"AI {gameObject.name}: 11시 도달, 체크아웃 완료 AI 디스폰.");
                isScheduledForDespawn = false; // 플래그 리셋
                TransitionToState(AIState.ReturningToSpawn);
                agent.SetDestination(spawnPoint.position);
                yield break;
            }

            // 17시 체크도 유지
            if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
            {
                Debug.Log($"AI {gameObject.name}: 배회 중 17시 감지, 즉시 강제 디스폰.");
                isScheduledForDespawn = false; // 플래그 리셋
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
                    Debug.Log($"AI {gameObject.name}: 대기 중 11시 도달, 체크아웃 완료 AI 디스폰.");
                    isScheduledForDespawn = false; // 플래그 리셋
                    TransitionToState(AIState.ReturningToSpawn);
                    agent.SetDestination(spawnPoint.position);
                    yield break;
                }
                
                // 17시 체크
                if (timeSystem != null && timeSystem.CurrentHour == 17 && timeSystem.CurrentMinute == 0)
                {
                    Debug.Log($"AI {gameObject.name}: 대기 중 17시 감지, 즉시 강제 디스폰.");
                    isScheduledForDespawn = false; // 플래그 리셋
                    Handle17OClockForcedDespawn();
                    yield break;
                }
            }
        }
    }

    private IEnumerator UseWanderingBehavior()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 방 {currentRoomIndex + 1}번 외부 배회 시작");

        while (currentState == AIState.UseWandering && agent.isOnNavMesh)
        {
            // 17시 체크는 하지 않음 - 방 사용 중인 AI는 계속 작동
            
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
                Debug.Log($"AI {gameObject.name}: 방 외부 배회 - 거리: {Vector3.Distance(currentPos, validPosition):F1}");
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh && 
                                                !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                Debug.Log($"AI {gameObject.name}: 방 외부 배회 위치를 찾지 못함, 기본 배회 사용");
                WanderOnGround();
            }

            float waitTime = Random.Range(4f, 10f);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private IEnumerator RoomWanderingBehavior()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            DetermineBehaviorByTime();
            yield break;
        }

        float wanderingTime = Random.Range(15f, 30f);
        float elapsedTime = 0f;
        
        Debug.Log($"AI {gameObject.name}: 방 {currentRoomIndex + 1}번 내부 배회 시작");

        while (currentState == AIState.RoomWandering && elapsedTime < wanderingTime && agent.isOnNavMesh)
        {
            // 17시 체크는 하지 않음 - 방 사용 중인 AI는 계속 작동
            
            // 방 내부에서만 배회
            if (TryGetRoomWanderingPosition(currentRoomIndex, out Vector3 roomPosition))
            {
                agent.SetDestination(roomPosition);
                Debug.Log($"AI {gameObject.name}: 방 내부 배회 - 위치: {roomPosition}");
                
                // 목적지까지 이동 대기
                yield return new WaitUntil(() => agent != null && agent.enabled && agent.isOnNavMesh && 
                                                !agent.pathPending && agent.remainingDistance < arrivalDistance);
            }
            else
            {
                // 방 내부 위치를 찾지 못한 경우 기존 방식 사용
                Vector3 roomCenter = roomList[currentRoomIndex].transform.position;
                float roomSize = roomList[currentRoomIndex].size * 0.5f; // 방 크기를 줄여서 확실히 내부에 위치
                if (TryGetValidPosition(roomCenter, roomSize, NavMesh.AllAreas, out Vector3 fallbackPos))
                {
                    agent.SetDestination(fallbackPos);
                }
            }

            float waitTime = Random.Range(3f, 6f);
            yield return new WaitForSeconds(waitTime);
            elapsedTime += waitTime;
        }

        Debug.Log($"AI {gameObject.name}: 방 내부 배회 완료");
        DetermineBehaviorByTime();
    }

    private void WanderOnGround()
    {
        // 더 넓은 범위로 배회 (20-40 유닛 범위)
        float wanderDistance = Random.Range(20f, 40f);
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0; // Y축 고정
        randomDirection.Normalize();
        
        Vector3 randomPoint = transform.position + randomDirection * wanderDistance;
        
        int groundMask = NavMesh.GetAreaFromName("Ground");
        if (groundMask == 0)
        {
            Debug.LogError($"AI {gameObject.name}: Ground NavMesh 영역 설정되지 않음.");
            return;
        }

        // 방을 피해서 배회하도록 수정
        if (TryGetWanderingPositionAvoidingRooms(randomPoint, 15f, groundMask, out Vector3 validPosition))
        {
            agent.SetDestination(validPosition);
            Debug.Log($"AI {gameObject.name}: 넓은 범위 배회 - 거리: {Vector3.Distance(transform.position, validPosition):F1}");
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
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
            return false;

        var room = roomList[currentRoomIndex];
        if (room == null || room.bedTransform == null)
            return false;

        bedTransform = room.bedTransform;
        return true;
    }

    /// <summary>
    /// 침대로 이동하는 코루틴
    /// </summary>
    private IEnumerator MoveToBedBehavior()
    {
        if (currentBedTransform == null)
        {
            Debug.LogError($"AI {gameObject.name}: 침대 Transform이 null입니다.");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 침대 {currentBedTransform.name}로 이동 시작");

        // 침대 위치로 이동
        agent.SetDestination(currentBedTransform.position);

        // 침대에 도착할 때까지 대기
        float timeout = 10f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (timer >= timeout)
            {
                Debug.LogWarning($"AI {gameObject.name}: 침대로 이동 타임아웃");
                DetermineBehaviorByTime();
                yield break;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 침대에 도착했으므로 수면 시작
        StartSleeping();
    }

    /// <summary>
    /// 수면을 시작합니다.
    /// </summary>
    private void StartSleeping()
    {
        if (currentBedTransform == null)
        {
            Debug.LogError($"AI {gameObject.name}: 침대 Transform이 null입니다.");
            return;
        }

        Debug.Log($"AI {gameObject.name}: 침대 {currentBedTransform.name}에서 수면 시작");

        // 침대 위치와 각도로 Transform 설정
        transform.position = currentBedTransform.position;
        transform.rotation = currentBedTransform.rotation;

        // NavMeshAgent 일시 정지
        if (agent != null)
        {
            agent.enabled = false;
        }

        // 수면 상태 설정
        isSleeping = true;
        TransitionToState(AIState.Sleeping);
        
        Debug.Log($"AI {gameObject.name}: 수면 상태로 전환 완료");
    }

    /// <summary>
    /// 수면에서 깨어납니다.
    /// </summary>
    private void WakeUp()
    {
        if (!isSleeping)
            return;

        Debug.Log($"AI {gameObject.name}: 수면에서 깨어남");

        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.enabled = true;
        }

        // 저장된 위치로 복귀
        transform.position = preSleepPosition;
        transform.rotation = preSleepRotation;

        // 수면 상태 해제
        isSleeping = false;
        currentBedTransform = null;

        // 방 사용 완료 보고로 전환
        TransitionToState(AIState.ReportingRoomQueue);
        
        Debug.Log($"AI {gameObject.name}: 수면 종료, 방 사용 완료 보고로 전환");
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
            return false;

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
            return false;

        // 방 내부에서 "Sunbed" 태그를 가진 오브젝트 찾기
        var allSunbeds = GameObject.FindGameObjectsWithTag("Sunbed");
        foreach (var sunbed in allSunbeds)
        {
            if (sunbed != null && room.bounds.Contains(sunbed.transform.position))
            {
                sunbedTransform = sunbed.transform;
                return true;
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
                Debug.Log($"AI {gameObject.name}: 사용 가능한 선베드 방 없음.");
                return false;
            }

            int selectedRoomIndex = availableSunbedRooms[Random.Range(0, availableSunbedRooms.Count)];
            if (!roomList[selectedRoomIndex].isOccupied)
            {
                roomList[selectedRoomIndex].isOccupied = true;
                currentRoomIndex = selectedRoomIndex;
                Debug.Log($"AI {gameObject.name}: 선베드 방 {selectedRoomIndex + 1}번 배정됨.");
                
                // 선베드로 이동
                if (FindSunbedInCurrentRoom(out Transform sunbedTransform))
                {
                    currentSunbedTransform = sunbedTransform;
                    // 타이머 바로 시작 (이동할 때부터)
                    sunbedCoroutine = StartCoroutine(SunbedUsageTimer());
                    TransitionToState(AIState.MovingToSunbed);
                    Debug.Log($"AI {gameObject.name}: 선베드로 이동 시작, 50분 타이머 시작");
                    return true;
                }
                else
                {
                    Debug.LogError($"AI {gameObject.name}: 선베드 방에 선베드가 없습니다.");
                    // 방 배정 취소
                    roomList[selectedRoomIndex].isOccupied = false;
                    currentRoomIndex = -1;
                    return false;
                }
            }

            Debug.Log($"AI {gameObject.name}: 선베드 방 {selectedRoomIndex + 1}번 이미 사용 중.");
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
            Debug.LogError($"AI {gameObject.name}: 선베드 Transform이 null입니다.");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 선베드 {currentSunbedTransform.name}로 이동 시작");

        // 선베드 위치로 이동
        agent.SetDestination(currentSunbedTransform.position);

        // 선베드에 도착할 때까지 대기
        float timeout = 10f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            // NavMesh에서 벗어났는지 체크
            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"AI {gameObject.name}: 선베드로 이동 중 NavMesh 벗어남, 타임아웃 처리");
                CleanupSunbedMovement();
                yield break;
            }
            
            if (timer >= timeout)
            {
                Debug.LogWarning($"AI {gameObject.name}: 선베드로 이동 타임아웃");
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
                    Debug.Log($"AI {gameObject.name}: 선베드 이동 실패로 방 {currentRoomIndex + 1}번 반납");
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
            Debug.LogError($"AI {gameObject.name}: 선베드 Transform이 null입니다.");
            return;
        }

        // 이미 선베드 사용 중인지 확인
        if (isUsingSunbed)
        {
            Debug.LogWarning($"AI {gameObject.name}: 이미 선베드 사용 중입니다. 중복 시작 방지.");
            return;
        }

        // NavMeshAgent 상태 체크
        if (agent != null && !agent.isOnNavMesh)
        {
            Debug.LogError($"AI {gameObject.name}: NavMesh에 없어서 선베드 사용을 시작할 수 없습니다.");
            return;
        }

        Debug.Log($"AI {gameObject.name}: 선베드 {currentSunbedTransform.name}에서 사용 시작");

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
            Debug.Log($"AI {gameObject.name}: NavMeshAgent 이동 중지 (enabled 유지)");
        }

        // 선베드 사용 상태 설정
        isUsingSunbed = true;
        TransitionToState(AIState.UsingSunbed);
        
        // 타이머가 없다면 다시 시작 (안전장치)
        if (sunbedCoroutine == null)
        {
            Debug.LogWarning($"AI {gameObject.name}: 선베드 타이머가 없어서 다시 시작합니다.");
            sunbedCoroutine = StartCoroutine(SunbedUsageTimer());
        }
        
        // BedTime 애니메이션 시작
        if (animator != null)
        {
            animator.SetBool("BedTime", true);
            Debug.Log($"AI {gameObject.name}: BedTime 애니메이션 시작");
        }
        
        Debug.Log($"AI {gameObject.name}: 선베드 사용 상태로 전환 완료");
    }

    /// <summary>
    /// 선베드 사용 시간 타이머 (게임 시간 50분)
    /// </summary>
    private IEnumerator SunbedUsageTimer()
    {
        Debug.Log($"AI {gameObject.name}: 선베드 타이머 시작!");
        
        // TimeSystem 안전성 체크
        if (timeSystem == null)
        {
            timeSystem = TimeSystem.Instance;
            if (timeSystem == null)
            {
                Debug.LogError($"AI {gameObject.name}: TimeSystem을 찾을 수 없습니다. 안전장치로 실시간 50초 후 종료.");
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
        
        Debug.Log($"AI {gameObject.name}: 선베드 사용 시작 - 게임 시간 {startHour:00}:{startMinute:00}");
        
        // 50분 후의 시간 계산
        int targetTotalMinutes = startTotalMinutes + 50;
        int targetHour = (targetTotalMinutes / 60) % 24;
        int targetMinute = targetTotalMinutes % 60;
        
        Debug.Log($"AI {gameObject.name}: 선베드 사용 종료 예정 - 게임 시간 {targetHour:00}:{targetMinute:00}");
        
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
                    Debug.LogError($"AI {gameObject.name}: TimeSystem이 사라졌습니다. 선베드 사용 강제 종료.");
                    break;
                }
            }
            
            int currentHour = timeSystem.CurrentHour;
            int currentMinute = timeSystem.CurrentMinute;
            int currentTotalMinutes = currentHour * 60 + currentMinute;
            
            // 디버그: 주기적으로 시간 상태 출력
            if ((int)elapsedTime % 10 == 0 && elapsedTime > 0)
            {
                Debug.Log($"AI {gameObject.name}: 선베드 타이머 체크 - 현재: {currentHour:00}:{currentMinute:00}, 목표: {targetHour:00}:{targetMinute:00}");
            }
            
            // 16시 이후 강제 종료 (15시까지만 선베드 사용 가능)
            if (currentHour >= 16)
            {
                Debug.Log($"AI {gameObject.name}: 16시 이후 도달로 선베드 사용 강제 종료 (현재: {currentHour:00}:{currentMinute:00})");
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
                    Debug.Log($"AI {gameObject.name}: 목표 시간 도달, 선베드 사용 종료 (목표: {targetHour:00}:{targetMinute:00}, 현재: {currentHour:00}:{currentMinute:00})");
                }
            }
            else
            {
                // 다음 날로 넘어가는 경우
                int nextDayTargetMinutes = targetTotalMinutes - 1440;
                if (currentTotalMinutes >= nextDayTargetMinutes || currentTotalMinutes < startTotalMinutes)
                {
                    timeReached = true;
                    Debug.Log($"AI {gameObject.name}: 다음 날 목표 시간 도달, 선베드 사용 종료 (목표: {targetHour:00}:{targetMinute:00}, 현재: {currentHour:00}:{currentMinute:00})");
                }
            }
            
            // 시간이 크게 점프한 경우 감지 (게임 시간 빨리 감기)
            int timeDifference = currentTotalMinutes - startTotalMinutes;
            if (timeDifference < 0) timeDifference += 1440; // 하루 넘어간 경우 보정
            
            if (timeDifference >= 50) // 50분 이상 지났으면
            {
                timeReached = true;
                Debug.Log($"AI {gameObject.name}: 시간 점프 감지로 선베드 사용 종료 (경과: {timeDifference}분, 현재: {currentHour:00}:{currentMinute:00})");
            }
            
            if (timeReached)
            {
                break;
            }
            
            yield return new WaitForSeconds(checkInterval);
            elapsedTime += checkInterval;
        }
        
        // 안전장치: 최대 대기 시간 초과 시 강제 종료
        if (elapsedTime >= maxWaitTime)
        {
            Debug.LogWarning($"AI {gameObject.name}: 선베드 타이머 최대 대기 시간 초과, 강제 종료");
        }
        
        // 선베드 사용 종료
        Debug.Log($"AI {gameObject.name}: 선베드 타이머 완료, 사용 종료 시작");
        if (isUsingSunbed)
        {
            FinishUsingSunbed();
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: 타이머 완료 시점에 이미 선베드 사용이 끝난 상태");
        }
        
        // 코루틴 참조 정리
        sunbedCoroutine = null;
        Debug.Log($"AI {gameObject.name}: 선베드 타이머 코루틴 완전 종료");
    }

    /// <summary>
    /// 선베드 사용을 종료합니다.
    /// </summary>
    private void FinishUsingSunbed()
    {
        Debug.Log($"AI {gameObject.name}: FinishUsingSunbed 호출됨 - isUsingSunbed: {isUsingSunbed}");
        
        if (!isUsingSunbed)
        {
            Debug.LogWarning($"AI {gameObject.name}: 선베드 사용 중이 아닌데 종료 시도");
            return;
        }

        Debug.Log($"AI {gameObject.name}: 선베드 사용 종료 시작");

        // 1. BedTime 애니메이션 종료
        if (animator != null)
        {
            animator.SetBool("BedTime", false);
            Debug.Log($"AI {gameObject.name}: BedTime 애니메이션 종료 완료");
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: Animator가 null입니다");
        }

        // 2. 저장된 위치로 복귀
        Debug.Log($"AI {gameObject.name}: 위치 복귀 시작 - 현재: {transform.position}, 복귀할 위치: {preSunbedPosition}");
        transform.position = preSunbedPosition;
        transform.rotation = preSunbedRotation;
        Debug.Log($"AI {gameObject.name}: 이전 위치로 복귀 완료");

        // 3. NavMeshAgent 이동 재시작
        if (agent != null)
        {
            agent.isStopped = false;
            Debug.Log($"AI {gameObject.name}: NavMeshAgent 이동 재시작");
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: NavMeshAgent가 null입니다");
        }

        // 4. 선베드 사용 상태 해제 (안전하게)
        isUsingSunbed = false;
        currentSunbedTransform = null;
        Debug.Log($"AI {gameObject.name}: 선베드 상태 변수 정리 완료");

        // 5. 선베드 결제 처리
        Debug.Log($"AI {gameObject.name}: 선베드 결제 처리 시작");
        ProcessSunbedPayment();
        
        Debug.Log($"AI {gameObject.name}: 선베드 사용 완료, 모든 절차 종료");
    }

    /// <summary>
    /// GameObject 비활성화 시 코루틴 없이 선베드 결제를 직접 처리합니다.
    /// </summary>
    private void ProcessSunbedPaymentDirectly()
    {
        Debug.Log($"AI {gameObject.name}: ProcessSunbedPaymentDirectly 호출");
        
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            return;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            Debug.LogError($"AI {gameObject.name}: 룸 정보가 null입니다.");
            return;
        }

        var roomContents = room.gameObject.GetComponent<RoomContents>();
        if (roomContents != null && roomContents.isSunbedRoom)
        {
            // 선베드 방의 고정 가격과 명성도 사용
            int price = roomContents.TotalRoomPrice;
            int reputation = roomContents.TotalRoomReputation;
            
            Debug.Log($"AI {gameObject.name}: 선베드 결제 처리 - 가격: {price}원, 명성도: {reputation}");
            
            // PaymentSystem을 통한 실제 결제 처리
            var paymentSystem = PaymentSystem.Instance;
            if (paymentSystem != null)
            {
                // 결제 정보를 PaymentSystem에 추가
                paymentSystem.AddPayment(gameObject.name, price, roomContents.roomID, reputation);
                
                // 결제 처리 실행
                int totalAmount = paymentSystem.ProcessPayment(gameObject.name);
                
                Debug.Log($"AI {gameObject.name}: 선베드 결제 완료 - 총 금액: {totalAmount}원, 명성도: {reputation}");
            }
            else
            {
                Debug.LogError($"AI {gameObject.name}: PaymentSystem을 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: 선베드 방 정보를 찾을 수 없습니다.");
        }

        // 방 사용 완료 처리 (선베드 사용 완료 후 방 반납)
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                    Debug.Log($"AI {gameObject.name}: 선베드 방 {currentRoomIndex + 1}번 반납 완료.");
                }
                // currentRoomIndex를 -1로 설정하여 방 사용 완료 처리
                currentRoomIndex = -1;
            }
        }

        // GameObject가 비활성화 상태이므로 상태 전환하지 않음
        Debug.Log($"AI {gameObject.name}: 선베드 결제 및 방 반납 완료 (비활성화 상태)");
    }
    
    /// <summary>
    /// 17시 강제 디스폰 시 선베드 사용을 강제로 종료합니다.
    /// </summary>
    private void ForceFinishUsingSunbed()
    {
        if (!isUsingSunbed)
        {
            Debug.LogWarning($"AI {gameObject.name}: 선베드 사용 중이 아닌데 강제 종료 시도");
            return;
        }

        Debug.Log($"AI {gameObject.name}: 17시 강제 선베드 사용 종료");

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
        
        Debug.Log($"AI {gameObject.name}: 17시 강제 선베드 사용 종료, 결제 및 방 반납 처리");
    }

    /// <summary>
    /// 선베드 결제를 처리합니다.
    /// </summary>
    private void ProcessSunbedPayment(bool isForcedDespawn = false)
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= roomList.Count)
        {
            Debug.LogError($"AI {gameObject.name}: 잘못된 룸 인덱스 {currentRoomIndex}.");
            TransitionToState(AIState.Wandering);
            return;
        }

        var room = roomList[currentRoomIndex];
        if (room == null || room.gameObject == null)
        {
            Debug.LogError($"AI {gameObject.name}: 룸 정보가 null입니다.");
            TransitionToState(AIState.Wandering);
            return;
        }

        var roomContents = room.gameObject.GetComponent<RoomContents>();
        if (roomContents != null && roomContents.isSunbedRoom)
        {
            // 선베드 방의 고정 가격과 명성도 사용
            int price = roomContents.TotalRoomPrice;
            int reputation = roomContents.TotalRoomReputation;
            
            Debug.Log($"AI {gameObject.name}: 선베드 결제 시작 - 가격: {price}원, 명성도: {reputation}");
            
            // PaymentSystem을 통한 실제 결제 처리
            var paymentSystem = PaymentSystem.Instance;
            if (paymentSystem != null)
            {
                // 결제 정보를 PaymentSystem에 추가
                paymentSystem.AddPayment(gameObject.name, price, roomContents.roomID, reputation);
                
                // 결제 처리 실행
                int totalAmount = paymentSystem.ProcessPayment(gameObject.name);
                
                Debug.Log($"AI {gameObject.name}: 선베드 결제 완료 - 총 금액: {totalAmount}원, 명성도: {reputation}");
            }
            else
            {
                Debug.LogError($"AI {gameObject.name}: PaymentSystem을 찾을 수 없습니다!");
            }
        }
        else
        {
            Debug.LogWarning($"AI {gameObject.name}: 선베드 방 정보를 찾을 수 없습니다.");
        }

        // 방 사용 완료 처리 (선베드 사용 완료 후 방 반납)
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                    Debug.Log($"AI {gameObject.name}: 선베드 방 {currentRoomIndex + 1}번 반납 완료.");
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
            Debug.Log($"AI {gameObject.name}: 선베드 사용 완료, 17시 강제 디스폰 시작");
        }
        else
        {
            // 일반적인 경우 배회로 전환
            TransitionToState(AIState.Wandering);
            Debug.Log($"AI {gameObject.name}: 선베드 사용 완료, 배회로 전환");
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
            Debug.Log($"AI {gameObject.name}: 사용 가능한 주방 카운터 없음.");
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
                    
                    Debug.Log($"AI {gameObject.name}: 주방 카운터 {closestCounter.name} 대기열 진입 성공, 의자 {selectedChair.name} 예약됨.");
                    TransitionToState(AIState.MovingToKitchenCounter);
                    return true;
                }
                else
                {
                    // 의자가 없으면 대기열에서 나가기
                    closestCounter.LeaveQueue(this);
                    Debug.Log($"AI {gameObject.name}: 사용 가능한 의자가 없어 대기열에서 나감.");
                    return false;
                }
            }
            else
            {
                Debug.Log($"AI {gameObject.name}: 주방 카운터 {closestCounter.name} 대기열이 가득 참.");
                return false;
            }
        }

        Debug.Log($"AI {gameObject.name}: 사용 가능한 주방 카운터 없음.");
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
        
        Debug.Log($"AI {gameObject.name}: 전체 KitchenChair 개수: {allChairs.Length}");
        
        if (allChairs.Length == 0)
        {
            Debug.Log($"AI {gameObject.name}: KitchenChair 태그를 가진 오브젝트가 없습니다.");
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
                Debug.Log($"AI {gameObject.name}: 의자 {chairObj.name}가 너무 멀음 (거리: {distance:F1}m)");
                continue;
            }

            // 의자가 사용 중인지 확인
            if (!IsChairOccupied(chairObj.transform))
            {
                availableChairs.Add(chairObj.transform);
                Debug.Log($"AI {gameObject.name}: 사용 가능한 의자 발견: {chairObj.name} (거리: {distance:F1}m)");
            }
            else
            {
                Debug.Log($"AI {gameObject.name}: 의자 {chairObj.name}는 사용 중");
            }
        }

        if (availableChairs.Count > 0)
        {
            // 랜덤하게 의자 선택
            selectedChair = availableChairs[Random.Range(0, availableChairs.Count)];
            kitchen = selectedChair; // 의자 자체를 kitchen으로 설정 (단순화)
            
            Debug.Log($"AI {gameObject.name}: 의자 선택 완료: {selectedChair.name} (총 {availableChairs.Count}개 중 선택)");
            return true;
        }

        Debug.Log($"AI {gameObject.name}: 사용 가능한 의자가 없습니다. (전체: {allChairs.Length}개, 사용 가능: 0개)");
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
            Debug.LogError($"AI {gameObject.name}: 주방 카운터가 null입니다.");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 주방 카운터 {currentKitchenCounter.name} 대기열 위치로 이동 시작");

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
                Debug.LogWarning($"AI {gameObject.name}: 주방 카운터 대기열 위치 이동 타임아웃");
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
                    Debug.Log($"AI {gameObject.name}: 주방 카운터 대기열 위치 도착 완료");
                    break;
                }
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 대기열 위치에 실제로 도착한 후 대기 상태로 전환
        Debug.Log($"AI {gameObject.name}: 주방 카운터 대기열에서 대기 시작");
        isWaitingAtKitchenCounter = true;
        
        // 주방 카운터에 도착했음을 알림 (서비스 시작 가능)
        if (currentKitchenCounter != null)
        {
            Debug.Log($"AI {gameObject.name}: 주방 카운터에 도착 완료, 서비스 대기 중");
            // 카운터의 ProcessCustomerQueue가 자동으로 호출되어 서비스 시작 확인
        }
        
        TransitionToState(AIState.WaitingAtKitchenCounter);
    }

    /// <summary>
    /// 주방 카운터 서비스 완료 시 호출 (KitchenCounter에서 호출)
    /// </summary>
    public void OnKitchenServiceComplete()
    {
        Debug.Log($"AI {gameObject.name}: 주방 카운터 서비스 완료, 주문 결제 처리");
        
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
            Debug.LogError($"AI {gameObject.name}: 예약된 의자가 없습니다.");
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
            
            Debug.Log($"AI {gameObject.name}: 주방 주문 결제 완료 - 총 금액: {totalAmount}원, 명성도: {orderReputation}");
        }
        else
        {
            Debug.LogError($"AI {gameObject.name}: PaymentSystem을 찾을 수 없습니다.");
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
            Debug.LogError($"AI {gameObject.name}: 식당 Transform이 null입니다.");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 식당 {currentKitchenTransform.name}로 이동 시작");

        // 식당 위치로 이동
        agent.SetDestination(currentKitchenTransform.position);

        // 식당에 도착할 때까지 대기
        float timeout = 15f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (timer >= timeout)
            {
                Debug.LogWarning($"AI {gameObject.name}: 식당으로 이동 타임아웃");
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
            Debug.LogError($"AI {gameObject.name}: 의자 Transform이 null입니다.");
            DetermineBehaviorByTime();
            yield break;
        }

        Debug.Log($"AI {gameObject.name}: 의자 {currentChairTransform.name}로 이동 시작");

        // 의자로 이동하기 전 현재 위치 저장 (정확한 위치 기록)
        preChairPosition = transform.position;
        preChairRotation = transform.rotation;
        Debug.Log($"AI {gameObject.name}: 의자 이동 전 위치 저장 - 위치: {preChairPosition}, 회전: {preChairRotation.eulerAngles}");

        // 의자 위치로 이동
        agent.SetDestination(currentChairTransform.position);

        // 의자에 도착할 때까지 대기
        float timeout = 10f;
        float timer = 0f;
        
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
        {
            if (timer >= timeout)
            {
                Debug.LogWarning($"AI {gameObject.name}: 의자로 이동 타임아웃");
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
            Debug.LogError($"AI {gameObject.name}: 의자 Transform이 null입니다.");
            return;
        }

        Debug.Log($"AI {gameObject.name}: 의자 {currentChairTransform.name}에서 식사 시작");

        // 의자 위치와 각도로 Transform 설정
        transform.position = currentChairTransform.position;
        transform.rotation = currentChairTransform.rotation;

        // NavMeshAgent는 끄지 않고 이동만 중지
        if (agent != null)
        {
            agent.isStopped = true;
            Debug.Log($"AI {gameObject.name}: 식사 시 NavMeshAgent 이동 중지 (enabled 유지)");
        }

        // 식사 상태 설정
        isEating = true;
        TransitionToState(AIState.Eating);
        
        // 10초 후 자동으로 식사 종료
        eatingCoroutine = StartCoroutine(EatingTimer());
        
        Debug.Log($"AI {gameObject.name}: 식사 상태로 전환 완료");
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

        Debug.Log($"AI {gameObject.name}: 식사 완료");

        // NavMeshAgent 이동 재시작
        if (agent != null)
        {
            agent.isStopped = false;
            Debug.Log($"AI {gameObject.name}: 식사 종료 - NavMeshAgent 이동 재시작");
        }

        // 저장된 위치로 복귀
        Debug.Log($"AI {gameObject.name}: 식사 완료 후 이전 위치로 복귀 - 목표 위치: {preChairPosition}");
        transform.position = preChairPosition;
        transform.rotation = preChairRotation;
        Debug.Log($"AI {gameObject.name}: 복귀 완료 - 현재 위치: {transform.position}");

        // 식사 상태 해제
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;

        // 방이 있는 사람은 방 밖 배회, 방 없는 사람은 배회
        if (currentRoomIndex != -1)
        {
            TransitionToState(AIState.UseWandering);
            Debug.Log($"AI {gameObject.name}: 식사 완료, 방 밖 배회로 전환");
        }
        else
        {
            TransitionToState(AIState.Wandering);
            Debug.Log($"AI {gameObject.name}: 식사 완료, 배회로 전환");
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
        Debug.Log($"AI {gameObject.name}: 17시 강제 주방 활동 종료");

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
        
        Debug.Log($"AI {gameObject.name}: 주방 활동 강제 종료 완료");
    }

    private void ForceFinishEating()
    {
        if (!isEating)
            return;

        Debug.Log($"AI {gameObject.name}: 17시 강제 식사 종료");

        // NavMeshAgent 다시 활성화
        if (agent != null)
        {
            agent.isStopped = false;
        }

        // 저장된 위치로 복귀
        transform.position = preChairPosition;
        transform.rotation = preChairRotation;

        // 식사 상태 해제
        isEating = false;
        currentKitchenTransform = null;
        currentChairTransform = null;
        
        Debug.Log($"AI {gameObject.name}: 17시 강제 식사 종료 완료");
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
            Debug.LogWarning($"AI {gameObject.name}: 스포너 참조 없음, 오브젝트 파괴.");
            Destroy(gameObject);
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
        if (wanderingCoroutine != null)
        {
            StopCoroutine(wanderingCoroutine);
            wanderingCoroutine = null;
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
        //Debug.Log($"[AIAgent] {gameObject.name}: CleanupResources 시작");
        
        // 수면 상태 정리
        if (isSleeping)
        {
            WakeUp();
        }
        
        // 선베드 사용 상태 정리 (애니메이션 지연 없이 직접 정리)
        if (isUsingSunbed)
        {
            Debug.Log($"AI {gameObject.name}: CleanupResources에서 선베드 상태 정리");
            
            // 1. BedTime 애니메이션 종료
            if (animator != null)
            {
                animator.SetBool("BedTime", false);
                Debug.Log($"AI {gameObject.name}: BedTime 애니메이션 종료");
            }

            // 2. 저장된 위치로 복귀
            transform.position = preSunbedPosition;
            transform.rotation = preSunbedRotation;
            Debug.Log($"AI {gameObject.name}: 이전 위치로 복귀");

            // 3. NavMeshAgent 이동 재시작
            if (agent != null)
            {
                agent.isStopped = false;
                Debug.Log($"AI {gameObject.name}: NavMeshAgent 이동 재시작");
            }

            // 4. 선베드 사용 상태 해제
            isUsingSunbed = false;
            currentSunbedTransform = null;

            // 5. 선베드 결제 처리 (GameObject 비활성화 시에는 코루틴 시작 없이)
            ProcessSunbedPaymentDirectly();
            
            Debug.Log($"AI {gameObject.name}: 선베드 사용 완료, 상태 정리 완료");
        }
        
        // 식사 상태 정리 (GameObject 비활성화 시에는 코루틴 시작 없이)
        // 주방 카운터 관련 상태 정리
        if (isWaitingAtKitchenCounter || currentKitchenCounter != null)
        {
            Debug.Log($"AI {gameObject.name}: CleanupResources에서 주방 카운터 상태 정리");
            
            // 주방 카운터 대기열에서 제거
            if (currentKitchenCounter != null)
            {
                currentKitchenCounter.LeaveQueue(this);
            }
            
            // NavMeshAgent 이동 재시작
            if (agent != null)
            {
                agent.isStopped = false;
                Debug.Log($"AI {gameObject.name}: 주방 카운터 활동 종료 - NavMeshAgent 이동 재시작");
            }
            
            // 주방 관련 변수들 정리
            CleanupKitchenVariables();
            
            Debug.Log($"AI {gameObject.name}: 주방 카운터 상태 정리 완료");
        }
        
        if (isEating)
        {
            Debug.Log($"AI {gameObject.name}: CleanupResources에서 식사 상태 정리");
            
            // NavMeshAgent 이동 재시작
            if (agent != null)
            {
                agent.isStopped = false;
                Debug.Log($"AI {gameObject.name}: 식사 종료 - NavMeshAgent 이동 재시작");
            }

            // 저장된 위치로 복귀
            transform.position = preChairPosition;
            transform.rotation = preChairRotation;
            Debug.Log($"AI {gameObject.name}: 식사 종료 - 이전 위치로 복귀");

            // 식사 상태 해제
            isEating = false;
            currentKitchenTransform = null;
            currentChairTransform = null;
            
            Debug.Log($"AI {gameObject.name}: 식사 상태 정리 완료");
        }
        
        if (currentRoomIndex != -1)
        {
            lock (lockObject)
            {
                if (currentRoomIndex >= 0 && currentRoomIndex < roomList.Count)
                {
                    roomList[currentRoomIndex].isOccupied = false;
                    //Debug.Log($"AI {gameObject.name} 정리: 룸 {currentRoomIndex + 1}번 반환.");
                }
                currentRoomIndex = -1;
            }
        }

        isInQueue = false;
        isWaitingForService = false;
        isScheduledForDespawn = false; // 디스폰 예정 플래그 리셋

        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
            //Debug.Log($"[AIAgent] {gameObject.name}: 대기열에서 제거 완료");
        }
        
        //Debug.Log($"[AIAgent] {gameObject.name}: CleanupResources 완료");
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

    public void OnServiceComplete()
    {
        isWaitingForService = false;
        isInQueue = false;
        if (counterManager != null)
        {
            counterManager.LeaveQueue(this);
        }
    }
    #endregion
}
}