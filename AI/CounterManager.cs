using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using JY;

namespace JY
{
public class CounterManager : MonoBehaviour
{
    [Header("Queue Settings")]
    public float queueSpacing = 2f;           // AI 간격
    public float counterServiceDistance = 2f;  // 카운터와 서비스 받는 위치 사이의 거리
    public int maxQueueLength = 10;           // 최대 대기열 길이
    public float serviceTime = 5f;            // 서비스 처리 시간

    [Header("Employee Management")]
    [Tooltip("카운터 운영에 필요한 직원 (리셉션 직원)")]
    [SerializeField] private bool requiresEmployee = true;
    [Tooltip("현재 배정된 직원")]
    [SerializeField] private AIEmployee assignedEmployee = null;
    
    //[Tooltip("직원이 없을 때 표시할 메시지")]
    //[SerializeField] private string noEmployeeMessage = "직원을 고용해주세요";

    // 통합 대기열 - 방 배정과 방 사용완료 보고를 모두 처리
    private Queue<AIAgent> waitingQueue = new Queue<AIAgent>();
    private AIAgent currentServingAgent = null;
    private Vector3 counterFront;
    private Transform counterTransform;
    private bool isProcessingService = false;
    private bool isCounterActive = false;  // 카운터 활성화 상태

    void Start()
    {
        counterTransform = transform;
        // 카운터 정면 위치 계산 (카운터의 forward 방향으로 2유닛)
        counterFront = counterTransform.position + counterTransform.forward * counterServiceDistance;
        
        // 초기 상태 설정
        UpdateCounterStatus();
        
        // 직원 고용 시스템 이벤트 구독
        if (EmployeeHiringSystem.Instance != null)
        {
            EmployeeHiringSystem.OnEmployeeHired += OnEmployeeHired;
            EmployeeHiringSystem.OnEmployeeFired += OnEmployeeFired;
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (EmployeeHiringSystem.Instance != null)
        {
            EmployeeHiringSystem.OnEmployeeHired -= OnEmployeeHired;
            EmployeeHiringSystem.OnEmployeeFired -= OnEmployeeFired;
        }
    }

    // 대기열에 합류 요청 (방 배정/방 사용완료 보고 모두 동일 대기열 사용)
    public bool TryJoinQueue(AIAgent agent)
    {
        // 직원이 필요한데 배정된 직원이 없으면 대기열 진입 거부
        if (requiresEmployee && !isCounterActive)
        {
            Debug.Log($"[CounterManager] AI {agent.name}: 대기열 진입 실패 - 카운터 비활성화 (직원 없음)");
            return false;
        }
        
        if (waitingQueue.Count >= maxQueueLength)
        {
            Debug.Log($"[CounterManager] AI {agent.name}: 대기열 진입 실패 - 대기열 가득참 ({waitingQueue.Count}/{maxQueueLength})");
            return false;
        }

        waitingQueue.Enqueue(agent);
        Debug.Log($"[CounterManager] AI {agent.name}: 대기열 진입 성공 - 현재 대기열 수: {waitingQueue.Count}, 위치: {waitingQueue.Count}번째");
        UpdateQueuePositions();
        return true;
    }

    // AI가 대기열에서 나가기 요청
    public void LeaveQueue(AIAgent agent)
    {
        if (currentServingAgent == agent)
        {
            Debug.Log($"[CounterManager] AI {agent.name}: 서비스 중인 AI가 대기열에서 나감");
            currentServingAgent = null;
            isProcessingService = false;
        }

        RemoveFromQueue(waitingQueue, agent);
        Debug.Log($"[CounterManager] AI {agent.name}: 대기열에서 나감 - 남은 대기열 수: {waitingQueue.Count}");
        UpdateQueuePositions();
    }

    private void RemoveFromQueue(Queue<AIAgent> queue, AIAgent agent)
    {
        int originalCount = queue.Count;
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
            }
        }
        while (tempQueue.Count > 0)
        {
            queue.Enqueue(tempQueue.Dequeue());
        }
        
        if (!removed)
        {
        }
    }

