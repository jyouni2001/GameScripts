using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;
using System.Collections.Generic;
using JY;

namespace JY
{
    /// <summary>
    /// AI 직원 시스템 - 기존 AIAgent와 별도로 동작하는 직원 관리 시스템
    /// 시간 기반 행동, 급여 시스템, 고용/해고 기능을 제공
    /// </summary>
    public class AIEmployee : MonoBehaviour
    {
        #region Inspector 설정
        
        [Header("직원 기본 정보 (EmployeeHiringSystem에서 설정됨)")]
        [Tooltip("직원 이름")]
        public string employeeName = "직원";
        
        [Tooltip("직업/역할 (자동 설정)")]
        [System.NonSerialized]
        public string jobRole = "서빙";
        
        [Tooltip("일급 (골드) (자동 설정)")]
        [System.NonSerialized]
        public int dailyWage = 100;
        
        [Header("시간 설정 (자동 설정)")]
        [Tooltip("출근 시간 (시) (자동 설정)")]
        [System.NonSerialized]
        public int workStartHour = 8;
        
        [Tooltip("퇴근 시간 (시) (자동 설정)")]
        [System.NonSerialized]
        public int workEndHour = 22;
        
        [Header("위치 설정")]
        [Tooltip("근무 위치")]
        public Transform workPosition;
        
        [Tooltip("스폰 포인트 (자동 설정)")]
        [System.NonSerialized]
        public Transform spawnPoint;
        
        [Tooltip("수동으로 위치 설정 (체크시 태그 기반 자동 할당 무시)")]
        public bool useManualPositions = false;
        
        [Header("태그 기반 위치 설정 (자동 설정)")]
        [Tooltip("근무 위치 태그 (EmployeeHiringSystem에서 자동 설정)")]
        [System.NonSerialized]
        public string workPositionTag = "WorkPosition_Reception";
        
        [Header("배정된 위치 (자동 설정)")]
        [Tooltip("배정된 카운터 (카운터 직원인 경우)")]
        [System.NonSerialized]
        public GameObject assignedCounter;
        
        [Tooltip("배정된 식당 (식당 직원인 경우)")]
        [System.NonSerialized]
        public GameObject assignedKitchen;
        
        [Tooltip("이동 속도")]
        [Range(1f, 10f)]
        public float moveSpeed = 3.5f;
        
        [Header("애니메이션")]
        [Tooltip("애니메이션 컨트롤러")]
        public Animator animator;
        
        [Tooltip("작업 애니메이션 트리거")]
        public string workAnimationTrigger = "Work";
        
        [Tooltip("이동 애니메이션 트리거")]
        public string moveAnimationTrigger = "Move";
        
        [Header("요리 도구")]
        [Tooltip("웍 오브젝트 (요리 시 활성화)")]
        public GameObject wokObject;
        
        [Tooltip("접시 오브젝트 (완성된 음식, 복귀 시 활성화)")]
        public GameObject plateObject;
        
        [Header("고용 상태")]
        [Tooltip("현재 고용 여부 (HireEmployee() 메서드로만 설정 가능)")]
        [SerializeField] private bool isHired = false;
        
        [Tooltip("고용 시작일")]
        [SerializeField] private DateTime hireDate = DateTime.MinValue;
        
        [Tooltip("마지막 급여 지급일")]
        [SerializeField] private DateTime lastPayDate = DateTime.MinValue;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시")]
        public bool showDebugLogs = true;
        
        [Tooltip("중요한 이벤트만 로그")]
        public bool showImportantLogsOnly = false;
        
        #endregion
        
        #region 비공개 변수
        
        // 컴포넌트 참조
        private NavMeshAgent navAgent;
        private TimeSystem timeSystem;
        private PlayerWallet playerWallet;
        
        // 상태 관리
        private EmployeeState currentState = EmployeeState.Idle;
        private EmployeeState previousState = EmployeeState.Idle;
        private Transform currentTarget;
        private bool isMoving = false;
        private bool isWorking = false;
        
        // 시간 관리
        private int lastHour = -1;
        private int lastDay = -1;
        
        // 위치 할당 재시도 관리
        private bool hasRetryAttempted = false;
        private float lastPositionCheckTime = 0f;
        
        // 퇴근 관리
        private bool shouldReturnToSpawn = false;
        
        // 주방 관련 변수
        private bool _isProcessingOrder = false;
        private Transform gasPosition;
        private Coroutine orderProcessingCoroutine;
        
        /// <summary>
        /// 현재 주문 처리 중인지 여부 (외부 접근용)
        /// </summary>
        public bool isProcessingOrder => _isProcessingOrder;
        
        // 코루틴 관리
        private Coroutine behaviorCoroutine;
        private Coroutine workCoroutine;
        
        #endregion
        
        #region 열거형
        
        /// <summary>
        /// 직원 상태 열거형
        /// </summary>
        public enum EmployeeState
        {
            Idle,           // 대기
            Moving,         // 이동 중
            Working,        // 작업 중
            Resting,        // 휴식 중
            OffDuty,        // 퇴근
            ReturningToSpawn, // 스폰 포인트로 복귀 중
            ReceivingOrder, // 주문 받는 중
            MovingToGas,    // 가스레인지로 이동 중
            Cooking         // 요리 중
        }
        
        #endregion
        
        #region 프로퍼티
        
        /// <summary>
        /// 현재 직원 상태
        /// </summary>
        public EmployeeState CurrentState => currentState;
        
        /// <summary>
        /// 고용 여부
        /// </summary>
        public bool IsHired => isHired;
        
        /// <summary>
        /// 현재 근무시간인지 확인
        /// </summary>
        public bool IsWorkTime
        {
            get
            {
                if (timeSystem == null) return false;
                int currentHour = timeSystem.CurrentHour;
                
                if (workStartHour <= workEndHour)
                {
                    return currentHour >= workStartHour && currentHour < workEndHour;
                }
                else
                {
                    // 자정을 넘나드는 경우
                    return currentHour >= workStartHour || currentHour < workEndHour;
                }
            }
        }
        
        /// <summary>
        /// 급여를 지급해야 하는지 확인
        /// </summary>
        public bool ShouldPaySalary
        {
            get
            {
                if (!isHired || timeSystem == null) return false;
                
                // 첫 급여이거나 날짜가 바뀌었고 0시인 경우
                return (lastPayDate == DateTime.MinValue) || 
                       (timeSystem.CurrentDay != lastDay && timeSystem.CurrentHour == 0);
            }
        }
        
        #endregion
        
        #region 주방 주문 시스템
        
