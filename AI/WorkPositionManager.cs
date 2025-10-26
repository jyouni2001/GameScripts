using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JY
{
    /// <summary>
    /// AI 작업 위치 관리 시스템
    /// 각 작업 타입별로 사용 가능한 위치를 관리하고 중복 사용을 방지
    /// </summary>
    public class WorkPositionManager : MonoBehaviour
    {
        public static WorkPositionManager Instance { get; private set; }
        
        [Header("작업 위치 설정")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showOccupiedPositions = true;
        
        // 작업 위치 데이터 저장
        private Dictionary<string, List<WorkPosition>> workPositions = new Dictionary<string, List<WorkPosition>>();
        private Dictionary<AIEmployee, WorkPosition> assignedPositions = new Dictionary<AIEmployee, WorkPosition>();
        
        // 이벤트
        public static event Action<string, int> OnPositionAvailabilityChanged;
        
        #region Unity 생명주기
        
        void Awake()
        {
            InitializeSingleton();
        }
        
        void Start()
        {
            InitializeWorkPositions();
            SubscribeToPlacementEvents();
        }
        
        void OnDestroy()
        {
            UnsubscribeFromPlacementEvents();
        }
        
        #endregion
        
        #region 초기화
        
        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 기존에 배치된 오브젝트들을 스캔하여 작업 위치 초기화
        /// </summary>
        private void InitializeWorkPositions()
        {
            // 태그 기반으로 기존 작업 위치들 찾기
            ScanForWorkPositions();
            
            DebugLog($"작업 위치 초기화 완료. 총 {workPositions.Count}개 타입", true);
        }
        
        /// <summary>
        /// 씬에서 작업 위치 태그를 가진 오브젝트들을 스캔
        /// </summary>
        private void ScanForWorkPositions()
        {
            // 하드코딩된 태그 목록 제거 - 필요시 나중에 동적으로 추가
            DebugLog("작업 위치 스캔 시작 - 현재는 빈 상태로 초기화", true);
            DebugLog("작업 위치가 필요하면 RegisterWorkPositionByTag() 메서드를 사용하여 추가하세요.", true);
        }
        
        /// <summary>
        /// 태그가 Unity Tag Manager에 정의되어 있는지 확인
        /// </summary>
        private bool IsTagDefined(string tagName)
        {
            try
            {
                // CompareTag은 정의되지 않은 태그에 대해 false를 반환하고 예외를 던지지 않음
                // 하지만 FindGameObjectsWithTag는 예외를 던지므로 다른 방법 사용
                
                // UnityEditorInternal.InternalEditorUtility.tags를 사용할 수 있지만
                // 런타임에서는 사용할 수 없으므로 try-catch 방식 사용
                GameObject.FindGameObjectsWithTag(tagName);
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
        }
        
        /// <summary>
        /// 오브젝트 배치 시스템과 연동
        /// </summary>
        private void SubscribeToPlacementEvents()
        {
            // PlacementSystem의 이벤트가 있다면 구독
            // 현재 코드에는 해당 이벤트가 없으므로 필요시 추가
        }
        
        private void UnsubscribeFromPlacementEvents()
        {
            // 이벤트 구독 해제
        }
        
        #endregion
        
        #region 작업 위치 할당
        
        /// <summary>
        /// AI에게 적절한 작업 위치 할당
        /// </summary>
        /// <param name="employee">작업 위치가 필요한 AI 직원</param>
        /// <returns>할당된 작업 위치, 없으면 null</returns>
        public Transform AssignWorkPosition(AIEmployee employee)
        {
            if (employee == null)
            {
                DebugLog("AI 직원이 null입니다.", true);
                return null;
            }
            
            string jobType = employee.jobRole.ToLower();
            
            // 이미 할당된 위치가 있으면 반환
            if (assignedPositions.ContainsKey(employee))
            {
                var currentPos = assignedPositions[employee];
                if (currentPos.isOccupied && currentPos.occupiedBy == employee)
                {
                    DebugLog($"{employee.employeeName}은 이미 작업 위치가 할당되어 있습니다: {currentPos.positionId}");
                    return currentPos.position;
                }
            }
            
            // 해당 직업에 맞는 사용 가능한 위치 찾기
            if (!workPositions.ContainsKey(jobType))
            {
                DebugLog($"❌ '{jobType}' 직업에 대한 작업 위치가 없습니다! 태그를 수동으로 설정하세요.", true);
                // 자동 생성 비활성화 - 사용자가 직접 태그 설정해야 함
                // return CreateDynamicWorkPosition(employee);
                return null;
            }
            
            var availablePositions = workPositions[jobType].Where(p => !p.isOccupied).ToList();
            
            if (availablePositions.Count == 0)
            {
                DebugLog($"❌ '{jobType}' 직업에 사용 가능한 작업 위치가 없습니다! 더 많은 위치를 태그로 설정하세요.", true);
                // 자동 생성 비활성화
                // return CreateDynamicWorkPosition(employee);
                return null;
            }
            
            // 가장 가까운 위치 선택 (현재는 첫 번째 위치)
            var selectedPosition = availablePositions.First();
            
            // 위치 점유 설정
            selectedPosition.isOccupied = true;
            selectedPosition.occupiedBy = employee;
            assignedPositions[employee] = selectedPosition;
            
            DebugLog($"{employee.employeeName}에게 작업 위치 할당: {selectedPosition.positionId}", true);
            
            // 이벤트 발생
            OnPositionAvailabilityChanged?.Invoke(jobType, GetAvailablePositionCount(jobType));
            
            return selectedPosition.position;
        }
        
        /// <summary>
        /// AI가 할당된 작업 위치 해제
        /// </summary>
        /// <param name="employee">위치를 해제할 AI 직원</param>
        public void ReleaseWorkPosition(AIEmployee employee)
        {
            if (employee == null || !assignedPositions.ContainsKey(employee))
            {
                return;
            }
            
            var workPos = assignedPositions[employee];
            workPos.isOccupied = false;
            workPos.occupiedBy = null;
            
            assignedPositions.Remove(employee);
            
            DebugLog($"{employee.employeeName}의 작업 위치 해제: {workPos.positionId}", true);
            
            // 이벤트 발생
            OnPositionAvailabilityChanged?.Invoke(workPos.jobType, GetAvailablePositionCount(workPos.jobType));
        }
        
        /// <summary>
        /// 동적으로 작업 위치 생성 (위치가 부족할 때)
        /// </summary>
        private Transform CreateDynamicWorkPosition(AIEmployee employee)
        {
            string jobType = employee.jobRole.ToLower();
            
            // 기본 위치 근처에 새로운 위치 생성
            Vector3 basePosition = GetBasePositionForJobType(jobType);
            Vector3 newPosition = FindNearbyFreePosition(basePosition, 2f);
            
            // 동적 위치 오브젝트 생성
            GameObject dynamicPosObj = new GameObject($"DynamicWorkPos_{jobType}_{employee.employeeName}");
            dynamicPosObj.transform.position = newPosition;
            
            WorkPosition dynamicWorkPos = new WorkPosition
            {
                position = dynamicPosObj.transform,
                jobType = jobType,
                isOccupied = true,
                occupiedBy = employee,
                positionId = $"Dynamic_{jobType}_{employee.GetInstanceID()}",
                isDynamic = true
            };
            
            if (!workPositions.ContainsKey(jobType))
            {
                workPositions[jobType] = new List<WorkPosition>();
            }
            
            workPositions[jobType].Add(dynamicWorkPos);
            assignedPositions[employee] = dynamicWorkPos;
            
            DebugLog($"{employee.employeeName}에게 동적 작업 위치 생성: {dynamicWorkPos.positionId}", true);
            
            return dynamicWorkPos.position;
        }
        
        #endregion
        
        #region 대기 위치 관리
        
        /// <summary>
        /// AI에게 대기 위치 할당 (자동 생성 비활성화)
        /// </summary>
        public Transform AssignWaitingPosition(AIEmployee employee)
        {
            DebugLog($"❌ WorkPositionManager의 대기 위치 자동 생성이 비활성화되었습니다. 태그를 사용하세요!", true);
            
            // 자동 생성 비활성화 - AIEmployee의 태그 기반 시스템 사용
            return null;
            
            /*
            // 기존 자동 생성 코드 (비활성화됨)
            Transform workPos = employee.workPosition;
            
            if (workPos == null)
            {
                workPos = AssignWorkPosition(employee);
            }
            
            if (workPos == null)
            {
                return transform; // 기본 위치
            }
            
            // 작업 위치 주변 2-3미터 거리에 대기 위치 생성
            Vector3 waitingPos = FindNearbyFreePosition(workPos.position, 3f);
            
            GameObject waitingPosObj = new GameObject($"WaitingPos_{employee.employeeName}");
            waitingPosObj.transform.position = waitingPos;
            
            DebugLog($"{employee.employeeName}에게 대기 위치 할당: {waitingPos}");
            
            return waitingPosObj.transform;
            */
        }
        
        #endregion
        
        #region 동적 위치 등록
        
        /// <summary>
        /// 특정 태그의 작업 위치들을 등록
        /// </summary>
        /// <param name="tag">검색할 태그 (예: "WorkPosition_Kitchen")</param>
        public void RegisterWorkPositionByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                DebugLog("❌ 등록할 태그가 비어있습니다.", true);
                return;
            }
            
            // 태그 존재 여부 확인
            if (!IsTagDefined(tag))
            {
                DebugLog($"⚠️ 태그 '{tag}'가 Tag Manager에 정의되지 않았습니다.", true);
                return;
            }
            
            try
            {
                GameObject[] positions = GameObject.FindGameObjectsWithTag(tag);
                string jobType = ExtractJobTypeFromTag(tag);
                
                if (!workPositions.ContainsKey(jobType))
                {
                    workPositions[jobType] = new List<WorkPosition>();
                }
                
                int addedCount = 0;
                foreach (GameObject pos in positions)
                {
                    // 이미 등록된 위치인지 확인
                    string positionId = $"{jobType}_{pos.name}_{pos.GetInstanceID()}";
                    bool alreadyExists = workPositions[jobType].Any(wp => wp.positionId == positionId);
                    
                    if (!alreadyExists)
                    {
                        WorkPosition workPos = new WorkPosition
                        {
                            position = pos.transform,
                            jobType = jobType,
                            isOccupied = false,
                            occupiedBy = null,
                            positionId = positionId
                        };
                        
                        workPositions[jobType].Add(workPos);
                        addedCount++;
                        DebugLog($"작업 위치 등록: {workPos.positionId}");
                    }
                }
                
                DebugLog($"✅ 태그 '{tag}' 등록 완료: {addedCount}개 새 위치 추가 (총 {positions.Length}개 발견)", true);
                
                // 이벤트 발생
                OnPositionAvailabilityChanged?.Invoke(jobType, GetAvailablePositionCount(jobType));
            }
            catch (UnityException ex)
            {
                DebugLog($"❌ 태그 '{tag}' 등록 중 오류: {ex.Message}", true);
            }
        }
        
        /// <summary>
        /// 여러 태그들을 한 번에 등록
        /// </summary>
        /// <param name="tags">등록할 태그 배열</param>
        public void RegisterMultipleWorkPositionTags(string[] tags)
        {
            if (tags == null || tags.Length == 0)
            {
                DebugLog("❌ 등록할 태그 배열이 비어있습니다.", true);
                return;
            }
            
            DebugLog($"🔄 {tags.Length}개 태그 일괄 등록 시작...", true);
            
            foreach (string tag in tags)
            {
                RegisterWorkPositionByTag(tag);
            }
            
            DebugLog($"✅ 일괄 등록 완료. 총 {workPositions.Count}개 직업 타입 등록됨", true);
        }
        
        #endregion
        
        #region 유틸리티 메서드
        
        /// <summary>
        /// 태그에서 직업 타입 추출
        /// </summary>
        private string ExtractJobTypeFromTag(string tag)
        {
            if (tag.StartsWith("WorkPosition_"))
            {
                return tag.Substring("WorkPosition_".Length).ToLower();
            }
            return "default";
        }
        
        /// <summary>
        /// 직업 타입별 기본 위치 반환
        /// </summary>
        private Vector3 GetBasePositionForJobType(string jobType)
        {
            switch (jobType.ToLower())
            {
                case "서빙":
                case "웨이터":
                    return new Vector3(0, 0, 5); // 식당 구역
                case "청소":
                    return new Vector3(-5, 0, 0); // 청소용품 보관소
                case "요리":
                    return new Vector3(10, 0, 0); // 주방 구역
                case "보안":
                    return new Vector3(0, 0, -10); // 입구/보안실
                case "관리":
                    return new Vector3(5, 0, 5); // 사무실
                default:
                    return Vector3.zero; // 기본 위치
            }
        }
        
        /// <summary>
        /// 기준 위치 근처에서 비어있는 위치 찾기
        /// </summary>
        private Vector3 FindNearbyFreePosition(Vector3 basePosition, float searchRadius)
        {
            int maxAttempts = 20;
            
            for (int i = 0; i < maxAttempts; i++)
            {
                // 원형으로 위치 검색
                float angle = (360f / maxAttempts) * i * Mathf.Deg2Rad;
                float radius = searchRadius * (i % 5 + 1) / 5f; // 점진적으로 반경 증가
                
                Vector3 candidatePos = basePosition + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );
                
                // 다른 AI와 너무 가깝지 않은지 확인
                if (IsPositionFree(candidatePos, 1.5f))
                {
                    return candidatePos;
                }
            }
            
            // 적절한 위치를 찾지 못하면 기본 위치에서 랜덤 오프셋
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-2f, 2f),
                0,
                UnityEngine.Random.Range(-2f, 2f)
            );
            
            return basePosition + randomOffset;
        }
        
        /// <summary>
        /// 해당 위치가 비어있는지 확인
        /// </summary>
        private bool IsPositionFree(Vector3 position, float checkRadius)
        {
            foreach (var kvp in assignedPositions)
            {
                if (kvp.Value.position != null)
                {
                    float distance = Vector3.Distance(position, kvp.Value.position.position);
                    if (distance < checkRadius)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        
        /// <summary>
        /// 특정 직업 타입의 사용 가능한 위치 개수 반환
        /// </summary>
        public int GetAvailablePositionCount(string jobType)
        {
            if (!workPositions.ContainsKey(jobType))
                return 0;
                
            return workPositions[jobType].Count(p => !p.isOccupied);
        }
        
        /// <summary>
        /// 모든 작업 위치 정보 반환 (디버깅용)
        /// </summary>
        public Dictionary<string, int> GetAllPositionCounts()
        {
            var counts = new Dictionary<string, int>();
            
            foreach (var kvp in workPositions)
            {
                counts[kvp.Key] = kvp.Value.Count(p => !p.isOccupied);
            }
            
            return counts;
        }
        
        #endregion
        
        #region 오브젝트 배치 연동
        
        /// <summary>
        /// 새로운 가구 배치 시 작업 위치 자동 등록
        /// PlacementSystem과 연동하여 호출
        /// </summary>
        public void OnFurnitureePlaced(GameObject placedObject, Vector3 position)
        {
            // ✅ 주방 가구는 KitchenDetector가 이미 관리하므로 여기서는 무시
            string furnitureName = placedObject.name.ToLower();
            
            // 주방 관련 가구는 KitchenDetector가 처리
            if (furnitureName.Contains("kitchen") || 
                furnitureName.Contains("stove") || 
                furnitureName.Contains("counter") ||
                furnitureName.Contains("인덕션") ||
                furnitureName.Contains("가스"))
            {
                DebugLog($"주방 가구 감지: {placedObject.name} - KitchenDetector에서 처리", true);
                return;
            }
            
            // 가구 타입에 따라 작업 위치 생성
            string jobType = GetJobTypeFromFurniture(furnitureName);
            
            if (!string.IsNullOrEmpty(jobType))
            {
                RegisterNewWorkPosition(position, jobType, $"Auto_{jobType}_{placedObject.GetInstanceID()}");
            }
        }
        
        /// <summary>
        /// 가구 이름으로부터 작업 타입 추정
        /// </summary>
        private string GetJobTypeFromFurniture(string furnitureName)
        {
            if (furnitureName.Contains("counter") || furnitureName.Contains("reception"))
                return "서빙";
            if (furnitureName.Contains("kitchen") || furnitureName.Contains("stove"))
                return "요리";
            if (furnitureName.Contains("security") || furnitureName.Contains("desk"))
                return "보안";
            if (furnitureName.Contains("clean") || furnitureName.Contains("storage"))
                return "청소";
                
            return null;
        }
        
        /// <summary>
        /// 새로운 작업 위치 등록
        /// </summary>
        private void RegisterNewWorkPosition(Vector3 position, string jobType, string positionId)
        {
            GameObject newPosObj = new GameObject($"WorkPos_{positionId}");
            newPosObj.transform.position = position;
            
            WorkPosition newWorkPos = new WorkPosition
            {
                position = newPosObj.transform,
                jobType = jobType,
                isOccupied = false,
                occupiedBy = null,
                positionId = positionId,
                isDynamic = true
            };
            
            if (!workPositions.ContainsKey(jobType))
            {
                workPositions[jobType] = new List<WorkPosition>();
            }
            
            workPositions[jobType].Add(newWorkPos);
            
            DebugLog($"새로운 작업 위치 자동 등록: {positionId}", true);
            
            OnPositionAvailabilityChanged?.Invoke(jobType, GetAvailablePositionCount(jobType));
        }
        
        #endregion
        
        #region 디버그
        
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!enableDebugLogs) return;
        }
        
        void OnDrawGizmos()
        {
            if (!showOccupiedPositions) return;
            
            foreach (var jobPositions in workPositions.Values)
            {
                foreach (var workPos in jobPositions)
                {
                    if (workPos.position == null) continue;
                    
                    // 점유된 위치는 빨간색, 빈 위치는 초록색
                    Gizmos.color = workPos.isOccupied ? Color.red : Color.green;
                    Gizmos.DrawWireSphere(workPos.position.position, 0.5f);
                    
                    // 직업 타입 표시
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(workPos.position.position, workPos.position.position + Vector3.up * 2f);
                }
            }
        }
        
        #endregion
        
        #region 에디터/테스트 메서드
        
        [Header("테스트 설정")]
        [SerializeField] private string[] testTags = { "WorkPosition_Kitchen" };
        
        [ContextMenu("테스트 - 태그 일괄 등록")]
        private void TestRegisterTags()
        {
            if (Application.isPlaying)
            {
                RegisterMultipleWorkPositionTags(testTags);
            }
            else
            {
                DebugLog("플레이 모드에서만 실행 가능합니다.", true);
            }
        }
        
        [ContextMenu("현재 등록된 위치 정보 출력")]
        private void PrintCurrentPositions()
        {
            DebugLog("=== 현재 등록된 작업 위치 정보 ===", true);
            foreach (var kvp in workPositions)
            {
                DebugLog($"직업: {kvp.Key}, 위치 수: {kvp.Value.Count}, 사용 가능: {kvp.Value.Count(p => !p.isOccupied)}", true);
                foreach (var pos in kvp.Value)
                {
                    DebugLog($"  - {pos.positionId} ({(pos.isOccupied ? "점유됨" : "비어있음")})", true);
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 작업 위치 데이터 클래스
    /// </summary>
    [System.Serializable]
    public class WorkPosition
    {
        public Transform position;          // 실제 위치
        public string jobType;             // 작업 타입 (서빙, 청소, 요리 등)
        public bool isOccupied;            // 점유 여부
        public AIEmployee occupiedBy;      // 점유한 직원
        public string positionId;          // 고유 ID
        public bool isDynamic = false;     // 동적으로 생성된 위치인지
        
        public override string ToString()
        {
            return $"{positionId} ({jobType}) - {(isOccupied ? $"점유됨 by {occupiedBy?.employeeName}" : "비어있음")}";
        }
    }
}
