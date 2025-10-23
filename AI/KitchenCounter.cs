using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JY;

namespace JY
{
    /// <summary>
    /// 주방 카운터 시스템
    /// 플레이어가 주방 카운터에 접근하면 주변 AI 직원에게 주문을 전달
    /// </summary>
    public class KitchenCounter : MonoBehaviour
    {
    [Header("디버그 설정")]
    [Tooltip("디버그 로그 표시")]
    [SerializeField] private bool showDebugLogs = true;
    
    [Header("대기열 설정")]
    [Tooltip("최대 대기열 길이")]
    [SerializeField] private int maxQueueLength = 2;
    [Tooltip("대기열 간격")]
    [SerializeField] private float queueSpacing = 2f;
    [Tooltip("카운터와 서비스 받는 위치 사이의 거리")]
    [SerializeField] private float counterServiceDistance = 2f;
    
    // 내부 변수
    private bool isProcessingOrder = false;
    private AIEmployee currentAssignedEmployee;
    private AIEmployee assignedKitchenEmployee;  // 이 주방에 배정된 전담 직원
    private KitchenComponent associatedKitchen;
    private Queue<AIAgent> customerQueue = new Queue<AIAgent>();
    private AIAgent currentCustomer;
    private Vector3 counterFront;
    private Dictionary<AIAgent, float> queueJoinTimes = new Dictionary<AIAgent, float>(); // AI가 대기열에 합류한 시간 추적
        
        // 컴포넌트 참조
        private Collider counterCollider;
        
        #region Unity 생명주기
        
        void Awake()
        {
            InitializeComponents();
        }
        
        void Start()
        {
            InitializeKitchenCounter();
            // 카운터 정면 위치 계산
            counterFront = transform.position + transform.forward * counterServiceDistance;
        }
        
        void Update()
        {
            ProcessCustomerQueue();
        }
        
        #endregion
        
        #region 초기화
        
        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            // Collider 설정 (트리거로)
            counterCollider = GetComponent<Collider>();
            if (counterCollider == null)
            {
                counterCollider = gameObject.AddComponent<BoxCollider>();
                DebugLog("BoxCollider 자동 추가됨");
            }
            counterCollider.isTrigger = true;
            
            // 태그 설정
            if (!CompareTag("KitchenCounter"))
            {
                tag = "KitchenCounter";
                DebugLog("태그가 KitchenCounter로 설정됨");
            }
        }
        
        /// <summary>
        /// 주방 카운터 초기화
        /// </summary>
        private void InitializeKitchenCounter()
        {
            // 연결된 주방 찾기
            FindAssociatedKitchen();
            
            DebugLog($"주방 카운터 초기화 완료 - 연결된 주방: {(associatedKitchen != null ? associatedKitchen.name : "없음")}");
        }
        
        /// <summary>
        /// 연결된 주방 찾기
        /// </summary>
        private void FindAssociatedKitchen()
        {
            if (KitchenDetector.Instance == null) return;
            
            var detectedKitchens = KitchenDetector.Instance.GetDetectedKitchens();
            foreach (var kitchen in detectedKitchens)
            {
                if (kitchen.gameObject != null)
                {
                    var kitchenComponent = kitchen.gameObject.GetComponent<KitchenComponent>();
                    if (kitchenComponent != null && kitchenComponent.ContainsPosition(transform.position))
                    {
                        associatedKitchen = kitchenComponent;
                        DebugLog($"연결된 주방 발견: {kitchenComponent.name}");
                        break;
                    }
                }
            }
        }
        
        #endregion
        
        #region 대기열 시스템
        
