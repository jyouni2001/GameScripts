using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using JY;

namespace JY
{
    /// <summary>
    /// AI 직원 고용 시스템
    /// 돈을 지불하여 다양한 종류의 AI 직원을 고용하고 관리하는 시스템
    /// </summary>
    public class EmployeeHiringSystem : MonoBehaviour
    {
        [Header("고용 시스템 설정")]
        [SerializeField] private Transform employeeSpawnPoint;
        [SerializeField] private bool enableDebugLogs = true;
        
        [Header("위치별 고용 제한 설정")]
        [Tooltip("카운터당 최대 고용 가능한 직원 수")]
        [SerializeField] private int maxEmployeesPerCounter = 1;
        
        [Tooltip("식당당 최대 고용 가능한 직원 수 (주방 1개당 1명)")]
        [SerializeField] private int maxEmployeesPerKitchen = 1;
        
        [Header("고용 가능한 직원 타입")]
        [SerializeField] private List<EmployeeType> availableEmployeeTypes = new List<EmployeeType>();
        
        // 싱글톤 인스턴스
        public static EmployeeHiringSystem Instance { get; private set; }
        
        // 고용된 직원들 관리
        private List<AIEmployee> hiredEmployees = new List<AIEmployee>();
        private PlayerWallet playerWallet;
        
        // 카운터별 직원 배정 관리
        private Dictionary<GameObject, List<AIEmployee>> counterEmployees = new Dictionary<GameObject, List<AIEmployee>>();
        private Dictionary<GameObject, List<AIEmployee>> kitchenEmployees = new Dictionary<GameObject, List<AIEmployee>>();
        
        // 디스폰된 직원 정보 (자동 리스폰용)
        [System.Serializable]
        private class DespawnedEmployeeInfo
        {
            public EmployeeType employeeType;
            public string employeeName;
            public int dailyWage;
            public int workStartHour;
            public int workEndHour;
            public string workPositionTag;
        }
        private List<DespawnedEmployeeInfo> despawnedEmployees = new List<DespawnedEmployeeInfo>();
        private TimeSystem timeSystem;
        private int lastCheckHour = -1;
        
        // 이벤트
        public static event Action<AIEmployee> OnEmployeeHired;
        public static event Action<AIEmployee> OnEmployeeFired;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
        
        private void Start()
        {
            InitializeSystem();
            timeSystem = TimeSystem.Instance;
        }
        
        private void Update()
        {
            CheckEmployeeRespawn();
        }
        
        #endregion
        
        #region 초기화
        
        private void InitializeSystem()
        {
            // PlayerWallet 참조 가져오기
            playerWallet = PlayerWallet.Instance;
            if (playerWallet == null)
            {
                return;
            }
            
            // 스폰 포인트 설정
            if (employeeSpawnPoint == null)
            {
                GameObject spawnPointObj = new GameObject("EmployeeSpawnPoint");
                spawnPointObj.transform.position = transform.position;
                employeeSpawnPoint = spawnPointObj.transform;
                DebugLog("기본 스폰 포인트가 생성되었습니다.");
            }
            
            // 직원 타입들은 Inspector에서 설정
            if (availableEmployeeTypes.Count == 0)
            {
                DebugLog("⚠️ 직원 타입이 설정되지 않았습니다. Inspector에서 직원 타입을 추가해주세요.");
            }
            
            // 카운터 모니터링 시작
            StartCoroutine(MonitorCountersAndKitchens());
            
            DebugLog("고용 시스템 초기화 완료");
        }
        
        #endregion
        
        #region 공개 메서드
        
        /// <summary>
        /// 직원을 고용합니다
        /// </summary>
        /// <param name="employeeTypeIndex">고용할 직원 타입의 인덱스</param>
        /// <returns>고용 성공 여부</returns>
        public bool HireEmployee(int employeeTypeIndex)
        {
            if (employeeTypeIndex < 0 || employeeTypeIndex >= availableEmployeeTypes.Count)
            {
                DebugLog($"잘못된 직원 타입 인덱스: {employeeTypeIndex}");
                return false;
            }
            
            return HireEmployee(availableEmployeeTypes[employeeTypeIndex]);
        }
        