        /// <summary>
        /// 주문 처리 시작
        /// </summary>
        /// <returns>주문 처리 시작 성공 여부</returns>
        public bool StartOrderProcessing()
        {
            DebugLog($"StartOrderProcessing 호출됨 - 처리중: {_isProcessingOrder}, 고용됨: {isHired}, 근무시간: {IsWorkTime}, 작업중: {isWorking}", true);
            DebugLog($"현재시간: {(timeSystem != null ? timeSystem.CurrentHour : -1)}시, 근무시간: {workStartHour}~{workEndHour}시", true);
            
            if (_isProcessingOrder)
            {
                DebugLog("이미 다른 주문을 처리 중입니다.", true);
                return false;
            }
            
            if (!isHired)
            {
                DebugLog("고용되지 않은 직원입니다.", true);
                return false;
            }
            
            if (!IsWorkTime)
            {
                DebugLog("근무시간이 아닙니다.", true);
                return false;
            }
            
            // 작업 위치에 도착했는지 확인 (중요!)
            if (!isWorking)
            {
                DebugLog("아직 작업 위치(WorkPosition_Kitchen)에 도착하지 않았습니다. 주문 불가!", true);
                return false;
            }
            
            DebugLog("주문 처리 시작!", true);
            _isProcessingOrder = true;
            
            // 기존 코루틴 정리
            if (orderProcessingCoroutine != null)
            {
                StopCoroutine(orderProcessingCoroutine);
            }
            
            orderProcessingCoroutine = StartCoroutine(ProcessOrderCoroutine());
            return true;
        }
        
        /// <summary>
        /// 주문 처리 코루틴 (고속 배속 대응) - 주문은 끝까지 처리하고 그 후 퇴근 체크
        /// </summary>
        private IEnumerator ProcessOrderCoroutine()
        {
            // 1. 주문 받기 (프레임 기반 대기)
            SetState(EmployeeState.ReceivingOrder);
            CleanUpAnimation();
            DebugLog("주문 받는 중...", true);
            
            // 실제 시간 대신 프레임 대기 (약 30프레임 = 0.5초)
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            
            // 2. Gas 위치 찾기 및 이동
            if (FindGasPosition())
            {
                DebugLog("🔥 인덕션으로 이동 시작", true);
                SetState(EmployeeState.MovingToGas);
                MoveToPosition(gasPosition);
                
                // 인덕션 위치 도착까지 대기 (타임아웃만 체크)
                float timeout = 0f;
                while (Vector3.Distance(transform.position, gasPosition.position) > 1.5f)
                {
                    timeout += Time.deltaTime;
                    if (timeout > 10f)
                    {
                        DebugLog("⚠️ 인덕션 이동 타임아웃", true);
                        goto CleanupAndExit;
                    }
                    yield return null;
                }
                
                // 3. 인덕션에서 요리
                SetState(EmployeeState.Cooking);
                transform.position = gasPosition.position;
                transform.rotation = gasPosition.rotation;
                DebugLog($"인덕션 도착 - 요리 시작", true);
                
                CleanUpAnimation();
                PlayAnimationBool(workAnimationTrigger, true);
                
                if (wokObject != null)
                {
                    wokObject.SetActive(true);
                    DebugLog("🥘 웍 활성화", true);
                }
                
                // 요리 시간 (프레임 기반 - 약 60프레임 = 1초)
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                }
                
                // 요리 종료 - 웍 비활성화, 접시 활성화
                PlayAnimationBool(workAnimationTrigger, false);
                if (wokObject != null)
                {
                    wokObject.SetActive(false);
                    DebugLog("🥘 웍 비활성화", true);
                }
                
                if (plateObject != null)
                {
                    plateObject.SetActive(true);
                    DebugLog("접시 활성화", true);
                }

                // 4. 작업 위치로 복귀
                DebugLog("작업 위치로 복귀", true);
                PlayAnimationBool("Picking", true);
                SetState(EmployeeState.Moving);
                MoveToPosition(workPosition);
                
                // 작업 위치 도착까지 대기 (타임아웃만 체크)
                timeout = 0f;
                while (workPosition != null && Vector3.Distance(transform.position, workPosition.position) > 1.5f)
                {
                    timeout += Time.deltaTime;
                    if (timeout > 10f)
                    {
                        DebugLog("⚠️ 복귀 타임아웃", true);
                        goto CleanupAndExit;
                    }
                    yield return null;
                }

                // 5. 작업 위치 도착
                if (workPosition != null)
                {
                    transform.position = workPosition.position;
                    transform.rotation = workPosition.rotation;
                    DebugLog($"작업 위치 복귀 완료", true);
                }
                
                // 6. Picking 애니메이션 종료
                PlayAnimationBool("Picking", false);
                
                // 7. 접시 비활성화
                if (plateObject != null)
                {
                    plateObject.SetActive(false);
                    DebugLog("접시 비활성화 (전달 완료)", true);
                }
                
                // 8. 정리
                CleanUpAnimation();
            }
            else
            {
                DebugLog("❌ 인덕션을 찾을 수 없습니다!", true);
            }
            
        CleanupAndExit:
            // 모든 오브젝트와 애니메이션 정리
            if (wokObject != null && wokObject.activeSelf)
            {
                wokObject.SetActive(false);
            }
            if (plateObject != null && plateObject.activeSelf)
            {
                plateObject.SetActive(false);
            }
            CleanUpAnimation();
            
            _isProcessingOrder = false;
            orderProcessingCoroutine = null;
            
            // Idle 상태로 전환
            SetState(EmployeeState.Idle);
            DebugLog("✅ 주문 처리 완료 - Idle", true);
            
            // 주문 처리 완료 후에만 퇴근 시간 체크
            if (shouldReturnToSpawn || !IsWorkTime)
            {
                DebugLog("주문 완료 후 퇴근 시간 확인 - 스폰 포인트로 복귀", true);
                ReturnToSpawn();
            }
        }
        
