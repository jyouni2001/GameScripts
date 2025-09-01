using UnityEngine;

/// <summary>
/// 기존 게임 시스템과 통계 시스템을 연동하는 어댑터 클래스
/// </summary>
public class StatisticsSystemAdapter : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private bool autoConnect = true;
    
    private bool isConnected = false;

    private void Start()
    {
        if (autoConnect)
        {
            ConnectToGameSystems();
        }
    }

    /// <summary>
    /// 게임 시스템들과 연결을 설정합니다.
    /// </summary>
    public void ConnectToGameSystems()
    {
        if (isConnected)
        {
            Debug.LogWarning("이미 게임 시스템과 연결되어 있습니다.");
            return;
        }

        StartCoroutine(ConnectToSystemsCoroutine());
    }

    private System.Collections.IEnumerator ConnectToSystemsCoroutine()
    {
        // 모든 시스템이 초기화될 때까지 대기
        yield return new WaitUntil(() => AreAllSystemsReady());

        // PlayerWallet 연결
        ConnectToPlayerWallet();
        
        // TimeSystem 연결
        ConnectToTimeSystem();
        
        // ReputationSystem 연결
        ConnectToReputationSystem();
        
        // PaymentSystem 연결
        ConnectToPaymentSystem();

        isConnected = true;
        Debug.Log("통계 시스템이 게임 시스템과 연결되었습니다.");
    }

    private bool AreAllSystemsReady()
    {
        bool ready = PlayerWallet.Instance != null &&
                    TimeSystem.Instance != null &&
                    ReputationSystem.Instance != null &&
                    PaymentSystem.Instance != null &&
                    GameStatisticsData.Instance != null;

        if (!ready)
        {
            Debug.Log($"시스템 준비 상태: PlayerWallet: {PlayerWallet.Instance != null}, " +
                     $"TimeSystem: {TimeSystem.Instance != null}, " +
                     $"ReputationSystem: {ReputationSystem.Instance != null}, " +
                     $"PaymentSystem: {PaymentSystem.Instance != null}, " +
                     $"GameStatisticsData: {GameStatisticsData.Instance != null}");
        }

        return ready;
    }

    private void ConnectToPlayerWallet()
    {
        if (PlayerWallet.Instance != null)
        {
            // PlayerWallet의 돈 변경 이벤트 구독
            // 실제 구현에서는 PlayerWallet에 이벤트를 추가해야 함
            Debug.Log("PlayerWallet과 연결되었습니다.");
        }
    }

    private void ConnectToTimeSystem()
    {
        if (TimeSystem.Instance != null)
        {
            // TimeSystem의 날짜 변경 이벤트 구독
            // 실제 구현에서는 TimeSystem에 이벤트를 추가해야 함
            Debug.Log("TimeSystem과 연결되었습니다.");
        }
    }

    private void ConnectToReputationSystem()
    {
        if (ReputationSystem.Instance != null)
        {
            // ReputationSystem의 명성도 변경 이벤트 구독
            // 실제 구현에서는 ReputationSystem에 이벤트를 추가해야 함
            Debug.Log("ReputationSystem과 연결되었습니다.");
        }
    }

    private void ConnectToPaymentSystem()
    {
        if (PaymentSystem.Instance != null)
        {
            // PaymentSystem의 결제 완료 이벤트 구독
            // 실제 구현에서는 PaymentSystem에 이벤트를 추가해야 함
            Debug.Log("PaymentSystem과 연결되었습니다.");
        }
    }

    /// <summary>
    /// 스폰 인원수 증가 이벤트를 처리합니다.
    /// </summary>
    public void OnSpawnCountIncreased(int count = 1)
    {
        if (GameStatisticsData.Instance != null)
        {
            GameStatisticsData.Instance.AddSpawnCount(count);
        }
    }

    /// <summary>
    /// 골드 획득 이벤트를 처리합니다.
    /// </summary>
    public void OnGoldEarned(int amount)
    {
        if (GameStatisticsData.Instance != null)
        {
            GameStatisticsData.Instance.AddGoldEarned(amount);
        }
    }

    /// <summary>
    /// 명성도 증가 이벤트를 처리합니다.
    /// </summary>
    public void OnReputationGained(int amount)
    {
        if (GameStatisticsData.Instance != null)
        {
            GameStatisticsData.Instance.AddReputationGained(amount);
        }
    }

    /// <summary>
    /// 새로운 날 시작 이벤트를 처리합니다.
    /// </summary>
    public void OnNewDay(int dayNumber)
    {
        if (GameStatisticsData.Instance != null)
        {
            GameStatisticsData.Instance.OnNewDay(dayNumber);
        }
    }

    /// <summary>
    /// 게임 시스템과의 연결을 해제합니다.
    /// </summary>
    public void DisconnectFromGameSystems()
    {
        if (!isConnected)
        {
            Debug.LogWarning("게임 시스템과 연결되어 있지 않습니다.");
            return;
        }

        // 이벤트 구독 해제 로직
        // 실제 구현에서는 각 시스템의 이벤트에서 구독 해제해야 함

        isConnected = false;
        Debug.Log("게임 시스템과의 연결이 해제되었습니다.");
    }

    private void OnDestroy()
    {
        DisconnectFromGameSystems();
    }

    /// <summary>
    /// 연결 상태를 확인합니다.
    /// </summary>
    public bool IsConnected()
    {
        return isConnected;
    }

    /// <summary>
    /// 수동으로 통계 데이터를 업데이트합니다.
    /// </summary>
    public void ManualUpdate()
    {
        if (GameStatisticsData.Instance != null)
        {
            // 현재 게임 상태에서 통계 데이터 업데이트
            UpdateCurrentDayStats();
        }
    }

    private void UpdateCurrentDayStats()
    {
        // PlayerWallet에서 현재 골드 상태 확인
        if (PlayerWallet.Instance != null)
        {
            // 이전 골드와 비교하여 증가량 계산
            // 실제 구현에서는 이전 값과 비교하는 로직이 필요
        }

        // ReputationSystem에서 현재 명성도 상태 확인
        if (ReputationSystem.Instance != null)
        {
            // 이전 명성도와 비교하여 증가량 계산
            // 실제 구현에서는 이전 값과 비교하는 로직이 필요
        }
    }
}
