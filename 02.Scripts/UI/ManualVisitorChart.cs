using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 수동 UI 생성 방식의 방문객 점선 그래프 시스템
/// 일차별 총 방문객 수를 점선 그래프로 표시하며 스크롤 기능을 제공합니다.
/// </summary>
public class ManualVisitorChart : MonoBehaviour
{
    [Header("UI 패널")]
    [SerializeField] private GameObject chartPanel;
    [SerializeField] private Button toggleButton;
    [SerializeField] private TextMeshProUGUI toggleButtonText;
    
    [Header("그래프 영역")]
    [SerializeField] private RectTransform chartContainer;
    [SerializeField] private RectTransform contentArea;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Scrollbar horizontalScrollbar;
    
    [Header("그래프 설정")]
    [SerializeField] private float containerWidth = 1720f;   // 모든 컨테이너 통일 너비
    [SerializeField] private float containerHeight = 880f;   // 모든 컨테이너 통일 높이
    [SerializeField] private float chartMarginLeft = 100f;   // 왼쪽 여백 (Y축 라벨 공간)
    [SerializeField] private float chartMarginRight = 50f;   // 오른쪽 여백
    [SerializeField] private float chartMarginTop = 80f;     // 위쪽 여백 (포인트 위 숫자 공간)
    [SerializeField] private float chartMarginBottom = 80f;  // 아래쪽 여백 (일차 라벨 공간)
    [SerializeField] private float daySpacing = 120f;        // 일차별 간격
    
    
    [Header("포인트 설정")]
    [SerializeField] private Color pointColor = Color.red;
    [SerializeField] private float pointSize = 20f; // 큰 화면에 맞게 증가
    
    [Header("그리드 설정")]
    [SerializeField] private bool showGrid = true;
    [SerializeField] private Material gridMaterial;
    [SerializeField] private Color gridColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private float gridLineWidth = 1f;
    
    [Header("라벨 설정")]
    [SerializeField] private RectTransform dayLabelsContainer;
    [SerializeField] private RectTransform visitorLabelsContainer;
    [SerializeField] private RectTransform visitorValueLabelsContainer; // 점 위에 표시될 방문객 수 라벨
    
    [Header("디버그")]
    [SerializeField] private bool showDebugLogs = true;
    
    // 데이터 관리
    private List<DailyData> visitorData = new List<DailyData>();
    private Dictionary<int, ChartPoint> chartPoints = new Dictionary<int, ChartPoint>();
    private List<GameObject> dayLabels = new List<GameObject>();
    private List<GameObject> visitorLabels = new List<GameObject>();
    private List<GameObject> visitorValueLabels = new List<GameObject>(); // 점 위 방문객 수 라벨들
    private LineRenderer gridRenderer;
    
    // 상태 관리
    private bool isPanelOpen = false;
    private float maxVisitors = 10f; // 동적으로 계산될 최대값
    private int totalDays = 0;
    
    // 실제 차트 영역 계산 속성들
    private float ActualChartWidth => containerWidth - chartMarginLeft - chartMarginRight;
    private float ActualChartHeight => containerHeight - chartMarginTop - chartMarginBottom;
    private float ChartStartX => chartMarginLeft;
    private float ChartStartY => chartMarginBottom;
    private float ChartEndX => containerWidth - chartMarginRight;
    private float ChartEndY => containerHeight - chartMarginTop;
    
    // 차트 포인트 정보 클래스
    [System.Serializable]
    public class ChartPoint
    {
        public int day;
        public int visitors;
        public Vector2 position;
        public GameObject pointObject;
        public GameObject dayLabel;
        
        public ChartPoint(int day, int visitors, Vector2 position)
        {
            this.day = day;
            this.visitors = visitors;
            this.position = position;
        }
    }
    
    #region Unity Lifecycle
    
    private void Start()
    {
        SetupEvents();
        InitializeChart();
    }
    
    private void OnDestroy()
    {
        UnsubscribeEvents();
    }
    
    #endregion
    
    #region Setup & Initialization
    
    private void SetupEvents()
    {
        // 차트 패널 초기 상태 설정
        if (chartPanel != null)
        {
            chartPanel.SetActive(false);
            isPanelOpen = false;
        }
        
        // 토글 버튼 텍스트 설정
        if (toggleButtonText != null)
        {
            toggleButtonText.text = "Open Visitor Chart";
        }
        
        // 통계 매니저 이벤트 구독
        if (DailyStatisticsManager.Instance != null)
        {
            DailyStatisticsManager.OnDailyDataUpdated += OnDailyDataUpdated;
            DailyStatisticsManager.OnDayReset += OnDayReset;
        }
        
        // 토글 버튼 이벤트
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(TogglePanel);
        }
        