        /// <summary>
        /// 현재 주방 공간의 인덕션 위치 찾기
        /// </summary>
        private bool FindGasPosition()
        {
            DebugLog($"🔍 FindGasPosition 시작 - assignedKitchen: {(assignedKitchen != null ? assignedKitchen.name : "null")}, 현재 위치: {transform.position}", true);
            
            // 1. 먼저 배정된 주방에서 인덕션 찾기 (assignedKitchen 사용)
            if (assignedKitchen != null)
            {
                DebugLog($"✅ assignedKitchen 존재: {assignedKitchen.name}", true);
                
                KitchenComponent kitchenComp = assignedKitchen.GetComponent<KitchenComponent>();
                if (kitchenComp != null)
                {
                    DebugLog($"✅ KitchenComponent 발견", true);
                    
                    // 주방 범위 정보 출력
                    if (kitchenComp.kitchenInfo != null)
                    {
                        Bounds bounds = kitchenComp.kitchenInfo.bounds;
                        DebugLog($"📦 주방 Bounds - Center: {bounds.center}, Size: {bounds.size}, Min: {bounds.min}, Max: {bounds.max}", true);
                    }
                    
                    // 배정된 주방 범위 내의 "WorkPosition_Gas" 태그 찾기
                    GameObject[] gasObjects = GameObject.FindGameObjectsWithTag("WorkPosition_Gas");
                    DebugLog($"🔍 WorkPosition_Gas 태그 오브젝트 발견: {gasObjects.Length}개", true);
                    
                    Transform closestGas = null;
                    float closestDistance = float.MaxValue;
                    
                    foreach (GameObject gasObj in gasObjects)
                    {
                        if (gasObj == null) continue;
                        
                        Vector3 gasPos = gasObj.transform.position;
                        bool isInRange = kitchenComp.ContainsPosition(gasPos);
                        
                        // 거리 계산 (디버그용)
                        float distanceToKitchen = Vector3.Distance(gasPos, assignedKitchen.transform.position);
                        
                        DebugLog($"  🔍 인덕션: {gasObj.name}", true);
                        DebugLog($"     위치: {gasPos}, 주방과 거리: {distanceToKitchen:F1}m", true);
                        DebugLog($"     주방 범위 내부: {isInRange}", true);
                        
                        // 배정된 주방 범위 내에 있는지 확인
                        if (isInRange)
                        {
                            float distance = Vector3.Distance(transform.position, gasObj.transform.position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestGas = gasObj.transform;
                            }
                        }
                    }
                    
                    if (closestGas != null)
                    {
                        gasPosition = closestGas;
                        DebugLog($"🔥 인덕션 발견 (assignedKitchen): {closestGas.name} (위치: {closestGas.position}, 회전: {closestGas.rotation.eulerAngles})", true);
                        return true;
                    }
                    else
                    {
                        DebugLog($"❌ 배정된 주방({assignedKitchen.name}) 내에 WorkPosition_Gas 태그를 가진 인덕션을 찾을 수 없습니다.", true);
                    }
                }
                else
                {
                    DebugLog($"❌ assignedKitchen에 KitchenComponent가 없습니다!", true);
                }
            }
            else
            {
                DebugLog($"⚠️ assignedKitchen이 null입니다. 현재 위치 기반 검색으로 fallback...", true);
            }
            
            // 2. assignedKitchen이 없으면 현재 위치 기반으로 찾기 (fallback)
            DebugLog($"🔍 Fallback: 현재 위치 기반으로 주방 찾기...", true);
            KitchenComponent currentKitchen = GetCurrentKitchen();
            if (currentKitchen == null)
            {
                DebugLog("❌ 현재 주방을 찾을 수 없습니다. (GetCurrentKitchen 실패)", true);
                DebugLog($"  - KitchenDetector.Instance: {(KitchenDetector.Instance != null ? "존재" : "null")}", true);
                if (KitchenDetector.Instance != null)
                {
                    var detectedKitchens = KitchenDetector.Instance.GetDetectedKitchens();
                    DebugLog($"  - 감지된 주방 수: {detectedKitchens.Count}개", true);
                }
                return false;
            }
            
            DebugLog($"✅ 현재 주방 발견: {currentKitchen.gameObject.name}", true);
            
            // 주방 범위 정보 출력
            if (currentKitchen.kitchenInfo != null)
            {
                Bounds bounds = currentKitchen.kitchenInfo.bounds;
                DebugLog($"📦 Fallback 주방 Bounds - Center: {bounds.center}, Size: {bounds.size}", true);
            }
            
            // 현재 주방 범위 내의 WorkPosition_Gas 태그 찾기
            GameObject[] allGasObjects = GameObject.FindGameObjectsWithTag("WorkPosition_Gas");
            DebugLog($"🔍 Fallback - WorkPosition_Gas 태그 오브젝트 발견: {allGasObjects.Length}개", true);
            
            Transform closestGasInCurrent = null;
            float closestDistInCurrent = float.MaxValue;
            
            foreach (GameObject gasObj in allGasObjects)
            {
                if (gasObj == null) continue;
                
                Vector3 gasPos = gasObj.transform.position;
                bool isInRange = currentKitchen.ContainsPosition(gasPos);
                float distanceToKitchen = Vector3.Distance(gasPos, currentKitchen.transform.position);
                
                DebugLog($"  🔍 Fallback 인덕션: {gasObj.name}", true);
                DebugLog($"     위치: {gasPos}, 주방과 거리: {distanceToKitchen:F1}m", true);
                DebugLog($"     주방 범위 내부: {isInRange}", true);
                
                if (isInRange)
                {
                    float distance = Vector3.Distance(transform.position, gasObj.transform.position);
                    if (distance < closestDistInCurrent)
                    {
                        closestDistInCurrent = distance;
                        closestGasInCurrent = gasObj.transform;
                    }
                }
            }
            
            if (closestGasInCurrent != null)
            {
                gasPosition = closestGasInCurrent;
                DebugLog($"🔥 인덕션 발견 (현재 위치): {closestGasInCurrent.name} (거리: {closestDistInCurrent:F1}m)", true);
                return true;
            }
            
            // ✅ 3. 최후의 수단: 범위 무시하고 가장 가까운 인덕션 사용
            DebugLog("⚠️ 주방 범위 내에 인덕션을 찾을 수 없습니다. 범위 무시하고 가장 가까운 인덕션 검색...", true);
            
            Transform closestGasAnywhere = null;
            float closestDistanceAnywhere = float.MaxValue;
            
            foreach (GameObject gasObj in allGasObjects)
            {
                if (gasObj == null) continue;
                
                float distance = Vector3.Distance(transform.position, gasObj.transform.position);
                if (distance < closestDistanceAnywhere)
                {
                    closestDistanceAnywhere = distance;
                    closestGasAnywhere = gasObj.transform;
                }
            }
            
            if (closestGasAnywhere != null)
            {
                gasPosition = closestGasAnywhere;
                DebugLog($"🔥 인덕션 발견 (범위 무시): {closestGasAnywhere.name} (거리: {closestDistanceAnywhere:F1}m)", true);
                DebugLog($"⚠️ 주의: 주방 범위를 벗어난 인덕션을 사용 중입니다. 주방 Bounds를 조정하는 것을 권장합니다.", true);
                return true;
            }
            
            DebugLog("❌ 주방 내 WorkPosition_Gas 태그를 가진 인덕션을 찾을 수 없습니다.", true);
            DebugLog("🔧 해결방법: 1) 주방에 WorkPosition_Gas 태그를 가진 오브젝트가 있는지 확인 2) 주방 범위(Bounds)가 인덕션을 포함하는지 확인", true);
            return false;
        }
        
        /// <summary>
        /// 현재 위치가 속한 주방 찾기
        /// </summary>
        private KitchenComponent GetCurrentKitchen()
        {
            if (KitchenDetector.Instance == null) return null;
            
            var detectedKitchens = KitchenDetector.Instance.GetDetectedKitchens();
            foreach (var kitchen in detectedKitchens)
            {
                if (kitchen.gameObject != null)
                {
                    var kitchenComponent = kitchen.gameObject.GetComponent<KitchenComponent>();
                    if (kitchenComponent != null && kitchenComponent.ContainsPosition(transform.position))
                    {
                        return kitchenComponent;
                    }
                }
            }
            
            return null;
        }
        
