using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 하루 통계 시스템의 핵심 매니저 클래스
/// 싱글톤 패턴으로 구현되어 전역에서 접근 가능하며,
/// 시간별 데이터 수집, 저장/로드, 이벤트 관리를 담당합니다.
/// </summary>
[DefaultExecutionOrder(-100)] // 다른 시스템보다 먼저 실행
public class DailyStatisticsManager : MonoBehaviour
{
    [Header("통계 설정")]
    [Tooltip("데이터 수집 활성화 여부")]
    [SerializeField] private bool enableDataCollection = true;
    
    [Tooltip("자동 저장 활성화 여부")]
    [SerializeField] private bool enableAutoSave = true;
    
    [Tooltip("저장 간격 (초)")]
    [SerializeField] private float saveInterval = 300f; // 5분마다 저장
    
    [Tooltip("최대 보관 일수")]
    [SerializeField] private int maxStorageDays = 7;
    
    [Header("디버그 설정")]
    [Tooltip("디버그 로그 표시 여부")]
    [SerializeField] private bool showDebugLogs = true;
    
    [Tooltip("중요한 이벤트만 로그 표시")]
    [SerializeField] private bool showImportantLogsOnly = false;
    
    [Header("파일 설정")]
    [Tooltip("저장 파일명")]
    [SerializeField] private string fileName = "DailyStatistics.json";
    
    // 싱글톤 인스턴스
    public static DailyStatisticsManager Instance { get; private set; }
    
    // 통계 데이터
    private StatisticsContainer statisticsContainer;
    private DailyStatistics currentDayStatistics;
    
    // 현재 값 추적
    private int currentReputation;
    private int currentGold;
    private int totalVisitorsToday;
    private int currentActiveVisitors; // 현재 활성 방문객 수
    
    // 시작 값 저장
    private int startingReputation;
    private int startingGold;
    private int lastActiveVisitorCount; // 이전 활성 방문객 수 (차이 계산용)
    
    // 코루틴 참조
    private Coroutine saveCoroutine;
    private Coroutine dataCollectionCoroutine;
    
    // 이벤트
    public static event Action<DailyStatistics> OnStatisticsUpdated;
    public static event Action<DailyData> OnDailyDataUpdated;
    public static event Action OnDayReset;
    
    // 파일 경로
    private string filePath;
    
    /// <summary>
    /// 현재 일의 통계 데이터 (읽기 전용)
    /// </summary>
    public DailyStatistics CurrentDayStatistics => currentDayStatistics;
    
    /// <summary>
    /// 통계 컨테이너 (읽기 전용)
    /// </summary>
    public StatisticsContainer StatisticsContainer => statisticsContainer;
    
