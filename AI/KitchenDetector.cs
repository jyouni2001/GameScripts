using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 주방 자동 감지 시스템
    /// 주방_카운터, 주방_인덕션, 주방_테이블 태그를 분석하여 주방을 자동으로 인식하고 생성
    /// </summary>
    public class KitchenDetector : MonoBehaviour
    {
        // 싱글톤 패턴
        public static KitchenDetector Instance { get; private set; }
        
        [Header("기본 설정")]
        [Tooltip("주방 감지 활성화")]
        [SerializeField] private bool enableKitchenDetection = true;
        
        [Tooltip("PlacementSystem 참조")]
        [SerializeField] private PlacementSystem placementSystem;
        
        [Header("주방 인식 조건")]
        [Tooltip("주방으로 인식하기 위한 최소 카운터 개수")]
        [Range(1, 10)]
        [SerializeField] private int minCounters = 1;
        
        [Tooltip("주방으로 인식하기 위한 최소 인덕션 개수")]
        [Range(1, 10)]
        [SerializeField] private int minInductions = 1;
        
        [Tooltip("주방으로 인식하기 위한 최소 테이블 개수")]
        [Range(1, 10)]
        [SerializeField] private int minTables = 1; // 테이블도 필수
        
        [Header("스캔 설정")]
        [Tooltip("주방 요소들을 그룹핑할 최대 거리")]
        [Range(1f, 30f)]
        [SerializeField] private float maxGroupingDistance = 15f;
        
        [Tooltip("주방 인식 범위 확장 (의자 등 주변 요소 포함용)")]
        [Range(0.5f, 10f)]
        [SerializeField] private float kitchenBoundsExpansion = 5f;
        
        [Tooltip("자동 주기적 스캔 활성화")]
        [SerializeField] private bool enablePeriodicScan = true;
        
        [Tooltip("주방 스캔 주기 (초)")]
        [Range(1f, 10f)]
        [SerializeField] private float scanInterval = 3f;
        
        [Header("층별 감지 설정")]
        
        [Tooltip("모든 층을 스캔할지 여부")]
        [SerializeField] private bool scanAllFloors = true;
        
        [Tooltip("현재 스캔할 층 번호 (scanAllFloors가 false일 때)")]
        [Range(1, 10)]
        [SerializeField] private int currentScanFloor = 1;
        
        [Header("주방 생성 설정")]
        [Tooltip("감지된 주방에 대해 실제 GameObject 생성")]
        [SerializeField] private bool createKitchenGameObjects = true;
        
        [Tooltip("생성된 주방 GameObject들의 부모 오브젝트")]
        [SerializeField] private Transform kitchenParent;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = true;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = false;
        
        [Header("현재 상태")]
        [Tooltip("현재 감지된 주방의 개수")]
        [SerializeField] private int detectedKitchenCount = 0;
        
        [Tooltip("현재 스캔 상태 정보")]
        [SerializeField] private string currentScanStatus = "초기화 중...";
        
        // 프라이빗 변수들
        private List<KitchenInfo> detectedKitchens = new List<KitchenInfo>();
        private List<GameObject> createdKitchenObjects = new List<GameObject>();
        private bool isScanning = false;
        
        // 태그 상수들 (영어)
        private const string KITCHEN_COUNTER_TAG = "KitchenCounter";
        private const string KITCHEN_INDUCTION_TAG = "KitchenInduction";
        private const string KITCHEN_TABLE_TAG = "KitchenTable";
        
        #region Unity 생명주기
        
        void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        void Start()
        {
            InitializeKitchenDetector();
        }
        
        void OnDestroy()
        {
            // InvokeRepeating 정리
            CancelInvoke();
            
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        #endregion
        
        #region 초기화
        
        /// <summary>
        /// 주방 감지기 초기화
        /// </summary>
        private void InitializeKitchenDetector()
        {
            DebugLog("주방 감지 시스템 초기화 중...", true);
            currentScanStatus = "준비 완료";
            
            // PlacementSystem 자동 찾기
            if (placementSystem == null)
            {
                placementSystem = FindFirstObjectByType<PlacementSystem>();
            }
            
            // PlacementSystem 이벤트 구독
            if (placementSystem != null)
            {
                SubscribeToPlacementEvents();
                DebugLog("PlacementSystem과 연동 완료", true);
            }
            else
            {
                DebugLog("⚠️ PlacementSystem을 찾을 수 없습니다!", true);
            }
            
            // 초기 스캔 실행
            if (enableKitchenDetection)
            {
                ScanForKitchens();
                
                // 주기적 스캔 시작 (RoomDetector와 동일한 방식)
                if (enablePeriodicScan)
                {
                    InvokeRepeating(nameof(ScanForKitchens), scanInterval, scanInterval);
                    DebugLog($"주기적 스캔 시작: {scanInterval}초마다", true);
                }
            }
        }
        
        /// <summary>
        /// PlacementSystem 이벤트 구독
        /// </summary>
        private void SubscribeToPlacementEvents()
        {
            // PlacementSystem의 건설 완료 이벤트가 있다면 구독
            // 예: placementSystem.OnObjectPlaced += OnObjectPlaced;
            // 예: placementSystem.OnObjectRemoved += OnObjectRemoved;
            
            DebugLog("PlacementSystem 이벤트 구독 완료");
        }
        
        /// <summary>
        /// 가구 배치 시 호출되는 메서드 (WorkPositionManager와 동일한 방식)
        /// </summary>
        public void OnFurnitureePlaced(GameObject placedObject, Vector3 worldPosition)
        {
            if (!enableKitchenDetection) 
            {
                DebugLog("주방 감지 비활성화됨", true);
                return;
            }
            
            DebugLog($"🔨 가구 배치 감지: {placedObject.name} (태그: {placedObject.tag}) at {worldPosition}", true);
            
            // 주방 관련 태그인지 확인
            if (IsKitchenRelatedTag(placedObject.tag))
            {
                DebugLog($"🍳 주방 요소 배치 감지 확인! {placedObject.name} ({placedObject.tag})", true);
                
                // 약간의 딜레이 후 스캔 (배치 완료 대기)
                Invoke(nameof(ScanForKitchens), 0.1f);
            }
            else
            {
                DebugLog($"❌ 주방 관련 태그가 아님: {placedObject.tag} (필요: {KITCHEN_COUNTER_TAG}, {KITCHEN_INDUCTION_TAG}, {KITCHEN_TABLE_TAG})", true);
            }
        }
        
        /// <summary>
        /// 가구 제거 시 호출되는 메서드
        /// </summary>
        public void OnFurnitureRemoved(GameObject removedObject, Vector3 worldPosition)
        {
            if (!enableKitchenDetection) 
            {
                DebugLog("주방 감지 비활성화됨", true);
                return;
            }
            
            DebugLog($"🗑️ 가구 제거 감지: {removedObject.name} (태그: {removedObject.tag}) at {worldPosition}", true);
            
            // 주방 관련 태그인지 확인
            if (IsKitchenRelatedTag(removedObject.tag))
            {
                DebugLog($"🍳 주방 요소 제거 감지 확인! {removedObject.name} ({removedObject.tag})", true);
                
                // 약간의 딜레이 후 스캔 (제거 완료 대기)
                Invoke(nameof(ScanForKitchens), 0.1f);
            }
            else
            {
                DebugLog($"❌ 주방 관련 태그가 아님: {removedObject.tag} (필요: {KITCHEN_COUNTER_TAG}, {KITCHEN_INDUCTION_TAG}, {KITCHEN_TABLE_TAG})", true);
            }
        }
        
        /// <summary>
        /// 주방 관련 태그인지 확인
        /// </summary>
        private bool IsKitchenRelatedTag(string tag)
        {
            return tag == KITCHEN_COUNTER_TAG || 
                   tag == KITCHEN_INDUCTION_TAG || 
                   tag == KITCHEN_TABLE_TAG;
        }
        
        #endregion
        
        #region 주방 스캔
        
        /// <summary>
        /// 수동 주방 스캔 (Inspector 컨텍스트 메뉴)
        /// </summary>
        [ContextMenu("수동 주방 스캔")]
        public void ManualScanKitchens()
        {
            DebugLog("=== 수동 주방 스캔 시작 ===", true);
            ScanForKitchens();
        }
        
        /// <summary>
        /// 주방 스캔 실행
        /// </summary>
        public void ScanForKitchens()
        {
            if (isScanning) return;
            
            isScanning = true;
            currentScanStatus = "스캔 중...";
            
            DebugLog("🔍 주방 스캔 시작", true);
            
            try
            {
                // 기존 주방 GameObject들 정리
                CleanupOldKitchens();
                
                // 기존 주방 정보 초기화
                detectedKitchens.Clear();
                
                // 주방 요소들 수집
                var kitchenElements = CollectKitchenElements();
                
                // 층별로 그룹핑
                var floorGroups = GroupElementsByFloor(kitchenElements);
                
                // 각 층별로 주방 감지
                foreach (var floorGroup in floorGroups)
                {
                    DetectKitchensOnFloor(floorGroup.Key, floorGroup.Value);
                }
                
                // 결과 업데이트
                detectedKitchenCount = detectedKitchens.Count;
                currentScanStatus = $"완료 - {detectedKitchenCount}개 주방 감지";
                
                DebugLog($"✅ 주방 스캔 완료: {detectedKitchenCount}개 주방 감지됨", true);
                
                // 이벤트 발생 (필요시)
                OnKitchensDetected?.Invoke(detectedKitchens);
            }
            catch (System.Exception e)
            {
                DebugLog($"❌ 주방 스캔 중 오류 발생: {e.Message}", true);
                currentScanStatus = "오류 발생";
            }
            finally
            {
                isScanning = false;
            }
        }
        
        /// <summary>
        /// 씬에서 주방 요소들 수집
        /// </summary>
        private List<KitchenElement> CollectKitchenElements()
        {
            List<KitchenElement> elements = new List<KitchenElement>();
            
            // 카운터 수집
            var counters = GameObject.FindGameObjectsWithTag(KITCHEN_COUNTER_TAG);
            foreach (var counter in counters)
            {
                elements.Add(new KitchenElement
                {
                    gameObject = counter,
                    elementType = KitchenElementType.Counter,
                    position = counter.transform.position,
                    floorLevel = FloorConstants.GetFloorLevel(counter.transform.position.y)
                });
            }
            
            // 인덕션 수집
            var inductions = GameObject.FindGameObjectsWithTag(KITCHEN_INDUCTION_TAG);
            foreach (var induction in inductions)
            {
                elements.Add(new KitchenElement
                {
                    gameObject = induction,
                    elementType = KitchenElementType.Induction,
                    position = induction.transform.position,
                    floorLevel = FloorConstants.GetFloorLevel(induction.transform.position.y)
                });
            }
            
            // 테이블 수집
            var tables = GameObject.FindGameObjectsWithTag(KITCHEN_TABLE_TAG);
            foreach (var table in tables)
            {
                elements.Add(new KitchenElement
                {
                    gameObject = table,
                    elementType = KitchenElementType.Table,
                    position = table.transform.position,
                    floorLevel = FloorConstants.GetFloorLevel(table.transform.position.y)
                });
            }
            
            DebugLog($"주방 요소 수집 완료: 카운터 {counters.Length}개, 인덕션 {inductions.Length}개, 테이블 {tables.Length}개", true);
            
            return elements;
        }
        
        /// <summary>
        /// 주방 요소들을 층별로 그룹핑
        /// </summary>
        private Dictionary<int, List<KitchenElement>> GroupElementsByFloor(List<KitchenElement> elements)
        {
            var floorGroups = new Dictionary<int, List<KitchenElement>>();
            
            foreach (var element in elements)
            {
                // scanAllFloors가 false면 현재 층만 처리
                if (!scanAllFloors && element.floorLevel != currentScanFloor)
                    continue;
                
                if (!floorGroups.ContainsKey(element.floorLevel))
                {
                    floorGroups[element.floorLevel] = new List<KitchenElement>();
                }
                
                floorGroups[element.floorLevel].Add(element);
            }
            
            return floorGroups;
        }
        
        /// <summary>
        /// 특정 층에서 주방 감지
        /// </summary>
        private void DetectKitchensOnFloor(int floorLevel, List<KitchenElement> elements)
        {
            DebugLog($"🏢 {floorLevel}층 주방 감지 시작 (요소 {elements.Count}개)");
            
            // 거리 기반으로 요소들을 그룹핑
            var elementGroups = GroupElementsByProximity(elements);
            
            foreach (var group in elementGroups)
            {
                // 그룹이 주방 조건을 만족하는지 확인
                if (IsValidKitchen(group))
                {
                    var kitchen = CreateKitchenInfo(floorLevel, group);
                    detectedKitchens.Add(kitchen);
                    
                    DebugLog($"✅ {floorLevel}층에서 주방 감지됨: {kitchen.kitchenName}", true);
                    
                    // 주방 GameObject 생성
                    if (createKitchenGameObjects)
                    {
                        CreateKitchenGameObject(kitchen);
                    }
                }
            }
        }
        
        /// <summary>
        /// 거리 기반으로 요소들을 그룹핑
        /// </summary>
        private List<List<KitchenElement>> GroupElementsByProximity(List<KitchenElement> elements)
        {
            var groups = new List<List<KitchenElement>>();
            var processed = new HashSet<KitchenElement>();
            
            foreach (var element in elements)
            {
                if (processed.Contains(element)) continue;
                
                var group = new List<KitchenElement> { element };
                processed.Add(element);
                
                // 현재 요소 주변의 다른 요소들 찾기
                FindNearbyElements(element, elements, group, processed);
                
                groups.Add(group);
            }
            
            return groups;
        }
        
        /// <summary>
        /// 주변 요소들을 재귀적으로 찾기
        /// </summary>
        private void FindNearbyElements(KitchenElement center, List<KitchenElement> allElements, 
                                      List<KitchenElement> currentGroup, HashSet<KitchenElement> processed)
        {
            foreach (var element in allElements)
            {
                if (processed.Contains(element)) continue;
                
                // 같은 층이고 거리가 가까운 요소들만
                if (element.floorLevel == center.floorLevel && 
                    Vector3.Distance(center.position, element.position) <= maxGroupingDistance)
                {
                    currentGroup.Add(element);
                    processed.Add(element);
                    
                    // 재귀적으로 더 찾기
                    FindNearbyElements(element, allElements, currentGroup, processed);
                }
            }
        }
        
        /// <summary>
        /// 그룹이 유효한 주방인지 확인
        /// </summary>
        private bool IsValidKitchen(List<KitchenElement> group)
        {
            int counterCount = group.Count(e => e.elementType == KitchenElementType.Counter);
            int inductionCount = group.Count(e => e.elementType == KitchenElementType.Induction);
            int tableCount = group.Count(e => e.elementType == KitchenElementType.Table);
            
            bool isValid = counterCount >= minCounters && 
                          inductionCount >= minInductions && 
                          tableCount >= minTables;
            
            DebugLog($"주방 유효성 검사: 카운터 {counterCount}/{minCounters}, 인덕션 {inductionCount}/{minInductions}, 테이블 {tableCount}/{minTables} → {(isValid ? "유효" : "무효")}");
            
            return isValid;
        }
        
        /// <summary>
        /// 주방 정보 생성
        /// </summary>
        private KitchenInfo CreateKitchenInfo(int floorLevel, List<KitchenElement> elements)
        {
            // 주방 중심점 계산
            Vector3 center = Vector3.zero;
            foreach (var element in elements)
            {
                center += element.position;
            }
            center /= elements.Count;
            
            // 주방 경계 계산
            Bounds bounds = CalculateKitchenBounds(elements);
            
            var kitchen = new KitchenInfo
            {
                kitchenName = $"주방_{floorLevel}층_{detectedKitchens.Count + 1}",
                floorLevel = floorLevel,
                centerPosition = center,
                bounds = FloorConstants.GetFloorBounds(floorLevel, bounds),
                elements = new List<KitchenElement>(elements),
                counterCount = elements.Count(e => e.elementType == KitchenElementType.Counter),
                inductionCount = elements.Count(e => e.elementType == KitchenElementType.Induction),
                tableCount = elements.Count(e => e.elementType == KitchenElementType.Table)
            };
            
            return kitchen;
        }
        
        /// <summary>
        /// 주방 경계 계산 (의자 등 주변 요소를 포함하도록 확장)
        /// </summary>
        private Bounds CalculateKitchenBounds(List<KitchenElement> elements)
        {
            if (elements.Count == 0) return new Bounds();
            
            Vector3 min = elements[0].position;
            Vector3 max = elements[0].position;
            
            foreach (var element in elements)
            {
                min = Vector3.Min(min, element.position);
                max = Vector3.Max(max, element.position);
            }
            
            // 주방 범위 확장 (의자, 테이블 주변 공간 포함)
            Vector3 expansion = Vector3.one * kitchenBoundsExpansion;
            expansion.y = 0.5f; // Y축은 적게 확장 (층 구분 유지)
            
            min -= expansion;
            max += expansion;
            
            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            
            DebugLog($"주방 경계 계산 완료: 크기 {bounds.size} (확장값: {kitchenBoundsExpansion})");
            
            return bounds;
        }
        
        #endregion
        
        #region 주방 GameObject 관리
        
        /// <summary>
        /// 기존 주방 GameObject들 정리
        /// </summary>
        private void CleanupOldKitchens()
        {
            foreach (var kitchenObj in createdKitchenObjects)
            {
                if (kitchenObj != null)
                {
                    DestroyImmediate(kitchenObj);
                }
            }
            createdKitchenObjects.Clear();
            
            DebugLog("기존 주방 GameObject들 정리 완료");
        }
        
        /// <summary>
        /// 주방 GameObject 생성
        /// </summary>
        private void CreateKitchenGameObject(KitchenInfo kitchen)
        {
            // 주방 GameObject 생성
            string kitchenName = $"Kitchen_F{kitchen.floorLevel}_{kitchen.centerPosition.x:F0}_{kitchen.centerPosition.z:F0}";
            GameObject kitchenObj = new GameObject(kitchenName);
            
            // 위치 설정
            kitchenObj.transform.position = kitchen.centerPosition;
            
            // 태그 설정
            kitchenObj.tag = "Kitchen";
            
            // 부모 설정
            if (kitchenParent != null)
            {
                kitchenObj.transform.SetParent(kitchenParent);
            }
            
            // BoxCollider 추가 (주방 영역 표시)
            BoxCollider collider = kitchenObj.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.zero;
            collider.size = kitchen.bounds.size;
            
            // KitchenInfo를 GameObject에 저장
            var kitchenComponent = kitchenObj.AddComponent<KitchenComponent>();
            kitchenComponent.kitchenInfo = kitchen;
            
            // 생성된 GameObject 목록에 추가
            createdKitchenObjects.Add(kitchenObj);
            kitchen.gameObject = kitchenObj;
            
            DebugLog($"🏠 주방 GameObject 생성: {kitchenName} at {kitchen.centerPosition}", true);
        }
        
        #endregion
        
        #region 공개 메서드
        
        /// <summary>
        /// 감지된 주방 목록 반환
        /// </summary>
        public List<KitchenInfo> GetDetectedKitchens()
        {
            return new List<KitchenInfo>(detectedKitchens);
        }
        
        /// <summary>
        /// 특정 층의 주방들 반환
        /// </summary>
        public List<KitchenInfo> GetKitchensOnFloor(int floorLevel)
        {
            return detectedKitchens.Where(k => k.floorLevel == floorLevel).ToList();
        }
        
        /// <summary>
        /// 특정 위치가 주방 영역 내에 있는지 확인
        /// </summary>
        public bool IsInKitchenArea(Vector3 position)
        {
            foreach (var kitchen in detectedKitchens)
            {
                if (kitchen.bounds.Contains(position))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 가장 가까운 주방 찾기
        /// </summary>
        public KitchenInfo GetNearestKitchen(Vector3 position)
        {
            if (detectedKitchens.Count == 0) return null;
            
            KitchenInfo nearest = null;
            float minDistance = float.MaxValue;
            
            foreach (var kitchen in detectedKitchens)
            {
                // 같은 층에 있는 주방만 고려
                if (FloorConstants.IsSameFloor(position.y, kitchen.centerPosition.y))
                {
                    float distance = Vector3.Distance(position, kitchen.centerPosition);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = kitchen;
                    }
                }
            }
            
            return nearest;
        }
        
        #endregion
        
        #region 이벤트
        
        /// <summary>
        /// 주방 감지 완료 시 발생하는 이벤트
        /// </summary>
        public System.Action<List<KitchenInfo>> OnKitchensDetected;
        
        #endregion
        
        #region 디버그
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            if (showImportantLogsOnly && !isImportant) return;
        }
        
        /// <summary>
        /// 기즈모로 주방 영역 표시 (확장된 영역 포함)
        /// </summary>
        void OnDrawGizmos()
        {
            if (detectedKitchens == null) return;
            
            foreach (var kitchen in detectedKitchens)
            {
                // 확장된 주방 경계 표시 (의자 포함 영역)
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(kitchen.bounds.center, kitchen.bounds.size);
                
                // 원래 주방 핵심 영역도 표시 (반투명)
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Vector3 coreSize = kitchen.bounds.size - Vector3.one * (kitchenBoundsExpansion * 2);
                coreSize.y = kitchen.bounds.size.y; // Y축은 원래 크기 유지
                Gizmos.DrawCube(kitchen.bounds.center, coreSize);
                
                // 주방 중심점 표시
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(kitchen.centerPosition, 0.3f);
                
                // 주방 요소들 표시
                foreach (var element in kitchen.elements)
                {
                    if (element.gameObject == null) continue;
                    
                    switch (element.elementType)
                    {
                        case KitchenElementType.Counter:
                            Gizmos.color = Color.blue;
                            break;
                        case KitchenElementType.Induction:
                            Gizmos.color = Color.red;
                            break;
                        case KitchenElementType.Table:
                            Gizmos.color = Color.green;
                            break;
                    }
                    
                    Gizmos.DrawWireCube(element.position, Vector3.one * 0.5f);
                }
                
                // 확장 범위 텍스트 표시
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(kitchen.centerPosition + Vector3.up * 2f, 
                    $"{kitchen.kitchenName}\n확장: +{kitchenBoundsExpansion}m");
                #endif
            }
        }
        
        #endregion
        
        #region 에디터 전용
        
        #if UNITY_EDITOR
        [ContextMenu("수동 주방 스캔")]
        private void EditorScanKitchens()
        {
            ScanForKitchens();
        }
        
        [ContextMenu("주방 정보 출력")]
        private void EditorPrintKitchenInfo()
        {
            for (int i = 0; i < detectedKitchens.Count; i++)
            {
                var kitchen = detectedKitchens[i];
            }
        }
        
        [ContextMenu("테스트 - 배치 이벤트")]
        private void EditorTestPlacementEvent()
        {
            // 테스트용: 임의의 주방 요소 배치 시뮬레이션
            var testObject = new GameObject("TestKitchenCounter");
            testObject.tag = KITCHEN_COUNTER_TAG;
            OnFurnitureePlaced(testObject, Vector3.zero);
            DestroyImmediate(testObject);
        }
        #endif
        
        #endregion
    }
    
    #region 데이터 클래스들
    
    /// <summary>
    /// 주방 정보 클래스
    /// </summary>
    [System.Serializable]
    public class KitchenInfo
    {
        public string kitchenName;
        public int floorLevel;
        public Vector3 centerPosition;
        public Bounds bounds;
        public List<KitchenElement> elements;
        public int counterCount;
        public int inductionCount;
        public int tableCount;
        public GameObject gameObject;  // 생성된 주방 GameObject
        
        public override string ToString()
        {
            return $"{kitchenName} ({floorLevel}층) - 카운터:{counterCount}, 인덕션:{inductionCount}, 테이블:{tableCount}";
        }
    }
    
    /// <summary>
    /// 주방 요소 클래스
    /// </summary>
    [System.Serializable]
    public class KitchenElement
    {
        public GameObject gameObject;
        public KitchenElementType elementType;
        public Vector3 position;
        public int floorLevel;
    }
    
    /// <summary>
    /// 주방 요소 타입
    /// </summary>
    public enum KitchenElementType
    {
        Counter,    // 카운터
        Induction,  // 인덕션
        Table       // 테이블
    }
    
    #endregion
}

/// <summary>
/// 주방 GameObject에 부착되는 컴포넌트
/// </summary>
public class KitchenComponent : MonoBehaviour
{
    [Tooltip("이 주방의 정보")]
    public JY.KitchenInfo kitchenInfo;
    
    /// <summary>
    /// 주방 내부에 특정 위치가 포함되는지 확인
    /// </summary>
    public bool ContainsPosition(Vector3 position)
    {
        return kitchenInfo != null && kitchenInfo.bounds.Contains(position);
    }
    
    /// <summary>
    /// 주방 정보를 문자열로 반환
    /// </summary>
    public override string ToString()
    {
        if (kitchenInfo == null) return "Invalid Kitchen";
        
        return $"{kitchenInfo.kitchenName} - " +
               $"카운터:{kitchenInfo.counterCount}, " +
               $"인덕션:{kitchenInfo.inductionCount}, " +
               $"테이블:{kitchenInfo.tableCount}";
    }
}
