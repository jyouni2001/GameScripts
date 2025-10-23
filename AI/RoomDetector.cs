using UnityEngine;
using System.Collections.Generic;
using ZLinq;

namespace JY
{
    /// <summary>
    /// 방 자동 감지 시스템
    /// 건축된 벽, 문, 침대를 분석하여 방을 자동으로 인식하고 생성
    /// </summary>
    public class RoomDetector : MonoBehaviour
    {
        [Header("기본 컴포넌트")]
        [Tooltip("건축 시스템 참조")]
        [SerializeField] private PlacementSystem placementSystem;
        
        [Tooltip("그리드 시스템 참조")]
        [SerializeField] private Grid grid;
        
        [Header("성능 및 안전 설정")]
        [Tooltip("FloodFill 최대 반복 횟수 (무한 루프 방지)")]
        [Range(100, 10000)]
        [SerializeField] private int maxFloodFillIterations = 5000;
        
        [Tooltip("최대 방 크기 (무한 확장 방지)")]
        [Range(50, 2000)]
        [SerializeField] private int maxRoomSize = 1000;
        
        [Header("방 탐색 범위 설정")]
        [Tooltip("벽 경계면 탐색 최대 반경 (큰 방 인식을 위해 충분히 크게)")]
        [Range(5, 50)]
        [SerializeField] private int maxWallSearchRadius = 20;
        
        [Tooltip("벽 경계면 탐색 최대 셀 수")]
        [Range(50, 500)]
        [SerializeField] private int maxWallSearchCells = 300;
        
        [Tooltip("방 내부 탐색 최대 반경 (긴 방 인식을 위해 충분히 크게)")]
        [Range(5, 50)]
        [SerializeField] private int maxRoomInteriorRadius = 25;
        
        [Tooltip("방 내부 탐색 최대 셀 수")]
        [Range(30, 500)]
        [SerializeField] private int maxRoomInteriorCells = 200;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = true;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = false;
        
        [Tooltip("방 스캔 과정 로그 표시")]
        [SerializeField] private bool showScanLogs = true;
        
        [Header("방 인식 조건")]
        [Tooltip("방으로 인식하기 위한 최소 벽 개수")]
        [Range(1, 20)]
        [SerializeField] private int minWalls = 3;
        
        [Tooltip("방으로 인식하기 위한 최소 문 개수")]
        [Range(1, 10)]
        [SerializeField] private int minDoors = 1;
        
        [Tooltip("방으로 인식하기 위한 최소 침대 개수")]
        [Range(1, 10)]
        [SerializeField] private int minBeds = 1;
        
        [Header("스캔 설정")]
        [Tooltip("방 스캔 주기 (초)")]
        [Range(0.5f, 10f)]
        [SerializeField] private float scanInterval = 2f;
        
        [Tooltip("방 요소들의 레이어 마스크")]
        [SerializeField] private LayerMask roomElementsLayer;
        
        [Header("층별 감지 설정")]
        [Tooltip("층 구분을 위한 Y축 허용 오차")]
        [Range(0.1f, 10f)]
        [SerializeField] private float floorHeightTolerance = FloorConstants.FLOOR_TOLERANCE;
        
        [Tooltip("바닥 감지 오프셋")]
        [Range(-2f, 2f)]
        [SerializeField] private float floorDetectionOffset = FloorConstants.FLOOR_DETECTION_OFFSET;
        
        [Header("다층 건물 설정")]
        [Tooltip("현재 스캔할 층 번호")]
        [Range(1, 10)]
        [SerializeField] private int currentScanFloor = 1;
        
        [Tooltip("모든 층을 스캔할지 여부")]
        [SerializeField] private bool scanAllFloors = true;
        
        [Tooltip("건물의 최대 층수")]
        [Range(1, 20)]
        [SerializeField] private int maxFloors = 5;
        
        [Header("선베드 방 설정")]
        [Tooltip("선베드 방 인식 기능 활성화")]
        [SerializeField] private bool enableSunbedRooms = false; // ✅ 선베드는 방이 아니라 오브젝트!
        
        [Tooltip("선베드 방의 고정 가격 (원)")]
        [Range(0f, 10000f)]
        [SerializeField] private float sunbedRoomPrice = 100f;
        
        [Tooltip("선베드 방의 고정 명성도")]
        [Range(0f, 1000f)]
        [SerializeField] private float sunbedRoomReputation = 50f;
        
        [Header("현재 상태")]
        [Tooltip("현재 감지된 방의 개수")]
        [SerializeField] private int detectedRoomCount = 0;
        
        [Tooltip("현재 스캔 상태 정보")]
        [SerializeField] private string currentScanStatus = "초기화 중...";
        
        [Tooltip("마지막 스캔 시간")]
        [SerializeField] private string lastScanTime = "아직 스캔하지 않음";

        // 3D 그리드로 변경하여 층 구분 지원
        private Dictionary<Vector3Int, RoomCell> roomGrid = new Dictionary<Vector3Int, RoomCell>();
        private List<RoomInfo> detectedRooms = new List<RoomInfo>();
        private HashSet<string> existingRoomIds = new HashSet<string>();
        private bool isInitialized = false;

        public delegate void RoomUpdateHandler(GameObject[] rooms);
        public event RoomUpdateHandler OnRoomsUpdated;

        /// <summary>
        /// 방 셀 정보 클래스
        /// </summary>
        public class RoomCell
        {
            public bool isFloor;
            public bool isWall;
            public bool isDoor;
            public bool isBed;
            public bool isSunbed;
            public Vector3Int position;
            public List<GameObject> objects = new List<GameObject>();
            public float worldHeight; // 실제 월드 높이 저장
        }

        /// <summary>
        /// 방 정보 클래스
        /// </summary>
        public class RoomInfo
        {
            public List<Vector3Int> floorCells = new List<Vector3Int>();
            public List<GameObject> walls = new List<GameObject>();
            public List<GameObject> doors = new List<GameObject>();
            public List<GameObject> beds = new List<GameObject>();
            public List<GameObject> sunbeds = new List<GameObject>();
            public Bounds bounds;
            public Vector3 center;
            public string roomId;
            public GameObject gameObject;
            public bool isSunbedRoom = false;
            public float fixedPrice = 0f;
            public float fixedReputation = 0f;
            public int floorLevel;

            public bool isValid(int minWalls, int minDoors, int minBeds)
            {
                // Sunbed 방은 별도 검증 로직
                if (isSunbedRoom)
                {
                    return sunbeds.Count > 0;
                }
                
                return walls.Count >= minWalls && doors.Count >= minDoors && beds.Count >= minBeds;
            }
        }

        private void Start()
        {
            InitializeComponents();
            InitializeFloorSettings();
            if (isInitialized)
            {
                InvokeRepeating(nameof(ScanForRooms), 1f, scanInterval);
            }
        }
        