        // 스크롤바 이벤트
        if (horizontalScrollbar != null)
        {
            horizontalScrollbar.onValueChanged.AddListener(OnScrollbarChanged);
        }
    }
    
    private void UnsubscribeEvents()
    {
        if (DailyStatisticsManager.Instance != null)
        {
            DailyStatisticsManager.OnDailyDataUpdated -= OnDailyDataUpdated;
            DailyStatisticsManager.OnDayReset -= OnDayReset;
        }
        
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(TogglePanel);
        }
        
        if (horizontalScrollbar != null)
        {
            horizontalScrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
        }
    }
    
    private void InitializeChart()
    {
        // 그리드 머티리얼 생성
        if (gridMaterial == null)
        {
            gridMaterial = new Material(Shader.Find("UI/Default"));
            gridMaterial.color = gridColor;
        }
        
        // 그리드 생성
        if (showGrid)
        {
            CreateGrid();
        }
        
        DebugLog("차트 초기화 완료");
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnDailyDataUpdated(DailyData dailyData)
    {
        AddOrUpdateDataPoint(dailyData);
        DebugLog($"데이터 업데이트: Day {dailyData.day}, Visitors: {dailyData.totalVisitors}");
    }
    
    private void OnDayReset()
    {
        RefreshChart();
        DebugLog("새 날 시작 - 차트 갱신");
    }
    
    private void OnScrollbarChanged(float value)
    {
        // 스크롤 위치에 따른 추가 처리가 필요하면 여기에 구현
        DebugLog($"스크롤 위치 변경: {value:F2}");
    }
    
    #endregion
    
    #region Chart Management
    
    private void AddOrUpdateDataPoint(DailyData data)
    {
        // 기존 데이터 업데이트 또는 새 데이터 추가
        var existingData = visitorData.Find(d => d.day == data.day);
        if (existingData != null)
        {
            existingData.totalVisitors = data.totalVisitors;
        }
        else
        {
            visitorData.Add(new DailyData(data.day, data.reputationGained, data.goldEarned, 
                                        data.totalVisitors, data.startingReputation, data.startingGold,
                                        data.endingReputation, data.endingGold));
        }
        
        // 데이터 정렬
        visitorData.Sort((a, b) => a.day.CompareTo(b.day));
        
        // 차트 업데이트
        RefreshChart();
    }
    
    private void RefreshChart()
    {
        if (!isPanelOpen || visitorData.Count == 0) return;
        
        // 최대값 계산
        CalculateMaxVisitors();
        
        // 컨텐츠 영역 크기 조정
        UpdateContentSize();
        
        // 기존 차트 요소들 정리
        ClearChart();
        
        // 새 차트 요소들 생성
        CreateChartPoints();
        CreateDayLabels();
        CreateVisitorValueLabels(); // 각 포인트 위에 방문객 수 표시
        CreateVisitorLabels(); // Y축 라벨을 데이터에 맞춰서 동적 생성
        
        // 그리드 업데이트
        if (showGrid)
        {
            UpdateGrid();
        }
    }
    
    private void CalculateMaxVisitors()
    {
        float actualMaxVisitors = 0f;
        
        // 실제 최대 방문객 수 찾기
        foreach (var data in visitorData)
        {
            if (data.totalVisitors > actualMaxVisitors)
            {
                actualMaxVisitors = data.totalVisitors;
            }
        }
        
        // 10단위로 반올림하여 Y축 최대값 설정
        // 예: 23명 -> 30, 45명 -> 50, 67명 -> 70
        maxVisitors = Mathf.Ceil(actualMaxVisitors / 10f) * 10f;
        
        // 최소값 보장 (최소 10)
        if (maxVisitors < 10f)
        {
            maxVisitors = 10f;
        }
        
        DebugLog($"최대 방문객 수 계산: 실제={actualMaxVisitors}, 스케일={maxVisitors}");
    }
    
    private void UpdateContentSize()
    {
        totalDays = visitorData.Count;
        // 고정 간격 기반으로 필요한 너비 계산
        float requiredWidth = chartMarginLeft + chartMarginRight;
        if (totalDays > 0)
        {
            requiredWidth += (totalDays - 1) * daySpacing + daySpacing; // 마지막 포인트 여유 공간
        }
        
        // 최소 컨테이너 크기는 보장
        requiredWidth = Mathf.Max(containerWidth, requiredWidth);
        
        // Content 영역을 스크롤에 필요한 너비로 설정 (라벨들이 자식이므로 함께 스크롤됨)
        if (contentArea != null)
        {
            contentArea.sizeDelta = new Vector2(requiredWidth, containerHeight);
        }
        
        // Y축 라벨은 고정되어야 하므로 고정 크기 유지
        if (visitorLabelsContainer != null)
        {
            visitorLabelsContainer.sizeDelta = new Vector2(containerWidth, containerHeight);
        }
        
        DebugLog($"컨테이너 크기 설정: Content영역={requiredWidth}x{containerHeight} (고정간격: {daySpacing}), Y축고정={containerWidth}x{containerHeight}");
    }
    
    private void ClearChart()
    {
        // 차트 포인트들 정리
        foreach (var kvp in chartPoints)
        {
            if (kvp.Value.pointObject != null)
            {
                DestroyImmediate(kvp.Value.pointObject);
            }
        }
        chartPoints.Clear();
        
        
        
        // 일차 라벨들 정리
        foreach (var label in dayLabels)
        {
            if (label != null)
            {
                DestroyImmediate(label);
            }
        }
        dayLabels.Clear();
        
        // 방문객 수 라벨들 정리
        foreach (var label in visitorValueLabels)
        {
            if (label != null)
            {
                DestroyImmediate(label);
            }
        }
        visitorValueLabels.Clear();
        
        // Y축 방문객 라벨들 정리
        foreach (var label in visitorLabels)
        {
            if (label != null)
            {
                DestroyImmediate(label);
            }
        }
        visitorLabels.Clear();
    }
    
    private void CreateChartPoints()
    {
        for (int i = 0; i < visitorData.Count; i++)
        {
            var data = visitorData[i];
            
            // X 좌표 계산 (고정된 daySpacing 간격으로)
            float x = ChartStartX + i * daySpacing;
            
            // Y 좌표 계산 (0~maxVisitors를 차트 높이에 매핑)
            float normalizedY = data.totalVisitors / maxVisitors; // 0~1로 정규화
            float y = ChartStartY + normalizedY * ActualChartHeight;
            
            Vector2 position = new Vector2(x, y);
            
            // 차트 포인트 생성
            ChartPoint chartPoint = new ChartPoint(data.day, data.totalVisitors, position);
            chartPoint.pointObject = CreatePointObject(position, data);
            
            chartPoints[data.day] = chartPoint;
        }
        
        DebugLog($"차트 포인트 생성 완료: {chartPoints.Count}개 (간격: {daySpacing}, 영역: {ActualChartWidth}x{ActualChartHeight})");
    }
    
    
    private GameObject CreatePointObject(Vector2 position, DailyData data)
    {
        // 기본 포인트 오브젝트 생성
        GameObject pointObj = new GameObject($"Point_Day_{data.day}");
        pointObj.transform.SetParent(contentArea);
        
        // 이미지 컴포넌트 추가
        var image = pointObj.AddComponent<Image>();
        image.color = pointColor;
        
        // 원형 스프라이트 생성
        image.sprite = CreateCircleSprite();
        
        // RectTransform 설정
        var rectTransform = pointObj.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = pointObj.AddComponent<RectTransform>();
        }
        
        rectTransform.sizeDelta = new Vector2(pointSize, pointSize);
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.anchoredPosition = position;
        
        return pointObj;
    }
    
    
    
    private void CreateGrid()
    {
        GameObject gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(contentArea);
        
        gridRenderer = gridObj.AddComponent<LineRenderer>();
        gridRenderer.material = gridMaterial;
        gridRenderer.startWidth = gridLineWidth;
        gridRenderer.endWidth = gridLineWidth;
        gridRenderer.useWorldSpace = false;
        gridRenderer.sortingOrder = 0;
        
        UpdateGrid();
    }
    
    private void UpdateGrid()
    {
        if (gridRenderer == null) return;
        
        List<Vector3> gridPoints = new List<Vector3>();
        
        // 수평 그리드 라인 (Y축 값과 정확히 맞춤)
        int labelInterval = 10;
        int labelCount = Mathf.RoundToInt(maxVisitors / labelInterval);
        
        for (int i = 0; i <= labelCount; i++)
        {
            int value = i * labelInterval;
            float normalizedY = value / maxVisitors;
            float y = ChartStartY + normalizedY * ActualChartHeight;
            
            gridPoints.Add(new Vector3(ChartStartX, y, 0));
            gridPoints.Add(new Vector3(ChartEndX, y, 0));
        }
        
        // 수직 그리드 라인 (포인트 X좌표와 정확히 맞춤)
        foreach (var kvp in chartPoints)
        {
            var chartPoint = kvp.Value;
            float x = chartPoint.position.x;
            
            gridPoints.Add(new Vector3(x, ChartStartY, 0));
            gridPoints.Add(new Vector3(x, ChartEndY, 0));
        }
        
        gridRenderer.positionCount = gridPoints.Count;
        gridRenderer.SetPositions(gridPoints.ToArray());
    }
    
    private void CreateDayLabels()
    {
        if (dayLabelsContainer == null) return;
        
        foreach (var kvp in chartPoints)
        {
            var chartPoint = kvp.Value;
            // 포인트 X좌표와 정확히 맞춤, 차트 하단 아래에 배치
            Vector2 labelPosition = new Vector2(chartPoint.position.x, ChartStartY - 30);
            
            GameObject dayLabel = CreateDayLabel(chartPoint.day, labelPosition);
            dayLabels.Add(dayLabel);
        }
        
        DebugLog($"일차 라벨 생성 완료: {dayLabels.Count}개");
    }
    
    private GameObject CreateDayLabel(int day, Vector2 position)
    {
        GameObject labelObj = new GameObject($"DayLabel_{day}");
        labelObj.transform.SetParent(contentArea); // 스크롤되도록 contentArea의 자식으로 설정
        
        var text = labelObj.AddComponent<TextMeshProUGUI>();
        text.text = $"{day}Day";
        text.fontSize = 14; // 큰 화면에 맞게 증가
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        
        var rectTransform = labelObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(60, 20);
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.anchoredPosition = position;
        
        return labelObj;
    }
    
    private void CreateVisitorLabels()
    {
        if (visitorLabelsContainer == null) return;
        
        // 실제 데이터에 맞춰 동적으로 라벨 생성 (0부터 maxVisitors까지)
        int labelInterval = 10;
        int labelCount = Mathf.RoundToInt(maxVisitors / labelInterval);
        
        for (int i = 0; i <= labelCount; i++)
        {
            int value = i * labelInterval;
            float normalizedY = value / maxVisitors; // 0~1로 정규화
            // Y축 라벨이 차트와 정확히 맞도록 계산
            float y = ChartStartY + normalizedY * ActualChartHeight;
            
            GameObject visitorLabel = CreateVisitorLabel(value, y);
            visitorLabels.Add(visitorLabel);
        }
        
        DebugLog($"Y축 라벨 동적 생성 완료: {visitorLabels.Count}개 (0~{maxVisitors}, 간격: {labelInterval})");
    }
    
    private GameObject CreateVisitorLabel(int visitors, float y)
    {
        GameObject labelObj = new GameObject($"VisitorLabel_{visitors}");
        labelObj.transform.SetParent(visitorLabelsContainer);
        
        var text = labelObj.AddComponent<TextMeshProUGUI>();
        text.text = visitors.ToString();
        text.fontSize = 16; // 큰 화면에 맞게 증가
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        
        var rectTransform = labelObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(80, 20);
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        // Y축 라벨을 차트 왼쪽에 가운데 정렬로 배치
        rectTransform.anchoredPosition = new Vector2(chartMarginLeft / 2, y);
        
        return labelObj;
    }
    
    private void CreateVisitorValueLabels()
    {
        if (visitorValueLabelsContainer == null) return;
        
        foreach (var kvp in chartPoints)
        {
            var chartPoint = kvp.Value;
            // 포인트 정확히 위에 방문객 수 표시
            Vector2 labelPosition = new Vector2(chartPoint.position.x, chartPoint.position.y + 25);
            
            GameObject valueLabel = CreateVisitorValueLabel(chartPoint.visitors, labelPosition);
            visitorValueLabels.Add(valueLabel);
        }
        
        DebugLog($"방문객 수 값 라벨 생성 완료: {visitorValueLabels.Count}개");
    }
    
    private GameObject CreateVisitorValueLabel(int visitors, Vector2 position)
    {
        GameObject labelObj = new GameObject($"VisitorValueLabel_{visitors}");
        labelObj.transform.SetParent(contentArea); // 스크롤되도록 contentArea의 자식으로 설정
        
        var text = labelObj.AddComponent<TextMeshProUGUI>();
        text.text = visitors.ToString();
        text.fontSize = 14; // 큰 화면에 맞게 증가
        text.color = Color.yellow;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        
        var rectTransform = labelObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(30, 15);
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.anchoredPosition = position;
        
        return labelObj;
    }
    
    #endregion
    
    #region UI Control
    
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
    
    public void OpenPanel()
    {
        if (chartPanel != null)
        {
            chartPanel.SetActive(true);
            isPanelOpen = true;
            
            if (toggleButtonText != null)
            {
                toggleButtonText.text = "Close Visitor Chart";
            }
            
            RefreshChart();
        }
    }
    
    public void ClosePanel()
    {
        if (chartPanel != null)
        {
            chartPanel.SetActive(false);
            isPanelOpen = false;
            
            if (toggleButtonText != null)
            {
                toggleButtonText.text = "Open Visitor Chart";
            }
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    private Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;
        
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                
                if (distance <= radius)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    #endregion
    
    #region Debug
    
    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ManualVisitorChart] {message}");
        }
    }
    
    #endregion
}