        /// <summary>
        /// AI가 대기열에 합류 요청
        /// </summary>
        public bool TryJoinQueue(AIAgent agent)
        {
            DebugLog($"대기열 진입 요청 - AI: {agent.gameObject.name}, 현재 대기 인원: {customerQueue.Count}/{maxQueueLength}", true);
            
            // 직원이 없으면 대기열 진입 불가
            AIEmployee availableEmployee = FindNearbyEmployee();
            if (availableEmployee == null)
            {
                DebugLog($"사용 가능한 직원이 없습니다. AI {agent.gameObject.name}의 대기열 진입 거부", true);
                return false;
            }
            
            if (customerQueue.Count >= maxQueueLength)
            {
                DebugLog($"대기열이 가득 찼습니다. (현재 {customerQueue.Count}명, 최대 {maxQueueLength}명)", true);
                return false;
            }

            customerQueue.Enqueue(agent);
            queueJoinTimes[agent] = Time.time; // 대기 시작 시간 기록
            UpdateQueuePositions();
            DebugLog($"AI {agent.gameObject.name}이(가) 주방 대기열에 합류했습니다. (대기 인원: {customerQueue.Count}명, 담당 직원: {availableEmployee.employeeName})", true);
            return true;
        }
        
        /// <summary>
        /// AI가 대기열에서 나가기 요청
        /// </summary>
        public void LeaveQueue(AIAgent agent)
        {
            DebugLog($"대기열 나가기 요청 - AI: {agent.gameObject.name}");
            
            if (currentCustomer == agent)
            {
                DebugLog($"현재 서비스 중인 AI {agent.gameObject.name} 서비스 중단");
                currentCustomer = null;
                isProcessingOrder = false;
            }

            // 대기 시간 기록 제거
            if (queueJoinTimes.ContainsKey(agent))
            {
                queueJoinTimes.Remove(agent);
            }

            RemoveFromQueue(customerQueue, agent);
            UpdateQueuePositions();
            DebugLog($"AI {agent.gameObject.name}이(가) 주방 대기열에서 나갔습니다. (남은 인원: {customerQueue.Count}명)");
        }
        
        /// <summary>
        /// AI가 대기열에서 대기한 시간을 반환합니다.
        /// </summary>
        public float GetWaitingTime(AIAgent agent)
        {
            if (queueJoinTimes.ContainsKey(agent))
            {
                return Time.time - queueJoinTimes[agent];
            }
            return 0f;
        }
        
        /// <summary>
        /// 대기열에서 특정 AI 제거
        /// </summary>
        private void RemoveFromQueue(Queue<AIAgent> queue, AIAgent agent)
        {
            var tempQueue = new Queue<AIAgent>();
            bool removed = false;
            
            while (queue.Count > 0)
            {
                var queuedAgent = queue.Dequeue();
                if (queuedAgent != agent)
                {
                    tempQueue.Enqueue(queuedAgent);
                }
                else
                {
                    removed = true;
                    DebugLog($"대기열에서 AI {agent.gameObject.name} 제거됨");
                }
            }
            while (tempQueue.Count > 0)
            {
                queue.Enqueue(tempQueue.Dequeue());
            }
            
            if (!removed)
            {
                DebugLog($"AI {agent.gameObject.name}이(가) 대기열에 없어서 제거할 수 없습니다.");
            }
        }
        
