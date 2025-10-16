using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 하루 통계 시스템의 UI 관리 클래스
/// Canvas 기반으로 통계 패널을 구성하고 실시간 차트 업데이트를 담당합니다.
/// </summary>
public class DailyStatisticsUI : MonoBehaviour
{
    [Header("UI 설정")]
    [Tooltip("통계 패널")]
    [SerializeField] private GameObject statisticsPanel;
    
    [Tooltip("패널 열기/닫기 버튼")]
    [SerializeField] private Button toggleButton;    
    
    [Header("차트 영역")]
    [Tooltip("명성도 차트 영역")]
    [SerializeField] private RectTransform reputationChartArea;
    
    [Tooltip("골드 차트 영역")]
    [SerializeField] private RectTransform goldChartArea;
    
    [Tooltip("방문객 차트 영역")]
    [SerializeField] private RectTransform visitorChartArea;
    
    [Header("정보 표시 영역")]
    [Tooltip("현재 값 표시 텍스트들")]
    [SerializeField] private TextMeshProUGUI currentReputationText;
    [SerializeField] private TextMeshProUGUI currentGoldText;
    [SerializeField] private TextMeshProUGUI currentVisitorText;
    
    [Tooltip("하루 총계 표시 텍스트들")]
    [SerializeField] private TextMeshProUGUI totalReputationText;
    [SerializeField] private TextMeshProUGUI totalGoldText;
    [SerializeField] private TextMeshProUGUI totalVisitorText;
    
    [Header("범례 영역")]
    [Tooltip("범례 텍스트들")]
    [SerializeField] private TextMeshProUGUI reputationLegendText;
    [SerializeField] private TextMeshProUGUI goldLegendText;
    [SerializeField] private TextMeshProUGUI visitorLegendText;
    
    [Header("시간축 표시")]
    [Tooltip("시간축 라벨들")]
    [SerializeField] private TextMeshProUGUI[] timeLabels;
    
    [Header("UI 설정")]
    [Tooltip("패널 기본 위치")]
    [SerializeField] private Vector2 panelPosition = new Vector2(0, 0);
    
    [Tooltip("패널 크기")]
    [SerializeField] private Vector2 panelSize = new Vector2(800, 600);
    
    [Tooltip("차트 크기")]
    [SerializeField] private Vector2 chartSize = new Vector2(300, 200);
    
    [Tooltip("UI 업데이트 간격 (초)")]
    [SerializeField] private float updateInterval = 5f; // 5초마다 업데이트 (실시간 불필요)
    
    [Header("디버그 설정")]
    [Tooltip("디버그 로그 표시")]
    [SerializeField] private bool showDebugLogs = true;
    
    // 차트 렌더러들
    private LineChartRenderer reputationChartRenderer;
    private LineChartRenderer goldChartRenderer;
    private LineChartRenderer visitorChartRenderer;
    
    // UI 상태
    private bool isPanelOpen = false;
    private Coroutine updateCoroutine;
    
    // 캐시된 값들
    private int lastReputation = -1;
    private int lastGold = -1;
    private int lastVisitors = -1;
    