        #endregion
        
        #region Unity 생명주기
        
        void Awake()
        {
            InitializeComponents();
        }
        
        void Start()
        {
            InitializeEmployee();
        }
        
        void Update()
        {
            if (!isHired) return;
            
            CheckTimeChanges();
            CheckWorkSchedule();  // 매 프레임 근무 시간 체크 (퇴근 처리용)
            UpdateBehavior();
            
            // 위치 유효성 검사는 3초마다만 실행 (성능 최적화)
            CheckWorkPositionValidityPeriodically();
        }
        
        void OnDestroy()
        {
            CleanupCoroutines();
        }
        
        #endregion
        
        #region 초기화
        
        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            // NavMeshAgent 설정
            navAgent = GetComponent<NavMeshAgent>();
            if (navAgent == null)
            {
                navAgent = gameObject.AddComponent<NavMeshAgent>();
            }
            navAgent.speed = moveSpeed;
            
            // NavMeshAgent 정확한 위치/회전 설정
            navAgent.acceleration = 100f;        // 가속도 증가 (빠르게 가속)
            navAgent.angularSpeed = 360f;        // ✅ 이동 중 자동 회전 (자연스러운 이동)
            navAgent.stoppingDistance = 0.05f;   // 정지 거리 최소화
            navAgent.autoBraking = true;         // 자동 브레이킹 활성화
            
            // Animator 설정
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
            
