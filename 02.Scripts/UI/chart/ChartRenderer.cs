using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChartRenderer : MonoBehaviour
{
    [Header("Chart Settings")]
    [SerializeField] private RectTransform chartContainer;
    [SerializeField] private Color lineColor = Color.blue;
    [SerializeField] private Color pointColor = Color.red;
    [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private float lineWidth = 2f;
    [SerializeField] private float pointSize = 6f;
    [SerializeField] private int maxDataPoints = 30;
    
    [Header("Chart Elements")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform lineContainer;
    [SerializeField] private RectTransform pointContainer;
    
    private List<GameObject> chartElements = new List<GameObject>();
    private ChartType currentChartType = ChartType.SpawnCount;
    
    public enum ChartType
    {
        SpawnCount,
        GoldEarned,
        ReputationGained
    }

    private void Awake()
    {
        InitializeChart();
    }

    private void InitializeChart()
    {
        if (chartContainer == null)
        {
            chartContainer = GetComponent<RectTransform>();
        }

        // 배경 이미지 생성
        if (backgroundImage == null)
        {
            GameObject bgObj = new GameObject("ChartBackground");
            bgObj.transform.SetParent(chartContainer);
            backgroundImage = bgObj.AddComponent<Image>();
            backgroundImage.color = backgroundColor;
            
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
        }

        // 라인 컨테이너 생성
        if (lineContainer == null)
        {
            GameObject lineObj = new GameObject("LineContainer");
            lineObj.transform.SetParent(chartContainer);
            lineContainer = lineObj.GetComponent<RectTransform>();
            lineContainer.anchorMin = Vector2.zero;
            lineContainer.anchorMax = Vector2.one;
            lineContainer.offsetMin = Vector2.zero;
            lineContainer.offsetMax = Vector2.zero;
        }

        // 포인트 컨테이너 생성
        if (pointContainer == null)
        {
            GameObject pointObj = new GameObject("PointContainer");
            pointObj.transform.SetParent(chartContainer);
            pointContainer = pointObj.GetComponent<RectTransform>();
            pointContainer.anchorMin = Vector2.zero;
            pointContainer.offsetMin = Vector2.zero;
            pointContainer.anchorMax = Vector2.one;
            pointContainer.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// 차트 데이터를 렌더링합니다.
    /// </summary>
    public void RenderChart(ChartType chartType, List<DailyStatistics> data)
    {
        currentChartType = chartType;
        ClearChart();
        
        if (data == null || data.Count == 0)
        {
            Debug.LogWarning("차트 데이터가 없습니다.");
            return;
        }

        // 최근 데이터만 사용 (maxDataPoints 개수만큼)
        List<DailyStatistics> recentData = data.Count > maxDataPoints 
            ? data.GetRange(data.Count - maxDataPoints, maxDataPoints) 
            : data;

        // 데이터 정규화를 위한 최대값 찾기
        float maxValue = GetMaxValue(recentData, chartType);
        if (maxValue <= 0) maxValue = 1f; // 0으로 나누기 방지

        // 라인 그리기
        DrawLine(recentData, maxValue, chartType);
        
        // 포인트 그리기
        DrawPoints(recentData, maxValue, chartType);
    }

    private float GetMaxValue(List<DailyStatistics> data, ChartType chartType)
    {
        float maxValue = 0f;
        
        foreach (var stat in data)
        {
            float value = GetValueFromStat(stat, chartType);
            if (value > maxValue)
                maxValue = value;
        }
        
        return maxValue;
    }

    private float GetValueFromStat(DailyStatistics stat, ChartType chartType)
    {
        switch (chartType)
        {
            case ChartType.SpawnCount:
                return stat.spawnCount;
            case ChartType.GoldEarned:
                return stat.goldEarned;
            case ChartType.ReputationGained:
                return stat.reputationGained;
            default:
                return 0f;
        }
    }

    private void DrawLine(List<DailyStatistics> data, float maxValue, ChartType chartType)
    {
        if (data.Count < 2) return;

        // 라인 렌더러 생성
        GameObject lineObj = new GameObject("ChartLine");
        lineObj.transform.SetParent(lineContainer);
        
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.color = lineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = data.Count;
        lineRenderer.useWorldSpace = false;

        // 라인 포인트 위치 계산
        Vector3[] linePositions = new Vector3[data.Count];
        for (int i = 0; i < data.Count; i++)
        {
            float normalizedValue = GetValueFromStat(data[i], chartType) / maxValue;
            float x = (float)i / (data.Count - 1) * chartContainer.rect.width;
            float y = normalizedValue * chartContainer.rect.height;
            
            linePositions[i] = new Vector3(x, y, 0);
        }
        
        lineRenderer.SetPositions(linePositions);
        chartElements.Add(lineObj);
    }

    private void DrawPoints(List<DailyStatistics> data, float maxValue, ChartType chartType)
    {
        for (int i = 0; i < data.Count; i++)
        {
            GameObject pointObj = new GameObject($"Point_{i}");
            pointObj.transform.SetParent(pointContainer);
            
            // 포인트 이미지 생성
            Image pointImage = pointObj.AddComponent<Image>();
            pointImage.color = pointColor;
            
            // 포인트 크기 설정
            RectTransform pointRect = pointObj.GetComponent<RectTransform>();
            pointRect.sizeDelta = new Vector2(pointSize, pointSize);
            
            // 포인트 위치 계산
            float normalizedValue = GetValueFromStat(data[i], chartType) / maxValue;
            float x = (float)i / (data.Count - 1) * chartContainer.rect.width;
            float y = normalizedValue * chartContainer.rect.height;
            
            pointRect.anchoredPosition = new Vector2(x, y);
            
            // 툴팁 정보 추가
            AddTooltip(pointObj, data[i], chartType);
            
            chartElements.Add(pointObj);
        }
    }

    private void AddTooltip(GameObject pointObj, DailyStatistics stat, ChartType chartType)
    {
        // 툴팁 컴포넌트 추가 (간단한 버전)
        var tooltip = pointObj.AddComponent<ChartPointTooltip>();
        tooltip.Initialize(stat, chartType);
    }

    private void ClearChart()
    {
        foreach (var element in chartElements)
        {
            if (element != null)
            {
                DestroyImmediate(element);
            }
        }
        chartElements.Clear();
    }

    /// <summary>
    /// 차트 색상을 변경합니다.
    /// </summary>
    public void SetChartColors(Color line, Color point, Color background)
    {
        lineColor = line;
        pointColor = point;
        backgroundColor = background;
        
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }
    }

    /// <summary>
    /// 차트 크기를 조정합니다.
    /// </summary>
    public void SetChartSize(Vector2 size)
    {
        if (chartContainer != null)
        {
            chartContainer.sizeDelta = size;
        }
    }

    private void OnDestroy()
    {
        ClearChart();
    }
}

/// <summary>
/// 차트 포인트에 마우스 오버 시 툴팁을 표시하는 컴포넌트
/// </summary>
public class ChartPointTooltip : MonoBehaviour
{
    private DailyStatistics statistics;
    private ChartRenderer.ChartType chartType;
    private bool isHovered = false;

    public void Initialize(DailyStatistics stat, ChartRenderer.ChartType type)
    {
        statistics = stat;
        chartType = type;
    }

    private void OnMouseEnter()
    {
        isHovered = true;
        ShowTooltip();
    }

    private void OnMouseExit()
    {
        isHovered = false;
        HideTooltip();
    }

    private void ShowTooltip()
    {
        string tooltipText = $"Day {statistics.day}\n";
        
        switch (chartType)
        {
            case ChartRenderer.ChartType.SpawnCount:
                tooltipText += $"Spawn Count: {statistics.spawnCount}";
                break;
            case ChartRenderer.ChartType.GoldEarned:
                tooltipText += $"Gold Earned: {statistics.goldEarned}";
                break;
            case ChartRenderer.ChartType.ReputationGained:
                tooltipText += $"Reputation: {statistics.reputationGained}";
                break;
        }
        
        Debug.Log($"Tooltip: {tooltipText}");
        // 실제 구현에서는 UI 툴팁을 표시해야 함
    }

    private void HideTooltip()
    {
        // 툴팁 숨기기
    }
}