    /// <summary>
    /// 패널이 열려있는지 여부
    /// </summary>
    public bool IsPanelOpen => isPanelOpen;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        InitializeUI();
    }
    
    private void Start()
    {
        SetupEventSubscriptions();
        SetupUI();
        StartUIUpdates();
        
        DebugLog("DailyStatisticsUI 초기화 완료");
    }
    
    private void OnDestroy()
    {
        UnsubscribeEvents();
        StopUIUpdates();
    }
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// UI 초기화
    /// </summary>
    private void InitializeUI()
    {
        // 패널이 없으면 생성
        if (statisticsPanel == null)
        {
            CreateStatisticsPanel();
        }
        
        // 버튼이 없으면 생성
        if (toggleButton == null)
        {
            CreateToggleButton();
        }
        
        // 초기 상태 설정
        isPanelOpen = false;
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// 통계 패널 생성
    /// </summary>
    private void CreateStatisticsPanel()
    {
        // 메인 패널 생성
        GameObject panelObj = new GameObject("StatisticsPanel");
        panelObj.transform.SetParent(transform);
        
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = panelPosition;
        panelRect.sizeDelta = panelSize;
        
        // 배경 이미지 추가
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        
        statisticsPanel = panelObj;
        
        // 차트 영역들 생성
        CreateChartAreas();
        
        // 정보 표시 영역 생성
        CreateInfoAreas();
        
        // 범례 영역 생성
        CreateLegendAreas();
        
        // 시간축 라벨 생성
        CreateTimeLabels();
    }
    
    /// <summary>
    /// 차트 영역들 생성
    /// </summary>
    private void CreateChartAreas()
    {
        // 명성도 차트 영역
        reputationChartArea = CreateChartArea("ReputationChart", new Vector2(-200, 100));
        
        // 골드 차트 영역
        goldChartArea = CreateChartArea("GoldChart", new Vector2(0, 100));
        
        // 방문객 차트 영역
        visitorChartArea = CreateChartArea("VisitorChart", new Vector2(200, 100));
    }
    
    /// <summary>
    /// 개별 차트 영역 생성
    /// </summary>
    private RectTransform CreateChartArea(string name, Vector2 position)
    {
        GameObject chartObj = new GameObject(name);
        chartObj.transform.SetParent(statisticsPanel.transform);
        
        RectTransform chartRect = chartObj.AddComponent<RectTransform>();
        chartRect.anchorMin = new Vector2(0.5f, 0.5f);
        chartRect.anchorMax = new Vector2(0.5f, 0.5f);
        chartRect.anchoredPosition = position;
        chartRect.sizeDelta = chartSize;
        
        // 차트 렌더러 추가
        LineChartRenderer renderer = chartObj.AddComponent<LineChartRenderer>();
        
        if (name.Contains("Reputation"))
        {
            reputationChartRenderer = renderer;
        }
        else if (name.Contains("Gold"))
        {
            goldChartRenderer = renderer;
        }
        else if (name.Contains("Visitor"))
        {
            visitorChartRenderer = renderer;
        }
        
        return chartRect;
    }
    
    /// <summary>
    /// 정보 표시 영역 생성
    /// </summary>
    private void CreateInfoAreas()
    {
        // 현재 값 표시 영역
        CreateInfoArea("CurrentValues", new Vector2(-200, -150), "Current Values");
        
        // 총계 표시 영역
        CreateInfoArea("TotalValues", new Vector2(200, -150), "Daily Total");
    }
    
    /// <summary>
    /// 개별 정보 영역 생성
    /// </summary>
    private void CreateInfoArea(string name, Vector2 position, string title)
    {
        GameObject infoObj = new GameObject(name);
        infoObj.transform.SetParent(statisticsPanel.transform);
        
        RectTransform infoRect = infoObj.AddComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoRect.anchoredPosition = position;
        infoRect.sizeDelta = new Vector2(300, 200);
        
        // 제목 텍스트
        CreateText(infoObj, "Title", new Vector2(0, 80), title, 18, Color.white);
        
        // 명성도 텍스트
        CreateText(infoObj, "Reputation", new Vector2(0, 40), "Reputation: 0", 14, Color.blue);
        
        // 골드 텍스트
        CreateText(infoObj, "Gold", new Vector2(0, 0), "Gold: 0", 14, Color.yellow);
        
        // 방문객 텍스트
        CreateText(infoObj, "Visitor", new Vector2(0, -40), "Visitors: 0", 14, Color.green);
        
        // 텍스트 참조 설정
        if (name == "CurrentValues")
        {
            currentReputationText = infoObj.transform.Find("Reputation").GetComponent<TextMeshProUGUI>();
            currentGoldText = infoObj.transform.Find("Gold").GetComponent<TextMeshProUGUI>();
            currentVisitorText = infoObj.transform.Find("Visitor").GetComponent<TextMeshProUGUI>();
        }
        else if (name == "TotalValues")
        {
            totalReputationText = infoObj.transform.Find("Reputation").GetComponent<TextMeshProUGUI>();
            totalGoldText = infoObj.transform.Find("Gold").GetComponent<TextMeshProUGUI>();
            totalVisitorText = infoObj.transform.Find("Visitor").GetComponent<TextMeshProUGUI>();
        }
    }
    
    /// <summary>
    /// 텍스트 생성
    /// </summary>
    private TextMeshProUGUI CreateText(GameObject parent, string name, Vector2 position, string text, int fontSize, Color color)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = position;
        textRect.sizeDelta = new Vector2(200, 30);
        
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.alignment = TextAlignmentOptions.Center;
        
        return textComponent;
    }
    
    /// <summary>
    /// 범례 영역 생성
    /// </summary>
    private void CreateLegendAreas()
    {
        GameObject legendObj = new GameObject("Legend");
        legendObj.transform.SetParent(statisticsPanel.transform);
        
        RectTransform legendRect = legendObj.AddComponent<RectTransform>();
        legendRect.anchorMin = new Vector2(0.5f, 0.5f);
        legendRect.anchorMax = new Vector2(0.5f, 0.5f);
        legendRect.anchoredPosition = new Vector2(0, -50);
        legendRect.sizeDelta = new Vector2(600, 50);
        
        // 범례 텍스트들 생성
        reputationLegendText = CreateText(legendObj, "ReputationLegend", new Vector2(-200, 0), "Reputation", 16, Color.blue);
        goldLegendText = CreateText(legendObj, "GoldLegend", new Vector2(0, 0), "Gold", 16, Color.yellow);
        visitorLegendText = CreateText(legendObj, "VisitorLegend", new Vector2(200, 0), "Visitors", 16, Color.green);
    }
    
    /// <summary>
    /// 시간축 라벨 생성
    /// </summary>
    private void CreateTimeLabels()
    {
        timeLabels = new TextMeshProUGUI[5];
        string[] timeTexts = { "Day 1", "Day 2", "Day 3", "Day 4", "Day 5" };
        
        for (int i = 0; i < 5; i++)
        {
            GameObject timeObj = new GameObject($"TimeLabel_{i}");
            timeObj.transform.SetParent(statisticsPanel.transform);
            
            RectTransform timeRect = timeObj.AddComponent<RectTransform>();
            timeRect.anchorMin = new Vector2(0.5f, 0.5f);
            timeRect.anchorMax = new Vector2(0.5f, 0.5f);
            timeRect.anchoredPosition = new Vector2(-200 + (i * 100), -100);
            timeRect.sizeDelta = new Vector2(50, 20);
            
            timeLabels[i] = timeObj.AddComponent<TextMeshProUGUI>();
            timeLabels[i].text = timeTexts[i];
            timeLabels[i].fontSize = 12;
            timeLabels[i].color = Color.white;
            timeLabels[i].alignment = TextAlignmentOptions.Center;
        }
    }
    
    /// <summary>
    /// 토글 버튼 생성
    /// </summary>
    private void CreateToggleButton()
    {
        GameObject buttonObj = new GameObject("ToggleButton");
        buttonObj.transform.SetParent(transform);
        
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 1);
        buttonRect.anchorMax = new Vector2(1, 1);
        buttonRect.anchoredPosition = new Vector2(-100, -50);
        buttonRect.sizeDelta = new Vector2(150, 50);
        
        // 버튼 이미지
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        // 버튼 컴포넌트
        toggleButton = buttonObj.AddComponent<Button>();
        toggleButton.onClick.AddListener(TogglePanel);
        
        // 버튼 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;        
    }
    
    #endregion
    
    #region Setup
    
    /// <summary>
    /// 이벤트 구독 설정
    /// </summary>
    private void SetupEventSubscriptions()
    {
        if (DailyStatisticsManager.Instance != null)
        {
            DailyStatisticsManager.OnStatisticsUpdated += OnStatisticsUpdated;
            DailyStatisticsManager.OnDailyDataUpdated += OnDailyDataUpdated;
            DailyStatisticsManager.OnDayReset += OnDayReset;
        }
    }
    
    /// <summary>
    /// 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeEvents()
    {
        DailyStatisticsManager.OnStatisticsUpdated -= OnStatisticsUpdated;
        DailyStatisticsManager.OnDailyDataUpdated -= OnDailyDataUpdated;
        DailyStatisticsManager.OnDayReset -= OnDayReset;
    }
    
    /// <summary>
    /// UI 설정
    /// </summary>
    private void SetupUI()
    {
        // 초기 텍스트 설정
        UpdateCurrentValues();
        UpdateTotalValues();
        
        // 차트 초기화
        if (reputationChartRenderer != null)
            reputationChartRenderer.ClearCharts();
        if (goldChartRenderer != null)
            goldChartRenderer.ClearCharts();
        if (visitorChartRenderer != null)
            visitorChartRenderer.ClearCharts();
    }
    
    #endregion
    
    #region UI Updates
    
    /// <summary>
    /// UI 업데이트 시작
    /// </summary>
    private void StartUIUpdates()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        
        updateCoroutine = StartCoroutine(UIUpdateCoroutine());
    }
    
    /// <summary>
    /// UI 업데이트 중지
    /// </summary>
    private void StopUIUpdates()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
            updateCoroutine = null;
        }
    }
    
    /// <summary>
    /// UI 업데이트 코루틴
    /// </summary>
    private IEnumerator UIUpdateCoroutine()
    {
        while (true)
        {
            UpdateCurrentValues();
            
            // 패널이 열려있을 때만 차트 업데이트 (실시간 업데이트 제거)
            if (isPanelOpen)
            {
                // 차트는 이벤트 기반으로만 업데이트
                // UpdateCharts(); // 실시간 차트 업데이트 제거
            }
            
            yield return new WaitForSeconds(updateInterval);
        }
    }
    
    /// <summary>
    /// 현재 값 업데이트
    /// </summary>
    private void UpdateCurrentValues()
    {
        // ReputationSystem에서 현재 명성도 가져오기
        if (JY.ReputationSystem.Instance != null)
        {
            int currentRep = JY.ReputationSystem.Instance.CurrentReputation;
            if (currentRep != lastReputation)
            {
                lastReputation = currentRep;
                if (currentReputationText != null)
                {
                    currentReputationText.text = $"Reputation: {FormatNumber(currentRep)}";
                }
            }
        }
        
        // PlayerWallet에서 현재 골드 가져오기
        if (PlayerWallet.Instance != null)
        {
            int currentGold = PlayerWallet.Instance.money;
            if (currentGold != lastGold)
            {
                lastGold = currentGold;
                if (currentGoldText != null)
                {
                    currentGoldText.text = $"Gold: {FormatNumber(currentGold)}";
                }
            }
        }
        
        // DailyStatisticsManager에서 하루 총 방문객 수 가져오기
        if (DailyStatisticsManager.Instance != null)
        {
            int currentVisitors = DailyStatisticsManager.Instance.GetTotalVisitorsToday();
            if (currentVisitors != lastVisitors)
            {
                lastVisitors = currentVisitors;
                if (currentVisitorText != null)
                {
                    currentVisitorText.text = $"Visitors: {currentVisitors}";
                }
            }
        }
    }
    
    /// <summary>
    /// 총계 값 업데이트
    /// </summary>
    private void UpdateTotalValues()
    {
        if (DailyStatisticsManager.Instance?.CurrentDayStatistics == null) return;
        
        var stats = DailyStatisticsManager.Instance.CurrentDayStatistics;
        
        if (totalReputationText != null)
        {
            totalReputationText.text = $"Reputation: +{FormatNumber(stats.totalReputationGained)}";
        }
        
        if (totalGoldText != null)
        {
            totalGoldText.text = $"Gold: +{FormatNumber(stats.totalGoldEarned)}";
        }
        
        if (totalVisitorText != null)
        {
            totalVisitorText.text = $"Visitors: {stats.totalVisitors}";
        }
    }
    
    /// <summary>
    /// 차트 업데이트 (일차별 데이터)
    /// </summary>
    private void UpdateCharts()
    {
        if (DailyStatisticsManager.Instance?.CurrentDayStatistics == null) return;
        
        var dailyData = DailyStatisticsManager.Instance.CurrentDayStatistics.dailyData;
        
        if (reputationChartRenderer != null)
        {
            reputationChartRenderer.UpdateChartData(dailyData);
        }
        
        if (goldChartRenderer != null)
        {
            goldChartRenderer.UpdateChartData(dailyData);
        }
        
        if (visitorChartRenderer != null)
        {
            visitorChartRenderer.UpdateChartData(dailyData);
        }
    }
    
    /// <summary>
    /// 차트 초기화
    /// </summary>
    private void ClearCharts()
    {
        if (reputationChartRenderer != null)
        {
            reputationChartRenderer.ClearCharts();
        }
        
        if (goldChartRenderer != null)
        {
            goldChartRenderer.ClearCharts();
        }
        
        if (visitorChartRenderer != null)
        {
            visitorChartRenderer.ClearCharts();
        }
    }
    
    /// <summary>
    /// 강제로 차트 업데이트 (디버그용)
    /// </summary>
    public void ForceUpdateCharts()
    {
        DebugLog("강제 차트 업데이트 시작");
        
        if (DailyStatisticsManager.Instance?.CurrentDayStatistics == null)
        {
            DebugLog("DailyStatisticsManager 또는 CurrentDayStatistics가 null입니다");
            return;
        }
        
        var dailyData = DailyStatisticsManager.Instance.CurrentDayStatistics.dailyData;
        DebugLog($"일차별 데이터 개수: {dailyData?.Count ?? 0}");
        
        if (dailyData != null && dailyData.Count > 0)
        {
            DebugLog($"첫 번째 데이터: Day {dailyData[0].day}, Rep: {dailyData[0].reputationGained}, Gold: {dailyData[0].goldEarned}, Visitors: {dailyData[0].totalVisitors}");
        }
        
        if (reputationChartRenderer != null)
        {
            DebugLog("명성도 차트 렌더러 발견, 데이터 업데이트 중...");
            reputationChartRenderer.UpdateChartData(dailyData);
            DebugLog("명성도 차트 업데이트 완료");
        }
        else
        {
            DebugLog("명성도 차트 렌더러가 null입니다!");
        }
        
        if (goldChartRenderer != null)
        {
            DebugLog("골드 차트 렌더러 발견, 데이터 업데이트 중...");
            goldChartRenderer.UpdateChartData(dailyData);
            DebugLog("골드 차트 업데이트 완료");
        }
        else
        {
            DebugLog("골드 차트 렌더러가 null입니다!");
        }
        
        if (visitorChartRenderer != null)
        {
            DebugLog("방문객 차트 렌더러 발견, 데이터 업데이트 중...");
            visitorChartRenderer.UpdateChartData(dailyData);
            DebugLog("방문객 차트 업데이트 완료");
        }
        else
        {
            DebugLog("방문객 차트 렌더러가 null입니다!");
        }
        
        DebugLog("강제 차트 업데이트 완료");
    }
    
    #endregion
    
    #region Event Handlers
    
    /// <summary>
    /// 통계 업데이트 이벤트 핸들러
    /// </summary>
    private void OnStatisticsUpdated(DailyStatistics statistics)
    {
        UpdateTotalValues();
        
        // 패널이 열려있을 때만 차트 업데이트
        if (isPanelOpen)
        {
            UpdateCharts();
        }
        
        DebugLog("통계 업데이트됨");
    }
    
    /// <summary>
    /// 일차별 데이터 업데이트 이벤트 핸들러
    /// </summary>
    private void OnDailyDataUpdated(DailyData dailyData)
    {
        // 일차별 데이터는 24시간마다 기록되므로 실시간 차트 업데이트 제거
        DebugLog($"일차별 데이터 업데이트: {dailyData.day}일차");
    }
    
    /// <summary>
    /// 날짜 리셋 이벤트 핸들러
    /// </summary>
    private void OnDayReset()
    {
        UpdateTotalValues();
        
        // 새 날 시작 시 차트 초기화
        if (isPanelOpen)
        {
            ClearCharts();
            UpdateCharts(); // 새 날 데이터로 차트 업데이트
        }
        
        DebugLog("새 날 시작 - 통계 리셋");
    }
    
    #endregion
    
    #region Panel Control
    
    /// <summary>
    /// 패널 토글
    /// </summary>
    public void TogglePanel()
    {
        if (isPanelOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }
    
    /// <summary>
    /// 패널 열기
    /// </summary>
    public void OpenPanel()
    {
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(true);
            isPanelOpen = true;
                        
            // 패널이 열릴 때 최신 데이터로 업데이트
            UpdateCurrentValues();
            UpdateTotalValues();
            ForceUpdateCharts(); // 강제로 차트 업데이트
            
            DebugLog("통계 패널 열림");
        }
    }
    
    /// <summary>
    /// 패널 닫기
    /// </summary>
    public void ClosePanel()
    {
        if (statisticsPanel != null)
        {
            statisticsPanel.SetActive(false);
            isPanelOpen = false;
            
            
            DebugLog("통계 패널 닫힘");
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// 숫자 포맷팅 (k, m, b 단위)
    /// </summary>
    private string FormatNumber(int number)
    {
        if (number >= 1_000_000_000)
        {
            return $"{(number / 1_000_000_000f):F1}b";
        }
        else if (number >= 1_000_000)
        {
            return $"{(number / 1_000_000f):F1}m";
        }
        else if (number >= 1_000)
        {
            return $"{(number / 1000f):F1}k";
        }
        else
        {
            return number.ToString();
        }
    }
    
    #endregion
    
    #region Debug Methods
    
    /// <summary>
    /// 디버그 로그 출력
    /// </summary>
    private void DebugLog(string message)
    {
#if UNITY_EDITOR
        if (!showDebugLogs) return;
        
        Debug.Log($"[DailyStatisticsUI] {message}");
#endif
    }
    
    #endregion
}