        /// <summary>
        /// 대기열 위치 업데이트
        /// </summary>
        private void UpdateQueuePositions()
        {
            DebugLog($"대기열 위치 업데이트 시작 - 총 {customerQueue.Count}명, 현재 서비스 고객: {(currentCustomer != null ? currentCustomer.name : "없음")}");
            
            // 카운터를 바라보는 회전값 계산 (카운터 방향)
            Quaternion faceCounterRotation = Quaternion.LookRotation(transform.forward);
            
            int queueIndex = 0;
            int agentIndex = 0;
            
            foreach (var agent in customerQueue)
            {
                if (agent != null)
                {
                    if (agent == currentCustomer)
                    {
                        // 현재 서비스 받는 고객은 서비스 위치로 (회전 포함)
                        agent.SetQueueDestination(counterFront, faceCounterRotation);
                        DebugLog($"AI {agent.name}: 서비스 위치로 이동 설정 (위치: {counterFront})");
                    }
                    else
                    {
                        // 대기 중인 고객들은 대기열 위치로 (queueIndex 사용, 회전 포함)
                        float distance = counterServiceDistance + (queueIndex * queueSpacing);
                        Vector3 queuePosition = transform.position + transform.forward * distance;
                        agent.SetQueueDestination(queuePosition, faceCounterRotation);
                        DebugLog($"AI {agent.name}: 대기열 {queueIndex + 1}번째 위치로 이동 설정 (위치: {queuePosition})");
                        queueIndex++; // 대기열 순서만 증가
                    }
                    agentIndex++; // 전체 에이전트 인덱스
                }
                else
                {
                    DebugLog($"null AI 발견됨 (인덱스: {agentIndex})");
                }
            }
            
            DebugLog($"대기열 위치 업데이트 완료 - 대기열: {queueIndex}명, 서비스 중: {(currentCustomer != null ? 1 : 0)}명");
        }
        
        /// <summary>
        /// 현재 서비스 받을 수 있는지 확인
        /// </summary>
        public bool CanReceiveService(AIAgent agent)
        {
            bool canReceive = customerQueue.Count > 0 && customerQueue.Peek() == agent && !isProcessingOrder;
            DebugLog($"서비스 가능 확인 - AI: {agent.gameObject.name}, 결과: {canReceive}");
            return canReceive;
        }
        
        #endregion
        
        #region 고객 대기열 처리
        
        /// <summary>
        /// 고객 대기열 처리
        /// </summary>
        private void ProcessCustomerQueue()
        {
            // 현재 주문 처리 중이면 대기
            if (isProcessingOrder) return;
            
            // 큐에서 null 객체 정리
            CleanupQueue();
            
            // 대기 중인 고객이 있고, 현재 처리 중인 고객이 없으면 다음 고객 처리
            if (customerQueue.Count > 0 && currentCustomer == null)
            {
                AIAgent nextCustomer = customerQueue.Peek(); // 다음 고객 확인
                
                // 고객이 실제로 서비스 위치에 도착했을 때만 currentCustomer 설정
                if (nextCustomer != null && IsCustomerAtServicePosition(nextCustomer))
                {
                    currentCustomer = nextCustomer;
                    DebugLog($"고객 {currentCustomer.name}이 서비스 위치 도착 - 서비스 시작");
                    TryPlaceOrder();
                }
                else if (nextCustomer != null)
                {
                    // 고객이 아직 도착하지 않았으면 대기
                    DebugLog($"⏳ 고객 {nextCustomer.name}이 서비스 위치로 이동 중 - 대기");
                }
            }
        }
        
        /// <summary>
        /// 큐에서 null 객체 정리
        /// </summary>
        private void CleanupQueue()
        {
            var tempQueue = new Queue<AIAgent>();
            while (customerQueue.Count > 0)
            {
                var customer = customerQueue.Dequeue();
                if (customer != null)
                {
                    tempQueue.Enqueue(customer);
                }
            }
            customerQueue = tempQueue;
        }
        
        /// <summary>
        /// 주문 시도
        /// </summary>
        public void TryPlaceOrder()
        {
            DebugLog($"TryPlaceOrder 호출됨 - 현재고객: {(currentCustomer != null ? currentCustomer.name : "null")}, 처리중: {isProcessingOrder}");
            
            if (isProcessingOrder) return;
            
            if (currentCustomer == null)
            {
                DebugLog("현재 고객이 없습니다.");
                return;
            }
            
            // 근처 AI 직원 찾기
            AIEmployee targetEmployee = FindNearbyEmployee();
            if (targetEmployee == null)
            {
                DebugLog("근처에 이용 가능한 직원이 없습니다. 고객 대기 중...");
                // 직원이 없으면 현재 고객을 null로 설정 (큐에는 그대로 유지)
                currentCustomer = null;
                return;
            }
            
            DebugLog($"주문 처리 시작 - 고객: {currentCustomer.name}, 직원: {targetEmployee.employeeName}");
            
            // 주문 처리 시작
            StartCoroutine(ProcessOrderCoroutine(targetEmployee));
        }
        