    // 대기열 위치 업데이트
    private void UpdateQueuePositions()
    {
        int index = 0;
        // 카운터를 바라보는 방향 계산 (카운터의 반대 방향)
        Quaternion faceCounterRotation = Quaternion.LookRotation(-counterTransform.forward);
        
        foreach (var agent in waitingQueue)
        {
            if (agent != null)
            {
                if (agent == currentServingAgent)
                {
                    agent.SetQueueDestination(counterFront, faceCounterRotation);
                }
                else
                {
                    float distance = counterServiceDistance + (index * queueSpacing);
                    Vector3 queuePosition = transform.position + counterTransform.forward * distance;
                    agent.SetQueueDestination(queuePosition, faceCounterRotation);
                }
                index++;
            }
        }
    }

    // 현재 서비스 받을 수 있는지 확인
    public bool CanReceiveService(AIAgent agent)
    {
        bool canReceive = waitingQueue.Count > 0 && waitingQueue.Peek() == agent && !isProcessingService;
        
        if (canReceive)
        {
            Debug.Log($"[CounterManager] AI {agent.name}: 서비스 받을 수 있음 (대기열 첫 번째, 서비스 처리 중 아님)");
        }
        
        return canReceive;
    }

    // 서비스 시작
    public void StartService(AIAgent agent)
    {
        if (CanReceiveService(agent))
        {
            currentServingAgent = agent;
            isProcessingService = true;
            agent.SetQueueDestination(counterFront);
            Debug.Log($"[CounterManager] AI {agent.name}: 서비스 시작 (처리 시간: {serviceTime}초)");
            UpdateQueuePositions();
            StartCoroutine(ServiceCoroutine(agent));
        }
        else
        {
            Debug.LogWarning($"[CounterManager] AI {agent.name}: 서비스 시작 실패 - CanReceiveService가 false");
        }
    }

    // 서비스 처리 코루틴
    private IEnumerator ServiceCoroutine(AIAgent agent)
    {
        yield return new WaitForSeconds(serviceTime);
        
        if (currentServingAgent == agent)
        {
            // 대기열에서 제거
            if (waitingQueue.Count > 0 && waitingQueue.Peek() == agent)
            {
                waitingQueue.Dequeue();
                Debug.Log($"[CounterManager] AI {agent.name}: 서비스 완료 - 대기열에서 제거됨, 남은 대기열: {waitingQueue.Count}");
            }
            else
            {
                Debug.LogWarning($"[CounterManager] AI {agent.name}: 서비스 완료했지만 대기열 첫 번째가 아님 (대기열 수: {waitingQueue.Count})");
            }

            currentServingAgent = null;
            isProcessingService = false;
            UpdateQueuePositions();
            Debug.Log($"[CounterManager] AI {agent.name}: OnServiceComplete() 호출");
            agent.OnServiceComplete();
        }
        else
        {
            Debug.LogWarning($"[CounterManager] 서비스 완료 시 currentServingAgent가 {agent.name}이 아님");
        }
    }

    // 대기열 위치 얻기
    public Vector3 GetCounterServicePosition()
    {
        return counterFront;
    }

    /// <summary>
    /// 대기열을 완전히 정리합니다 (17시 강제 디스폰 등에 사용)
    /// </summary>
    public void ForceCleanupQueue()
    {
        int originalCount = waitingQueue.Count;
        var cleanQueue = new Queue<AIAgent>();
        
        while (waitingQueue.Count > 0)
        {
            var agent = waitingQueue.Dequeue();
            if (agent != null && agent.gameObject != null)
            {
                // AI가 실제로 유효한지 확인
                try
                {
                    // GameObject가 파괴되었는지 확인
                    if (agent.gameObject.activeInHierarchy)
                    {
                        cleanQueue.Enqueue(agent);
                    }
                    else
                    {
                    }
                }
                catch
                {
                }
            }
            else
            {
            }
        }
        
        waitingQueue = cleanQueue;
        
        // 서비스 중인 AI도 정리
        if (currentServingAgent != null)
        {
            try
            {
                if (currentServingAgent.gameObject == null || !currentServingAgent.gameObject.activeInHierarchy)
                {
                    currentServingAgent = null;
                    isProcessingService = false;
                }
            }
            catch
            {
                currentServingAgent = null;
                isProcessingService = false;
            }
        }
        
        UpdateQueuePositions();
    }