            // 시스템 참조 가져오기
            timeSystem = TimeSystem.Instance;
            playerWallet = PlayerWallet.Instance;
        }
        
        /// <summary>
        /// 직원 초기화
        /// </summary>
        private void InitializeEmployee()
        {
            if (timeSystem == null)
            {
                DebugLog("TimeSystem을 찾을 수 없습니다!", true);
                return;
            }
            
            if (playerWallet == null)
            {
                DebugLog("PlayerWallet을 찾을 수 없습니다!", true);
                return;
            }
            
            // 초기 상태 설정
            SetState(EmployeeState.Idle);
            
            // 시간 이벤트 구독
            timeSystem.OnHourChanged += OnHourChanged;
            timeSystem.OnDayChanged += OnDayChanged;
            
            DebugLog($"직원 초기화 완료: {employeeName} ({jobRole})", true);
        }
        
        #endregion
        
        #region 고용/해고 시스템
        
        /// <summary>
        /// 직원 고용
        /// </summary>
        public bool HireEmployee()
        {
            if (isHired)
            {
                DebugLog("이미 고용된 직원입니다.", true);
                return false;
            }
            
            isHired = true;
            hireDate = DateTime.Now;
            lastPayDate = DateTime.MinValue;
            
            // 수동 위치 설정 모드 강제 해제 (태그 기반 사용)
            if (useManualPositions)
            {
                DebugLog("수동 위치 설정 모드가 활성화되어 있어 자동으로 해제합니다.", true);
                useManualPositions = false;
            }
            
            // 작업 위치 자동 할당 (태그 기반)
            if (!useManualPositions)
            {
                DebugLog($"🏷️ 태그 확인 - 작업: '{workPositionTag}'", true);
                DebugLog($"🔄 자동 위치 할당 시작...", true);
                AssignWorkPositions();
                
                // 할당 결과 확인
                if (workPosition != null)
                {
                    DebugLog($"✅ 작업 위치 할당 성공: {workPosition.name}", true);
                }
                else
                {
                    DebugLog($"❌ 작업 위치 할당 실패! 태그 '{workPositionTag}'를 확인하세요!", true);
                }
            }
            else
            {
                DebugLog("수동 위치 설정 모드 - 자동 할당 건너뜀", true);
            }
            
            SetState(EmployeeState.Idle);
            StartBehaviorCoroutine();
            
            DebugLog($"직원 고용 완료: {employeeName}", true);
            return true;
        }
        
        /// <summary>
        /// 직원 해고
        /// </summary>
        public void FireEmployee()
        {
            if (!isHired)
            {
                DebugLog("고용되지 않은 직원입니다.", true);
                return;
            }
            
            // 작업 위치 해제
            if (!useManualPositions && WorkPositionManager.Instance != null)
            {
                WorkPositionManager.Instance.ReleaseWorkPosition(this);
            }
            
            isHired = false;
            SetState(EmployeeState.OffDuty);
            CleanupCoroutines();
            
            DebugLog($"직원 해고 완료: {employeeName}", true);
        }
        
        #endregion
        
        #region 급여 시스템
        
        /// <summary>
        /// 급여 지급 처리
        /// </summary>
        private void ProcessSalary()
        {
            if (!ShouldPaySalary) return;
            
            // 플레이어 지갑에서 급여 차감
            if (playerWallet.money >= dailyWage)
            {
                playerWallet.SpendMoney(dailyWage);
                lastPayDate = DateTime.Now;
                lastDay = timeSystem.CurrentDay;
                
                DebugLog($"급여 지급 완료: {employeeName} - {dailyWage}골드", true);
            }
            else
            {
                DebugLog($"급여 지급 실패: 골드 부족 ({dailyWage}골드 필요)", true);
                // TODO: 급여를 지급할 수 없는 경우의 처리
            }
        }
        
        #endregion
        
        #region 시간 관리
        
        /// <summary>
        /// 시간 변경 체크
        /// </summary>
        private void CheckTimeChanges()
        {
            if (timeSystem == null) return;
            
            int currentHour = timeSystem.CurrentHour;
            int currentDay = timeSystem.CurrentDay;
            
            // 시간이 바뀌었을 때
            if (currentHour != lastHour)
            {
                lastHour = currentHour;
                OnHourChanged(currentHour, timeSystem.CurrentMinute);
            }
            
            // 날짜가 바뀌었을 때
            if (currentDay != lastDay)
            {
                lastDay = currentDay;
                OnDayChanged(currentDay);
            }
        }
        
        /// <summary>
        /// 시간 변경 이벤트 핸들러
        /// </summary>
        private void OnHourChanged(int hour, int minute)
        {
            if (!isHired) return;
            
            // 0시에 급여 지급
            if (hour == 0 && minute == 0)
            {
                ProcessSalary();
            }
        }
        
        /// <summary>
        /// 날짜 변경 이벤트 핸들러
        /// </summary>
        private void OnDayChanged(int newDay)
        {
            if (!isHired) return;
            
            DebugLog($"새 날 시작: {newDay}일차", showImportantLogsOnly);
        }
        
        /// <summary>
        /// 근무 스케줄 체크
        /// </summary>
        private void CheckWorkSchedule()
        {
            if (!isHired) return;
            
            if (IsWorkTime)
            {
                // 근무시간 시작
                shouldReturnToSpawn = false;  // 퇴근 플래그 리셋
                
                // 작업위치로 이동
                if (currentState == EmployeeState.OffDuty || currentState == EmployeeState.Resting || currentState == EmployeeState.ReturningToSpawn)
                {
                    SetState(EmployeeState.Idle);
                    
                    // 즉시 작업위치로 이동 시작
                    if (workPosition != null)
                    {
                        MoveToPosition(workPosition);
                    }
                }
            }
            else
            {
                // 퇴근시간 - 플래그 설정
                if (!shouldReturnToSpawn)
                {
                    shouldReturnToSpawn = true;
                }
                
                // 작업 중이 아니면 즉시 퇴근 (Idle, Working 상태만 체크)
                if (currentState != EmployeeState.ReturningToSpawn && 
                    !_isProcessingOrder && 
                    (currentState == EmployeeState.Idle || currentState == EmployeeState.Working))
                {
                    ReturnToSpawn();
                }
            }
        }
        
        /// <summary>
        /// 스폰 포인트로 복귀
        /// </summary>
        private void ReturnToSpawn()
        {
            SetState(EmployeeState.ReturningToSpawn);
            
            // 스폰 포인트로 이동 시작
            if (spawnPoint != null)
            {
                MoveToPosition(spawnPoint);
            }
            else
            {
                // 스폰 포인트가 없으면 즉시 디스폰
                DespawnEmployee();
            }
        }
        
        #endregion
        
        #region 행동 관리
        
        /// <summary>
        /// 행동 업데이트
        /// </summary>
        private void UpdateBehavior()
        {
            if (!isHired) return;
            
            switch (currentState)
            {
                case EmployeeState.Idle:
                    HandleIdleState();
                    break;
                case EmployeeState.Moving:
                    HandleMovingState();
                    break;
                case EmployeeState.Working:
                    HandleWorkingState();
                    break;
                case EmployeeState.Resting:
                    HandleRestingState();
                    break;
                case EmployeeState.OffDuty:
                    HandleOffDutyState();
                    break;
                case EmployeeState.ReturningToSpawn:
                    HandleReturningToSpawnState();
                    break;
                case EmployeeState.ReceivingOrder:
                    HandleReceivingOrderState();
                    break;
                case EmployeeState.MovingToGas:
                    HandleMovingToGasState();
                    break;
                case EmployeeState.Cooking:
                    HandleCookingState();
                    break;
            }
        }
        
        /// <summary>
        /// 대기 상태 처리
        /// </summary>
        private void HandleIdleState()
        {
            // 근무시간인지 확인
            if (!IsWorkTime) return;
            
            // workPosition 확인
            if (workPosition == null) return;
            
            // 이미 작업위치에 있는지 확인
            if (Vector3.Distance(transform.position, workPosition.position) < 1f)
            {
                // 작업위치에 도착했으므로 작업 시작
                if (!isMoving)
                {
                    SetState(EmployeeState.Working);
                }
                return;
            }
            
            // 작업위치로 이동
            if (!isMoving)
            {
                MoveToPosition(workPosition);
            }
        }
        
        /// <summary>
        /// 이동 상태 처리
        /// </summary>
        private void HandleMovingState()
        {
            if (navAgent != null && navAgent.remainingDistance < 0.5f && !navAgent.pathPending)
            {
                // ✅ 목적지 도착 - 정확한 위치와 회전값 설정 (반동 제거)
                if (IsWorkTime && workPosition != null)
                {
                    // 작업 위치로 정확히 이동 및 회전
                    transform.position = workPosition.position;
                    transform.rotation = workPosition.rotation;
                    
                    // NavMeshAgent 완전 정지 (반동 제거)
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                    
                    isMoving = false;
                    SetState(EmployeeState.Working);
                }
                else if (!IsWorkTime && spawnPoint != null && currentState == EmployeeState.ReturningToSpawn)
                {
                    // 스폰 포인트로 정확히 이동 및 회전
                    transform.position = spawnPoint.position;
                    transform.rotation = spawnPoint.rotation;
                    
                    // NavMeshAgent 완전 정지
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                    
                    isMoving = false;
                    // 스폰 포인트 도착 - 디스폰 처리는 HandleReturningToSpawnState에서 처리
                }
                else
                {
                    // NavMeshAgent 정지
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                    
                    isMoving = false;
                    
                    // 위치 정보가 없으면 상태만 변경
                    if (IsWorkTime)
                    {
                        SetState(EmployeeState.Working);
                    }
                    else
                    {
                        SetState(EmployeeState.ReturningToSpawn);
                    }
                }
            }
        }
        
        /// <summary>
        /// 작업 상태 처리
        /// </summary>
        private void HandleWorkingState()
        {
            // 작업 위치에 있을 때는 기본 대기 애니메이션 유지
            if (animator != null)
            {
                // 이동 중이 아니고 주문 처리 중이 아니면 모든 애니메이션 끄기
                bool isActuallyMoving = navAgent != null && navAgent.velocity.magnitude > 0.1f;
                
                if (!isActuallyMoving && !_isProcessingOrder)
                {
                    // 애니메이션과 오브젝트 완전히 정리
                    CleanUpAnimation(); // 모든 애니메이션 끄기 → 자동으로 Idle
                    
                    // 추가: 혹시 모를 오브젝트 정리
                    if (wokObject != null && wokObject.activeSelf)
                    {
                        wokObject.SetActive(false);
                    }
                    if (plateObject != null && plateObject.activeSelf)
                    {
                        plateObject.SetActive(false);
                    }
                }
            }
            
            if (!isWorking)
            {
                StartWork();
            }
        }
        
        /// <summary>
        /// 휴식 상태 처리
        /// </summary>
        private void HandleRestingState()
        {
            // 근무시간이 되었는지 확인
            if (IsWorkTime)
            {
                // 근무시간이 되었으므로 다시 근무 시작
                SetState(EmployeeState.Idle);
                DebugLog("🔔 휴식 중 근무시간 시작 - 작업 시작", true);
            }
        }
        
        /// <summary>
        /// 퇴근 상태 처리 (더 이상 사용하지 않음, 호환성을 위해 유지)
        /// </summary>
        private void HandleOffDutyState()
        {
            // 스폰 포인트로 복귀 상태로 전환
            if (IsWorkTime) 
            {
                // 근무시간이 되었으므로 다시 근무 시작 (만약을 위한 fallback)
                SetState(EmployeeState.Idle);
                return;
            }
        }
        
        /// <summary>
        /// 스폰 포인트로 복귀 상태 처리
        /// </summary>
        private void HandleReturningToSpawnState()
        {
            // 퇴근시간인지 확인
            if (IsWorkTime) 
            {
                // 근무시간이 되었으므로 다시 근무 시작
                SetState(EmployeeState.Idle);
                return;
            }
            
            // 스폰 포인트에 도착했는지 확인
            if (spawnPoint != null && Vector3.Distance(transform.position, spawnPoint.position) < 1f)
            {
                // 스폰 포인트 도착 - 디스폰
                if (!isMoving)
                {
                    DespawnEmployee();
                }
                return;
            }
            
            // 스폰 포인트가 없으면 즉시 디스폰
            if (spawnPoint == null)
            {
                DespawnEmployee();
            }
        }
        
        /// <summary>
        /// 직원 디스폰 처리
        /// </summary>
        private void DespawnEmployee()
        {
            // EmployeeHiringSystem에 알림
            if (EmployeeHiringSystem.Instance != null)
            {
                EmployeeHiringSystem.Instance.OnEmployeeDespawned(this);
            }
            
            // 오브젝트 파괴
            Destroy(gameObject);
        }
        
        #endregion
        
        #region 상태 관리
        
        /// <summary>
        /// 상태 설정
        /// </summary>
        private void SetState(EmployeeState newState)
        {
            if (currentState == newState) return;
            
            previousState = currentState;
            currentState = newState;
            
            OnStateChanged(previousState, newState);
            DebugLog($"상태 변경: {previousState} -> {newState}", showImportantLogsOnly);
        }
        
        /// <summary>
        /// 상태 변경 처리
        /// </summary>
        private void OnStateChanged(EmployeeState oldState, EmployeeState newState)
        {
            // 이전 상태 정리
            CleanupPreviousState(oldState);
            
            // 새 상태 시작
            StartNewState(newState);
        }
        
        /// <summary>
        /// 이전 상태 정리
        /// </summary>
        private void CleanupPreviousState(EmployeeState state)
        {
            switch (state)
            {
                case EmployeeState.Working:
                    StopWork();
                    break;
                case EmployeeState.Moving:
                    StopMoving();
                    break;
            }
        }
        
        /// <summary>
        /// 새 상태 시작
        /// </summary>
        private void StartNewState(EmployeeState state)
        {
            switch (state)
            {
                case EmployeeState.Idle:
                    CleanUpAnimation(); // 모든 애니메이션 끄기 → 자동으로 Idle
                    break;
                case EmployeeState.Moving:
                    CleanUpAnimation();
                    PlayAnimationBool(moveAnimationTrigger, true);
                    //PlayAnimation(moveAnimationTrigger);
                    break;
                case EmployeeState.Working:
                    CleanUpAnimation();
                    PlayAnimationBool(workAnimationTrigger, true);
                    //PlayAnimation(workAnimationTrigger);
                    break;
                case EmployeeState.Resting:
                    CleanUpAnimation(); // 모든 애니메이션 끄기 → 자동으로 Idle
                    break;
                case EmployeeState.OffDuty:
                    CleanUpAnimation(); // 모든 애니메이션 끄기 → 자동으로 Idle
                    break;
            }
        }

        private void CleanUpAnimation()
        {
            // 모든 애니메이션 Bool 파라미터를 false로 설정
            if (animator != null)
            {
                if (!string.IsNullOrEmpty(workAnimationTrigger))
                {
                    animator.SetBool(workAnimationTrigger, false);
                }
                if (!string.IsNullOrEmpty(moveAnimationTrigger))
                {
                    animator.SetBool(moveAnimationTrigger, false);
                }
            }
            
            // 요리 도구 오브젝트 비활성화 (확실하게)
            if (wokObject != null && wokObject.activeSelf)
            {
                wokObject.SetActive(false);
                DebugLog("🥘 웍 오브젝트 비활성화 (정리)", showImportantLogsOnly);
            }
            
            if (plateObject != null && plateObject.activeSelf)
            {
                plateObject.SetActive(false);
                DebugLog("🍽️ 접시 오브젝트 비활성화 (정리)", showImportantLogsOnly);
            }
        }

        #endregion
        
        #region 이동 관리
        
        /// <summary>
        /// 위치로 이동
        /// </summary>
        private void MoveToPosition(Transform target)
        {
            if (target == null || navAgent == null) return;
            
            currentTarget = target;
            Vector3 targetPosition = target.position;
            
            // ✅ NavMeshAgent 재시작 (이전에 정지되었을 수 있음)
            navAgent.isStopped = false;
            
            // 정확히 설정한 위치로 이동 (보정 없음)
            navAgent.SetDestination(targetPosition);
            
            SetState(EmployeeState.Moving);
            isMoving = true;
            
            DebugLog($"🎯 정확한 위치로 이동: {target.name} -> {targetPosition}", true);
        }
        
        /// <summary>
        /// 이동 중지
        /// </summary>
        private void StopMoving()
        {
            isMoving = false;
            if (navAgent != null)
            {
                navAgent.ResetPath();
            }
        }
        
        /// <summary>
        /// 주문 받는 상태 처리
        /// </summary>
        private void HandleReceivingOrderState()
        {
            // 주문 받는 중 - 코루틴에서 처리하므로 여기서는 애니메이션만 확인
            if (animator != null)
            {
                CleanUpAnimation(); // 모든 애니메이션 끄기 → 자동으로 Idle
            }
        }
        
        /// <summary>
        /// 가스레인지로 이동 상태 처리
        /// </summary>
        private void HandleMovingToGasState()
        {
            // 가스 위치로 이동 중 - 코루틴에서 처리
            if (animator != null && !isMoving)
            {
                CleanUpAnimation();
                PlayAnimationBool(moveAnimationTrigger, true);
                isMoving = true;
            }
        }
        
        /// <summary>
        /// 요리 상태 처리
        /// </summary>
        private void HandleCookingState()
        {
            // 요리 중 - 코루틴에서 애니메이션 관리하므로 여기서는 아무것도 하지 않음
            // (매 프레임 애니메이션을 켜면 코루틴에서 끄려고 해도 계속 켜져서 멈춤)
        }
        
        #endregion
        
        #region 작업 관리
        
        /// <summary>
        /// 작업 시작
        /// </summary>
        private void StartWork()
        {
            if (isWorking) return;
            
            isWorking = true;
            
            if (workCoroutine != null)
            {
                StopCoroutine(workCoroutine);
            }
            
            workCoroutine = StartCoroutine(WorkCoroutine());
            DebugLog("작업 시작", showImportantLogsOnly);
        }
        
        /// <summary>
        /// 작업 중지
        /// </summary>
        private void StopWork()
        {
            isWorking = false;
            
            if (workCoroutine != null)
            {
                StopCoroutine(workCoroutine);
                workCoroutine = null;
            }
            
            DebugLog("작업 중지", showImportantLogsOnly);
        }
        
        /// <summary>
        /// 작업 코루틴
        /// </summary>
        private IEnumerator WorkCoroutine()
        {
            while (isWorking && currentState == EmployeeState.Working)
            {
                // 작업 로직 (예: 서빙, 청소 등)
                PerformWorkAction();
                
                // 프레임 기반 대기 (약 120프레임 = 2초)
                for (int i = 0; i < 120; i++)
                {
                    yield return null;
                }
                
                // 작업 한 사이클 완료 후 퇴근 시간 체크
                if (!IsWorkTime || shouldReturnToSpawn)
                {
                    DebugLog("작업 완료 후 퇴근 시간 확인 - 작업 종료", true);
                    yield break;
                }
            }
        }
        
        /// <summary>
        /// 작업 행동 수행
        /// </summary>
        private void PerformWorkAction()
        {
            // 직업에 따른 특별한 작업 로직
            switch (jobRole.ToLower())
            {
                case "서빙":
                    // 서빙 작업 로직
                    break;
                case "청소":
                    // 청소 작업 로직
                    break;
                case "요리":
                    // 요리 작업 로직
                    break;
                default:
                    // 기본 작업 로직
                    break;
            }
        }
        
        #endregion
        
        #region 애니메이션 관리
        
        /// <summary>
        /// 애니메이션 재생
        /// </summary>
        private void PlayAnimation(string triggerName)
        {
            if (animator != null && !string.IsNullOrEmpty(triggerName))
            {
                animator.SetTrigger(triggerName);
            }
        }

        private void PlayAnimationBool(string animationName, bool working)
        {
            if (animator != null && !string.IsNullOrEmpty(animationName))
            {

                animator.SetBool(animationName, working);
            }
        }
        
        #endregion
        
        #region 코루틴 관리
        
        /// <summary>
        /// 행동 코루틴 시작
        /// </summary>
        private void StartBehaviorCoroutine()
        {
            if (behaviorCoroutine != null)
            {
                StopCoroutine(behaviorCoroutine);
            }
            
            behaviorCoroutine = StartCoroutine(BehaviorCoroutine());
        }
        
        /// <summary>
        /// 행동 코루틴
        /// </summary>
        private IEnumerator BehaviorCoroutine()
        {
            while (isHired)
            {
                // 주기적으로 행동 업데이트 (프레임 기반 - 약 60프레임 = 1초)
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                }
            }
        }
        
        /// <summary>
        /// 코루틴 정리
        /// </summary>
        private void CleanupCoroutines()
        {
            if (behaviorCoroutine != null)
            {
                StopCoroutine(behaviorCoroutine);
                behaviorCoroutine = null;
            }
            
            if (workCoroutine != null)
            {
                StopCoroutine(workCoroutine);
                workCoroutine = null;
            }
            
            if (orderProcessingCoroutine != null)
            {
                StopCoroutine(orderProcessingCoroutine);
                orderProcessingCoroutine = null;
            }
        }
        
        #endregion
        
        #region 공개 메서드
        
        /// <summary>
        /// 직원 정보 반환
        /// </summary>
        public string GetEmployeeInfo()
        {
            return $"이름: {employeeName}, 직업: {jobRole}, 상태: {currentState}, 고용: {(isHired ? "예" : "아니오")}";
        }
        
        /// <summary>
        /// 급여 정보 반환
        /// </summary>
        public string GetSalaryInfo()
        {
            return $"일급: {dailyWage}골드, 마지막 지급: {lastPayDate:yyyy-MM-dd}";
        }
        
        /// <summary>
        /// 근무시간 정보 반환
        /// </summary>
        public string GetWorkScheduleInfo()
        {
            return $"근무시간: {workStartHour:00}:00 - {workEndHour:00}:00";
        }
        
        #endregion
        
        #region 디버그
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[AIEmployee] {message}");
        }
        
        #endregion
        
        #region 작업 위치 관리
        
        /// <summary>
        /// 작업 위치 자동 할당 (태그 기반)
        /// </summary>
        private void AssignWorkPositions()
        {
            // 수동 위치 설정인 경우 건너뜀
            if (useManualPositions)
            {
                DebugLog("수동 위치 설정 모드입니다. 자동 할당을 건너뜁니다.");
                return;
            }
            
            // 태그가 비어있으면 기본값으로 설정
            if (string.IsNullOrEmpty(workPositionTag))
            {
                workPositionTag = "WorkPosition_Reception";
                DebugLog($"⚠️ 작업 위치 태그가 비어있어 기본값으로 설정: {workPositionTag}", true);
            }
            
            DebugLog($"🏷️ 할당할 태그 최종 확인 - 작업: '{workPositionTag}'", true);
            
            // 태그 기반으로 작업 위치 찾기
            AssignWorkPositionByTag();
        }
        
        /// <summary>
        /// 태그로 작업 위치 찾기
        /// </summary>
        private void AssignWorkPositionByTag()
        {
            if (string.IsNullOrEmpty(workPositionTag))
            {
                DebugLog("❌ 작업 위치 태그가 설정되지 않았습니다!", true);
                return;
            }
            
            DebugLog($"🔍 태그 '{workPositionTag}' 검색 시작...", true);
            
            // 씬의 모든 태그들 확인 (디버깅용)
            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int taggedObjectsCount = 0;
            foreach (GameObject obj in allObjects)
            {
                if (obj.tag == workPositionTag)
                {
                    taggedObjectsCount++;
                    DebugLog($"🎯 발견: {obj.name} (태그: {obj.tag}) 위치: {obj.transform.position}", true);
                }
            }
            
            GameObject[] workPositions = GameObject.FindGameObjectsWithTag(workPositionTag);
            DebugLog($"📋 FindGameObjectsWithTag 결과: {workPositions.Length}개, 직접 검색: {taggedObjectsCount}개", true);
            
            if (workPositions.Length == 0)
            {
                DebugLog($"❌ 태그 '{workPositionTag}'를 가진 오브젝트를 찾을 수 없습니다!", true);
                DebugLog($"🔧 해결방법: 1) 오브젝트에 태그 설정 확인 2) 태그 이름 철자 확인", true);
                return;
            }
            
            // 사용 가능한 위치 찾기 (다른 AI가 사용하지 않는 위치)
            foreach (GameObject pos in workPositions)
            {
                if (!IsPositionOccupiedByOtherAI(pos.transform))
                {
                    workPosition = pos.transform;
                    DebugLog($"✅ 작업 위치 할당됨: {pos.name} 위치: {pos.transform.position}", true);
                    return;
                }
            }
            
            // 모든 위치가 점유된 경우 첫 번째 위치 사용 (겹침 허용)
            workPosition = workPositions[0].transform;
            DebugLog($"⚠️ 모든 위치가 점유됨. 첫 번째 위치 사용: {workPositions[0].name}", true);
        }
        
        /// <summary>
        /// 다른 AI가 해당 위치를 사용하고 있는지 확인
        /// </summary>
        private bool IsPositionOccupiedByOtherAI(Transform position)
        {
            AIEmployee[] allEmployees = FindObjectsByType<AIEmployee>(FindObjectsSortMode.None);
            foreach (var emp in allEmployees)
            {
                if (emp != this && emp.isHired && emp.workPosition == position)
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 주기적 위치 유효성 검사 (30초마다)
        /// </summary>
        private void CheckWorkPositionValidityPeriodically()
        {
            // 30초마다만 검사 (성능 최적화)
            if (Time.time - lastPositionCheckTime >= 30f)
            {
                lastPositionCheckTime = Time.time;
                CheckWorkPositionValidity();
            }
        }
        
        /// <summary>
        /// 작업 위치 유효성 검사 (오브젝트 소멸 감지)
        /// </summary>
        private void CheckWorkPositionValidity()
        {
            if (!isHired) return;
            
            // ✅ 작업 위치만 체크 (카운터/주방 삭제는 EmployeeHiringSystem에서 처리)
            if (workPosition == null)
            {
                // 위치 재할당 시도
                DebugLog("⚠️ 작업 위치가 null입니다. 재할당을 시도합니다.", true);
                AssignWorkPositions();
                
                // 재할당 후에도 null이면 문제가 있음
                if (workPosition == null)
                {
                    DebugLog($"❌ 위치 재할당 실패. 태그 '{workPositionTag}'를 확인하세요!", true);
                    
                    // 30초 후 다시 시도
                    if (!hasRetryAttempted)
                    {
                        hasRetryAttempted = true;
                        StartCoroutine(RetryPositionAssignment());
                    }
                }
                else
                {
                    DebugLog($"✅ 위치 재할당 성공: {workPosition.name}", true);
                    hasRetryAttempted = false;
                }
                return;
            }
            
            // ✅ workPosition이 실제로 파괴되었는지 확인
            if (workPosition != null && workPosition.gameObject == null)
            {
                DebugLog($"⚠️ 작업 위치 오브젝트가 삭제되었습니다. 재할당을 시도합니다.", true);
                workPosition = null; // null로 설정하고 다음 체크에서 재할당
                return;
            }
            
            // ✅ 인덕션 위치만 체크 (요리 중일 때 필요)
            if (gasPosition != null && gasPosition.gameObject == null)
            {
                DebugLog($"🚨 인덕션이 삭제되었습니다! 주문 처리를 중단합니다.", true);
                HandleGasDestroyed();
            }
        }
        
        /// <summary>
        /// 가스레인지가 삭제되었을 때 처리
        /// </summary>
        private void HandleGasDestroyed()
        {
            // 주문 처리 중지
            _isProcessingOrder = false;
            gasPosition = null;
            
            // 코루틴 중지
            if (orderProcessingCoroutine != null)
            {
                StopCoroutine(orderProcessingCoroutine);
                orderProcessingCoroutine = null;
            }
            
            // 작업 위치로 복귀
            if (workPosition != null)
            {
                SetState(EmployeeState.Moving);
                MoveToPosition(workPosition);
            }
            else
            {
                SetState(EmployeeState.Idle);
            }
        }
        
        /// <summary>
        /// 스폰 포인트로 돌아가서 강제 해고
        /// </summary>
        private void ReturnToSpawnAndFire()
        {
            // 주문 처리 중지 (혹시 모를 경우 대비)
            _isProcessingOrder = false;
            
            // 스폰 포인트로 이동
            if (EmployeeHiringSystem.Instance != null && EmployeeHiringSystem.Instance.transform != null)
            {
                Transform spawnPoint = EmployeeHiringSystem.Instance.transform;
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.SetDestination(spawnPoint.position);
                    DebugLog($"📍 스폰 포인트로 이동 중: {spawnPoint.position}", true);
                }
            }
            
            // 잠시 후 해고 및 오브젝트 파괴
            StartCoroutine(DelayedFireAndDestroy());
        }
        
        /// <summary>
        /// 지연된 해고 및 파괴
        /// </summary>
        private System.Collections.IEnumerator DelayedFireAndDestroy()
        {
            // 스폰 포인트로 이동할 시간 대기 (프레임 기반 - 약 180프레임 = 3초)
            for (int i = 0; i < 180; i++)
            {
                yield return null;
            }
            
            // 해고 처리
            FireEmployee();
            
            // EmployeeHiringSystem에서 제거
            if (EmployeeHiringSystem.Instance != null)
            {
                EmployeeHiringSystem.Instance.RemoveEmployeeFromList(this);
            }
            
            DebugLog($"직원 {employeeName}이 강제 해고되어 파괴됩니다.", true);
            
            // 오브젝트 파괴
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 작업 위치 재할당 (필요시 호출)
        /// </summary>
        public void ReassignWorkPosition()
        {
            if (!useManualPositions && WorkPositionManager.Instance != null)
            {
                // 기존 위치 해제
                WorkPositionManager.Instance.ReleaseWorkPosition(this);
                
                // 새 위치 할당
                AssignWorkPositions();
            }
        }
        
        /// <summary>
        /// 현재 할당된 작업 위치 정보 반환
        /// </summary>
        public string GetAssignedPositionInfo()
        {
            string workInfo = workPosition != null ? workPosition.name : "미할당";
            string spawnInfo = spawnPoint != null ? spawnPoint.name : "미할당";
            return $"작업위치: {workInfo}, 스폰포인트: {spawnInfo}";
        }
        
        /// <summary>
        /// 위치 할당 재시도 코루틴
        /// </summary>
        private System.Collections.IEnumerator RetryPositionAssignment()
        {
            DebugLog("30초 후 위치 할당을 재시도합니다...", true);
            
            // 프레임 기반 대기 (약 1800프레임 = 30초)
            for (int i = 0; i < 1800; i++)
            {
                yield return null;
            }
            
            // 마지막 재시도
            AssignWorkPositions();
            
            if (workPosition == null)
            {
                DebugLog($"⚠️ 최종 위치 할당 실패! 태그 '{workPositionTag}'를 가진 오브젝트가 씬에 있는지 확인하세요.", true);
                // ✅ 해고는 EmployeeHiringSystem이 처리하도록 함
                hasRetryAttempted = false; // 다음에 다시 시도할 수 있도록
            }
            else
            {
                DebugLog($"✅ 재시도 성공! 위치 할당됨: {workPosition.name}", true);
                hasRetryAttempted = false;
            }
        }
        
        #endregion
        
        #region Inspector 버튼 (Editor에서만 동작)
        
        #if UNITY_EDITOR
        [ContextMenu("직원 고용")]
        private void EditorHireEmployee()
        {
            HireEmployee();
        }
        
        [ContextMenu("직원 해고")]
        private void EditorFireEmployee()
        {
            FireEmployee();
        }
        
        [ContextMenu("급여 지급")]
        private void EditorPaySalary()
        {
            ProcessSalary();
        }
        
        [ContextMenu("작업 위치 재할당")]
        private void EditorReassignPosition()
        {
            ReassignWorkPosition();
        }
        
        [ContextMenu("위치 정보 출력")]
        private void EditorShowPositionInfo()
        {
        }
        #endif
        
        #endregion
    }
}