        /// <summary>
        /// 고객이 서비스 위치에 도착했는지 확인
        /// </summary>
        private bool IsCustomerAtServicePosition(AIAgent customer)
        {
            if (customer == null) return false;
            
            // 고객과 서비스 위치(counterFront) 사이의 거리 확인
            float distance = Vector3.Distance(customer.transform.position, counterFront);
            float arrivalThreshold = 4.0f; // 도착 판정 거리 (2m → 4m로 완화)
            
            // 추가 조건: AI가 대기 상태인지도 확인
            bool isWaitingState = customer.IsWaitingAtKitchenCounter;
            bool hasArrived = distance <= arrivalThreshold || isWaitingState;
            
            DebugLog($"위치확인 - 고객: {customer.name}, 고객위치: {customer.transform.position}, 서비스위치: {counterFront}, 거리: {distance:F1}m, 대기상태: {isWaitingState}");
            
            if (hasArrived)
            {
                DebugLog($"고객 {customer.name} 서비스 위치 도착 확인 (거리: {distance:F1}m, 대기상태: {isWaitingState})");
            }
            else
            {
                DebugLog($"고객 {customer.name} 아직 이동 중 (거리: {distance:F1}m, 필요: {arrivalThreshold}m 이내, 대기상태: {isWaitingState})");
            }
            
            return hasArrived;
        }
        
