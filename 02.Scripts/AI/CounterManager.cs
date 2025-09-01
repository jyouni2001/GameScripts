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

    // 통합 대기열 - 방 배정과 방 사용완료 보고를 모두 처리
    private Queue<AIAgent> waitingQueue = new Queue<AIAgent>();
    private AIAgent currentServingAgent = null;
    private Vector3 counterFront;
    private Transform counterTransform;
    private bool isProcessingService = false;

    void Start()
    {
        counterTransform = transform;
        // 카운터 정면 위치 계산 (카운터의 forward 방향으로 2유닛)
        counterFront = counterTransform.position + counterTransform.forward * counterServiceDistance;
    }

    // 대기열에 합류 요청 (방 배정/방 사용완료 보고 모두 동일 대기열 사용)
    public bool TryJoinQueue(AIAgent agent)
    {
        Debug.Log($"[CounterManager] 대기열 진입 요청 - AI: {agent.gameObject.name}, 현재 대기 인원: {waitingQueue.Count}/{maxQueueLength}");
        
        if (waitingQueue.Count >= maxQueueLength)
        {
            Debug.Log($"[CounterManager] 대기열이 가득 찼습니다. (현재 {waitingQueue.Count}명, 최대 {maxQueueLength}명)");
            return false;
        }

        waitingQueue.Enqueue(agent);
        UpdateQueuePositions();
        Debug.Log($"[CounterManager] AI {agent.gameObject.name}이(가) 대기열에 합류했습니다. (대기 인원: {waitingQueue.Count}명)");
        return true;
    }

    // AI가 대기열에서 나가기 요청
    public void LeaveQueue(AIAgent agent)
    {
        Debug.Log($"[CounterManager] 대기열 나가기 요청 - AI: {agent.gameObject.name}");
        
        if (currentServingAgent == agent)
        {
            Debug.Log($"[CounterManager] 현재 서비스 중인 AI {agent.gameObject.name} 서비스 중단");
            currentServingAgent = null;
            isProcessingService = false;
        }

        RemoveFromQueue(waitingQueue, agent);
        UpdateQueuePositions();
        Debug.Log($"[CounterManager] AI {agent.gameObject.name}이(가) 대기열에서 나갔습니다. (남은 인원: {waitingQueue.Count}명)");
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
                Debug.Log($"[CounterManager] 대기열에서 AI {agent.gameObject.name} 제거됨");
            }
        }
        while (tempQueue.Count > 0)
        {
            queue.Enqueue(tempQueue.Dequeue());
        }
        
        if (!removed)
        {
            Debug.LogWarning($"[CounterManager] AI {agent.gameObject.name}이(가) 대기열에 없어서 제거할 수 없습니다.");
        }
    }

    // 대기열 위치 업데이트
    private void UpdateQueuePositions()
    {
        int index = 0;
        foreach (var agent in waitingQueue)
        {
            if (agent != null)
            {
                if (agent == currentServingAgent)
                {
                    agent.SetQueueDestination(counterFront);
                }
                else
                {
                    float distance = counterServiceDistance + (index * queueSpacing);
                    Vector3 queuePosition = transform.position + counterTransform.forward * distance;
                    agent.SetQueueDestination(queuePosition);
                }
                index++;
            }
        }
    }

    // 현재 서비스 받을 수 있는지 확인
    public bool CanReceiveService(AIAgent agent)
    {
        bool canReceive = waitingQueue.Count > 0 && waitingQueue.Peek() == agent && !isProcessingService;
        Debug.Log($"[CounterManager] 서비스 가능 확인 - AI: {agent.gameObject.name}, 결과: {canReceive}, 대기열 첫번째: {(waitingQueue.Count > 0 ? waitingQueue.Peek().gameObject.name : "없음")}, 처리 중: {isProcessingService}");
        return canReceive;
    }

    // 서비스 시작
    public void StartService(AIAgent agent)
    {
        Debug.Log($"[CounterManager] StartService 호출 - AI: {agent.gameObject.name}");
        
        if (CanReceiveService(agent))
        {
            currentServingAgent = agent;
            isProcessingService = true;
            agent.SetQueueDestination(counterFront);
            UpdateQueuePositions();
            StartCoroutine(ServiceCoroutine(agent));
            Debug.Log($"[CounterManager] AI {agent.gameObject.name} 서비스가 시작되었습니다.");
        }
        else
        {
            Debug.LogWarning($"[CounterManager] AI {agent.gameObject.name}은(는) 서비스를 받을 수 없습니다.");
        }
    }

    // 서비스 처리 코루틴
    private IEnumerator ServiceCoroutine(AIAgent agent)
    {
        Debug.Log($"[CounterManager] 서비스 시작 - AI: {agent.gameObject.name}, 서비스 시간: {serviceTime}초");
        yield return new WaitForSeconds(serviceTime);
        
        Debug.Log($"[CounterManager] 서비스 시간 완료 - AI: {agent.gameObject.name}");
        
        if (currentServingAgent == agent)
        {
            // 대기열에서 제거
            if (waitingQueue.Count > 0 && waitingQueue.Peek() == agent)
            {
                waitingQueue.Dequeue();
                Debug.Log($"[CounterManager] 서비스 완료로 AI {agent.gameObject.name}을(를) 대기열에서 제거");
            }
            else
            {
                Debug.LogWarning($"[CounterManager] 서비스 완료했지만 AI {agent.gameObject.name}이(가) 대기열 첫 번째가 아님");
            }

            currentServingAgent = null;
            isProcessingService = false;
            UpdateQueuePositions();
            agent.OnServiceComplete();
            Debug.Log($"[CounterManager] AI {agent.gameObject.name} 서비스가 완료되었습니다. (남은 대기 인원: {waitingQueue.Count}명)");
        }
        else
        {
            Debug.LogWarning($"[CounterManager] 서비스 완료 시 currentServingAgent가 다른 AI입니다. 현재: {currentServingAgent?.gameObject.name}, 완료된 AI: {agent.gameObject.name}");
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
        Debug.Log($"[CounterManager] 강제 대기열 정리 시작 - 정리 전: {waitingQueue.Count}명");
        
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
                        Debug.Log($"[CounterManager] 비활성화된 AI {agent.gameObject.name} 대기열에서 제거");
                    }
                }
                catch
                {
                    Debug.Log($"[CounterManager] 파괴된 AI 참조 대기열에서 제거");
                }
            }
            else
            {
                Debug.Log($"[CounterManager] null AI 참조 대기열에서 제거");
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
                    Debug.Log($"[CounterManager] 서비스 중인 AI가 비활성화됨, 서비스 중단");
                    currentServingAgent = null;
                    isProcessingService = false;
                }
            }
            catch
            {
                Debug.Log($"[CounterManager] 서비스 중인 AI 참조가 파괴됨, 서비스 중단");
                currentServingAgent = null;
                isProcessingService = false;
            }
        }
        
        UpdateQueuePositions();
        Debug.Log($"[CounterManager] 강제 대기열 정리 완료 - 정리 후: {waitingQueue.Count}명 (제거됨: {originalCount - waitingQueue.Count}명)");
    }

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
        }
    }
}