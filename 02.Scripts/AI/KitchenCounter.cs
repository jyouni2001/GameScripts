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
        
        // 내부 변수
        private bool isProcessingOrder = false;
        private AIEmployee currentAssignedEmployee;
        private KitchenComponent associatedKitchen;
        private Queue<GameObject> customerQueue = new Queue<GameObject>();
        private GameObject currentCustomer;
        
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
        
        #region 트리거 시스템
        
        /// <summary>
        /// 트리거 진입 - AI 고객이 접근
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            // AI 고객이 카운터에 접근
            if (other.CompareTag("Customer") || other.CompareTag("Player"))
            {
                // 이미 줄에 서 있지 않은 경우에만 추가
                if (!customerQueue.Contains(other.gameObject) && currentCustomer != other.gameObject)
                {
                    customerQueue.Enqueue(other.gameObject);
                    DebugLog($"고객이 줄에 섬: {other.name} (대기: {customerQueue.Count}명)");
                }
            }
        }
        
        /// <summary>
        /// 트리거 퇴장 - AI 고객이 벗어남
        /// </summary>
        private void OnTriggerExit(Collider other)
        {
            // 현재 처리 중인 고객이 아니고, 줄을 벗어나는 경우
            if ((other.CompareTag("Customer") || other.CompareTag("Player")) && 
                currentCustomer != other.gameObject)
            {
                // 큐에서 제거 (LINQ 사용하지 않고 직접 처리)
                var tempQueue = new Queue<GameObject>();
                while (customerQueue.Count > 0)
                {
                    var customer = customerQueue.Dequeue();
                    if (customer != other.gameObject && customer != null)
                    {
                        tempQueue.Enqueue(customer);
                    }
                }
                customerQueue = tempQueue;
                
                DebugLog($"고객이 줄에서 벗어남: {other.name} (대기: {customerQueue.Count}명)");
            }
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
                currentCustomer = customerQueue.Dequeue();
                if (currentCustomer != null)
                {
                    DebugLog($"다음 고객 처리 시작: {currentCustomer.name}");
                    TryPlaceOrder();
                }
            }
        }
        
        /// <summary>
        /// 큐에서 null 객체 정리
        /// </summary>
        private void CleanupQueue()
        {
            var tempQueue = new Queue<GameObject>();
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
            if (isProcessingOrder) return;
            
            // 근처 AI 직원 찾기
            AIEmployee targetEmployee = FindNearbyEmployee();
            if (targetEmployee == null)
            {
                DebugLog("❌ 근처에 이용 가능한 직원이 없습니다. 고객 대기 중...");
                // 직원이 없으면 현재 고객을 다시 큐에 추가
                if (currentCustomer != null)
                {
                    customerQueue.Enqueue(currentCustomer);
                    currentCustomer = null;
                }
                return;
            }
            
            // 주문 처리 시작
            StartCoroutine(ProcessOrderCoroutine(targetEmployee));
        }
        
        /// <summary>
        /// 주문 처리 코루틴
        /// </summary>
        private IEnumerator ProcessOrderCoroutine(AIEmployee employee)
        {
            isProcessingOrder = true;
            currentAssignedEmployee = employee;
            
            DebugLog($"🍽️ 주문 처리 시작 - 고객: {(currentCustomer != null ? currentCustomer.name : "Unknown")}, 담당 직원: {employee.employeeName}");
            
            // AI 직원에게 주문 전달
            employee.StartOrderProcessing();
            
            // 처리 완료까지 대기 (최대 20초)
            float timeout = 20f;
            float elapsed = 0f;
            
            while (employee != null && employee.isProcessingOrder && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 처리 완료
            isProcessingOrder = false;
            currentAssignedEmployee = null;
            currentCustomer = null; // 현재 고객 처리 완료
            
            DebugLog($"✅ 주문 처리 완료 - 대기 고객: {customerQueue.Count}명");
        }
        
        /// <summary>
        /// 근처 AI 직원 찾기
        /// </summary>
        private AIEmployee FindNearbyEmployee()
        {
            // 모든 AI 직원 검색 (간단하게)
            AIEmployee[] allEmployees = FindObjectsByType<AIEmployee>(FindObjectsSortMode.None);
            AIEmployee closestEmployee = null;
            float closestDistance = float.MaxValue;
            
            foreach (AIEmployee employee in allEmployees)
            {
                if (employee != null && employee.isHired && !employee.isProcessingOrder)
                {
                    // 같은 주방에 있는 직원인지 확인
                    if (IsEmployeeInSameKitchen(employee))
                    {
                        float distance = Vector3.Distance(transform.position, employee.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestEmployee = employee;
                        }
                    }
                }
            }
            
            if (closestEmployee != null)
            {
                DebugLog($"담당 직원 발견: {closestEmployee.employeeName} (거리: {closestDistance:F1}m)");
            }
            
            return closestEmployee;
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
        private void DebugLog(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[KitchenCounter] {message}");
            }
        }
        
        #endregion
        
        #region 공개 메서드
        
        /// <summary>
        /// 강제 주문 처리 (스크립트에서 호출용)
        /// </summary>
        public void ForceOrder(GameObject customer = null)
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
        public GameObject CurrentCustomer => currentCustomer;
        public AIEmployee CurrentEmployee => currentAssignedEmployee;
        
        #endregion
    }
}