        /// <summary>
        /// 주문 처리 코루틴
        /// </summary>
        private IEnumerator ProcessOrderCoroutine(AIEmployee employee)
        {
            isProcessingOrder = true;
            currentAssignedEmployee = employee;
            
            DebugLog($"주문 처리 시작 - 고객: {(currentCustomer != null ? currentCustomer.name : "Unknown")}, 담당 직원: {(employee != null ? employee.employeeName : "NULL!")}", true);
            
            if (employee == null)
            {
                DebugLog("employee가 null입니다! 주문 처리 중단", true);
                isProcessingOrder = false;
                currentAssignedEmployee = null;
                yield break;
            }
            
            DebugLog($"직원 AI에게 StartOrderProcessing() 호출 - IsHired: {employee.IsHired}, IsWorkTime: {employee.IsWorkTime}, isProcessingOrder: {employee.isProcessingOrder}", true);
            
            // AI 직원에게 주문 전달
            employee.StartOrderProcessing();
            
            DebugLog($"StartOrderProcessing() 호출 완료 - employee.isProcessingOrder: {employee.isProcessingOrder}", true);
            
            // 처리 완료까지 대기 (최대 20초)
            float timeout = 20f;
            float elapsed = 0f;
            
            while (employee != null && employee.isProcessingOrder && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 처리 완료 - 대기열에서 제거
            if (customerQueue.Count > 0 && customerQueue.Peek() == currentCustomer)
            {
                AIAgent completedCustomer = customerQueue.Dequeue();
                DebugLog($"서비스 완료로 AI {completedCustomer.name}을(를) 대기열에서 제거 (남은 대기: {customerQueue.Count}명)");
            }
            else
            {
                DebugLog($"서비스 완료 시 대기열 상태 불일치 - 큐 크기: {customerQueue.Count}, 현재 고객: {(currentCustomer != null ? currentCustomer.name : "null")}");
            }
            
            // 처리 완료
            isProcessingOrder = false;
            currentAssignedEmployee = null;
            
            // AI에게 서비스 완료 알림
            if (currentCustomer != null)
            {
                DebugLog($"AI {currentCustomer.name}에게 서비스 완료 알림 전송");
                currentCustomer.OnKitchenServiceComplete();
            }
            currentCustomer = null;
            
            DebugLog($"서비스 처리 상태 초기화 완료 - 다음 고객 처리 준비");
            
            // 대기열 위치 업데이트
            UpdateQueuePositions();
            
            DebugLog($"주문 처리 완료 - 대기 고객: {customerQueue.Count}명");
        }
        
        /// <summary>
        /// 근처 AI 직원 찾기 (주방당 1명 전담 제도)
        /// </summary>
        private AIEmployee FindNearbyEmployee()
        {
            // 1단계: 이미 배정된 전담 직원이 있고, 사용 가능하면 그 직원 사용
            if (assignedKitchenEmployee != null && 
                assignedKitchenEmployee.IsHired &&  // isHired -> IsHired (프로퍼티)
                assignedKitchenEmployee.IsWorkTime &&
                !assignedKitchenEmployee.isProcessingOrder &&
                assignedKitchenEmployee.CurrentState == AIEmployee.EmployeeState.Working && // 작업 위치에 도착한 직원만!
                assignedKitchenEmployee.workPosition != null) // 작업 위치가 있는지 확인
            {
                DebugLog($"전담 직원 사용: {assignedKitchenEmployee.employeeName}");
                return assignedKitchenEmployee;
            }
            else if (assignedKitchenEmployee != null)
            {
                // 전담 직원이 있지만 사용 불가능한 이유 로그
                if (!assignedKitchenEmployee.IsHired)
                    DebugLog($"전담 직원 {assignedKitchenEmployee.employeeName}이(가) 고용되지 않음");
                else if (!assignedKitchenEmployee.IsWorkTime)
                    DebugLog($"전담 직원 {assignedKitchenEmployee.employeeName}이(가) 근무 시간이 아님");
                else if (assignedKitchenEmployee.isProcessingOrder)
                    DebugLog($"전담 직원 {assignedKitchenEmployee.employeeName}이(가) 주문 처리 중");
                else if (assignedKitchenEmployee.CurrentState != AIEmployee.EmployeeState.Working)
                    DebugLog($"전담 직원 {assignedKitchenEmployee.employeeName}이(가) 작업 위치에 없음 (상태: {assignedKitchenEmployee.CurrentState})");
                else if (assignedKitchenEmployee.workPosition == null)
                    DebugLog($"전담 직원 {assignedKitchenEmployee.employeeName}의 작업 위치가 없음");
            }
            
            // 2단계: 전담 직원이 없거나 사용 불가능하면 새로 배정
            AIEmployee[] allEmployees = FindObjectsByType<AIEmployee>(FindObjectsSortMode.None);
            DebugLog($"주방 직원 검색 시작 - 전체 직원 수: {allEmployees.Length}");
            
            AIEmployee closestEmployee = null;
            float closestDistance = float.MaxValue;
            int validEmployeeCount = 0;
            
            foreach (AIEmployee employee in allEmployees)
            {
                // null 체크 강화
                if (employee == null || employee.gameObject == null) 
                {
                    DebugLog($"null 직원 발견 (스킵)");
                    continue;
                }
                
                // 작업 위치에 도착했고, 주문 처리 가능한 직원만 선택
                if (employee.IsHired &&  // isHired -> IsHired (프로퍼티)
                    employee.IsWorkTime &&
                    !employee.isProcessingOrder &&
                    employee.CurrentState == AIEmployee.EmployeeState.Working && // 작업 위치에 도착한 직원만!
                    employee.workPosition != null) // 작업 위치가 있는지 확인
                {
                    validEmployeeCount++;
                    
                    // 같은 주방에 있는 직원인지 확인
                    if (IsEmployeeInSameKitchen(employee))
                    {
                        // 이미 다른 주방에 배정된 직원인지 확인
                        if (!IsEmployeeAssignedToOtherKitchen(employee))
                        {
                            float distance = Vector3.Distance(transform.position, employee.transform.position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestEmployee = employee;
                            }
                        }
                        else
                        {
                            DebugLog($"직원 {employee.employeeName}는 다른 주방에 배정됨");
                        }
                    }
                    else
                    {
                        DebugLog($"직원 {employee.employeeName}는 다른 주방에 있음");
                    }
                }
            }
            
            if (closestEmployee != null)
            {
                // 이 주방에 전담 직원으로 배정
                assignedKitchenEmployee = closestEmployee;
                DebugLog($"새 전담 직원 배정: {closestEmployee.employeeName} (거리: {closestDistance:F1}m, 사용 가능 직원: {validEmployeeCount}명)", true);
            }
            else
            {
                DebugLog($"사용 가능한 직원 없음 (검색된 유효 직원: {validEmployeeCount}명)", true);
            }
            
            return closestEmployee;
        }
        
        /// <summary>
        /// 직원이 다른 주방에 이미 배정되어 있는지 확인
        /// </summary>
        private bool IsEmployeeAssignedToOtherKitchen(AIEmployee employee)
        {
            KitchenCounter[] allCounters = FindObjectsByType<KitchenCounter>(FindObjectsSortMode.None);
            foreach (var counter in allCounters)
            {
                if (counter != this && counter.assignedKitchenEmployee == employee)
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 직원이 같은 주방에 있는지 확인
        /// </summary>
        private bool IsEmployeeInSameKitchen(AIEmployee employee)
        {
            if (associatedKitchen == null) return true; // 주방이 없으면 모든 직원 허용
            
            return associatedKitchen.ContainsPosition(employee.transform.position);
        }
        
        #endregion
        
        #region 디버그
        
        /// <summary>
        /// 디버그 로그
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            Debug.Log($"[KitchenCounter] {message}");
        }
        
        #endregion
        
        #region 공개 메서드
        
        /// <summary>
        /// 강제 주문 처리 (스크립트에서 호출용)
        /// </summary>
        public void ForceOrder(AIAgent customer = null)
        {
            if (!isProcessingOrder)
            {
                if (customer != null)
                {
                    currentCustomer = customer;
                }
                TryPlaceOrder();
            }
        }
        
        /// <summary>
        /// 카운터 상태 정보
        /// </summary>
        public bool IsProcessingOrder => isProcessingOrder;
        public int QueueCount => customerQueue.Count;
        public AIAgent CurrentCustomer => currentCustomer;
        public AIEmployee CurrentEmployee => currentAssignedEmployee;
        
        #endregion
        
        #region 시각화
        
        /// <summary>
        /// 대기열 시각화 (에디터용)
        /// </summary>
        void OnDrawGizmos()
        {
            // 에디터에서도 대기열 위치를 시각화
            if (!Application.isPlaying)
            {
                counterFront = transform.position + transform.forward * counterServiceDistance;
            }

            // 서비스 위치 표시 (노란색)
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(counterFront, 0.3f);

            // 대기열 위치 표시 (파란색)
            Gizmos.color = Color.blue;
            for (int i = 0; i < maxQueueLength; i++)
            {
                float distance = counterServiceDistance + (i * queueSpacing);
                Vector3 queuePos = transform.position + transform.forward * distance;
                Gizmos.DrawSphere(queuePos, 0.2f);
                
                // 대기열 라인 표시
                if (i < maxQueueLength - 1)
                {
                    float nextDistance = counterServiceDistance + ((i + 1) * queueSpacing);
                    Vector3 nextPos = transform.position + transform.forward * nextDistance;
                    Gizmos.DrawLine(queuePos, nextPos);
                }
            }
            
            // 주방 카운터 상태 텍스트 표시 (Scene 뷰에서만)
            #if UNITY_EDITOR
            if (Application.isPlaying)
            {
                string statusText = $"대기: {customerQueue.Count}/{maxQueueLength}명";
                if (isProcessingOrder)
                {
                    statusText += "\n(주문 처리 중)";
                }
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, statusText);
            }
            #endif
        }
        
        #endregion
    }
}
