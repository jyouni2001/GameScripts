using UnityEngine;

namespace JY
{
/// <summary>
/// 식당 의자 포인트 관리 스크립트
/// 각 의자마다 부착되며, 의자 앞에 있는 테이블(책상)과 연결됩니다.
/// </summary>
public class ChairPoint : MonoBehaviour
{
    [Header("테이블 설정")]
    [Tooltip("이 의자 앞에 배치될 테이블 GameObject")]
    [SerializeField] private GameObject tableObject;
    
    [Header("디버그 설정")]
    [SerializeField] private bool showDebugLogs = false;
    
    /// <summary>
    /// 현재 의자를 사용 중인 AI
    /// </summary>
    private AIAgent currentUser = null;
    
    /// <summary>
    /// 의자가 현재 사용 중인지 확인
    /// </summary>
    public bool IsOccupied => currentUser != null;
    
    /// <summary>
    /// 현재 사용자 반환
    /// </summary>
    public AIAgent CurrentUser => currentUser;

    private void Awake()
    {
        // 테이블이 설정되어 있으면 초기에는 비활성화
        if (tableObject != null)
        {
            tableObject.SetActive(false);
            DebugLog($"ChairPoint 초기화 완료 - 테이블 비활성화: {tableObject.name}");
        }
        else
        {
            DebugLog($"테이블이 설정되지 않았습니다: {gameObject.name}");
        }
    }

    /// <summary>
    /// AI가 의자에 앉을 때 호출 (예약 및 점유)
    /// 테이블을 활성화합니다.
    /// </summary>
    /// <param name="agent">앉는 AI</param>
    /// <param name="activateTable">테이블을 활성화할지 여부 (기본값: true)</param>
    public void OccupyChair(AIAgent agent, bool activateTable = true)
    {
        if (currentUser != null && currentUser != agent)
        {
            Debug.LogWarning($"[ChairPoint] 이미 다른 AI가 사용 중입니다: {currentUser.name}");
            return;
        }
        
        // 처음 점유하는 경우
        if (currentUser == null)
        {
            currentUser = agent;
            DebugLog($"의자 점유: {agent.name}이(가) 의자를 예약함");
        }
        
        // 테이블 활성화 (activateTable이 true일 때만)
        if (activateTable && tableObject != null)
        {
            tableObject.SetActive(true);
            DebugLog($"테이블 활성화: {agent.name}이(가) 의자에 앉음 - 테이블: {tableObject.name}");
        }
    }
    
    /// <summary>
    /// AI가 의자에서 일어날 때 호출
    /// 테이블을 비활성화합니다.
    /// </summary>
    /// <param name="agent">일어나는 AI</param>
    public void ReleaseChair(AIAgent agent)
    {
        if (currentUser != agent)
        {
            Debug.LogWarning($"[ChairPoint] 잘못된 해제 요청: {agent.name} (현재 사용자: {currentUser?.name ?? "없음"})");
            return;
        }
        
        // 테이블 비활성화
        if (tableObject != null)
        {
            tableObject.SetActive(false);
            DebugLog($"테이블 비활성화: {agent.name}이(가) 의자에서 일어남 - 테이블: {tableObject.name}");
        }
        
        currentUser = null;
    }
    
    /// <summary>
    /// 강제로 의자를 비웁니다 (AI가 삭제되거나 예외 상황 발생 시)
    /// </summary>
    public void ForceRelease()
    {
        if (currentUser != null)
        {
            DebugLog($"강제 해제: {currentUser.name}");
        }
        
        if (tableObject != null)
        {
            tableObject.SetActive(false);
        }
        
        currentUser = null;
    }
    
    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ChairPoint - {gameObject.name}] {message}");
        }
    }
    
    private void OnDestroy()
    {
        // 의자가 삭제될 때 테이블도 정리
        if (tableObject != null)
        {
            tableObject.SetActive(false);
        }
        
        // 현재 사용 중인 AI에게 알림 (필요시)
        if (currentUser != null)
        {
            DebugLog($"의자가 삭제됨 - 현재 사용자: {currentUser.name}");
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 Gizmo 표시 (의자와 테이블 연결선)
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (tableObject != null)
        {
            // 의자에서 테이블로 선 그리기
            Gizmos.color = IsOccupied ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, tableObject.transform.position);
            
            // 테이블 위치에 구 그리기
            Gizmos.DrawWireSphere(tableObject.transform.position, 0.3f);
        }
    }
#endif
}
}