        /// <summary>
        /// 층 설정 초기화
        /// </summary>
        private void InitializeFloorSettings()
        {
            // FloorConstants에서 층 설정 가져오기
            floorHeightTolerance = FloorConstants.FLOOR_TOLERANCE;
            floorDetectionOffset = FloorConstants.FLOOR_DETECTION_OFFSET;
            
            DebugLog($"층 설정 초기화 완료 - 층간 높이: {FloorConstants.FLOOR_HEIGHT}, 허용 오차: {floorHeightTolerance}, 바닥 오프셋: {floorDetectionOffset}", true);
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            placementSystem = placementSystem ?? FindFirstObjectByType<PlacementSystem>();
            grid = grid ?? FindFirstObjectByType<Grid>();

            if (placementSystem == null || grid == null)
            {
                DebugLog("필수 컴포넌트 누락!", true);
                isInitialized = false;
                currentScanStatus = "초기화 실패 - 필수 컴포넌트 누락";
                return;
            }

            isInitialized = true;
            currentScanStatus = "초기화 완료 - 스캔 대기 중";
            DebugLog("방 감지 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// 오브젝트가 현재 스캔 대상인지 확인 (층별 필터링)
        /// </summary>
        private bool ShouldProcessObject(GameObject obj)
        {
            if (scanAllFloors) return true;
            
            // 현재 층만 스캔하는 경우
            float objY = obj.transform.position.y;
            int objFloor = FloorConstants.GetFloorLevel(objY); // FloorConstants 사용
            
            bool shouldProcess = objFloor == currentScanFloor;
            
            if (showScanLogs && shouldProcess)
            {
                DebugLog($"오브젝트 {obj.name} 스캔 대상 - Y: {objY:F2}, 층: {objFloor}, 현재 스캔 층: {currentScanFloor}");
            }
            
            return shouldProcess;
        }
        
        /// <summary>
        /// 스캔할 층 설정
        /// </summary>
        public void SetScanFloor(int floorLevel)
        {
            currentScanFloor = Mathf.Clamp(floorLevel, 1, maxFloors);
            scanAllFloors = false;
            DebugLog($"스캔 층 설정: {currentScanFloor}층", true);
        }
        
        /// <summary>
        /// 모든 층 스캔 모드 설정
        /// </summary>
        public void SetScanAllFloors()
        {
            scanAllFloors = true;
            DebugLog("모든 층 스캔 모드 활성화", true);
        }
        
        /// <summary>
        /// 현재 스캔 정보 반환
        /// </summary>
        public string GetScanInfo()
        {
            return $"감지된 방: {detectedRoomCount}개, 상태: {currentScanStatus}, 마지막 스캔: {lastScanTime}";
        }
        
        /// <summary>
        /// 수동 방 스캔 (디버그용)
        /// </summary>
        [ContextMenu("수동 방 스캔")]
        public void ManualScanRooms()
        {
            DebugLog("=== 수동 방 스캔 시작 ===", true);
            ScanForRooms();
        }
        
        /// <summary>
        /// 태그 상태 확인 (디버그용)
        /// </summary>
        [ContextMenu("태그 상태 확인")]
        public void CheckTagStatus()
        {
            DebugLog("=== 태그 상태 확인 시작 ===", true);
            
            string[] tagsToCheck = { "Floor", "Wall", "Door", "Bed", "Sunbed" };
            
            foreach (string tag in tagsToCheck)
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                DebugLog($"{tag} 태그: {objects.Length}개", true);
                
                if (objects.Length == 0)
                {
                    DebugLog($"⚠️ 경고: {tag} 태그가 설정된 오브젝트가 없습니다!", true);
                }
                else
                {
                    for (int i = 0; i < Mathf.Min(objects.Length, 3); i++) // 최대 3개만 출력
                    {
                        var obj = objects[i];
                        DebugLog($"  - {obj.name} (위치: {obj.transform.position})", true);
                    }
                    if (objects.Length > 3)
                    {
                        DebugLog($"  ... 그 외 {objects.Length - 3}개 더", true);
                    }
                }
            }
            
            DebugLog("=== 태그 상태 확인 완료 ===", true);
        }
        
        /// <summary>
        /// 방 스캔 실행
        /// </summary>
        public void ScanForRooms()
        {
            if (!isInitialized)
            {
                DebugLog("시스템이 초기화되지 않았습니다.", true);
                return;
            }

            currentScanStatus = "스캔 중...";
            lastScanTime = System.DateTime.Now.ToString("HH:mm:ss");
            
            DebugLog("방 스캔 시작", showScanLogs);

            try
            {
                // 그리드 업데이트
                UpdateGridFromScene();
                
                                                    // 방 찾기 (각 침대별로 독립적인 방 생성)
                List<RoomInfo> newRooms = new List<RoomInfo>();
                HashSet<Vector3Int> globalVisitedCells = new HashSet<Vector3Int>();

                foreach (var kvp in roomGrid)
                {
                    Vector3Int pos = kvp.Key;
                    RoomCell cell = kvp.Value;

                    if (cell.isBed && !globalVisitedCells.Contains(pos))
                    {
                        DebugLog($"=== 침대 셀에서 독립적인 방 탐색 시작: {pos} ===", true);
                        
                        // 각 침대별로 독립적인 방문 셀 추적
                        HashSet<Vector3Int> localVisitedCells = new HashSet<Vector3Int>();
                        RoomInfo room = FindEnclosedRoom(pos, localVisitedCells);
                        
                        if (room != null)
                        {
                            // 다른 방과 겹치는지 확인
                            bool overlapsWithExisting = false;
                            foreach (var existingRoom in newRooms)
                            {
                                if (DoRoomsOverlap(room, existingRoom))
                                {
                                    overlapsWithExisting = true;
                                    DebugLog($"방 {room.roomId}가 기존 방 {existingRoom.roomId}와 겹쳐서 제외됨", true);
                                    break;
                                }
                            }
                            
                            if (!overlapsWithExisting)
                        {
                            bool isValidRoom = room.isValid(minWalls, minDoors, minBeds);
                            DebugLog($"방 유효성 검사:\n" +
                                     $"  최소 벽 요구: {minWalls}개 (현재: {room.walls.Count}개) {(room.walls.Count >= minWalls ? "✓" : "✗")}\n" +
                                     $"  최소 문 요구: {minDoors}개 (현재: {room.doors.Count}개) {(room.doors.Count >= minDoors ? "✓" : "✗")}\n" +
                                     $"  최소 침대 요구: {minBeds}개 (현재: {room.beds.Count}개) {(room.beds.Count >= minBeds ? "✓" : "✗")}\n" +
                                     $"  최종 결과: {(isValidRoom ? "방 인식 성공" : "방 인식 실패")}", true);
                            
                            if (isValidRoom)
                            {
                                newRooms.Add(room);
                                    
                                    // 전역 방문 목록에 추가 (다른 침대가 같은 영역을 중복 탐색하지 않도록)
                                    foreach (var floorCell in room.floorCells)
                                    {
                                        globalVisitedCells.Add(floorCell);
                                    }
                                    foreach (var bed in room.beds)
                                    {
                                        if (bed != null)
                                        {
                                            Vector3Int bedGridPos = GetAdjustedGridPosition(bed, floorDetectionOffset);
                                            globalVisitedCells.Add(bedGridPos);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            DebugLog($"FloodFill 결과: null (방 생성 실패)", true);
                        }
                    }
                }

                // Sunbed 방 찾기
                if (enableSunbedRooms)
                {
                    List<RoomInfo> sunbedRooms = FindSunbedRooms(newRooms);
                    newRooms.AddRange(sunbedRooms);
                }

                // 방 업데이트
                UpdateRooms(newRooms);
                
                detectedRoomCount = detectedRooms.Count;
                currentScanStatus = $"스캔 완료 - {detectedRoomCount}개 방 감지";
                
                DebugLog($"방 스캔 완료: {detectedRoomCount}개 방 감지됨", true);
            }
            catch (System.Exception e)
            {
                currentScanStatus = "스캔 오류 발생";
                DebugLog($"방 스캔 중 오류 발생: {e.Message}", true);
            }
        }

        private void UpdateRooms(List<RoomInfo> newRooms)
        {
            foreach (var room in detectedRooms)
            {
                if (room.gameObject != null)
                {
                    GameObject.Destroy(room.gameObject);
                }
            }

            detectedRooms = newRooms;
            foreach (var room in detectedRooms)
            {
                CreateRoomGameObject(room);
            }

            if (detectedRooms.Count > 0)
            {
                DebugLog($"총 {detectedRooms.Count}개의 방이 감지됨");
                OnRoomsUpdated?.Invoke(detectedRooms.AsValueEnumerable().Select(r => r.gameObject).ToArray());
            }
            else
            {
                DebugLog("감지된 방이 없음");
            }
        }

        private void UpdateGridFromScene()
        {
            roomGrid.Clear();
            DebugLog($"그리드 업데이트 시작 - 스캔 모드: {(scanAllFloors ? "전체 층" : $"{currentScanFloor}층만")}");

            // 각 태그별로 오브젝트 검색 및 3D 위치 사용 (층별 필터링 적용)
            ProcessTaggedObjects("Floor", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, floorDetectionOffset);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y + floorDetectionOffset
                    };
                }
                roomGrid[gridPosition].isFloor = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            ProcessTaggedObjects("Wall", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, 0f);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y
                    };
                }
                roomGrid[gridPosition].isWall = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            ProcessTaggedObjects("Door", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, 0f);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y
                    };
                }
                roomGrid[gridPosition].isDoor = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            ProcessTaggedObjects("Bed", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, floorDetectionOffset);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y + floorDetectionOffset
                    };
                }
                roomGrid[gridPosition].isBed = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            // Sunbed 태그 처리 추가
            ProcessTaggedObjects("Sunbed", (obj) => {
                // 층별 필터링 적용
                if (!ShouldProcessObject(obj)) return;
                Vector3Int gridPosition = GetAdjustedGridPosition(obj, floorDetectionOffset);
                if (!roomGrid.ContainsKey(gridPosition))
                {
                    roomGrid[gridPosition] = new RoomCell { 
                        position = gridPosition, 
                        objects = new List<GameObject>(),
                        worldHeight = obj.transform.position.y + floorDetectionOffset
                    };
                }
                roomGrid[gridPosition].isSunbed = true;
                roomGrid[gridPosition].objects.Add(obj.transform.parent?.gameObject ?? obj);
            });

            int floorCount = roomGrid.Values.AsValueEnumerable().Count(c => c.isFloor);
            int wallCount = roomGrid.Values.AsValueEnumerable().Count(c => c.isWall);
            int doorCount = roomGrid.Values.AsValueEnumerable().Count(c => c.isDoor);
            int bedCount = roomGrid.Values.AsValueEnumerable().Count(c => c.isBed);
            
            DebugLog($"=== 그리드 업데이트 완료 ===\n" +
                     $"총 셀 개수: {roomGrid.Count}\n" +
                     $"바닥 셀: {floorCount}개\n" +
                     $"벽 셀: {wallCount}개\n" +
                     $"문 셀: {doorCount}개\n" +
                     $"침대 셀: {bedCount}개\n" +
                     $"===============================", true);
            
            // 바닥 셀 위치 상세 출력
            if (floorCount > 0)
            {
                DebugLog("바닥 셀 위치들:");
                foreach (var kvp in roomGrid)
                {
                    if (kvp.Value.isFloor)
                    {
                        DebugLog($"  바닥: {kvp.Key} (월드 높이: {kvp.Value.worldHeight:F1})");
                    }
                }
            }
        }

        // 오프셋을 적용한 그리드 위치 계산
        private Vector3Int GetAdjustedGridPosition(GameObject obj, float yOffset)
        {
            Vector3 worldPosition = obj.transform.position;
            worldPosition.y += yOffset;
            Vector3Int gridPosition = grid.WorldToCell(worldPosition);
            
            DebugLog($"오브젝트 위치 변환: {obj.name}\n" +
                     $"Original Position: {obj.transform.position}\n" +
                     $"Adjusted Position: {worldPosition}\n" +
                     $"Grid Position: {gridPosition}");
                     
            return gridPosition;
        }

        private Vector3Int GetParentGridPosition(GameObject obj)
        {
            // 기존 메서드는 유지하되, 새로운 메서드 사용 권장
            return GetAdjustedGridPosition(obj, 0f);
        }

        private void ProcessTaggedObjects(string tag, System.Action<GameObject> processor)
        {
            var taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            DebugLog($"{tag} 태그 오브젝트 수: {taggedObjects.Length}");
            
            foreach (var obj in taggedObjects)
            {
                if (obj == null) continue;
                processor(obj);
            }
        }

                 private RoomInfo FindEnclosedRoom(Vector3Int startPos, HashSet<Vector3Int> localVisitedCells)
         {
             DebugLog($"=== 침대 기반 독립적인 방 탐색 시작: {startPos} ===", true);
             
             // 침대가 있는 위치에서만 방 탐색 시작
             if (!roomGrid.TryGetValue(startPos, out RoomCell startCell) || !startCell.isBed)
             {
                 return null;
             }
             
             DebugLog($"침대 발견: {startPos}", true);
             
             // 1단계: 먼저 침대 주변의 벽과 문을 찾기 (벽 경계면 우선)
             HashSet<Vector3Int> roomWalls = new HashSet<Vector3Int>();
             HashSet<Vector3Int> roomDoors = new HashSet<Vector3Int>();
             FindWallBoundariesAroundPosition(startPos, roomWalls, roomDoors);
             
             if (roomWalls.Count == 0 && roomDoors.Count == 0)
             {
                 DebugLog($"침대 {startPos} 주변에 벽이나 문을 찾을 수 없음", true);
                 return null;
             }
             
             DebugLog($"벽 경계면 발견: 벽 {roomWalls.Count}개, 문 {roomDoors.Count}개", true);
             
             // 2단계: 벽으로 둘러싸인 내부 영역 찾기 (독립적인 탐색)
             HashSet<Vector3Int> roomFloors = new HashSet<Vector3Int>();
             HashSet<Vector3Int> bedsInRoom = new HashSet<Vector3Int>();
             HashSet<Vector3Int> tempGlobalVisited = new HashSet<Vector3Int>(); // 임시 전역 방문 목록
             
             FindRoomInteriorByWallBoundaries(startPos, roomWalls, roomDoors, roomFloors, bedsInRoom, localVisitedCells, tempGlobalVisited);
             
             // 방 정보 생성
            RoomInfo room = new RoomInfo();
             room.floorCells.AddRange(roomFloors);
             
             // 벽, 문, 침대 오브젝트 수집
             CollectRoomObjectsSimple(roomWalls, roomDoors, bedsInRoom, room);
             
             // 방 경계 및 ID 설정 (벽 경계면 기준)
             SetupRoomBounds(room);
             
             // 간단한 유효성 검사 (침대 1개, 벽 3개 이상, 문 1개 이상)
             bool isValid = room.beds.Count >= minBeds && room.walls.Count >= minWalls && room.doors.Count >= minDoors;
             
             DebugLog($"=== 벽 경계면 기준 독립적인 방 탐색 결과 ===\n" +
                      $"ID: {room.roomId}\n" +
                      $"바닥: {room.floorCells.Count}개\n" +
                      $"벽: {room.walls.Count}개 (최소 {minWalls}개 필요) {(room.walls.Count >= minWalls ? "✓" : "✗")}\n" +
                      $"문: {room.doors.Count}개 (최소 {minDoors}개 필요) {(room.doors.Count >= minDoors ? "✓" : "✗")}\n" +
                      $"침대: {room.beds.Count}개 (최소 {minBeds}개 필요) {(room.beds.Count >= minBeds ? "✓" : "✗")}\n" +
                      $"유효성: {(isValid ? "✅ 방 인식 성공" : "❌ 방 인식 실패")}\n" +
                      $"================================", true);
             
             return isValid ? room : null;
         }
         
         /// <summary>
         /// 특정 위치 주변의 벽 경계면을 찾기 (제한된 범위)
         /// </summary>
         private void FindWallBoundariesAroundPosition(Vector3Int centerPos, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors)
         {
             DebugLog($"위치 {centerPos} 주변 벽 경계면 탐색 시작 (최대 반경: {maxWallSearchRadius}, 최대 셀: {maxWallSearchCells})", true);
             
             HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
             Queue<Vector3Int> queue = new Queue<Vector3Int>();
             
             queue.Enqueue(centerPos);
             visited.Add(centerPos);
             
             Vector3Int[] directions = GetDirections();
             int iterations = 0;
             
             while (queue.Count > 0 && iterations < maxFloodFillIterations && visited.Count < maxWallSearchCells)
             {
                 iterations++;
                 Vector3Int current = queue.Dequeue();
                 
                 // 중심점에서 너무 멀면 중단
                 float distance = Vector3Int.Distance(centerPos, current);
                 if (distance > maxWallSearchRadius)
                 {
                     DebugLog($"탐색 반경 초과로 중단: {current} (거리: {distance:F1})", showScanLogs);
                     continue;
                 }

                 foreach (var dir in directions)
                 {
                     Vector3Int neighbor = current + dir;
                     
                     if (visited.Contains(neighbor)) continue;
                     
                     // 반경 체크
                     if (Vector3Int.Distance(centerPos, neighbor) > maxWallSearchRadius)
                         continue;
                     
                     visited.Add(neighbor);
                     
                     // Y축 오프셋을 고려하여 벽과 문 확인
                     bool foundBoundary = false;
                     for (int yOffset = 0; yOffset <= 2; yOffset++)
                     {
                         Vector3Int checkPos = new Vector3Int(neighbor.x, centerPos.y + yOffset, neighbor.z);
                         
                         if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                         {
                             if (cell.isWall)
                             {
                                 walls.Add(checkPos);
                                 foundBoundary = true;
                                 DebugLog($"벽 경계 발견: {checkPos} (거리: {Vector3Int.Distance(centerPos, neighbor):F1})", showScanLogs);
                             }
                             if (cell.isDoor)
                             {
                                 doors.Add(checkPos);
                                 foundBoundary = true;
                                 DebugLog($"문 경계 발견: {checkPos} (거리: {Vector3Int.Distance(centerPos, neighbor):F1})", showScanLogs);
                             }
                         }
                     }
                     
                     // 벽이나 문이 아닌 빈 공간이면 계속 탐색 (단, 반경 내에서만)
                     if (!foundBoundary && Vector3Int.Distance(centerPos, neighbor) < maxWallSearchRadius)
                     {
                         queue.Enqueue(neighbor);
                     }
                 }
             }
             
             DebugLog($"벽 경계면 탐색 완료 - 반복: {iterations}회, 방문 셀: {visited.Count}/{maxWallSearchCells}개, 벽: {walls.Count}개, 문: {doors.Count}개, 최대 반경: {maxWallSearchRadius}", true);
         }
         
         /// <summary>
         /// 벽 경계면을 기준으로 방 내부 영역 찾기 (제한된 범위)
         /// </summary>
         private void FindRoomInteriorByWallBoundaries(Vector3Int startPos, HashSet<Vector3Int> wallBoundaries, HashSet<Vector3Int> doorBoundaries,
             HashSet<Vector3Int> roomFloors, HashSet<Vector3Int> bedsInRoom, HashSet<Vector3Int> localVisited, HashSet<Vector3Int> globalVisited)
         {
             DebugLog($"벽 경계면 기준 방 내부 탐색 시작: {startPos} (최대 반경: {maxRoomInteriorRadius}, 최대 셀: {maxRoomInteriorCells})", true);
             
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(startPos);
             localVisited.Add(startPos);
             globalVisited.Add(startPos);
             
             Vector3Int[] directions = GetDirections();
             int iterations = 0;
             
             while (queue.Count > 0 && iterations < maxFloodFillIterations && roomFloors.Count < maxRoomInteriorCells)
             {
                 iterations++;
                 Vector3Int current = queue.Dequeue();
                 
                 // 시작점에서 너무 멀면 중단
                 float distance = Vector3Int.Distance(startPos, current);
                 if (distance > maxRoomInteriorRadius)
                 {
                     DebugLog($"방 내부 탐색 반경 초과로 중단: {current} (거리: {distance:F1})", showScanLogs);
                     continue;
                 }
                 
                 // 현재 위치 분석
                 if (roomGrid.TryGetValue(current, out RoomCell currentCell))
                 {
                     if (currentCell.isFloor)
                     {
                         roomFloors.Add(current);
                         DebugLog($"방 바닥 추가: {current} (거리: {distance:F1})", showScanLogs);
                     }
                     if (currentCell.isBed)
                     {
                         bedsInRoom.Add(current);
                         DebugLog($"방 침대 추가: {current} (거리: {distance:F1})", showScanLogs);
                     }
                 }
                 else
                 {
                     // 빈 공간도 방 내부로 포함 (벽으로 둘러싸인 경우)
                     roomFloors.Add(current);
                     DebugLog($"빈 공간을 방 내부로 추가: {current} (거리: {distance:F1})", showScanLogs);
                 }
                 
                 // 인접한 위치들 탐색
                 foreach (var dir in directions)
                 {
                     Vector3Int neighbor = current + dir;

                     if (localVisited.Contains(neighbor) || globalVisited.Contains(neighbor))
                         continue;
                     
                     // 시작점에서 너무 멀면 스킵
                     float neighborDistance = Vector3Int.Distance(startPos, neighbor);
                     if (neighborDistance > maxRoomInteriorRadius)
                         continue;
                     
                     // 벽이나 문 경계에 막혔는지 확인
                     bool isBlocked = false;
                     for (int yOffset = 0; yOffset <= 2; yOffset++)
                     {
                         Vector3Int checkPos = new Vector3Int(neighbor.x, current.y + yOffset, neighbor.z);
                         if (wallBoundaries.Contains(checkPos) || doorBoundaries.Contains(checkPos))
                         {
                             isBlocked = true;
                             break;
                         }
                     }
                     
                     if (!isBlocked)
                     {
                         localVisited.Add(neighbor);
                         globalVisited.Add(neighbor);
                         queue.Enqueue(neighbor);
                         DebugLog($"방 영역 확장: {current} -> {neighbor} (거리: {neighborDistance:F1})", showScanLogs);
                     }
                 }
             }
             
             DebugLog($"벽 경계면 기준 방 내부 탐색 완료 - 반복: {iterations}/{maxFloodFillIterations}회, 바닥: {roomFloors.Count}/{maxRoomInteriorCells}개, 침대: {bedsInRoom.Count}개, 최대 반경: {maxRoomInteriorRadius}", true);
         }
         
         /// <summary>
         /// 두 방이 겹치는지 확인 (침대, 경계, 바닥 셀 기준) - 독립적인 방 생성을 위한 엄격한 검사
         /// </summary>
         private bool DoRoomsOverlap(RoomInfo room1, RoomInfo room2)
         {
             if (room1 == null || room2 == null) return false;
             
             // 1. 침대 위치 겹침 확인 (같은 침대를 공유하는지) - 가장 중요한 기준
             bool bedOverlap = false;
             foreach (var bed1 in room1.beds)
             {
                 foreach (var bed2 in room2.beds)
                 {
                     if (bed1 == bed2)
                     {
                         bedOverlap = true;
                         DebugLog($"같은 침대 공유 감지: {bed1.name}", true);
                         break;
                     }
                 }
                 if (bedOverlap) break;
             }
             
             // 2. 바닥 셀 겹침 확인 (50% 이상 겹치면 같은 방으로 판단)
             int overlapCount = 0;
             foreach (var floor1 in room1.floorCells)
             {
                 if (room2.floorCells.Contains(floor1))
                 {
                     overlapCount++;
                 }
             }
             float overlapRatio = (float)overlapCount / Mathf.Min(room1.floorCells.Count, room2.floorCells.Count);
             bool significantFloorOverlap = overlapRatio > 0.5f; // 50% 이상 겹치면 같은 방
             
             // 3. 방 중심점 거리 확인 (너무 가까우면 같은 방일 가능성)
             float centerDistance = Vector3.Distance(room1.center, room2.center);
             bool tooClose = centerDistance < 3f; // 3 유닛 이내면 너무 가까움
             
             // 4. 바운더리 겹침 확인 (완전히 포함되는 경우)
             bool boundsContained = room1.bounds.Contains(room2.center) || room2.bounds.Contains(room1.center);
             
             bool overlaps = bedOverlap || significantFloorOverlap || (tooClose && boundsContained);
             
             if (overlaps)
             {
                 DebugLog($"방 겹침 감지: {room1.roomId} vs {room2.roomId}\n" +
                          $"  - 침대 공유: {bedOverlap}\n" +
                          $"  - 바닥 겹침: {overlapCount}/{Mathf.Min(room1.floorCells.Count, room2.floorCells.Count)} ({overlapRatio:P1})\n" +
                          $"  - 중심점 거리: {centerDistance:F1} (기준: 3.0)\n" +
                          $"  - 바운더리 포함: {boundsContained}", true);
             }
             else
             {
                 DebugLog($"방 독립성 확인: {room1.roomId} vs {room2.roomId} - 독립적인 방으로 인정", true);
             }
             
             return overlaps;
         }
        
        /// <summary>
        /// 유효한 방의 시작점인지 확인 (완화된 조건)
        /// </summary>
        private bool IsValidRoomStart(Vector3Int pos)
        {
            if (!roomGrid.TryGetValue(pos, out RoomCell cell) || !cell.isFloor)
            {
                return false;
            }
            
            // 주변에 최소한의 벽이 있는지 확인 (조건 완화)
            int wallCount = 0;
            Vector3Int[] directions = GetDirections();
            
            foreach (var dir in directions)
            {
                Vector3Int neighbor = pos + dir;
                if (HasWallAt(neighbor) || HasDoorAt(neighbor))
                {
                    wallCount++;
                }
            }
            
            return wallCount >= 1; // 최소 1개 방향에 벽이나 문이 있으면 시작점으로 인정 (완화)
        }
        
                /// <summary>
        /// 침대에서 시작해서 벽과 문을 만날 때까지 확장하며 찾기 (반경 제한 없음)
        /// </summary>
        private void FindWallsAndDoorsAroundBed(Vector3Int bedPos, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors, int unusedRadius)
        {
            DebugLog($"침대 {bedPos}에서 벽/문 탐색 시작 (반경 제한 없음)", true);
            
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            
            queue.Enqueue(bedPos);
            visited.Add(bedPos);
            
            Vector3Int[] directions = GetDirections();
            Vector3Int[] diagonals = {
                new Vector3Int(1, 0, 1), new Vector3Int(-1, 0, 1),
                new Vector3Int(1, 0, -1), new Vector3Int(-1, 0, -1)
            };
            
            // 직선 + 대각선 방향 모두 탐색
            Vector3Int[] allDirections = new Vector3Int[directions.Length + diagonals.Length];
            directions.CopyTo(allDirections, 0);
            diagonals.CopyTo(allDirections, directions.Length);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();

                foreach (var dir in allDirections)
                {
                    Vector3Int neighbor = current + dir;
                    
                    if (visited.Contains(neighbor)) continue;
                    visited.Add(neighbor);
                    
                    // Y축 오프셋을 고려하여 벽과 문 확인
                    bool foundWallOrDoor = false;
                    for (int yOffset = 0; yOffset <= 2; yOffset++)
                    {
                        Vector3Int checkPos = new Vector3Int(neighbor.x, bedPos.y + yOffset, neighbor.z);
                        
                        if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                        {
                            if (cell.isWall)
                            {
                                walls.Add(checkPos);
                                foundWallOrDoor = true;
                                DebugLog($"벽 발견: {checkPos}", true);
                            }
                            if (cell.isDoor)
                            {
                                doors.Add(checkPos);
                                foundWallOrDoor = true;
                                DebugLog($"문 발견: {checkPos}", true);
                            }
                        }
                    }
                    
                    // 벽이나 문이 아닌 빈 공간이면 계속 탐색
                    if (!foundWallOrDoor)
                    {
                        // 바닥이 있거나 빈 공간이면 계속 확장
                        if (!roomGrid.ContainsKey(neighbor) || 
                            (roomGrid.TryGetValue(neighbor, out RoomCell emptyCell) && emptyCell.isFloor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
            
            DebugLog($"벽/문 탐색 완료 - 벽: {walls.Count}개, 문: {doors.Count}개", true);
        }
        
                 /// <summary>
         /// 침대에서 시작해서 벽에 막힐 때까지 실제 방 영역 탐색 (무한 루프 방지)
         /// </summary>
         private void FindActualRoomArea(Vector3Int startPos, HashSet<Vector3Int> roomFloors, HashSet<Vector3Int> roomWalls, 
             HashSet<Vector3Int> roomDoors, HashSet<Vector3Int> bedsInRoom, HashSet<Vector3Int> localVisited, HashSet<Vector3Int> globalVisited)
         {
             DebugLog($"실제 방 영역 탐색 시작: {startPos} (최대 반복: {maxFloodFillIterations}회, 최대 크기: {maxRoomSize})", true);
             
             Queue<Vector3Int> queue = new Queue<Vector3Int>();
             queue.Enqueue(startPos);
             localVisited.Add(startPos);
             globalVisited.Add(startPos);
             
             Vector3Int[] directions = GetDirections();
             int iterations = 0;
             
             while (queue.Count > 0 && iterations < maxFloodFillIterations && roomFloors.Count < maxRoomSize)
             {
                 iterations++;
                 Vector3Int current = queue.Dequeue();
                 
                 // 현재 위치 분석
                 if (roomGrid.TryGetValue(current, out RoomCell currentCell))
                 {
                if (currentCell.isFloor)
                {
                         roomFloors.Add(current);
                         DebugLog($"방 바닥 추가: {current}", showScanLogs);
                     }
                     if (currentCell.isBed)
                     {
                         bedsInRoom.Add(current);
                         DebugLog($"방 침대 추가: {current}", true);
                     }
                 }
                 
                 // 인접한 위치들 탐색
                    foreach (var dir in directions)
                    {
                        Vector3Int neighbor = current + dir;

                     if (localVisited.Contains(neighbor) || globalVisited.Contains(neighbor))
                         continue;
                     
                     // Y축 오프셋을 고려하여 벽과 문 확인
                     bool isWallOrDoor = false;
                     for (int yOffset = 0; yOffset <= 2; yOffset++)
                     {
                         Vector3Int checkPos = new Vector3Int(neighbor.x, current.y + yOffset, neighbor.z);
                         if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                         {
                             if (cell.isWall)
                             {
                                 roomWalls.Add(checkPos);
                                 isWallOrDoor = true;
                                 DebugLog($"방 경계 벽 발견: {checkPos}", showScanLogs);
                             }
                             if (cell.isDoor)
                             {
                                 roomDoors.Add(checkPos);
                                 isWallOrDoor = true;
                                 DebugLog($"방 경계 문 발견: {checkPos}", showScanLogs);
                             }
                         }
                     }
                     
                     // 벽이나 문이 아닌 경우 계속 탐색
                     if (!isWallOrDoor)
                     {
                         // 바닥이나 침대가 있거나 빈 공간이면 방 영역으로 확장
                         if (!roomGrid.ContainsKey(neighbor) || 
                             (roomGrid.TryGetValue(neighbor, out RoomCell neighborCell) && 
                              (neighborCell.isFloor || neighborCell.isBed)))
                         {
                             localVisited.Add(neighbor);
                             globalVisited.Add(neighbor);
                             queue.Enqueue(neighbor);
                             DebugLog($"방 영역 확장: {current} -> {neighbor}", showScanLogs);
                         }
                     }
                 }
             }
             
             DebugLog($"실제 방 영역 탐색 완료 - 반복: {iterations}/{maxFloodFillIterations}회, 바닥: {roomFloors.Count}개, 벽: {roomWalls.Count}개, 문: {roomDoors.Count}개, 침대: {bedsInRoom.Count}개", true);
             
             if (iterations >= maxFloodFillIterations)
             {
                 DebugLog($"⚠️ 최대 반복 횟수 도달로 탐색 중단: {startPos}", true);
             }
             if (roomFloors.Count >= maxRoomSize)
             {
                 DebugLog($"⚠️ 최대 방 크기 도달로 탐색 중단: {startPos}", true);
             }
         }
        
        /// <summary>
        /// 벽으로 둘러싸인 실제 방 영역의 모든 바닥 찾기
        /// </summary>
        private void FindFloorsInsideWalls(HashSet<Vector3Int> walls, HashSet<Vector3Int> doors, HashSet<Vector3Int> floors, Vector3Int centerPos)
        {
            DebugLog($"침대 {centerPos}에서 벽으로 둘러싸인 방 영역 탐색 시작", true);
            
            if (walls.Count == 0)
            {
                // 벽이 없으면 침대 위치만 추가
                floors.Add(centerPos);
                return;
            }
            
            // 벽들의 실제 경계 계산
            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            
            foreach (var wall in walls)
            {
                if (wall.x < minX) minX = wall.x;
                if (wall.x > maxX) maxX = wall.x;
                if (wall.z < minZ) minZ = wall.z;
                if (wall.z > maxZ) maxZ = wall.z;
            }
            
            DebugLog($"벽 경계: X({minX}~{maxX}), Z({minZ}~{maxZ})", true);
            
            // 벽 경계 내부의 모든 바닥을 FloodFill로 찾기
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            
            // 침대 위치에서 시작
            queue.Enqueue(centerPos);
            visited.Add(centerPos);
            
            Vector3Int[] directions = GetDirections();
            
            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();
                
                // 현재 위치가 바닥이면 추가
                if (roomGrid.TryGetValue(current, out RoomCell currentCell) && currentCell.isFloor)
                {
                    floors.Add(current);
                    DebugLog($"방 바닥 추가: {current}", true);
                }
                
                // 인접한 위치 탐색
                    foreach (var dir in directions)
                    {
                        Vector3Int neighbor = current + dir;

                    if (visited.Contains(neighbor)) continue;
                    
                    // 벽 경계를 벗어나면 스킵
                    if (neighbor.x < minX || neighbor.x > maxX || neighbor.z < minZ || neighbor.z > maxZ)
                        continue;
                    
                    // 벽이나 문에 막히면 스킵
                    if (IsWallOrDoorAt(neighbor, walls, doors))
                    {
                        DebugLog($"벽/문으로 차단: {current} -> {neighbor}", true);
                        continue;
                    }
                    
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
            
            DebugLog($"방 바닥 탐색 완료: {floors.Count}개", true);
        }
        
        /// <summary>
        /// 특정 위치에 벽이나 문이 있는지 확인
        /// </summary>
        private bool IsWallOrDoorAt(Vector3Int position, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors)
        {
            // Y축 오프셋을 고려하여 확인
            for (int yOffset = 0; yOffset <= 2; yOffset++)
            {
                Vector3Int checkPos = new Vector3Int(position.x, position.y + yOffset, position.z);
                if (walls.Contains(checkPos) || doors.Contains(checkPos))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 바닥이 벽으로 둘러싸여 있는지 확인
        /// </summary>
        private bool IsFloorSurroundedByWalls(Vector3Int floorPos, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors)
        {
            Vector3Int[] directions = GetDirections();
            int surroundedSides = 0;
            
            foreach (var dir in directions)
            {
                Vector3Int neighbor = floorPos + dir;
                
                // 인접한 위치에 벽이나 문이 있는지 확인 (Y축 오프셋 고려)
                bool hasWallOrDoor = false;
                for (int yOffset = 0; yOffset <= 2; yOffset++)
                {
                    Vector3Int checkPos = new Vector3Int(neighbor.x, neighbor.y + yOffset, neighbor.z);
                    if (walls.Contains(checkPos) || doors.Contains(checkPos))
                    {
                        hasWallOrDoor = true;
                        break;
                    }
                }
                
                if (hasWallOrDoor)
                {
                    surroundedSides++;
                }
            }
            
            // 최소 2면이 벽으로 둘러싸여 있으면 방 내부로 인정
            return surroundedSides >= 2;
        }
        
        /// <summary>
        /// 간단한 방 오브젝트 수집
        /// </summary>
        private void CollectRoomObjectsSimple(HashSet<Vector3Int> walls, HashSet<Vector3Int> doors, HashSet<Vector3Int> beds, RoomInfo room)
        {
            // 벽 오브젝트 수집
            foreach (var wallPos in walls)
            {
                if (roomGrid.TryGetValue(wallPos, out RoomCell wallCell))
                {
                    foreach (var obj in wallCell.objects)
                                {
                                    if (!room.walls.Contains(obj))
                                    {
                                        room.walls.Add(obj);
                        }
                    }
                }
            }
            
            // 문 오브젝트 수집
            foreach (var doorPos in doors)
            {
                if (roomGrid.TryGetValue(doorPos, out RoomCell doorCell))
                {
                    foreach (var obj in doorCell.objects)
                    {
                        if (!room.doors.Contains(obj))
                        {
                            room.doors.Add(obj);
                        }
                    }
                }
            }
            
            // 침대 오브젝트 수집
            foreach (var bedPos in beds)
            {
                if (roomGrid.TryGetValue(bedPos, out RoomCell bedCell))
                            {
                                foreach (var obj in bedCell.objects)
                                {
                                    if (!room.beds.Contains(obj))
                                    {
                                        room.beds.Add(obj);
                        }
                    }
                }
            }
            
            DebugLog($"오브젝트 수집 완료 - 벽: {room.walls.Count}, 문: {room.doors.Count}, 침대: {room.beds.Count}", true);
        }
        
                 /// <summary>
         /// 연결된 바닥 셀들 찾기 (벽으로 차단되지 않은 인접한 바닥들, 무한 루프 방지)
         /// </summary>
         private void FindConnectedFloors(Vector3Int startPos, HashSet<Vector3Int> connectedFloors, HashSet<Vector3Int> globalVisited)
         {
             Queue<Vector3Int> queue = new Queue<Vector3Int>();
             HashSet<Vector3Int> localVisited = new HashSet<Vector3Int>();
             
             queue.Enqueue(startPos);
             localVisited.Add(startPos);
             
             Vector3Int[] directions = GetDirections();
             int iterations = 0;
             
             while (queue.Count > 0 && iterations < maxFloodFillIterations && connectedFloors.Count < maxRoomSize)
             {
                 iterations++;
                 Vector3Int current = queue.Dequeue();
                 
                 if (!roomGrid.TryGetValue(current, out RoomCell currentCell) || !currentCell.isFloor)
                 {
                     continue;
                 }
                 
                 connectedFloors.Add(current);
                 globalVisited.Add(current);
                 
                 DebugLog($"연결된 바닥 추가: {current}", showScanLogs);
                 
                 // 인접한 바닥 셀 탐색
                 foreach (var dir in directions)
                 {
                     Vector3Int neighbor = current + dir;
                     
                     // 이미 방문했으면 스킵
                     if (localVisited.Contains(neighbor) || globalVisited.Contains(neighbor))
                     {
                         continue;
                     }
                     
                     // 벽이나 문으로 차단되어 있는지 확인
                     if (IsBlockedByWallOrDoor(current, neighbor))
                     {
                         DebugLog($"벽/문으로 차단됨: {current} -> {neighbor}", showScanLogs);
                         continue;
                     }
                     
                     // 바닥이 있으면 큐에 추가
                     if (roomGrid.TryGetValue(neighbor, out RoomCell neighborCell) && neighborCell.isFloor)
                     {
                         queue.Enqueue(neighbor);
                         localVisited.Add(neighbor);
                         DebugLog($"바닥 확장: {current} -> {neighbor}", showScanLogs);
                     }
                 }
             }
             
             DebugLog($"연결된 바닥 탐색 완료: 반복 {iterations}/{maxFloodFillIterations}회, 바닥 {connectedFloors.Count}개", true);
             
             if (iterations >= maxFloodFillIterations)
             {
                 DebugLog($"⚠️ 연결된 바닥 탐색에서 최대 반복 횟수 도달: {startPos}", true);
             }
             if (connectedFloors.Count >= maxRoomSize)
             {
                 DebugLog($"⚠️ 연결된 바닥 탐색에서 최대 방 크기 도달: {startPos}", true);
             }
         }
        
        /// <summary>
        /// 바닥들 주변의 벽과 문 찾기
        /// </summary>
        private void FindWallsAroundFloors(HashSet<Vector3Int> floors, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors)
        {
            Vector3Int[] directions = GetDirections();
            
            foreach (var floorPos in floors)
            {
                foreach (var dir in directions)
                {
                    Vector3Int neighbor = floorPos + dir;
                    
                    // Y축 오프셋을 고려하여 벽과 문 찾기
                    for (int yOffset = 0; yOffset <= 2; yOffset++)
                    {
                        Vector3Int checkPos = new Vector3Int(neighbor.x, neighbor.y + yOffset, neighbor.z);
                        
                        if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                        {
                            if (cell.isWall && !walls.Contains(checkPos))
                            {
                                walls.Add(checkPos);
                                DebugLog($"바닥 {floorPos} 주변 벽 발견: {checkPos}", true);
                            }
                            if (cell.isDoor && !doors.Contains(checkPos))
                            {
                                doors.Add(checkPos);
                                DebugLog($"바닥 {floorPos} 주변 문 발견: {checkPos}", true);
                            }
                        }
                    }
                }
            }
            
            DebugLog($"바닥 주변 벽/문 탐색 완료 - 벽: {walls.Count}개, 문: {doors.Count}개", true);
        }
        
        /// <summary>
        /// 방이 완전히 닫힌 공간인지 검증 (벽으로 둘러싸인 정도 확인)
        /// </summary>
        private bool IsCompletelyEnclosedRoom(HashSet<Vector3Int> floors, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors)
        {
            Vector3Int[] directions = GetDirections();
            int totalBoundaries = 0;
            int enclosedBoundaries = 0;
            
            foreach (var floorPos in floors)
            {
                foreach (var dir in directions)
                {
                    Vector3Int neighbor = floorPos + dir;
                    
                    // 방 내부가 아닌 경계 위치인지 확인
                    if (!floors.Contains(neighbor))
                    {
                        totalBoundaries++;
                        
                        // 이 경계에 벽이나 문이 있는지 확인 (Y축 오프셋 고려)
                        bool hasWallOrDoor = false;
                        for (int yOffset = 0; yOffset <= 2; yOffset++)
                        {
                            Vector3Int checkPos = new Vector3Int(neighbor.x, neighbor.y + yOffset, neighbor.z);
                            if (walls.Contains(checkPos) || doors.Contains(checkPos))
                            {
                                hasWallOrDoor = true;
                                break;
                            }
                        }
                        
                        if (hasWallOrDoor)
                        {
                            enclosedBoundaries++;
                        }
                        else
                        {
                            DebugLog($"열린 경계 발견: 바닥 {floorPos} -> {neighbor} (벽/문 없음)", true);
                        }
                    }
                }
            }
            
            // 90% 이상이 벽이나 문으로 둘러싸여 있어야 닫힌 방으로 인정
            float enclosureRatio = totalBoundaries > 0 ? (float)enclosedBoundaries / totalBoundaries : 0f;
            bool isEnclosed = enclosureRatio >= 0.9f;
            
            DebugLog($"=== 방 폐쇄도 검증 ===\n" +
                     $"총 경계: {totalBoundaries}개\n" +
                     $"막힌 경계: {enclosedBoundaries}개\n" +
                     $"폐쇄율: {enclosureRatio:P1} (요구: 90% 이상)\n" +
                     $"결과: {(isEnclosed ? "닫힌 방" : "열린 공간")}\n" +
                     $"====================", true);
            
            return isEnclosed;
        }
        
        /// <summary>
        /// 시작점 주변의 벽과 문을 찾기
        /// </summary>
        private void FindNearbyWalls(Vector3Int startPos, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors, int maxRadius)
        {
            DebugLog($"시작점 {startPos} 주변 벽 탐색 (반경: {maxRadius})", true);
            
            int totalChecked = 0;
            int wallsFound = 0;
            int doorsFound = 0;
            
            for (int x = startPos.x - maxRadius; x <= startPos.x + maxRadius; x++)
            {
                for (int z = startPos.z - maxRadius; z <= startPos.z + maxRadius; z++)
                {
                    for (int yOffset = 0; yOffset <= 2; yOffset++) // Y축 오프셋 고려
                    {
                        Vector3Int checkPos = new Vector3Int(x, startPos.y + yOffset, z);
                        totalChecked++;
                        
                        if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                        {
                            if (cell.isWall)
                            {
                                walls.Add(checkPos);
                                wallsFound++;
                                DebugLog($"벽 발견: {checkPos}", true);
                            }
                            if (cell.isDoor)
                            {
                                doors.Add(checkPos);
                                doorsFound++;
                                DebugLog($"문 발견: {checkPos}", true);
                            }
                        }
                    }
                }
            }
            
            DebugLog($"벽 탐색 완료 - 총 확인: {totalChecked}개 위치, 벽: {wallsFound}개, 문: {doorsFound}개 발견", true);
        }
        
        /// <summary>
        /// 벽들로 둘러싸인 바닥 영역 찾기
        /// </summary>
        private void FindFloorsWithinWalls(Vector3Int startPos, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors, HashSet<Vector3Int> floors, HashSet<Vector3Int> globalVisited)
        {
            DebugLog($"벽 내부 바닥 탐색 시작: {startPos}");
            
            // 벽들의 경계 박스 계산
            if (walls.Count == 0)
            {
                // 벽이 없으면 시작점만 포함
                floors.Add(startPos);
                globalVisited.Add(startPos);
                return;
            }
            
            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            
            foreach (var wall in walls)
            {
                if (wall.x < minX) minX = wall.x;
                if (wall.x > maxX) maxX = wall.x;
                if (wall.z < minZ) minZ = wall.z;
                if (wall.z > maxZ) maxZ = wall.z;
            }
            
            minX -= 1;
            maxX += 1;
            minZ -= 1;
            maxZ += 1;
            
            DebugLog($"벽 경계 박스: X({minX}~{maxX}), Z({minZ}~{maxZ})");
            
            // 경계 박스 내의 모든 바닥 셀 확인
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3Int floorPos = new Vector3Int(x, startPos.y, z);
                    
                    // 이미 방문한 셀은 스킵
                    if (globalVisited.Contains(floorPos))
                    continue;
                    
                    // 바닥이 있는지 확인
                    if (roomGrid.TryGetValue(floorPos, out RoomCell floorCell) && floorCell.isFloor)
                    {
                        // 이 바닥이 벽들로 둘러싸여 있는지 확인
                        bool isEnclosed = IsFloorEnclosedByWalls(floorPos, walls, doors);
                        DebugLog($"바닥 {floorPos} 둘러싸임 확인: {(isEnclosed ? "YES" : "NO")}", true);
                        
                        if (isEnclosed)
                        {
                            floors.Add(floorPos);
                            globalVisited.Add(floorPos);
                            DebugLog($"둘러싸인 바닥 발견: {floorPos}", true);
                        }
                    }
                    else
                    {
                        if (roomGrid.ContainsKey(floorPos))
                        {
                            DebugLog($"위치 {floorPos}에 바닥 없음 (다른 오브젝트 있음)", true);
                        }
                        else
                        {
                            DebugLog($"위치 {floorPos}에 아무것도 없음", true);
                        }
                    }
                }
            }
            
            DebugLog($"벽 내부 바닥: {floors.Count}개 발견");
        }
        
        /// <summary>
        /// 바닥이 벽들로 둘러싸여 있는지 확인
        /// </summary>
                private bool IsFloorEnclosedByWalls(Vector3Int floorPos, HashSet<Vector3Int> walls, HashSet<Vector3Int> doors)
        {
            Vector3Int[] directions = GetDirections();
            int enclosedSides = 0;
            string[] dirNames = { "오른쪽", "왼쪽", "앞", "뒤" };
            
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3Int checkPos = floorPos + directions[i];
                
                // 벽이나 문이 있는지 확인 (Y축 오프셋 고려)
                bool hasWallOrDoor = false;
                for (int yOffset = 0; yOffset <= 2; yOffset++)
                {
                    Vector3Int adjustedPos = new Vector3Int(checkPos.x, checkPos.y + yOffset, checkPos.z);
                    if (walls.Contains(adjustedPos) || doors.Contains(adjustedPos))
                    {
                        hasWallOrDoor = true;
                        DebugLog($"바닥 {floorPos} {dirNames[i]} 방향에 벽/문 발견: {adjustedPos}", true);
                        break;
                    }
                }
                
                if (hasWallOrDoor)
                {
                    enclosedSides++;
                }
                else
                {
                    DebugLog($"바닥 {floorPos} {dirNames[i]} 방향에 벽/문 없음", true);
                }
            }
            
            bool isEnclosed = enclosedSides >= 2;
            DebugLog($"바닥 {floorPos} 둘러싸임 결과: {enclosedSides}/4면 둘러싸임, 최소 2면 필요 → {(isEnclosed ? "성공" : "실패")}", true);
            
            return isEnclosed;
        }
        
        /// <summary>
        /// 방 오브젝트들 수집
        /// </summary>
        private void CollectRoomObjects(HashSet<Vector3Int> walls, HashSet<Vector3Int> doors, HashSet<Vector3Int> floors, RoomInfo room)
        {
            DebugLog("방 오브젝트 수집 시작");
            
            HashSet<GameObject> foundWalls = new HashSet<GameObject>();
            HashSet<GameObject> foundDoors = new HashSet<GameObject>();
            HashSet<GameObject> foundBeds = new HashSet<GameObject>();
            HashSet<GameObject> foundSunbeds = new HashSet<GameObject>();
            
            // 벽 오브젝트 수집
            foreach (var wallPos in walls)
            {
                if (roomGrid.TryGetValue(wallPos, out RoomCell wallCell))
                {
                    foreach (var obj in wallCell.objects)
                    {
                        foundWalls.Add(obj);
                    }
                }
            }
            
            // 문 오브젝트 수집
            foreach (var doorPos in doors)
            {
                if (roomGrid.TryGetValue(doorPos, out RoomCell doorCell))
                {
                    foreach (var obj in doorCell.objects)
                    {
                        foundDoors.Add(obj);
                    }
                }
            }
            
            // 바닥 영역에서 침대와 선베드 찾기
            foreach (var floorPos in floors)
            {
                for (int yOffset = -1; yOffset <= 2; yOffset++)
                {
                    Vector3Int checkPos = new Vector3Int(floorPos.x, floorPos.y + yOffset, floorPos.z);
                    if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                    {
                        if (cell.isBed)
                        {
                            foreach (var obj in cell.objects)
                            {
                                foundBeds.Add(obj);
                            }
                        }
                        if (cell.isSunbed)
                        {
                            foreach (var obj in cell.objects)
                            {
                                foundSunbeds.Add(obj);
                            }
                        }
                    }
                }
            }
            
            // 방 정보에 추가
            room.walls.AddRange(foundWalls);
            room.doors.AddRange(foundDoors);
            room.beds.AddRange(foundBeds);
            room.sunbeds.AddRange(foundSunbeds);
            
            DebugLog($"오브젝트 수집 완료 - 벽: {room.walls.Count}, 문: {room.doors.Count}, 침대: {room.beds.Count}, 선베드: {room.sunbeds.Count}");
        }
        
        /// <summary>
        /// 방 요소들(벽, 문, 침대) 찾기
        /// </summary>
        private void FindRoomElements(HashSet<Vector3Int> floorCells, RoomInfo room)
        {
            Vector3Int[] directions = GetDirections();
            HashSet<GameObject> foundWalls = new HashSet<GameObject>();
            HashSet<GameObject> foundDoors = new HashSet<GameObject>();
            HashSet<GameObject> foundBeds = new HashSet<GameObject>();
            HashSet<GameObject> foundSunbeds = new HashSet<GameObject>();
            
            foreach (var floorPos in floorCells)
            {
                // 각 바닥 셀 주변에서 방 요소 찾기
                foreach (var dir in directions)
                {
                    Vector3Int neighbor = floorPos + dir;
                    
                    // 벽과 문 찾기 (Y축 오프셋 고려)
                    for (int yOffset = 0; yOffset <= 2; yOffset++)
                    {
                        Vector3Int checkPos = new Vector3Int(neighbor.x, neighbor.y + yOffset, neighbor.z);
                        if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                        {
                            if (cell.isWall)
                            {
                                foreach (var obj in cell.objects)
                                {
                                    foundWalls.Add(obj);
                                }
                            }
                            
                            if (cell.isDoor)
                            {
                                foreach (var obj in cell.objects)
                                {
                                    foundDoors.Add(obj);
                                }
                            }
                        }
                    }
                }
                
                // 침대와 선베드 찾기 (같은 위치 또는 Y축 오프셋)
                for (int yOffset = -1; yOffset <= 2; yOffset++)
                {
                    Vector3Int checkPos = new Vector3Int(floorPos.x, floorPos.y + yOffset, floorPos.z);
                    if (roomGrid.TryGetValue(checkPos, out RoomCell cell))
                    {
                        if (cell.isBed)
                        {
                            foreach (var obj in cell.objects)
                            {
                                foundBeds.Add(obj);
                            }
                        }
                        
                        if (cell.isSunbed)
                        {
                            foreach (var obj in cell.objects)
                            {
                                foundSunbeds.Add(obj);
                            }
                        }
                    }
                }
            }
            
            // HashSet을 List로 변환
            room.walls.AddRange(foundWalls);
            room.doors.AddRange(foundDoors);
            room.beds.AddRange(foundBeds);
            room.sunbeds.AddRange(foundSunbeds);
            
            DebugLog($"방 요소 수집 완료 - 벽: {room.walls.Count}, 문: {room.doors.Count}, 침대: {room.beds.Count}, 선베드: {room.sunbeds.Count}");
        }
        
        /// <summary>
        /// 특정 위치에 벽이 있는지 확인 (Y축 오프셋 고려)
        /// </summary>
        private bool HasWallAt(Vector3Int position)
        {
            // 기본 위치 확인
            if (roomGrid.TryGetValue(position, out RoomCell cell) && cell.isWall)
            {
                return true;
            }
            
            // Y축 위쪽 확인 (벽이 바닥보다 위에 있을 수 있음)
            for (int yOffset = 1; yOffset <= 2; yOffset++)
            {
                Vector3Int upperPos = new Vector3Int(position.x, position.y + yOffset, position.z);
                if (roomGrid.TryGetValue(upperPos, out RoomCell upperCell) && upperCell.isWall)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 두 위치 사이에 벽이나 문이 있어서 방 확장이 차단되는지 확인
        /// </summary>
        private bool IsBlockedByWallOrDoor(Vector3Int from, Vector3Int to)
        {
            // 목표 위치 자체에 벽이 있는지 확인 (Y축 오프셋 고려)
            if (HasWallAt(to))
            {
                return true;
            }
            
            // 목표 위치에 문이 있는지도 확인 (문은 방 경계를 의미)
            if (HasDoorAt(to))
            {
                return true;
            }
            
            // 두 위치 사이의 중간 지점에서도 벽 확인 (대각선 이동 시)
            if (Mathf.Abs(to.x - from.x) + Mathf.Abs(to.z - from.z) > 1)
            {
                // 대각선 이동인 경우 중간 경로 확인
                Vector3Int midX = new Vector3Int(to.x, from.y, from.z);
                Vector3Int midZ = new Vector3Int(from.x, from.y, to.z);
                
                if (HasWallAt(midX) || HasWallAt(midZ))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 특정 위치에 문이 있는지 확인 (Y축 오프셋 고려)
        /// </summary>
        private bool HasDoorAt(Vector3Int position)
        {
            // 기본 위치 확인
            if (roomGrid.TryGetValue(position, out RoomCell cell) && cell.isDoor)
            {
                return true;
            }
            
            // Y축 위쪽 확인 (문이 바닥보다 위에 있을 수 있음)
            for (int yOffset = 1; yOffset <= 2; yOffset++)
            {
                Vector3Int upperPos = new Vector3Int(position.x, position.y + yOffset, position.z);
                if (roomGrid.TryGetValue(upperPos, out RoomCell upperCell) && upperCell.isDoor)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 방향 벡터 반환
        /// </summary>
        private Vector3Int[] GetDirections()
        {
            return new Vector3Int[]
            {
                new Vector3Int(1, 0, 0),   // 오른쪽
                new Vector3Int(-1, 0, 0),  // 왼쪽
                new Vector3Int(0, 0, 1),   // 앞
                new Vector3Int(0, 0, -1)   // 뒤
            };
        }
        
                 /// <summary>
         /// 방 경계 및 ID 설정 (벽 경계면 기준으로 방 크기 계산)
         /// </summary>
         private void SetupRoomBounds(RoomInfo room)
         {
             if (room.walls.Count == 0 && room.doors.Count == 0)
             {
                 DebugLog("벽이나 문이 없어서 방 경계를 설정할 수 없습니다.", true);
                 return;
             }
             
             // 벽과 문의 위치를 기준으로 방 경계 계산
             List<Vector3> boundaryPositions = new List<Vector3>();
             
             // 벽 위치 추가
             foreach (var wall in room.walls)
             {
                 if (wall != null)
                 {
                     Vector3 wallPos = wall.transform.position;
                     boundaryPositions.Add(wallPos);
                     DebugLog($"🧱 벽 위치: {wall.name} at {wallPos} (Y={wallPos.y:F1})", true);
                 }
             }
             
             // 문 위치 추가
             foreach (var door in room.doors)
             {
                 if (door != null)
                 {
                     Vector3 doorPos = door.transform.position;
                     boundaryPositions.Add(doorPos);
                     DebugLog($"🚪 문 위치: {door.name} at {doorPos} (Y={doorPos.y:F1})", true);
                 }
             }
             
             // 침대 위치도 고려 (방 크기 결정에 도움)
             foreach (var bed in room.beds)
             {
                 if (bed != null)
                 {
                     Vector3 bedPos = bed.transform.position;
                     boundaryPositions.Add(bedPos);
                     DebugLog($"🛏️ 침대 위치: {bed.name} at {bedPos} (Y={bedPos.y:F1})", true);
                 }
             }
             
             if (boundaryPositions.Count == 0)
             {
                 DebugLog("유효한 벽이나 문 오브젝트가 없습니다.", true);
                 return;
             }
             
             // 벽 경계면 기준으로 최소/최대 좌표 계산
             float minX = boundaryPositions.AsValueEnumerable().Min(p => p.x);
             float maxX = boundaryPositions.AsValueEnumerable().Max(p => p.x);
             float minZ = boundaryPositions.AsValueEnumerable().Min(p => p.z);
             float maxZ = boundaryPositions.AsValueEnumerable().Max(p => p.z);
             float baseY = boundaryPositions.AsValueEnumerable().Min(p => p.y);
             
             DebugLog($"📏 경계 계산: X({minX:F1}~{maxX:F1}), Z({minZ:F1}~{maxZ:F1}), baseY={baseY:F1}", true);
             
             // FloorConstants를 사용하여 정확한 층 계산
             int floorLevel = FloorConstants.GetFloorLevel(baseY);
             float floorBaseY = FloorConstants.GetFloorBaseY(floorLevel);
             
             DebugLog($"🏢 층 계산: baseY({baseY:F1}) → 층레벨({floorLevel}) → 기준Y({floorBaseY:F1})", true);
             
             // 벽 두께를 고려하여 내부 공간 계산 (벽 안쪽으로 약간 들어간 위치)
             float wallThickness = 0.5f; // 벽 두께의 절반
             float roomMinX = minX + wallThickness;
             float roomMaxX = maxX - wallThickness;
             float roomMinZ = minZ + wallThickness;
             float roomMaxZ = maxZ - wallThickness;
             
             // 최소 방 크기 보장 (너무 작으면 확장)
             float minRoomSize = 2.0f; // 최소 방 크기를 늘림
             float roomWidth = roomMaxX - roomMinX;
             float roomDepth = roomMaxZ - roomMinZ;
             
             if (roomWidth < minRoomSize)
             {
                 float centerX = (roomMinX + roomMaxX) / 2f;
                 roomMinX = centerX - minRoomSize / 2f;
                 roomMaxX = centerX + minRoomSize / 2f;
                 roomWidth = minRoomSize;
             }
             
             if (roomDepth < minRoomSize)
             {
                 float centerZ = (roomMinZ + roomMaxZ) / 2f;
                 roomMinZ = centerZ - minRoomSize / 2f;
                 roomMaxZ = centerZ + minRoomSize / 2f;
                 roomDepth = minRoomSize;
             }
             
             // 방 바운더리 설정 (층별 정확한 Y축 사용)
             room.bounds = new Bounds();
             Vector3 boundsMin = new Vector3(roomMinX, floorBaseY, roomMinZ);
             Vector3 boundsMax = new Vector3(roomMaxX, floorBaseY + FloorConstants.ROOM_HEIGHT, roomMaxZ);
             room.bounds.SetMinMax(boundsMin, boundsMax);
             
             room.center = room.bounds.center;
             room.floorLevel = floorLevel;
             room.roomId = $"Room_F{room.floorLevel}_{room.center.x:F0}_{room.center.z:F0}";
             
             DebugLog($"🎯 최종 바운더리: Min({boundsMin}) Max({boundsMax}) Center({room.center})", true);
             
             DebugLog($"벽 경계면 기준 방 경계 설정 완료:\n" +
                      $"벽 경계 범위: X({minX:F1}~{maxX:F1}), Z({minZ:F1}~{maxZ:F1}), Y({baseY:F1})\n" +
                      $"방 내부 범위: X({roomMinX:F1}~{roomMaxX:F1}), Z({roomMinZ:F1}~{roomMaxZ:F1})\n" +
                      $"층 정보: {floorLevel}층 (기준 Y: {floorBaseY:F1}, 높이: {FloorConstants.ROOM_HEIGHT:F1})\n" +
                      $"방 크기: {room.bounds.size} (W:{roomWidth:F1} x D:{roomDepth:F1} x H:{FloorConstants.ROOM_HEIGHT:F1})\n" +
                      $"벽: {room.walls.Count}개, 문: {room.doors.Count}개, 침대: {room.beds.Count}개", true);
         }
        
        /// <summary>
        /// Y축 오프셋을 고려하여 침대와 선베드를 찾는 헬퍼 메서드
        /// </summary>
        private void CheckForBedsAndSunbeds(Vector3Int basePosition, RoomInfo room)
        {
            // 기본 위치에서 확인
            if (roomGrid.TryGetValue(basePosition, out RoomCell baseCell))
            {
                if (baseCell.isBed)
                {
                    foreach (var obj in baseCell.objects)
                    {
                        if (!room.beds.Contains(obj))
                        {
                            room.beds.Add(obj);
                            DebugLog($"침대 발견: {basePosition}");
                        }
                    }
                }
                
                if (baseCell.isSunbed)
                {
                    foreach (var obj in baseCell.objects)
                    {
                        if (!room.sunbeds.Contains(obj))
                        {
                            room.sunbeds.Add(obj);
                            DebugLog($"Sunbed 발견: {basePosition}");
                        }
                    }
                }
            }
            
            // Y축 위아래에서도 확인 (침대가 바닥과 다른 높이에 있을 수 있음)
            for (int yOffset = -1; yOffset <= 2; yOffset++)
            {
                if (yOffset == 0) continue; // 기본 위치는 이미 확인했음
                
                Vector3Int adjustedPos = new Vector3Int(basePosition.x, basePosition.y + yOffset, basePosition.z);
                if (roomGrid.TryGetValue(adjustedPos, out RoomCell adjustedCell))
                {
                    if (adjustedCell.isBed)
                    {
                        foreach (var obj in adjustedCell.objects)
                        {
                            if (!room.beds.Contains(obj))
                            {
                                room.beds.Add(obj);
                                DebugLog($"침대 발견 (Y{yOffset:+0;-0}): {adjustedPos}");
                            }
                        }
                    }
                    
                    if (adjustedCell.isSunbed)
                    {
                        foreach (var obj in adjustedCell.objects)
                        {
                            if (!room.sunbeds.Contains(obj))
                            {
                                room.sunbeds.Add(obj);
                                DebugLog($"Sunbed 발견 (Y{yOffset:+0;-0}): {adjustedPos}");
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 방 유효성 검증 (기본 검증만)
        /// </summary>
        private bool ValidateRoomSize(RoomInfo room)
        {
            // 크기 제한 없음 - 벽으로 구분되면 모든 크기 허용
            return true;
        }
        
        /// <summary>
        /// 방이 벽으로 제대로 둘러싸여 있는지 검증 (완화된 기준)
        /// </summary>
        private bool ValidateRoomEnclosure(RoomInfo room, HashSet<Vector3Int> roomCells)
        {
            // 방의 경계를 따라 벽이나 문이 있는지 확인
            HashSet<Vector3Int> boundaryPositions = new HashSet<Vector3Int>();
            
            Vector3Int[] directions = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
            };
            
            // 모든 바닥 셀의 인접한 위치 중 방 외부 위치를 찾음
            foreach (var floorPos in roomCells)
            {
                foreach (var dir in directions)
                {
                    Vector3Int adjacentPos = floorPos + dir;
                    
                    // 방 내부가 아닌 위치
                    if (!roomCells.Contains(adjacentPos))
                    {
                        boundaryPositions.Add(adjacentPos);
                    }
                }
            }
            
            // 경계 위치 중 벽이나 문이 없는 빈 공간이 있는지 확인
            int openBoundaries = 0;
            int totalBoundaries = boundaryPositions.Count;
            
            foreach (var boundaryPos in boundaryPositions)
            {
                if (roomGrid.TryGetValue(boundaryPos, out RoomCell boundaryCell))
                {
                    // 벽이나 문이 있으면 올바른 경계
                    if (!boundaryCell.isWall && !boundaryCell.isDoor)
                    {
                        openBoundaries++;
                    }
                }
                else
                {
                    // 그리드에 없는 위치 = 빈 공간
                    openBoundaries++;
                }
            }
            
            // 경계의 30% 이상이 벽이나 문으로 막혀있으면 유효한 방으로 인정 (기준 완화: 80% -> 30%)
            float enclosureRatio = totalBoundaries > 0 ? (float)(totalBoundaries - openBoundaries) / totalBoundaries : 1f;
            bool isEnclosed = enclosureRatio >= 0.3f || totalBoundaries == 0; // 경계가 없어도 허용
            
            DebugLog($"=== 방 경계 검증 (완화) ===\n" +
                     $"방 바닥 셀 개수: {roomCells.Count}개\n" +
                     $"총 경계 위치: {totalBoundaries}개\n" +
                     $"열린 경계: {openBoundaries}개\n" +
                     $"막힌 경계: {totalBoundaries - openBoundaries}개\n" +
                     $"폐쇄율: {enclosureRatio:P1} (요구: 30% 이상)\n" +
                     $"검증 결과: {(isEnclosed ? "✅ 통과" : "❌ 실패")}\n" +
                     $"===================", true);
            
            return isEnclosed;
        }

        /// <summary>
        /// 벽의 위치를 고려하여 방의 실제 경계를 계산
        /// </summary>
        private void CalculateRoomBoundsFromWalls(RoomInfo room, Vector3Int floorMinBounds, Vector3Int floorMaxBounds)
        {
            // 기본값은 바닥 영역으로 설정
            Vector3 worldFloorMin = grid.GetCellCenterWorld(floorMinBounds);
            Vector3 worldFloorMax = grid.GetCellCenterWorld(floorMaxBounds);
            
            Vector3 realMinBounds = worldFloorMin;
            Vector3 realMaxBounds = worldFloorMax;
            
            if (room.walls.Count > 0)
            {
                // 벽의 위치를 기준으로 방 경계 재계산
                Vector3 wallMinBounds = room.walls[0].transform.position;
                Vector3 wallMaxBounds = room.walls[0].transform.position;
                
                foreach (var wall in room.walls)
                {
                    Vector3 wallPos = wall.transform.position;
                    wallMinBounds = Vector3.Min(wallMinBounds, wallPos);
                    wallMaxBounds = Vector3.Max(wallMaxBounds, wallPos);
                }
                
                // 벽의 경계에서 약간 안쪽으로 방 영역 설정 (벽 두께 고려)
                float wallThickness = 0.5f; // 벽 두께의 절반
                realMinBounds = new Vector3(
                    wallMinBounds.x + wallThickness, 
                    worldFloorMin.y, 
                    wallMinBounds.z + wallThickness
                );
                realMaxBounds = new Vector3(
                    wallMaxBounds.x - wallThickness, 
                    worldFloorMax.y + 3f, // 방 높이 
                    wallMaxBounds.z - wallThickness
                );
                
                // 바닥 영역을 넘지 않도록 제한
                realMinBounds.x = Mathf.Max(realMinBounds.x, worldFloorMin.x - 0.5f);
                realMinBounds.z = Mathf.Max(realMinBounds.z, worldFloorMin.z - 0.5f);
                realMaxBounds.x = Mathf.Min(realMaxBounds.x, worldFloorMax.x + 0.5f);
                realMaxBounds.z = Mathf.Min(realMaxBounds.z, worldFloorMax.z + 0.5f);
                
                DebugLog($"벽 기준 방 경계 계산:\n" +
                         $"바닥 영역: {worldFloorMin} ~ {worldFloorMax}\n" +
                         $"벽 영역: {wallMinBounds} ~ {wallMaxBounds}\n" +
                         $"최종 방 영역: {realMinBounds} ~ {realMaxBounds}");
            }
            else
            {
                // 벽이 없는 경우 바닥 영역 그대로 사용하되 높이만 설정
                realMaxBounds.y = worldFloorMin.y + 3f;
                DebugLog($"벽 없음 - 바닥 기준 방 경계: {realMinBounds} ~ {realMaxBounds}");
            }
            
            // 방 객체에 경계 설정
            room.bounds = new Bounds();
            room.bounds.SetMinMax(realMinBounds, realMaxBounds);
            room.center = room.bounds.center;
        }
        

        // 추가: 벽으로 둘러싸인 영역을 더 정확히 감지하는 헬퍼 메서드 (완화된 기준)
        private bool IsEnclosedByWalls(Vector3Int position, HashSet<Vector3Int> roomCells)
        {
            Vector3Int[] directions = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
                new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1)
            };

            int wallCount = 0;
            int doorCount = 0;

            foreach (var dir in directions)
            {
                Vector3Int neighbor = position + dir;
                
                if (roomGrid.TryGetValue(neighbor, out RoomCell neighborCell))
                {
                    if (neighborCell.isWall) wallCount++;
                    if (neighborCell.isDoor) doorCount++;
                }
            }

            // 최소 1면이 벽으로 둘러싸여 있거나 문이 있으면 방으로 인정 (기준 완화)
            return wallCount >= 1 || doorCount >= 1;
        }

        // 방 검증을 유연하게 하는 메서드 (크기 제한 없음)
        private bool ValidateRoomStructure(RoomInfo room)
        {
            if (room.floorCells.Count == 0) return false;
            
            // 크기 제한 제거 - 모든 크기 허용
            // 단지 유효한 바닥 셀이 있는지만 확인
            
            // 벽으로 둘러싸인 정도 확인 (기준 완화)
            int enclosedCells = 0;
            foreach (var floorCell in room.floorCells)
            {
                if (IsEnclosedByWalls(floorCell, new HashSet<Vector3Int>(room.floorCells)))
                {
                    enclosedCells++;
                }
            }
            
            // 최소 30% 이상의 바닥이 벽으로 둘러싸여 있거나, 벽이 아예 없어도 허용 (기준 완화)
            return room.floorCells.Count >= 1 && (enclosedCells == 0 || (float)enclosedCells / room.floorCells.Count >= 0.3f);
        }

        private bool AreRoomListsEqual(List<RoomInfo> list1, List<RoomInfo> list2)
        {
            if (list1.Count != list2.Count)
                return false;

            var sortedList1 = list1.AsValueEnumerable().OrderBy(r => r.center.x).ThenBy(r => r.center.z).ToList();
            var sortedList2 = list2.AsValueEnumerable().OrderBy(r => r.center.x).ThenBy(r => r.center.z).ToList();

            for (int i = 0; i < sortedList1.Count; i++)
            {
                if (!AreRoomsEqual(sortedList1[i], sortedList2[i]))
                    return false;
            }
            return true;
        }

        private bool AreRoomsEqual(RoomInfo room1, RoomInfo room2)
        {
            if (room1.roomId != room2.roomId || room1.center != room2.center)
                return false;

            if (room1.walls.Count != room2.walls.Count ||
                room1.doors.Count != room2.doors.Count ||
                room1.beds.Count != room2.beds.Count ||
                room1.floorCells.Count != room2.floorCells.Count)
                return false;

            bool wallsEqual = room1.walls.AsValueEnumerable().All(w1 => room2.walls.AsValueEnumerable().Any(w2 => w2.GetInstanceID() == w1.GetInstanceID()));
            bool doorsEqual = room1.doors.AsValueEnumerable().All(d1 => room2.doors.AsValueEnumerable().Any(d2 => d2.GetInstanceID() == d1.GetInstanceID()));
            bool bedsEqual = room1.beds.AsValueEnumerable().All(b1 => room2.beds.AsValueEnumerable().Any(b2 => b2.GetInstanceID() == b1.GetInstanceID()));

            if (!wallsEqual || !doorsEqual || !bedsEqual)
                return false;

            var sortedFloors1 = room1.floorCells.AsValueEnumerable().OrderBy(v => v.x).ThenBy(v => v.z).ToList();
            var sortedFloors2 = room2.floorCells.AsValueEnumerable().OrderBy(v => v.x).ThenBy(v => v.z).ToList();
            
            for (int i = 0; i < sortedFloors1.Count; i++)
            {
                if (sortedFloors1[i] != sortedFloors2[i])
                    return false;
            }

            return true;
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !showDebugLogs || detectedRooms == null)
                return;

            Gizmos.color = Color.yellow;
            Vector3 gridCenter = transform.position;
            Vector3 gridSize = new Vector3(100f, 10f, 100f);
            Gizmos.DrawWireCube(gridCenter, gridSize);

            foreach (var room in detectedRooms)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(room.bounds.center, room.bounds.size);

                foreach (var wall in room.walls)
                {
                    if (wall != null)
                    {
                        Gizmos.color = Color.blue;
                        Gizmos.DrawWireCube(wall.transform.position, Vector3.one);
                        Gizmos.DrawLine(wall.transform.position, room.bounds.center);
                    }
                }

                foreach (var door in room.doors)
                {
                    if (door != null)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(door.transform.position, 0.5f);
                        Gizmos.DrawLine(door.transform.position, room.bounds.center);
                    }
                }

                foreach (var bed in room.beds)
                {
                    if (bed != null)
                    {
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireCube(bed.transform.position, new Vector3(1f, 0.5f, 2f));
                        Gizmos.DrawLine(bed.transform.position, room.bounds.center);
                    }
                }

                /*UnityEditor.Handles.Label(room.bounds.center, 
                    $"Room {room.roomId}\nWalls: {room.walls.Count}\n" +
                    $"Doors: {room.doors.Count}\nBeds: {room.beds.Count}\n" +
                    $"Valid: {room.isValid(minWalls, minDoors, minBeds)}");*/
            }
        }

        private void CreateRoomGameObject(RoomInfo room)
        {
            // 층 정보를 포함한 더 명확한 방 이름 생성
            string roomName = room.isSunbedRoom ? 
                $"SunbedRoom_F{room.floorLevel}_{room.center.x:F0}_{room.center.z:F0}" : 
                $"Room_F{room.floorLevel}_{room.center.x:F0}_{room.center.z:F0}";
                
            room.gameObject = new GameObject(roomName);
            room.gameObject.transform.position = room.center;
            
            // ✅ 선베드는 방이 아니므로 Room 태그를 달지 않음!
            if (!room.isSunbedRoom)
            {
                room.gameObject.tag = "Room";
            }

            // RoomManager를 통해 방 등록 (Sunbed 방 설정 포함)
            RoomManager roomManager = FindFirstObjectByType<RoomManager>();
            if (roomManager != null)
            {
                roomManager.RegisterRoomFromDetector(room, room.gameObject);
            }
            else
            {
                // RoomManager가 없는 경우 직접 RoomContents 추가
                var roomContents = room.gameObject.AddComponent<RoomContents>();
                roomContents.roomID = room.roomId;
                roomContents.SetRoomBounds(room.bounds);
                
                if (room.isSunbedRoom)
                {
                    roomContents.SetAsSunbedRoom(room.fixedPrice, room.fixedReputation);
                }
            }

            BoxCollider roomCollider = room.gameObject.AddComponent<BoxCollider>();
            roomCollider.center = new Vector3(0, FloorConstants.ROOM_HEIGHT / 2f, 0);
            roomCollider.size = new Vector3(room.bounds.size.x, FloorConstants.ROOM_HEIGHT, room.bounds.size.z);
            roomCollider.isTrigger = true;

            DebugLog($"방 생성: {room.roomId}\n" +
                     $"- 층: {room.floorLevel}층\n" +
                     $"- 위치: {room.center}\n" +
                     $"- 벽: {room.walls.Count}개\n" +
                     $"- 문: {room.doors.Count}개\n" +
                     $"- 침대: {room.beds.Count}개\n" +
                     $"- Sunbed: {room.sunbeds.Count}개\n" +
                     $"- 바닥: {room.floorCells.Count}개\n" +
                     $"- Sunbed 방: {room.isSunbedRoom}");
        }

        // Sunbed 방 찾기
        private List<RoomInfo> FindSunbedRooms(List<RoomInfo> existingRooms)
        {
            List<RoomInfo> sunbedRooms = new List<RoomInfo>();
            
            // 모든 sunbed 오브젝트 찾기
            var sunbedObjects = GameObject.FindGameObjectsWithTag("Sunbed");
            DebugLog($"Sunbed 태그 오브젝트 수: {sunbedObjects.Length}");
            
            foreach (var sunbedObj in sunbedObjects)
            {
                if (sunbedObj == null) continue;
                
                Vector3Int sunbedGridPos = GetParentGridPosition(sunbedObj);
                
                // 기존 방에 포함되어 있는지 확인
                bool isInsideExistingRoom = false;
                foreach (var existingRoom in existingRooms)
                {
                    if (IsPositionInsideRoom(sunbedGridPos, existingRoom))
                    {
                        isInsideExistingRoom = true;
                        DebugLog($"Sunbed {sunbedObj.name}은 기존 방 {existingRoom.roomId} 내부에 있음");
                        break;
                    }
                }
                
                // 기존 방에 포함되지 않은 경우에만 독립적인 방으로 생성
                if (!isInsideExistingRoom)
                {
                    RoomInfo sunbedRoom = CreateSunbedRoom(sunbedObj, sunbedGridPos);
                    if (sunbedRoom != null)
                    {
                        sunbedRooms.Add(sunbedRoom);
                        DebugLog($"독립적인 Sunbed 방 생성: {sunbedRoom.roomId}");
                    }
                    else
                    {
                    }
                }
            }
            
            return sunbedRooms;
        }

        // 위치가 방 내부에 있는지 확인
        private bool IsPositionInsideRoom(Vector3Int position, RoomInfo room)
        {
            // 방의 바닥 셀 중 하나와 일치하거나 인접한지 확인
            foreach (var floorCell in room.floorCells)
            {
                if (floorCell == position)
                {
                    return true;
                }
                
                // 인접 셀도 확인 (1칸 거리)
                float distance = Vector3Int.Distance(floorCell, position);
                if (distance <= 1.5f) // 대각선 포함
                {
                    return true;
                }
            }
            
            return false;
        }

        // Sunbed 방 생성
        private RoomInfo CreateSunbedRoom(GameObject sunbedObj, Vector3Int gridPos)
        {
            RoomInfo room = new RoomInfo();
            room.isSunbedRoom = true;
            room.fixedPrice = sunbedRoomPrice;
            room.fixedReputation = sunbedRoomReputation;
            
            // Sunbed를 중심으로 한 작은 영역 설정
            room.floorCells.Add(gridPos);
            room.sunbeds.Add(sunbedObj.transform.parent?.gameObject ?? sunbedObj);
            
            // FloorConstants를 사용하여 정확한 층 정보 계산
            float sunbedY = sunbedObj.transform.position.y;
            room.floorLevel = FloorConstants.GetFloorLevel(sunbedY);
            float floorBaseY = FloorConstants.GetFloorBaseY(room.floorLevel);
            
            // 방 경계 설정 (sunbed 위치 기준, 층별 정확한 Y축 사용)
            Vector3 worldPos = grid.GetCellCenterWorld(gridPos);
            worldPos.y = floorBaseY; // 층 기준 Y축으로 조정
            
            room.bounds = new Bounds();
            room.bounds.SetMinMax(
                new Vector3(worldPos.x - 1f, floorBaseY, worldPos.z - 1f),
                new Vector3(worldPos.x + 1f, floorBaseY + FloorConstants.ROOM_HEIGHT, worldPos.z + 1f)
            );
            room.center = room.bounds.center;
            
            string roomId = $"SunbedRoom_F{room.floorLevel}_{room.center.x:F0}_{room.center.z:F0}";
            room.roomId = roomId;
            
            DebugLog($"Sunbed 방 생성:\n" +
                     $"ID: {room.roomId}\n" +
                     $"위치: {room.center} (원래 Y: {sunbedY:F1}, 조정된 Y: {worldPos.y:F1})\n" +
                     $"층: {room.floorLevel}층 (기준 Y: {floorBaseY:F1})\n" +
                     $"방 크기: {room.bounds.size}\n" +
                     $"고정 가격: {room.fixedPrice}\n" +
                     $"고정 명성도: {room.fixedReputation}");
            
            return room;
        }

        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
        }
        
        /// <summary>
        /// 셀 내용 설명을 반환하는 헬퍼 메서드
        /// </summary>
        private string GetCellContentDescription(RoomCell cell)
        {
            List<string> contents = new List<string>();
            if (cell.isFloor) contents.Add("바닥");
            if (cell.isWall) contents.Add("벽");
            if (cell.isDoor) contents.Add("문");
            if (cell.isBed) contents.Add("침대");
            if (cell.isSunbed) contents.Add("선베드");
            
            return contents.Count > 0 ? string.Join(", ", contents) : "빈 셀";
        }
    }
} 