    /// <summary>
    /// 데이터 수집 활성화 여부
    /// </summary>
    public bool IsDataCollectionEnabled => enableDataCollection;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        InitializeSingleton();
        InitializeData();
    }
    
    private void Start()
    {
        SetupEventSubscriptions();
        LoadStatistics();
        StartDataCollection();
        
        // 현재 날의 통계 초기화 (OnDayChanged가 호출되지 않을 경우를 대비)
        if (currentDayStatistics == null)
        {
            int currentDay = JY.TimeSystem.Instance != null ? JY.TimeSystem.Instance.CurrentDay : 1;
            currentDayStatistics = statisticsContainer.GetOrCreateDailyStatistics(currentDay);
            UpdateCurrentValues();
            startingReputation = currentReputation;
            startingGold = currentGold;
            totalVisitorsToday = 0;
            currentActiveVisitors = 0;
            lastActiveVisitorCount = 0;
            DebugLog($"현재 날 통계 초기화: {currentDay}일차", true);
        }
        
        if (enableAutoSave)
        {
            StartAutoSave();
        }
        
        DebugLog("DailyStatisticsManager 초기화 완료", true);
    }
    
    private void OnDestroy()
    {
        UnsubscribeEvents();
        StopAllCoroutines();
        
        if (enableAutoSave)
        {
            SaveStatistics();
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && enableAutoSave)
        {
            SaveStatistics();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && enableAutoSave)
        {
            SaveStatistics();
        }
    }
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// 싱글톤 초기화
    /// </summary>
    private void InitializeSingleton()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 데이터 초기화
    /// </summary>
    private void InitializeData()
    {
        statisticsContainer = new StatisticsContainer();
        statisticsContainer.maxDays = maxStorageDays;
        
        filePath = Path.Combine(Application.persistentDataPath, fileName);
        
        currentReputation = 0;
        currentGold = 0;
        totalVisitorsToday = 0;
        currentActiveVisitors = 0;
        startingReputation = 0;
        startingGold = 0;
        lastActiveVisitorCount = 0;
    }
    
    /// <summary>
    /// 이벤트 구독 설정
    /// </summary>
    private void SetupEventSubscriptions()
    {
        // TimeSystem 이벤트 구독
        if (JY.TimeSystem.Instance != null)
        {
            JY.TimeSystem.Instance.OnDayChanged += OnDayChanged;
            JY.TimeSystem.Instance.OnHourChanged += OnHourChanged;
        }
        
        // ReputationSystem 이벤트 구독
        if (JY.ReputationSystem.Instance != null)
        {
            JY.ReputationSystem.Instance.OnReputationChanged += OnReputationChanged;
        }
        
        // PlayerWallet 이벤트 구독
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.OnMoneyChanged += OnMoneyChanged;
        }
        
        // AISpawner 이벤트 구독
        if (JY.AISpawner.Instance != null)
        {
            // AISpawner에는 OnVisitorSpawned 이벤트가 없으므로 제거
            // 대신 GetActiveAICount()를 사용하여 방문객 수를 추적
        }
        
        DebugLog("이벤트 구독 완료", true);
    }
    
    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeEvents()
    {
        if (JY.TimeSystem.Instance != null)
        {
            JY.TimeSystem.Instance.OnDayChanged -= OnDayChanged;
            JY.TimeSystem.Instance.OnHourChanged -= OnHourChanged;
        }
        if (JY.ReputationSystem.Instance != null)
        {
            JY.ReputationSystem.Instance.OnReputationChanged -= OnReputationChanged;
        }
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.OnMoneyChanged -= OnMoneyChanged;
        }
        // AISpawner.OnVisitorSpawned 이벤트는 없으므로 제거
    }
    
    #endregion
    
    #region Data Collection
    
    /// <summary>
    /// 데이터 수집 시작
    /// </summary>
    private void StartDataCollection()
    {
        if (!enableDataCollection) return;
        
        if (dataCollectionCoroutine != null)
        {
            StopCoroutine(dataCollectionCoroutine);
        }
        
        dataCollectionCoroutine = StartCoroutine(DataCollectionCoroutine());
    }
    
    /// <summary>
    /// 데이터 수집 코루틴
    /// </summary>
    private IEnumerator DataCollectionCoroutine()
    {
        while (enableDataCollection)
        {
            // 현재 값 업데이트 (실시간)
            UpdateCurrentValues();
            
            // 방문객 수 변화 감지 및 총 방문객 수 업데이트
            UpdateVisitorCount();
            
            // 0시 정각에 일차 변경 체크 (TimeSystem에서 자동으로 OnDayChanged 호출)
            
            yield return new WaitForSeconds(5f); // 5초마다 체크 (실시간 업데이트)
        }
    }
    
    /// <summary>
    /// 현재 값들을 업데이트
    /// </summary>
    private void UpdateCurrentValues()
    {
        // ReputationSystem에서 현재 명성도 가져오기
        if (JY.ReputationSystem.Instance != null)
        {
            currentReputation = JY.ReputationSystem.Instance.CurrentReputation;
        }
        
        // PlayerWallet에서 현재 골드 가져오기
        if (PlayerWallet.Instance != null)
        {
            currentGold = PlayerWallet.Instance.money;
        }
        
        // AISpawner에서 현재 활성 방문객 수 가져오기
        if (JY.AISpawner.Instance != null)
        {
            currentActiveVisitors = JY.AISpawner.Instance.GetActiveAICount();
        }
    }
    
    /// <summary>
    /// 방문객 수 변화 감지 및 총 방문객 수 업데이트
    /// </summary>
    private void UpdateVisitorCount()
    {
        // 현재 활성 방문객 수와 이전 수를 비교
        int visitorDifference = currentActiveVisitors - lastActiveVisitorCount;
        
        if (visitorDifference > 0)
        {
            // 방문객이 증가한 경우, 차이만큼 하루 총 방문객 수에 추가
            totalVisitorsToday += visitorDifference;
            
            // DailyStatisticsData의 totalVisitors도 업데이트
            if (currentDayStatistics != null)
            {
                currentDayStatistics.totalVisitors = totalVisitorsToday;
            }
            
            DebugLog($"방문객 수 증가: +{visitorDifference}명 (총: {totalVisitorsToday}명)", true);
        }
        else if (visitorDifference < 0)
        {
            // 방문객이 감소한 경우 (퇴장), 총 방문객 수는 그대로 유지
            DebugLog($"방문객 수 감소: {visitorDifference}명 (총 방문객 수는 유지: {totalVisitorsToday}명)", true);
        }
        
        // 이전 값 업데이트
        lastActiveVisitorCount = currentActiveVisitors;
    }
    
    /// <summary>
    /// 일차별 데이터 기록 (일차 변경 시)
    /// </summary>
    private void RecordDailyData()
    {
        if (currentDayStatistics == null) return;
        
        // 현재 값들을 먼저 업데이트
        UpdateCurrentValues();
        
        int currentDay = currentDayStatistics.day; // 현재 통계의 일차 사용
        
        // 하루 동안 획득한 양 계산
        int reputationGained = currentReputation - startingReputation;
        int goldEarned = currentGold - startingGold;
        
        // 일차별 데이터 업데이트
        currentDayStatistics.UpdateDailyData(
            currentDay,
            reputationGained, // 하루 동안 획득한 명성도
            goldEarned, // 하루 동안 획득한 골드
            totalVisitorsToday, // 하루 총 방문객 수
            startingReputation, // 시작 명성도
            startingGold, // 시작 골드
            currentReputation, // 종료 명성도
            currentGold // 종료 골드
        );
        
        // 이벤트 발생
        var dailyData = currentDayStatistics.GetDailyData(currentDay);
        if (dailyData != null)
        {
            OnDailyDataUpdated?.Invoke(dailyData);
            OnStatisticsUpdated?.Invoke(currentDayStatistics);
        }
        
        DebugLog($"일차별 데이터 기록: {currentDay}일차 - Rep+{reputationGained}, Gold+{goldEarned}, Visitors:{totalVisitorsToday}", true);
        DebugLog($"시작값 - Rep:{startingReputation}, Gold:{startingGold}", true);
        DebugLog($"종료값 - Rep:{currentReputation}, Gold:{currentGold}", true);
    }
    
    #endregion
    
    #region Event Handlers
    
    /// <summary>
    /// 날짜 변경 이벤트 핸들러 (0시에 호출됨)
    /// </summary>
    private void OnDayChanged(int newDay)
    {
        if (!enableDataCollection) return;
        
        int currentDay = newDay;
        
        DebugLog($"=== 0시 일차 변경 감지: {currentDay}일차 시작 ===", true);
        
        // 현재 값들을 먼저 업데이트
        UpdateCurrentValues();
        
        // 이전 일차의 데이터가 있다면 먼저 기록
        if (currentDayStatistics != null && currentDayStatistics.day < currentDay)
        {
            RecordDailyData();
            DebugLog($"이전 일차 데이터 기록 완료: {currentDayStatistics.day}일차", true);
        }
        
        // 새 날의 통계 시작
        currentDayStatistics = statisticsContainer.GetOrCreateDailyStatistics(currentDay);
        
        // 시작 값 저장 (업데이트된 현재 값으로)
        startingReputation = currentReputation;
        startingGold = currentGold;
        totalVisitorsToday = 0;
        currentActiveVisitors = 0;
        lastActiveVisitorCount = 0;
        
        currentDayStatistics.startingReputation = startingReputation;
        currentDayStatistics.startingGold = startingGold;
        
        DebugLog($"새 날 시작: {currentDay}일차 (0시)", true);
        DebugLog($"시작값 설정 - Rep:{startingReputation}, Gold:{startingGold}", true);
        OnDayReset?.Invoke();
    }
    
    /// <summary>
    /// 시간 변경 이벤트 핸들러
    /// </summary>
    private void OnHourChanged(int hour, int minute)
    {
        if (!enableDataCollection) return;
        
        int currentHour = hour;
        
        // 시간 변경은 단순히 로그만 출력 (일차 변경은 OnDayChanged에서 처리)
        DebugLog($"시간 변경: {currentHour}시 {minute}분", showImportantLogsOnly);
    }
    
    /// <summary>
    /// 명성도 변경 이벤트 핸들러
    /// </summary>
    /// <param name="newReputation">새로운 명성도 값</param>
    private void OnReputationChanged(int newReputation)
    {
        if (!enableDataCollection) return;
        
        currentReputation = newReputation;
        DebugLog($"명성도 변경: {newReputation}", showImportantLogsOnly);
    }
    
    /// <summary>
    /// 골드 변경 이벤트 핸들러
    /// </summary>
    /// <param name="newMoney">새로운 골드 값</param>
    private void OnMoneyChanged(int newMoney)
    {
        if (!enableDataCollection) return;
        
        currentGold = newMoney;
        DebugLog($"골드 변경: {newMoney}", showImportantLogsOnly);
    }
    

    
    /// <summary>
    /// 방문객 스폰 이벤트 핸들러 (수동 호출용)
    /// </summary>
    public void OnVisitorSpawned()
    {
        DebugLog("OnVisitorSpawned() 호출됨", true);
        
        if (!enableDataCollection) 
        {
            DebugLog("데이터 수집이 비활성화되어 있어 방문객 스폰을 기록하지 않습니다.", true);
            return;
        }
        
        // currentDayStatistics가 null인 경우 초기화
        if (currentDayStatistics == null)
        {
            int currentDay = JY.TimeSystem.Instance != null ? JY.TimeSystem.Instance.CurrentDay : 1;
            currentDayStatistics = statisticsContainer.GetOrCreateDailyStatistics(currentDay);
            DebugLog($"OnVisitorSpawned에서 통계 초기화: {currentDay}일차", true);
        }
        
        // 현재 활성 AI 수 업데이트
        if (JY.AISpawner.Instance != null)
        {
            int newActiveCount = JY.AISpawner.Instance.GetActiveAICount();
            DebugLog($"방문객 스폰 전 활성 AI 수: {currentActiveVisitors}명", true);
            DebugLog($"방문객 스폰 후 활성 AI 수: {newActiveCount}명", true);
            
            // 방문객이 실제로 증가했는지 확인하고 총 방문객 수 증가
            if (newActiveCount > currentActiveVisitors)
            {
                int increase = newActiveCount - currentActiveVisitors;
                totalVisitorsToday += increase;
                
                // DailyStatisticsData의 totalVisitors도 업데이트
                if (currentDayStatistics != null)
                {
                    currentDayStatistics.totalVisitors = totalVisitorsToday;
                }
                
                DebugLog($"방문객 수 증가 감지: +{increase}명 (총: {totalVisitorsToday}명)", true);
            }
            
            currentActiveVisitors = newActiveCount;
            lastActiveVisitorCount = currentActiveVisitors;
        }
        else
        {
            DebugLog("AISpawner 인스턴스를 찾을 수 없습니다!", true);
        }
        
        DebugLog("방문객 스폰 이벤트 처리 완료", true);
    }
    
    #endregion
    
    #region Save/Load System
    
    /// <summary>
    /// 자동 저장 시작
    /// </summary>
    private void StartAutoSave()
    {
        if (saveCoroutine != null)
        {
            StopCoroutine(saveCoroutine);
        }
        
        saveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }
    
    /// <summary>
    /// 자동 저장 코루틴
    /// </summary>
    private IEnumerator AutoSaveCoroutine()
    {
        while (enableAutoSave)
        {
            yield return new WaitForSeconds(saveInterval);
            SaveStatistics();
        }
    }
    
    /// <summary>
    /// 통계 데이터 저장
    /// </summary>
    public void SaveStatistics()
    {
        try
        {
            if (statisticsContainer == null) return;
            
            statisticsContainer.UpdateSaveTime();
            
            string json = JsonUtility.ToJson(statisticsContainer, true);
            File.WriteAllText(filePath, json);
            
            DebugLog($"통계 데이터 저장 완료: {filePath}", true);
        }
        catch (Exception e)
        {
            DebugLogError($"통계 데이터 저장 실패: {e.Message}");
        }
    }
    
    /// <summary>
    /// 통계 데이터 로드
    /// </summary>
    public void LoadStatistics()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                DebugLog("저장된 통계 데이터가 없습니다. 새로 시작합니다.", true);
                return;
            }
            
            string json = File.ReadAllText(filePath);
            statisticsContainer = JsonUtility.FromJson<StatisticsContainer>(json);
            
            if (statisticsContainer == null)
            {
                statisticsContainer = new StatisticsContainer();
            }
            
            // 현재 날의 통계 가져오기
            int currentDay = JY.TimeSystem.Instance != null ? JY.TimeSystem.Instance.CurrentDay : 1;
            currentDayStatistics = statisticsContainer.GetOrCreateDailyStatistics(currentDay);
            
            DebugLog($"통계 데이터 로드 완료: {filePath}", true);
        }
        catch (Exception e)
        {
            DebugLogError($"통계 데이터 로드 실패: {e.Message}");
            statisticsContainer = new StatisticsContainer();
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// 특정 일의 통계를 가져옴
    /// </summary>
    /// <param name="day">일차</param>
    /// <returns>해당 일의 통계</returns>
    public DailyStatistics GetDailyStatistics(int day)
    {
        return statisticsContainer?.GetDailyStatistics(day);
    }
    
    /// <summary>
    /// 가장 최근 통계를 가져옴
    /// </summary>
    /// <returns>가장 최근 일의 통계</returns>
    public DailyStatistics GetLatestStatistics()
    {
        return statisticsContainer?.GetLatestStatistics();
    }
    
    /// <summary>
    /// 데이터 수집 활성화/비활성화
    /// </summary>
    /// <param name="enabled">활성화 여부</param>
    public void SetDataCollectionEnabled(bool enabled)
    {
        enableDataCollection = enabled;
        
        if (enabled)
        {
            StartDataCollection();
        }
        else
        {
            if (dataCollectionCoroutine != null)
            {
                StopCoroutine(dataCollectionCoroutine);
                dataCollectionCoroutine = null;
            }
        }
        
        DebugLog($"데이터 수집 {(enabled ? "활성화" : "비활성화")}", true);
    }
    
    /// <summary>
    /// 수동으로 일차별 데이터 기록
    /// </summary>
    public void ForceRecordDailyData()
    {
        if (currentDayStatistics == null) return;
        
        RecordDailyData();
    }
    
    /// <summary>
    /// 통계 데이터 초기화
    /// </summary>
    public void ClearAllStatistics()
    {
        statisticsContainer = new StatisticsContainer();
        currentDayStatistics = null;
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        
        DebugLog("모든 통계 데이터가 초기화되었습니다.", true);
    }
    
    /// <summary>
    /// 수동으로 방문객 스폰 테스트 (디버그용)
    /// </summary>
    public void TestVisitorSpawn()
    {
        DebugLog("=== 방문객 스폰 테스트 시작 ===", true);
        DebugLog($"테스트 전 총 방문객 수: {totalVisitorsToday}명", true);
        DebugLog($"테스트 전 활성 방문객 수: {currentActiveVisitors}명", true);
        DebugLog($"데이터 수집 활성화: {enableDataCollection}", true);
        DebugLog($"현재 일차 통계: {(currentDayStatistics != null ? "존재" : "null")}", true);
        
        // AISpawner가 있는지 확인
        if (JY.AISpawner.Instance != null)
        {
            DebugLog($"AISpawner 현재 활성 AI 수: {JY.AISpawner.Instance.GetActiveAICount()}명", true);
        }
        else
        {
            DebugLog("AISpawner 인스턴스가 null입니다!", true);
        }
        
        OnVisitorSpawned();
        
        DebugLog($"테스트 후 총 방문객 수: {totalVisitorsToday}명", true);
        DebugLog($"테스트 후 활성 방문객 수: {currentActiveVisitors}명", true);
        DebugLog("=== 방문객 스폰 테스트 완료 ===", true);
    }
    
    /// <summary>
    /// 현재 통계 상태 출력 (디버그용)
    /// </summary>
    public void PrintCurrentStatus()
    {
        DebugLog("=== 현재 통계 상태 ===", true);
        DebugLog($"데이터 수집 활성화: {enableDataCollection}", true);
        DebugLog($"현재 명성도: {currentReputation}", true);
        DebugLog($"현재 골드: {currentGold}", true);
        DebugLog($"현재 활성 방문객 수: {currentActiveVisitors}", true);
        DebugLog($"하루 총 방문객 수: {totalVisitorsToday}", true);
        DebugLog($"시작 명성도: {startingReputation}", true);
        DebugLog($"시작 골드: {startingGold}", true);
        DebugLog($"현재 일차 통계: {(currentDayStatistics != null ? $"일차 {currentDayStatistics.day}" : "null")}", true);
        DebugLog("========================", true);
    }
    
    /// <summary>
    /// 하루 총 방문객 수 반환 (UI용)
    /// </summary>
    public int GetTotalVisitorsToday()
    {
        return totalVisitorsToday;
    }
    
    /// <summary>
    /// 현재 활성 방문객 수 반환 (UI용)
    /// </summary>
    public int GetCurrentActiveVisitors()
    {
        return currentActiveVisitors;
    }
    
    /// <summary>
    /// 수동으로 방문객 수 증가 (디버그/테스트용)
    /// </summary>
    public void IncrementVisitorCount(int count = 1)
    {
        totalVisitorsToday += count;
        DebugLog($"수동으로 방문객 수 증가: +{count}명 (총: {totalVisitorsToday}명)", true);
    }
    
    #endregion
    
    #region Debug Methods
    
    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void DebugLog(string message, bool isImportant = false)
    {
#if UNITY_EDITOR
        if (!showDebugLogs) return;
        
        if (showImportantLogsOnly && !isImportant) return;
        
        Debug.Log($"[DailyStatisticsManager] {message}");
#endif
    }
    
    /// <summary>
    /// 에러 로그 출력
    /// </summary>
    private void DebugLogError(string message)
    {
#if UNITY_EDITOR
        Debug.LogError($"[DailyStatisticsManager] {message}");
#endif
    }
    
    #endregion
}
