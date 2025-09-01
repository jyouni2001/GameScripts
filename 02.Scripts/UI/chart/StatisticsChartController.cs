using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatisticsChartController : MonoBehaviour
{
    [Header("Chart Components")]
    [SerializeField] private ChartRenderer chartRenderer;
    [SerializeField] private RectTransform chartContainer;
    
    [Header("Chart Type Buttons")]
    [SerializeField] private Button spawnCountButton;
    [SerializeField] private Button goldEarnedButton;
    [SerializeField] private Button reputationButton;
    
    [Header("Chart Information")]
    [SerializeField] private TextMeshProUGUI chartTitleText;
    [SerializeField] private TextMeshProUGUI currentValueText;
    [SerializeField] private TextMeshProUGUI totalValueText;
    
    [Header("Chart Settings")]
    [SerializeField] private int displayDays = 30;
    [SerializeField] private bool autoUpdate = true;
    [SerializeField] private float updateInterval = 5f;
    
    [Header("Chart Colors")]
    [SerializeField] private Color spawnCountColor = Color.green;
    [SerializeField] private Color goldEarnedColor = Color.yellow;
    [SerializeField] private Color reputationColor = Color.blue;
    
    private ChartRenderer.ChartType currentChartType = ChartRenderer.ChartType.SpawnCount;
    private float lastUpdateTime;
    private bool isInitialized = false;

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        SetupButtonListeners();
        SetupChartColors();
        LoadInitialData();
        isInitialized = true;
    }

    private void Update()
    {
        if (autoUpdate && isInitialized)
        {
            lastUpdateTime += Time.deltaTime;
            if (lastUpdateTime >= updateInterval)
            {
                UpdateChart();
                lastUpdateTime = 0f;
            }
        }
    }

    private void InitializeComponents()
    {
        // ChartRenderer 컴포넌트 찾기
        if (chartRenderer == null)
        {
            chartRenderer = GetComponentInChildren<ChartRenderer>();
        }

        // 차트 컨테이너 찾기
        if (chartContainer == null)
        {
            chartContainer = GetComponent<RectTransform>();
        }

        // 버튼들 찾기
        if (spawnCountButton == null)
        {
            spawnCountButton = transform.Find("SpawnCountButton")?.GetComponent<Button>();
        }
        if (goldEarnedButton == null)
        {
            goldEarnedButton = transform.Find("GoldEarnedButton")?.GetComponent<Button>();
        }
        if (reputationButton == null)
        {
            reputationButton = transform.Find("ReputationButton")?.GetComponent<Button>();
        }

        // 텍스트 컴포넌트들 찾기
        if (chartTitleText == null)
        {
            chartTitleText = transform.Find("ChartTitle")?.GetComponent<TextMeshProUGUI>();
        }
        if (currentValueText == null)
        {
            currentValueText = transform.Find("CurrentValue")?.GetComponent<TextMeshProUGUI>();
        }
        if (totalValueText == null)
        {
            totalValueText = transform.Find("TotalValue")?.GetComponent<TextMeshProUGUI>();
        }
    }

    private void SetupButtonListeners()
    {
        if (spawnCountButton != null)
        {
            spawnCountButton.onClick.AddListener(() => SwitchChart(ChartRenderer.ChartType.SpawnCount));
        }
        
        if (goldEarnedButton != null)
        {
            goldEarnedButton.onClick.AddListener(() => SwitchChart(ChartRenderer.ChartType.GoldEarned));
        }
        
        if (reputationButton != null)
        {
            reputationButton.onClick.AddListener(() => SwitchChart(ChartRenderer.ChartType.ReputationGained));
        }
    }

    private void SetupChartColors()
    {
        if (chartRenderer != null)
        {
            // 각 차트 타입별 색상 설정
            chartRenderer.SetChartColors(spawnCountColor, Color.white, new Color(0.1f, 0.1f, 0.1f, 0.8f));
        }
    }

    private void LoadInitialData()
    {
        // 테스트 데이터 생성 (실제 게임에서는 저장된 데이터 로드)
        if (GameStatisticsData.Instance != null)
        {
            GameStatisticsData.Instance.GenerateTestData();
        }
        
        // 초기 차트 표시
        SwitchChart(currentChartType);
    }

    /// <summary>
    /// 차트 타입을 전환합니다.
    /// </summary>
    public void SwitchChart(ChartRenderer.ChartType chartType)
    {
        currentChartType = chartType;
        
        if (chartRenderer == null)
        {
            Debug.LogError("ChartRenderer가 없습니다.");
            return;
        }

        // 차트 색상 변경
        Color lineColor = GetChartColor(chartType);
        chartRenderer.SetChartColors(lineColor, Color.white, new Color(0.1f, 0.1f, 0.1f, 0.8f));

        // 차트 제목 업데이트
        UpdateChartTitle(chartType);

        // 차트 데이터 렌더링
        UpdateChart();

        // 버튼 활성화 상태 업데이트
        UpdateButtonStates(chartType);
    }

    private Color GetChartColor(ChartRenderer.ChartType chartType)
    {
        switch (chartType)
        {
            case ChartRenderer.ChartType.SpawnCount:
                return spawnCountColor;
            case ChartRenderer.ChartType.GoldEarned:
                return goldEarnedColor;
            case ChartRenderer.ChartType.ReputationGained:
                return reputationColor;
            default:
                return Color.white;
        }
    }

    private void UpdateChartTitle(ChartRenderer.ChartType chartType)
    {
        if (chartTitleText != null)
        {
            switch (chartType)
            {
                case ChartRenderer.ChartType.SpawnCount:
                    chartTitleText.text = "일별 스폰 인원수";
                    break;
                case ChartRenderer.ChartType.GoldEarned:
                    chartTitleText.text = "일별 획득 골드량";
                    break;
                case ChartRenderer.ChartType.ReputationGained:
                    chartTitleText.text = "일별 명성도 증가량";
                    break;
            }
        }
    }

    private void UpdateButtonStates(ChartRenderer.ChartType chartType)
    {
        if (spawnCountButton != null)
        {
            spawnCountButton.interactable = (chartType != ChartRenderer.ChartType.SpawnCount);
        }
        
        if (goldEarnedButton != null)
        {
            goldEarnedButton.interactable = (chartType != ChartRenderer.ChartType.GoldEarned);
        }
        
        if (reputationButton != null)
        {
            reputationButton.interactable = (chartType != ChartRenderer.ChartType.ReputationGained);
        }
    }

    /// <summary>
    /// 차트를 업데이트합니다.
    /// </summary>
    public void UpdateChart()
    {
        if (GameStatisticsData.Instance == null || chartRenderer == null)
        {
            Debug.LogWarning("필요한 컴포넌트가 없습니다.");
            return;
        }

        // 차트 데이터 가져오기
        List<DailyStatistics> chartData = GameStatisticsData.Instance.GetRecentStats(displayDays);
        
        if (chartData.Count == 0)
        {
            Debug.LogWarning("차트 데이터가 없습니다.");
            return;
        }

        // 차트 렌더링
        chartRenderer.RenderChart(currentChartType, chartData);

        // 현재 값과 총합 업데이트
        UpdateValueTexts(chartData);
    }

    private void UpdateValueTexts(List<DailyStatistics> chartData)
    {
        if (chartData.Count == 0) return;

        // 현재 날의 값
        int currentValue = GetCurrentValue(chartData[chartData.Count - 1]);
        
        // 총합 계산
        int totalValue = 0;
        foreach (var stat in chartData)
        {
            totalValue += GetCurrentValue(stat);
        }

        // UI 업데이트
        if (currentValueText != null)
        {
            currentValueText.text = $"현재: {FormatValue(currentValue, currentChartType)}";
        }
        
        if (totalValueText != null)
        {
            totalValueText.text = $"총합: {FormatValue(totalValue, currentChartType)}";
        }
    }

    private int GetCurrentValue(DailyStatistics stat)
    {
        switch (currentChartType)
        {
            case ChartRenderer.ChartType.SpawnCount:
                return stat.spawnCount;
            case ChartRenderer.ChartType.GoldEarned:
                return stat.goldEarned;
            case ChartRenderer.ChartType.ReputationGained:
                return stat.reputationGained;
            default:
                return 0;
        }
    }

    private string FormatValue(int value, ChartRenderer.ChartType chartType)
    {
        switch (chartType)
        {
            case ChartRenderer.ChartType.SpawnCount:
                return $"{value}명";
            case ChartRenderer.ChartType.GoldEarned:
                return $"{value}골드";
            case ChartRenderer.ChartType.ReputationGained:
                return $"{value}점";
            default:
                return value.ToString();
        }
    }

    /// <summary>
    /// 차트 표시 일수를 변경합니다.
    /// </summary>
    public void SetDisplayDays(int days)
    {
        displayDays = Mathf.Clamp(days, 7, 365);
        UpdateChart();
    }

    /// <summary>
    /// 자동 업데이트를 토글합니다.
    /// </summary>
    public void ToggleAutoUpdate()
    {
        autoUpdate = !autoUpdate;
        Debug.Log($"자동 업데이트: {(autoUpdate ? "활성화" : "비활성화")}");
    }

    /// <summary>
    /// 차트를 새로고침합니다.
    /// </summary>
    public void RefreshChart()
    {
        UpdateChart();
        Debug.Log("차트가 새로고침되었습니다.");
    }

    /// <summary>
    /// 테스트 데이터를 생성합니다.
    /// </summary>
    public void GenerateTestData()
    {
        if (GameStatisticsData.Instance != null)
        {
            GameStatisticsData.Instance.GenerateTestData();
            UpdateChart();
            Debug.Log("테스트 데이터가 생성되었습니다.");
        }
    }

    /// <summary>
    /// 모든 통계 데이터를 초기화합니다.
    /// </summary>
    public void ClearAllData()
    {
        if (GameStatisticsData.Instance != null)
        {
            GameStatisticsData.Instance.ClearAllStats();
            UpdateChart();
            Debug.Log("모든 통계 데이터가 초기화되었습니다.");
        }
    }

    private void OnDestroy()
    {
        // 버튼 리스너 제거
        if (spawnCountButton != null)
        {
            spawnCountButton.onClick.RemoveAllListeners();
        }
        if (goldEarnedButton != null)
        {
            goldEarnedButton.onClick.RemoveAllListeners();
        }
        if (reputationButton != null)
        {
            reputationButton.onClick.RemoveAllListeners();
        }
    }
}