        /// <summary>
        /// 직원을 고용합니다
        /// </summary>
        /// <param name="employeeType">고용할 직원 타입</param>
        /// <returns>고용 성공 여부</returns>
        public bool HireEmployee(EmployeeType employeeType)
        {
            DebugLog($"=== 고용 시도 시작: {employeeType.typeName} ===");
            DebugLog($"workPositionTag: '{employeeType.workPositionTag}'");
            
            // 프리팹 확인
            if (employeeType.employeePrefab == null)
            {
                DebugLog($"'{employeeType.typeName}' 타입의 프리팹이 설정되지 않았습니다.");
                return false;
            }
            
            // 위치별 고용 제한 확인
            bool canHire = CanHireEmployeeAtPosition(employeeType);
            DebugLog($"고용 가능 여부: {canHire}");
            
            if (!canHire)
            {
                string reason = GetHiringRestrictionReason(employeeType);
                DebugLog($"❌ 고용 실패: {employeeType.typeName} - {reason}");
                return false;
            }
            
            // 비용 확인 및 지불
            if (!playerWallet.SpendMoney(employeeType.hiringCost))
            {
                DebugLog($"고용 비용이 부족합니다. 필요: {employeeType.hiringCost}, 보유: {playerWallet.money}");
                return false;
            }
            
            DebugLog($"{employeeType.hiringCost} 골드를 지불하여 {employeeType.typeName}을(를) 고용합니다.");
            
            // 직원 생성
            AIEmployee newEmployee = CreateEmployee(employeeType);
            if (newEmployee != null)
            {
                hiredEmployees.Add(newEmployee);
                
                // 카운터별 배정
                AssignEmployeeToPosition(newEmployee, employeeType.workPositionTag);
                
                OnEmployeeHired?.Invoke(newEmployee);
                DebugLog($"{employeeType.typeName} '{newEmployee.employeeName}'이(가) 고용되었습니다!");
                return true;
            }
            else
            {
                // 실패 시 돈 환불
                playerWallet.AddMoney(employeeType.hiringCost);
                DebugLog($"{employeeType.typeName} 생성에 실패했습니다. 비용을 환불합니다.");
                return false;
            }
        }
        
        /// <summary>
        /// 직원을 해고합니다
        /// </summary>
        /// <param name="employee">해고할 직원</param>
        public void FireEmployee(AIEmployee employee)
        {
            if (employee == null)
            {
                Debug.LogWarning("[FireEmployee] employee가 null입니다.");
                return;
            }
            
            if (!hiredEmployees.Contains(employee))
            {
                Debug.LogWarning($"[FireEmployee] '{employee.employeeName}'은(는) 고용된 직원 목록에 없습니다.");
                DebugLog("해고할 수 없는 직원입니다.");
                return;
            }
            
            Debug.Log($"====================================");
            Debug.Log($"[직원 해고] '{employee.employeeName}' 해고 시작");
            Debug.Log($"[직원 해고] 태그: {employee.workPositionTag}");
            Debug.Log($"[직원 해고] 해고 전 hiredEmployees 크기: {hiredEmployees.Count}");
            
            // 배정에서 제거
            RemoveEmployeeFromAssignment(employee);
            
            hiredEmployees.Remove(employee);
            Debug.Log($"[직원 해고] hiredEmployees에서 제거 완료 - 현재 크기: {hiredEmployees.Count}");
            
            OnEmployeeFired?.Invoke(employee);
            
            DebugLog($"{employee.employeeName}이(가) 해고되었습니다.");
            
            // 오브젝트 제거
            if (employee.gameObject != null)
            {
                Destroy(employee.gameObject);
                Debug.Log($"[직원 해고] GameObject 파괴 완료");
            }
            
            Debug.Log($"====================================");
        }
        
        /// <summary>
        /// 모든 고용된 직원 목록을 반환합니다
        /// </summary>
        public List<AIEmployee> GetHiredEmployees()
        {
            return new List<AIEmployee>(hiredEmployees);
        }
        
        /// <summary>
        /// 고용 가능한 직원 타입 목록을 반환합니다
        /// </summary>
        public List<EmployeeType> GetAvailableEmployeeTypes()
        {
            return new List<EmployeeType>(availableEmployeeTypes);
        }
        
        /// <summary>
        /// 특정 타입의 고용 비용을 반환합니다
        /// </summary>
        public int GetHiringCost(int employeeTypeIndex)
        {
            if (employeeTypeIndex >= 0 && employeeTypeIndex < availableEmployeeTypes.Count)
            {
                return availableEmployeeTypes[employeeTypeIndex].hiringCost;
            }
            return 0;
        }
        
        /// <summary>
        /// 현재 고용된 직원 수를 반환합니다
        /// </summary>
        public int GetEmployeeCount()
        {
            return hiredEmployees.Count;
        }
        