    void OnDrawGizmos()
    {
        // 에디터에서도 대기열 위치를 시각화
        if (!Application.isPlaying)
        {
            counterFront = transform.position + transform.forward * counterServiceDistance;
        }

        // 카운터 상태에 따른 색상 설정
        Color counterColor = isCounterActive ? Color.green : Color.red;
        
        // 서비스 위치 표시 (활성화 상태에 따라 색상 변경)
        Gizmos.color = counterColor;
        Gizmos.DrawSphere(counterFront, 0.3f);

        // 대기열 위치 표시 (파란색 - 활성화 상태와 무관)
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
        
        // 카운터 상태 텍스트 표시 (Scene 뷰에서만)
        #if UNITY_EDITOR
        if (Application.isPlaying)
        {
            string statusText = isCounterActive ? "활성화" : "비활성화";
            if (!isCounterActive && requiresEmployee)
            {
                statusText += "\n(직원 필요)";
            }
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, statusText);
        }
        #endif
    }

    #region 직원 관리

    /// <summary>
    /// 직원 고용 이벤트 핸들러
    /// </summary>
    private void OnEmployeeHired(AIEmployee employee)
    {
        // 이 카운터에 배정된 직원인지 확인
        if (IsEmployeeAssignedToThisCounter(employee))
        {
            assignedEmployee = employee;
            UpdateCounterStatus();
        }
    }

    /// <summary>
    /// 직원 해고 이벤트 핸들러
    /// </summary>
    private void OnEmployeeFired(AIEmployee employee)
    {
        if (assignedEmployee == employee)
        {
            assignedEmployee = null;
            UpdateCounterStatus();
            
            // 대기 중인 AI들을 모두 제거
            ClearAllQueues();
        }
    }

    /// <summary>
    /// 직원이 이 카운터에 배정되었는지 확인
    /// </summary>
    private bool IsEmployeeAssignedToThisCounter(AIEmployee employee)
    {
        if (employee == null || employee.workPosition == null) return false;
        
        // 작업 위치가 이 카운터 근처에 있는지 확인
        float distance = Vector3.Distance(transform.position, employee.workPosition.position);
        return distance <= counterServiceDistance * 2f; // 카운터 서비스 거리의 2배 이내
    }

    /// <summary>
    /// 카운터 상태 업데이트
    /// </summary>
    private void UpdateCounterStatus()
    {
        bool wasActive = isCounterActive;
        
        if (requiresEmployee)
        {
            // ✅ 직원이 필요한 경우 - 배정된 직원이 있고, 고용되었고, 근무시간이며, 작업 위치에 도착했는지 확인
            isCounterActive = assignedEmployee != null && 
                              assignedEmployee.IsHired && 
                              assignedEmployee.IsWorkTime;
        }
        else
        {
            // 직원이 필요없는 경우 - 항상 활성화
            isCounterActive = true;
        }
        
        // 상태 변경 시 로그
        if (wasActive != isCounterActive)
        {
            if (isCounterActive)
            {
            }
            else
            {
            }
        }
    }

    /// <summary>
    /// 모든 대기열 정리
    /// </summary>
    private void ClearAllQueues()
    {
        // 대기 중인 모든 AI에게 대기열에서 나가라고 알림
        while (waitingQueue.Count > 0)
        {
            var agent = waitingQueue.Dequeue();
            if (agent != null)
            {
                agent.OnServiceComplete(); // 서비스 완료로 처리하여 다른 행동으로 전환
            }
        }
        
        // 현재 서비스 중인 AI도 정리
        if (currentServingAgent != null)
        {
            currentServingAgent.OnServiceComplete();
            currentServingAgent = null;
        }
        
        isProcessingService = false;
    }

    /// <summary>
    /// 카운터 활성화 상태 확인 (외부 접근용)
    /// </summary>
    public bool IsCounterActive => isCounterActive;

    /// <summary>
    /// 배정된 직원 확인 (외부 접근용)
    /// </summary>
    public AIEmployee AssignedEmployee => assignedEmployee;

    /// <summary>
    /// 직원 수동 배정 (디버그/테스트용)
    /// </summary>
    public void AssignEmployee(AIEmployee employee)
    {
        if (employee != null && employee.IsHired)
        {
            assignedEmployee = employee;
            UpdateCounterStatus();
        }
    }

    /// <summary>
    /// 직원 배정 해제
    /// </summary>
    public void UnassignEmployee()
    {
        if (assignedEmployee != null)
        {
            assignedEmployee = null;
            UpdateCounterStatus();
            ClearAllQueues();
        }
    }

    #endregion
}
}