        /// <summary>
        /// 특정 직업의 직원 수를 반환합니다
        /// </summary>
        public int GetEmployeeCountByRole(string jobRole)
        {
            int count = 0;
            foreach (var employee in hiredEmployees)
            {
                if (employee.jobRole == jobRole)
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// 직원을 리스트에서 제거 (오브젝트 파괴 시 사용)
        /// </summary>
        public void RemoveEmployeeFromList(AIEmployee employee)
        {
            if (employee != null && hiredEmployees.Contains(employee))
            {
                hiredEmployees.Remove(employee);
                DebugLog($"직원 리스트에서 제거됨: {employee.employeeName}");
                OnEmployeeFired?.Invoke(employee);
            }
        }
        
        /// <summary>
        /// 직원이 디스폰되었을 때 호출 (자동 리스폰을 위해 정보 저장)
        /// </summary>
        public void OnEmployeeDespawned(AIEmployee employee)
        {
            if (employee == null) return;
            
            // 디스폰된 직원 정보 저장 (리스폰용)
            var employeeType = availableEmployeeTypes.Find(t => t.jobRole == employee.jobRole && t.workPositionTag == employee.workPositionTag);
            if (employeeType != null)
            {
                var info = new DespawnedEmployeeInfo
                {
                    employeeType = employeeType,
                    employeeName = employee.employeeName,
                    dailyWage = employee.dailyWage,
                    workStartHour = employee.workStartHour,
                    workEndHour = employee.workEndHour,
                    workPositionTag = employee.workPositionTag
                };
                despawnedEmployees.Add(info);
            }
            
            // 리스트에서 제거
            if (hiredEmployees.Contains(employee))
            {
                hiredEmployees.Remove(employee);
            }
        }
        
        /// <summary>
        /// 근무시간 체크 및 자동 리스폰
        /// </summary>
        private void CheckEmployeeRespawn()
        {
            if (timeSystem == null || despawnedEmployees.Count == 0) return;
            
            int currentHour = timeSystem.CurrentHour;
            
            // 매 시간 정각에만 체크
            if (currentHour != lastCheckHour)
            {
                lastCheckHour = currentHour;
                
                // 디스폰된 직원 중 근무시간인 직원 찾기
                for (int i = despawnedEmployees.Count - 1; i >= 0; i--)
                {
                    var info = despawnedEmployees[i];
                    
                    // 근무시간 체크
                    if (currentHour >= info.workStartHour && currentHour < info.workEndHour)
                    {
                        // 직원 생성 (고용은 이미 되어있는 상태이므로 새로 생성만 함)
                        AIEmployee newEmployee = CreateEmployee(info.employeeType);
                        if (newEmployee != null)
                        {
                            hiredEmployees.Add(newEmployee);
                            AssignEmployeeToPosition(newEmployee, info.workPositionTag);
                            despawnedEmployees.RemoveAt(i);
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region 비공개 메서드
        
        private AIEmployee CreateEmployee(EmployeeType employeeType)
        {
            try
            {
                // 프리팹 인스턴스 생성
                GameObject employeeObj = Instantiate(employeeType.employeePrefab, employeeSpawnPoint.position, employeeSpawnPoint.rotation);
                
                // AIEmployee 컴포넌트 가져오기 또는 추가
                AIEmployee employee = employeeObj.GetComponent<AIEmployee>();
                if (employee == null)
                {
                    employee = employeeObj.AddComponent<AIEmployee>();
                }
                
                // EmployeeType에서 모든 정보를 가져와서 설정
                employee.employeeName = GenerateEmployeeName(employeeType.typeName);
                employee.jobRole = employeeType.jobRole;
                employee.dailyWage = employeeType.dailyWage;
                employee.workStartHour = employeeType.workStartHour;
                employee.workEndHour = employeeType.workEndHour;
                employee.workPositionTag = employeeType.workPositionTag;
                employee.spawnPoint = employeeSpawnPoint; // 스폰 포인트 설정
                
                // 태그 설정 확인 로그
                DebugLog($"🏷️ 태그 설정 확인 - 작업: '{employee.workPositionTag}'");
                
                // 직원 고용 처리 (isHired는 HireEmployee 내부에서 설정됨)
                bool hireResult = employee.HireEmployee();
                if (!hireResult)
                {
                    DebugLog($"❌ {employee.employeeName} 고용 프로세스 실패!");
                    return null;
                }
                
                return employee;
            }
            catch (Exception e)
            {
                Debug.Log($"{e} - Exception Occured");
                return null;
            }
        }
        
        private string GenerateEmployeeName(string jobType)
        {
            // 간단한 이름 생성기
            string[] firstNames = { "김", "이", "박", "최", "정", "강", "조", "윤", "장", "임" };
            string[] lastNames = { "민수", "영희", "철수", "영미", "현우", "지영", "성민", "하영", "준호", "수진" };
            
            string firstName = firstNames[UnityEngine.Random.Range(0, firstNames.Length)];
            string lastName = lastNames[UnityEngine.Random.Range(0, lastNames.Length)];
            
            return $"{firstName}{lastName}";
        }
        
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
            }
        }
        
        #endregion
        
        #region 에디터 전용
        
        #if UNITY_EDITOR
        [Header("에디터 테스트")]
        [SerializeField] private int testEmployeeTypeIndex = 0;
        
        [ContextMenu("테스트 - 직원 고용")]
        private void TestHireEmployee()
        {
            if (Application.isPlaying)
            {
                HireEmployee(testEmployeeTypeIndex);
            }
        }
        
        [ContextMenu("테스트 - 모든 직원 해고")]
        private void TestFireAllEmployees()
        {
            if (Application.isPlaying)
            {
                var employeesToFire = new List<AIEmployee>(hiredEmployees);
                foreach (var employee in employeesToFire)
                {
                    FireEmployee(employee);
                }
            }
        }
        
        [ContextMenu("현재 고용 상태 확인")]
        private void TestCheckHiringStatus()
        {
            if (Application.isPlaying)
            {
            }
        }
        
        #endif
        
        #endregion
        
        #region 위치별 고용 제한 관리
        
        /// <summary>
        /// 특정 위치에서 직원을 고용할 수 있는지 확인
        /// </summary>
        /// <param name="employeeType">고용하려는 직원 타입</param>
        /// <returns>고용 가능 여부</returns>
        public bool CanHireEmployeeAtPosition(EmployeeType employeeType)
        {
            Debug.Log($"====================================");
            Debug.Log($"[고용 체크] 직원 타입: {employeeType.typeName}");
            Debug.Log($"[고용 체크] workPositionTag: '{employeeType.workPositionTag}'");
            Debug.Log($"====================================");
            
            DebugLog($"조건 체크: {employeeType.typeName}, workPositionTag: '{employeeType.workPositionTag}'");
            
            // 카운터 직원인 경우
            if (employeeType.workPositionTag == "WorkPosition_Reception")
            {
                Debug.Log($"→ 카운터 직원으로 인식됨!");
                bool result = CanHireCounterEmployee();
                DebugLog($"카운터 직원 조건 체크 결과: {result}");
                return result;
            }
            // 주방 직원인 경우
            else if (employeeType.workPositionTag == "WorkPosition_Kitchen")
            {
                Debug.Log($"→ 주방 직원으로 인식됨!");
                bool result = CanHireKitchenEmployee();
                DebugLog($"주방 직원 조건 체크 결과: {result}");
                return result;
            }
            
            // 기타 직원은 제한 없음
            Debug.Log($"→ 기타 직원으로 인식됨 (제한 없음)");
            DebugLog($"기타 직원 - 제한 없음: {employeeType.typeName}");
            return true;
        }
        
        /// <summary>
        /// 카운터 직원 고용 가능 여부 확인
        /// </summary>
        private bool CanHireCounterEmployee()
        {
            // 사용 가능한 카운터 찾기
            GameObject[] counters = GameObject.FindGameObjectsWithTag("Counter");
            DebugLog($"카운터 개수: {counters.Length}개");
            
            if (counters.Length == 0)
            {
                DebugLog("카운터가 없습니다! 카운터 직원을 고용할 수 없습니다.");
                return false;
            }
            
            foreach (GameObject counter in counters)
            {
                if (!counterEmployees.ContainsKey(counter))
                {
                    counterEmployees[counter] = new List<AIEmployee>();
                }
                
                // 이 카운터에 배정 가능한 직원 수 확인
                int assignedCount = counterEmployees[counter].Count;
                DebugLog($"카운터 '{counter.name}': {assignedCount}/{maxEmployeesPerCounter}명 배정됨");
                
                if (assignedCount < maxEmployeesPerCounter)
                {
                    DebugLog($"카운터 '{counter.name}'에 배정 가능!");
                    return true; // 이 카운터에 배정 가능
                }
            }
            
            DebugLog($"모든 카운터가 가득 참: 총 {counters.Length}개 카운터, 각각 {maxEmployeesPerCounter}명씩");
            return false;
        }
        
        /// <summary>
        /// 주방 직원 고용 가능 여부 확인 (Kitchen 태그 1개당 AI 1명)
        /// </summary>
        private bool CanHireKitchenEmployee()
        {
            // 1. Kitchen 태그 개수 확인
            GameObject[] kitchens = GameObject.FindGameObjectsWithTag("Kitchen");
            int kitchenCount = kitchens.Length;
            
            Debug.Log($"====================================");
            Debug.Log($"[주방 고용 체크] Kitchen 태그 오브젝트: {kitchenCount}개");
            Debug.Log($"[주방 고용 체크] hiredEmployees 전체 크기: {hiredEmployees.Count}");
            
            if (kitchenCount == 0)
            {
                Debug.LogWarning("❌ Kitchen 태그 없음 - 주방을 먼저 만드세요!");
                Debug.Log($"====================================");
                return false;
            }
            
            // 주방 목록 출력
            for (int i = 0; i < kitchens.Length; i++)
            {
                Debug.Log($"  - Kitchen {i + 1}: {kitchens[i].name}");
            }
            
            // 2. 현재 고용된 주방 직원 수 (workPositionTag == "WorkPosition_Kitchen")
            int currentKitchenEmployees = 0;
            Debug.Log($"[주방 고용 체크] hiredEmployees 순회 시작:");
            foreach (var emp in hiredEmployees)
            {
                if (emp == null)
                {
                    Debug.LogWarning($"  - ⚠️ NULL 직원 발견!");
                    continue;
                }
                
                Debug.Log($"  - 직원: {emp.employeeName}, 태그: {emp.workPositionTag}");
                
                if (emp.workPositionTag == "WorkPosition_Kitchen")
                {
                    currentKitchenEmployees++;
                    Debug.Log($"    → 주방 직원으로 카운트! (현재: {currentKitchenEmployees}명)");
                }
            }
            
            Debug.Log($"[주방 직원] 최종 카운트: {currentKitchenEmployees}명 / Kitchen 태그: {kitchenCount}개");
            Debug.Log($"====================================");
            
            // 3. Kitchen 태그 1개당 AI 1명 제한
            if (currentKitchenEmployees >= kitchenCount)
            {
                Debug.LogWarning($"❌ 주방 고용 불가! Kitchen {kitchenCount}개에 이미 {currentKitchenEmployees}명 고용됨");
                return false;
            }
            
            Debug.Log($"✅ 주방 고용 가능! ({currentKitchenEmployees}/{kitchenCount}명)");
            return true;
        }
        
        /// <summary>
        /// 씬에 있는 카운터 개수 반환
        /// </summary>
        private int GetCounterCount()
        {
            GameObject[] counters = GameObject.FindGameObjectsWithTag("Counter");
            return counters.Length;
        }
        
        /// <summary>
        /// 씬에 있는 식당 개수 반환
        /// </summary>
        private int GetKitchenCount()
        {
            GameObject[] kitchens = GameObject.FindGameObjectsWithTag("Kitchen");
            return kitchens.Length;
        }
        
        /// <summary>
        /// 현재 고용된 카운터 직원 수 반환
        /// </summary>
        private int GetCurrentCounterEmployeeCount()
        {
            return hiredEmployees.Count(emp => emp.workPositionTag == "WorkPosition_Reception");
        }
        
        /// <summary>
        /// 현재 고용된 주방 직원 수 반환 (workPositionTag == "WorkPosition_Kitchen")
        /// </summary>
        private int GetCurrentKitchenEmployeeCount()
        {
            int count = 0;
            foreach (var emp in hiredEmployees)
            {
                if (emp != null && emp.workPositionTag == "WorkPosition_Kitchen")
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// 현재 고용된 식당 카운터 직원 수 반환
        /// </summary>
        private int GetCurrentKitchenCounterEmployeeCount()
        {
            return hiredEmployees.Count(emp => emp.workPositionTag == "WorkPosition_Kitchen");
        }
        
        /// <summary>
        /// 현재 고용된 식당 대기 직원 수 반환
        /// </summary>
        private int GetCurrentKitchenWaitingEmployeeCount()
        {
            return hiredEmployees.Count(emp => emp.workPositionTag == "WorkPosition_Kitchen");
        }
        
        /// <summary>
        /// 현재 고용 상태 정보 반환 (디버깅용)
        /// </summary>
        public string GetHiringStatusInfo()
        {
            int counterCount = GetCounterCount();
            int kitchenCount = GetKitchenCount();
            int currentCounterEmployees = GetCurrentCounterEmployeeCount();
            int currentKitchenEmployees = GetCurrentKitchenEmployeeCount();
            
            return $"카운터: {currentCounterEmployees}/{counterCount * maxEmployeesPerCounter}명 (카운터 {counterCount}개)\n" +
                   $"주방: {currentKitchenEmployees}/{kitchenCount}명 (Kitchen 태그 {kitchenCount}개, 1개당 1명)";
        }
        
        /// <summary>
        /// 고용 제한 이유 반환
        /// </summary>
        private string GetHiringRestrictionReason(EmployeeType employeeType)
        {
            if (employeeType.workPositionTag == "WorkPosition_Reception")
            {
                GameObject[] counters = GameObject.FindGameObjectsWithTag("Counter");
                if (counters.Length == 0)
                {
                    return "카운터가 없습니다. 카운터를 먼저 배치해주세요.";
                }
                
                int currentCounterEmployees = GetCurrentCounterEmployeeCount();
                int maxCounterEmployees = counters.Length * maxEmployeesPerCounter;
                return $"카운터 직원이 가득 참 ({currentCounterEmployees}/{maxCounterEmployees}명, 카운터 1개당 1명)";
            }
            else if (employeeType.workPositionTag == "WorkPosition_Kitchen")
            {
                GameObject[] kitchens = GameObject.FindGameObjectsWithTag("Kitchen");
                if (kitchens.Length == 0)
                {
                    return "주방이 없습니다. Kitchen 태그를 가진 주방을 먼저 배치해주세요.";
                }
                
                int currentKitchenEmployees = GetCurrentKitchenEmployeeCount();
                
                return $"주방 직원이 가득 참 ({currentKitchenEmployees}/{kitchens.Length}명, Kitchen 태그 1개당 1명)";
            }
            
            return "알 수 없는 제한 사유";
        }
        
        /// <summary>
        /// 직원을 특정 위치에 배정
        /// </summary>
        private void AssignEmployeeToPosition(AIEmployee employee, string workPositionTag)
        {
            Debug.Log($"====================================");
            Debug.Log($"[직원 배정] 시작 - {employee.employeeName}, 태그: '{workPositionTag}'");
            DebugLog($"직원 배정 시작: {employee.employeeName}, workPositionTag: '{workPositionTag}'");
            
            // ✅ 배정 전에 null 키 정리 (안전성 확보)
            CleanupNullKeysInDictionaries();
            
            // 카운터 직원 배정
            if (workPositionTag == "WorkPosition_Reception")
            {
                GameObject[] counters = GameObject.FindGameObjectsWithTag("Counter");
                Debug.Log($"[직원 배정] Counter 태그 오브젝트: {counters.Length}개 발견");
                DebugLog($"카운터 직원 배정 시도: 총 {counters.Length}개 카운터 발견");
                
                // Dictionary 상태 로그
                Debug.Log($"[직원 배정] 현재 counterEmployees Dictionary 크기: {counterEmployees.Count}");
                foreach (var kvp in counterEmployees)
                {
                    if (kvp.Key != null)
                    {
                        Debug.Log($"  - {kvp.Key.name}: {kvp.Value.Count}명 배정됨");
                    }
                    else
                    {
                        Debug.LogWarning($"  - ⚠️ NULL 카운터 발견! (직원 {kvp.Value.Count}명)");
                    }
                }
                
                foreach (GameObject counter in counters)
                {
                    Debug.Log($"[직원 배정] 카운터 '{counter.name}' 체크 중...");
                    
                    if (!counterEmployees.ContainsKey(counter))
                    {
                        counterEmployees[counter] = new List<AIEmployee>();
                        Debug.Log($"[직원 배정] 새 카운터 '{counter.name}'를 Dictionary에 추가");
                    }
                    
                    int currentCount = counterEmployees[counter].Count;
                    Debug.Log($"[직원 배정] 카운터 '{counter.name}': {currentCount}/{maxEmployeesPerCounter}명");
                    
                    if (currentCount < maxEmployeesPerCounter)
                    {
                        counterEmployees[counter].Add(employee);
                        employee.assignedCounter = counter;
                        Debug.Log($"[직원 배정] ✅ '{employee.employeeName}'을(를) '{counter.name}'에 추가");
                        
                        // 카운터 매니저에 직원 배정 알림
                        CounterManager counterManager = counter.GetComponent<CounterManager>();
                        if (counterManager != null)
                        {
                            counterManager.AssignEmployee(employee);
                            Debug.Log($"[직원 배정] ✅ CounterManager에 직원 배정 완료");
                            DebugLog($"✅ 직원 '{employee.employeeName}'이(가) 카운터 '{counter.name}'에 배정되었습니다.");
                        }
                        else
                        {
                            Debug.LogWarning($"[직원 배정] ⚠️ 카운터에 CounterManager 컴포넌트 없음!");
                        }
                        Debug.Log($"====================================");
                        return;
                    }
                    else
                    {
                        Debug.Log($"[직원 배정] 카운터 '{counter.name}' 가득 참 - 다음 카운터 확인");
                    }
                }
                Debug.LogWarning($"[직원 배정] ❌ 모든 카운터가 가득 참!");
                Debug.Log($"====================================");
                DebugLog($"❌ 모든 카운터가 가득 찼습니다.");
            }
            // 주방 직원 배정
            else if (workPositionTag == "WorkPosition_Kitchen")
            {
                GameObject[] kitchens = GameObject.FindGameObjectsWithTag("Kitchen");
                Debug.Log($"[직원 배정] Kitchen 태그 오브젝트: {kitchens.Length}개 발견");
                DebugLog($"주방 직원 배정 시도: 총 {kitchens.Length}개 주방 발견");
                
                if (kitchens.Length == 0)
                {
                    Debug.LogWarning($"[직원 배정] ❌ Kitchen 태그 없음!");
                    Debug.Log($"====================================");
                    DebugLog($"❌ Kitchen 태그를 가진 오브젝트가 없습니다!");
                    return;
                }
                
                // Dictionary 상태 로그
                Debug.Log($"[직원 배정] 현재 kitchenEmployees Dictionary 크기: {kitchenEmployees.Count}");
                foreach (var kvp in kitchenEmployees)
                {
                    if (kvp.Key != null)
                    {
                        Debug.Log($"  - {kvp.Key.name}: {kvp.Value.Count}명 배정됨");
                    }
                    else
                    {
                        Debug.LogWarning($"  - ⚠️ NULL 주방 발견! (직원 {kvp.Value.Count}명)");
                    }
                }
                
                foreach (GameObject kitchen in kitchens)
                {
                    Debug.Log($"[직원 배정] 주방 '{kitchen.name}' 체크 중...");
                    
                    if (!kitchenEmployees.ContainsKey(kitchen))
                    {
                        kitchenEmployees[kitchen] = new List<AIEmployee>();
                        Debug.Log($"[직원 배정] 새 주방 '{kitchen.name}'를 Dictionary에 추가");
                    }
                    
                    int currentCount = kitchenEmployees[kitchen].Count;
                    Debug.Log($"[직원 배정] 주방 '{kitchen.name}': {currentCount}/{maxEmployeesPerKitchen}명");
                    
                    // 주방 1개당 직원 1명 제한
                    if (currentCount < maxEmployeesPerKitchen)
                    {
                        kitchenEmployees[kitchen].Add(employee);
                        employee.assignedKitchen = kitchen;
                        Debug.Log($"[직원 배정] ✅ '{employee.employeeName}'을(를) '{kitchen.name}'에 추가");
                        Debug.Log($"[직원 배정] ✅ 주방 배정 완료 ({currentCount + 1}/{maxEmployeesPerKitchen}명)");
                        Debug.Log($"====================================");
                        DebugLog($"✅ 직원 '{employee.employeeName}'이(가) 주방 '{kitchen.name}'에 배정되었습니다 ({kitchenEmployees[kitchen].Count}/{maxEmployeesPerKitchen}명).");
                        return;
                    }
                    else
                    {
                        Debug.Log($"[직원 배정] 주방 '{kitchen.name}' 가득 참 - 다음 주방 확인");
                    }
                }
                Debug.LogWarning($"[직원 배정] ❌ 모든 주방이 가득 참!");
                Debug.Log($"====================================");
                DebugLog($"❌ 모든 주방이 가득 찼습니다.");
            }
        }
        
        /// <summary>
        /// 카운터와 식당을 모니터링하여 삭제된 경우 직원 해고 및 Dictionary 정리
        /// </summary>
        private System.Collections.IEnumerator MonitorCountersAndKitchens()
        {
            while (true)
            {
                yield return new WaitForSeconds(3f); // ✅ 3초마다 체크 (더 여유있게)
                
                // ✅ null 키만 정리 (직원 해고는 하지 않음)
                CleanupNullKeysInDictionaries();
            }
        }
        
        /// <summary>
        /// 카운터가 삭제될 때 호출 - 해당 카운터의 직원을 즉시 해고
        /// </summary>
        public void OnCounterDestroyed(GameObject counter)
        {
            if (counter == null) return;
            
            Debug.Log($"====================================");
            Debug.Log($"[OnCounterDestroyed] 카운터 삭제: {counter.name}");
            
            // ✅ 부모 또는 자식에서 Counter 태그를 가진 GameObject 찾기
            GameObject counterObject = null;
            
            // 먼저 본인이 Counter 태그를 가지고 있는지 확인
            if (counter.CompareTag("Counter"))
            {
                counterObject = counter;
                Debug.Log($"[OnCounterDestroyed] 부모가 Counter 태그를 가짐");
            }
            else
            {
                // 자식에서 Counter 태그 찾기
                Transform[] children = counter.GetComponentsInChildren<Transform>();
                foreach (Transform child in children)
                {
                    if (child.CompareTag("Counter"))
                    {
                        counterObject = child.gameObject;
                        Debug.Log($"[OnCounterDestroyed] 자식 '{child.name}'에서 Counter 태그 발견");
                        break;
                    }
                }
            }
            
            if (counterObject == null)
            {
                Debug.Log($"[OnCounterDestroyed] ⚠️ Counter 태그를 가진 오브젝트를 찾지 못함");
                return;
            }
            
            // ✅ Dictionary에서 해당 GameObject 키를 직접 찾기
            if (counterEmployees.ContainsKey(counterObject))
            {
                var employees = counterEmployees[counterObject];
                Debug.Log($"[OnCounterDestroyed] 카운터에 배정된 직원: {employees.Count}명");
                
                if (employees != null && employees.Count > 0)
                {
                    var employeesToFire = employees.Where(e => e != null && e.IsHired).ToList();
                    Debug.Log($"[OnCounterDestroyed] 해고할 직원: {employeesToFire.Count}명");
                    
                    foreach (var emp in employeesToFire)
                    {
                        Debug.Log($"[OnCounterDestroyed] 🔥 '{emp.employeeName}' 해고!");
                        FireEmployee(emp);
                    }
                }
                
                // Dictionary에서 키 제거
                counterEmployees.Remove(counterObject);
                Debug.Log($"[OnCounterDestroyed] ✅ Dictionary에서 카운터 키 제거 완료");
            }
            else
            {
                Debug.Log($"[OnCounterDestroyed] ⚠️ Dictionary에서 카운터를 찾지 못함 (Dictionary 크기: {counterEmployees.Count})");
            }
        }
        
        /// <summary>
        /// 주방이 삭제될 때 호출 - 해당 주방의 직원을 즉시 해고
        /// </summary>
        public void OnKitchenDestroyed(GameObject kitchen)
        {
            if (kitchen == null) return;
            
            Debug.Log($"====================================");
            Debug.Log($"[OnKitchenDestroyed] 주방 삭제: {kitchen.name}");
            
            // ✅ Kitchen 태그를 가진 GameObject 확인
            if (!kitchen.CompareTag("Kitchen"))
            {
                Debug.Log($"[OnKitchenDestroyed] ⚠️ Kitchen 태그가 없는 오브젝트입니다");
                return;
            }
            
            // ✅ Dictionary에서 해당 GameObject 키를 직접 찾기
            if (kitchenEmployees.ContainsKey(kitchen))
            {
                var employees = kitchenEmployees[kitchen];
                Debug.Log($"[OnKitchenDestroyed] 주방에 배정된 직원: {employees.Count}명");
                
                if (employees != null && employees.Count > 0)
                {
                    var employeesToFire = employees.Where(e => e != null && e.IsHired).ToList();
                    Debug.Log($"[OnKitchenDestroyed] 해고할 직원: {employeesToFire.Count}명");
                    
                    foreach (var emp in employeesToFire)
                    {
                        Debug.Log($"[OnKitchenDestroyed] 🔥 '{emp.employeeName}' 해고!");
                        FireEmployee(emp);
                    }
                }
                
                // Dictionary에서 키 제거
                kitchenEmployees.Remove(kitchen);
                Debug.Log($"[OnKitchenDestroyed] ✅ Dictionary에서 주방 키 제거 완료");
            }
            else
            {
                Debug.Log($"[OnKitchenDestroyed] ⚠️ Dictionary에서 주방을 찾지 못함 (Dictionary 크기: {kitchenEmployees.Count})");
            }
        }
        
        /// <summary>
        /// Dictionary에서 null 키만 정리 (외부 호출 가능)
        /// Kitchen GameObject 파괴 시 즉시 호출하여 직원 해고 처리
        /// </summary>
        public void CleanupNullKeysInDictionaries()
        {
            Debug.Log($"====================================");
            Debug.Log($"[CleanupNullKeysInDictionaries] 시작!");
            Debug.Log($"[CleanupNullKeysInDictionaries] counterEmployees 크기: {counterEmployees.Count}");
            Debug.Log($"[CleanupNullKeysInDictionaries] kitchenEmployees 크기: {kitchenEmployees.Count}");
            
            // ✅ 카운터 Dictionary 정리
            var nullCounterKeys = counterEmployees.Where(kvp => kvp.Key == null).Select(kvp => kvp.Key).ToList();
            Debug.Log($"[CleanupNullKeysInDictionaries] null 카운터 키 발견: {nullCounterKeys.Count}개");
            
            foreach (var nullKey in nullCounterKeys)
            {
                if (counterEmployees.ContainsKey(nullKey))
                {
                    var employees = counterEmployees[nullKey];
                    
                    // ✅ 직원이 있으면 해고 (카운터가 사라졌으므로)
                    if (employees != null && employees.Count > 0)
                    {
                        Debug.Log($"[Dictionary 정리] null 카운터 키에 직원 {employees.Count}명 발견 - 해고 처리");
                        var employeesToFire = employees.Where(e => e != null && e.IsHired).ToList();
                        
                        foreach (var emp in employeesToFire)
                        {
                            Debug.Log($"[Dictionary 정리] 카운터 삭제로 인해 '{emp.employeeName}' 해고");
                            FireEmployee(emp);
                        }
                    }
                    
                    counterEmployees.Remove(nullKey);
                    Debug.Log($"[Dictionary 정리] ✅ null 카운터 키 제거 완료");
                }
            }
            
            // ✅ 주방 Dictionary 정리
            var nullKitchenKeys = kitchenEmployees.Where(kvp => kvp.Key == null).Select(kvp => kvp.Key).ToList();
            foreach (var nullKey in nullKitchenKeys)
            {
                if (kitchenEmployees.ContainsKey(nullKey))
                {
                    var employees = kitchenEmployees[nullKey];
                    
                    // ✅ 직원이 있으면 해고 (주방이 사라졌으므로)
                    if (employees != null && employees.Count > 0)
                    {
                        Debug.Log($"[Dictionary 정리] null 주방 키에 직원 {employees.Count}명 발견 - 해고 처리");
                        var employeesToFire = employees.Where(e => e != null && e.IsHired).ToList();
                        
                        foreach (var emp in employeesToFire)
                        {
                            Debug.Log($"[Dictionary 정리] 주방 삭제로 인해 '{emp.employeeName}' 해고");
                            FireEmployee(emp);
                        }
                    }
                    
                    kitchenEmployees.Remove(nullKey);
                    Debug.Log($"[Dictionary 정리] ✅ null 주방 키 제거 완료");
                }
            }
        }
        
        /// <summary>
        /// 직원 해고 시 배정에서 제거
        /// </summary>
        private void RemoveEmployeeFromAssignment(AIEmployee employee)
        {
            // 카운터에서 제거
            foreach (var kvp in counterEmployees)
            {
                if (kvp.Value.Contains(employee))
                {
                    kvp.Value.Remove(employee);
                    
                    // 카운터 매니저에서도 직원 해제
                    if (kvp.Key != null)
                    {
                        CounterManager counterManager = kvp.Key.GetComponent<CounterManager>();
                        if (counterManager != null)
                        {
                            counterManager.UnassignEmployee();
                            DebugLog($"직원 '{employee.employeeName}'이(가) 카운터 '{kvp.Key.name}'과 CounterManager에서 제거되었습니다.");
                        }
                        else
                        {
                            DebugLog($"직원 '{employee.employeeName}'이(가) 카운터 '{kvp.Key.name}'에서 제거되었습니다 (CounterManager 없음).");
                        }
                    }
                    break;
                }
            }
            
            // 식당에서 제거
            foreach (var kvp in kitchenEmployees)
            {
                if (kvp.Value.Contains(employee))
                {
                    kvp.Value.Remove(employee);
                    DebugLog($"직원 '{employee.employeeName}'이(가) 식당 '{kvp.Key.name}'에서 제거되었습니다.");
                    break;
                }
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 직원 타입 정보
    /// </summary>
    [System.Serializable]
    public class EmployeeType
    {
        [Header("기본 정보")]
        public string typeName = "직원";
        [TextArea(2, 4)]
        public string description = "직원 설명";
        
        [Header("작업 위치")]
        [Tooltip("직원의 작업 위치 태그 (카운터, 요리, 서빙 등)")]
        public string workPositionTag = "카운터";
        
        [Header("비용")]
        public int hiringCost = 500;
        public int dailyWage = 100;
        
        [Header("프리팹")]
        public GameObject employeePrefab;
        
        [Header("작업 정보")]
        public string jobRole = "일반";
        public int workStartHour = 9;
        public int workEndHour = 18;
        
        
        [Header("추가 설정")]
        public Color uiColor = Color.white;
        public Sprite iconSprite;
    }
}
