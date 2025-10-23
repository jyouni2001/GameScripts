using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace JY
{
    /// <summary>
    /// AI 스폰 관리 시스템
    /// 시간 기반으로 AI를 자동 스폰하고 오브젝트 풀링으로 성능 최적화
    /// 싱글톤 패턴으로 구현되어 전역 접근 가능
    /// </summary>
    public class AISpawner : MonoBehaviour 
    {
        [Header("AI 프리팹 설정")]
        [Tooltip("스폰할 AI 프리팹")]
        public GameObject aiPrefab;
        
        [Tooltip("오브젝트 풀 크기")]
        public int poolSize = 200;
        
        [Header("시간 기반 스폰 설정")]
        [SerializeField] private int minSpawner = 1;   // 최소 스폰 개수
        [SerializeField] private int maxSpawner = 5;   // 최대 스폰 개수
        [SerializeField] private int startHour = 11;    // 스폰 시작 시간 (11시)
        [SerializeField] private int endHour = 16;     // 스폰 종료 시간 (17시)
        [SerializeField] private int spawnInterval = 2; // 스폰 간격 (2시간)
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = true;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = false;
        
        [Tooltip("인스펙터에서 실시간 정보 표시")]
        [SerializeField] private bool showRuntimeInfo = true;
        
        [Header("실시간 정보")]
        [SerializeField] private int currentActiveAIs = 0;
        
        private Queue<GameObject> aiPool;
        private List<GameObject> activeAIs;
        private TimeSystem timeSystem;
        
        // 다음 스폰 시간을 추적하기 위한 변수
        private List<int> spawnTimes = new List<int>();
        private int lastSpawnHour = -1;
        
        // 싱글톤 인스턴스
        public static AISpawner Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            InitializePool();
            InitializeTimeSystem();
            SetupSpawnTimes();
        }
        
        void Update()
        {
            // 인스펙터에서 실시간으로 활성화된 AI 수 확인
            if (showRuntimeInfo)
            {
                currentActiveAIs = activeAIs.Count;
            }
        }
        
        /// <summary>
        /// TimeSystem 초기화 및 이벤트 구독
        /// </summary>
        private void InitializeTimeSystem()
        {
            timeSystem = TimeSystem.Instance;
            
            if (timeSystem != null)
            {
                // 시간이 변경될 때마다 스폰 체크
                timeSystem.OnMinuteChanged += CheckForSpawn;
                DebugLog("TimeSystem 연결 완료", true);
            }
            else
            {
            }
        }
        
        /// <summary>
        /// 스폰 시간 리스트 설정
        /// </summary>
        private void SetupSpawnTimes()
        {
            spawnTimes.Clear();
            
            for (int hour = startHour; hour <= endHour; hour += spawnInterval)
            {
                spawnTimes.Add(hour);
            }
            
            DebugLog($"스폰 시간 설정: {string.Join(", ", spawnTimes)}시", true);
        }

        /// <summary>
        /// AI 오브젝트 풀 초기화
        /// </summary>
        void InitializePool()
        {
            aiPool = new Queue<GameObject>();
            activeAIs = new List<GameObject>();

            // 풀에 AI 오브젝트들을 미리 생성
            for (int i = 0; i < poolSize; i++)
            {
                GameObject ai = Instantiate(aiPrefab, transform.position, Quaternion.identity);
                ai.name = $"AI_{i}";
                ai.SetActive(false);
                ai.transform.parent = transform;
                aiPool.Enqueue(ai);

                // AIAgent 컴포넌트에 spawner 참조 설정
                AIAgent aiAgent = ai.GetComponent<AIAgent>();
                if (aiAgent != null)
                {
                    aiAgent.SetSpawner(this);
                }
            }
            
            DebugLog($"AI 풀 초기화 완료: {poolSize}개 생성", true);
        }
        
        /// <summary>
        /// 매 분마다 호출되어 스폰 시간인지 확인
        /// </summary>
        private void CheckForSpawn(int hour, int minute)
        {
            // 정각(0분)일 때만 체크하고, 이미 이 시간에 스폰했다면 건너뛰기
            if (minute == 0 && spawnTimes.Contains(hour) && lastSpawnHour != hour)
            {
                int spawnCount = Random.Range(minSpawner, maxSpawner + 1);
                StartCoroutine(SpawnMultipleAIs(spawnCount));
                lastSpawnHour = hour;
                
                DebugLog($"{hour}시 정각: {spawnCount}명의 AI 스폰 (범위: {minSpawner}~{maxSpawner})", true);
            }
        }
        
        /// <summary>
        /// 여러 AI를 연속으로 스폰 (약간의 딜레이를 두고)
        /// </summary>
        private IEnumerator SpawnMultipleAIs(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnAI();
                
                // 각 스폰 사이에 짧은 딜레이
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        /// <summary>
        /// 단일 AI 스폰
        /// </summary>
        void SpawnAI()
        {
            if (aiPool.Count <= 0)
            {
                DebugLog("풀에 사용 가능한 AI가 없습니다!", true);
                return;
            }

            GameObject ai = aiPool.Dequeue();
            ai.transform.position = transform.position;
            ai.transform.rotation = Quaternion.identity;

            // AI 컴포넌트 초기화
            AIAgent aiAgent = ai.GetComponent<AIAgent>();
            if (aiAgent != null)
            {
                aiAgent.SetSpawner(this);
            }

            ai.SetActive(true);
            activeAIs.Add(ai);
            
            // 통계 시스템에 방문객 스폰 알림 (총 방문객 수 증가)
            if (DailyStatisticsManager.Instance != null)
            {
                DailyStatisticsManager.Instance.OnVisitorSpawned();
                DebugLog($"방문객 스폰 기록: {ai.name} (총 방문객 수 증가)", true);
            }
            else
            {
                DebugLog("DailyStatisticsManager가 아직 초기화되지 않았습니다!", true);
            }
            
            DebugLog($"{ai.name} 활성화됨 (현재 활성화된 AI: {activeAIs.Count}개)", true);
            
            // UI 업데이트 알림
            NotifyAICountChanged();
        }

        /// <summary>
        /// AI 오브젝트를 풀로 반환
        /// </summary>
        public void ReturnToPool(GameObject ai)
        {
            if (ai == null) return;

            ai.SetActive(false);
            activeAIs.Remove(ai);
            aiPool.Enqueue(ai);
            ai.transform.position = transform.position;
            
            DebugLog($"{ai.name} 풀로 반환됨 (현재 활성화된 AI: {activeAIs.Count}개)");
            
            // UI 업데이트 알림
            NotifyAICountChanged();
        }

        /// <summary>
        /// 모든 활성 AI를 풀로 반환
        /// </summary>
        public void ReturnAllToPool()
        {
            List<GameObject> tempList = new List<GameObject>(activeAIs);
            foreach (GameObject ai in tempList)
            {
                ReturnToPool(ai);
            }
            
            DebugLog("모든 AI를 풀로 반환했습니다.", true);
        }

        /// <summary>
        /// 수동 스폰 (테스트용)
        /// </summary>
        public void ManualSpawn(int count = -1)
        {
            if (count <= 0)
            {
                count = Random.Range(minSpawner, maxSpawner + 1);
            }
            
            StartCoroutine(SpawnMultipleAIs(count));
            DebugLog($"수동 스폰: {count}명의 AI", true);
        }
        
        /// <summary>
        /// AI 수 변경 시 UI 업데이트 알림
        /// </summary>
        private void NotifyAICountChanged()
        {
            // RealTimeAICounterUI 찾기 및 업데이트
            var aiCounterUI = FindFirstObjectByType<RealTimeAICounterUI>();
            if (aiCounterUI != null)
            {
                aiCounterUI.OnAICountChanged();
            }
        }
        
        /// <summary>
        /// 방문객 수 테스트 (디버그용)
        /// </summary>
        public void TestVisitorCount()
        {
            DebugLog($"현재 활성 AI 수: {GetActiveAICount()}명", true);
            DebugLog($"풀에 남은 AI 수: {GetPooledAICount()}명", true);
            
            if (DailyStatisticsManager.Instance != null)
            {
                DailyStatisticsManager.Instance.TestVisitorSpawn();
                DebugLog("방문객 수 테스트 완료", true);
            }
            else
            {
                DebugLog("DailyStatisticsManager가 null입니다!", true);
            }
        }

        /// <summary>
        /// 스폰 설정 변경
        /// </summary>
        public void SetSpawnSettings(int minSpawn, int maxSpawn, int startH, int endH, int intervalH)
        {
            minSpawner = minSpawn;
            maxSpawner = maxSpawn;
            startHour = startH;
            endHour = endH;
            spawnInterval = intervalH;
            
            SetupSpawnTimes();
            DebugLog($"스폰 설정 변경: {minSpawn}-{maxSpawn}명, {startH}-{endH}시, {intervalH}시간 간격", true);
        }

        /// <summary>
        /// 현재 활성화된 AI 수 반환
        /// </summary>
        public int GetActiveAICount()
        {
            return activeAIs != null ? activeAIs.Count : 0;
        }

        /// <summary>
        /// 풀에 남은 AI 수 반환
        /// </summary>
        public int GetPooledAICount()
        {
            return aiPool != null ? aiPool.Count : 0;
        }

        /// <summary>
        /// 활성화된 모든 AI GameObject 목록 반환 (저장/로드용)
        /// </summary>
        public List<GameObject> GetActiveAIs()
        {
            return activeAIs != null ? new List<GameObject>(activeAIs) : new List<GameObject>();
        }

        /// <summary>
        /// 모든 AI (활성화 + 비활성화) 반환 (저장/로드용)
        /// </summary>
        public List<GameObject> GetAllAIs()
        {
            List<GameObject> allAIs = new List<GameObject>();
            
            // 활성화된 AI 추가
            if (activeAIs != null)
            {
                allAIs.AddRange(activeAIs);
            }
            
            // 비활성화된 AI 추가 (풀에 있는 AI)
            if (aiPool != null)
            {
                allAIs.AddRange(aiPool);
            }
            
            return allAIs;
        }

        /// <summary>
        /// 저장된 데이터로부터 AI를 활성화하고 복원 (로드 전용)
        /// </summary>
        public bool RestoreAI(GameObject aiObject)
        {
            if (aiObject == null) return false;
            
            // 풀에서 제거
            if (aiPool != null && aiPool.Contains(aiObject))
            {
                // Queue를 임시 리스트로 변환
                List<GameObject> tempList = new List<GameObject>(aiPool);
                tempList.Remove(aiObject);
                
                // Queue 재생성
                aiPool.Clear();
                foreach (var ai in tempList)
                {
                    aiPool.Enqueue(ai);
                }
            }
            
            // AI 활성화
            if (!aiObject.activeSelf)
            {
                aiObject.SetActive(true);
            }
            
            // activeAIs에 추가 (중복 체크)
            if (activeAIs != null && !activeAIs.Contains(aiObject))
            {
                activeAIs.Add(aiObject);
                DebugLog($"{aiObject.name} 복원됨 (현재 활성화된 AI: {activeAIs.Count}개)", true);
            }
            
            // UI 업데이트 알림
            NotifyAICountChanged();
            
            return true;
        }

        /// <summary>
        /// 모든 활성 AI를 비활성화하고 풀로 반환 (로드 전 초기화용)
        /// </summary>
        public void DeactivateAllAIs()
        {
            if (activeAIs == null) return;
            
            List<GameObject> tempList = new List<GameObject>(activeAIs);
            foreach (var ai in tempList)
            {
                if (ai != null)
                {
                    ai.SetActive(false);
                    activeAIs.Remove(ai);
                    aiPool.Enqueue(ai);
                }
            }
            
            DebugLog($"모든 AI 비활성화 완료 (풀로 반환)", true);
            NotifyAICountChanged();
        }

        /// <summary>
        /// 다음 스폰 시간 반환 (분 단위) - 하루 기준으로만 반환
        /// </summary>
        public float GetNextSpawnTime()
        {
            if (timeSystem == null || spawnTimes.Count == 0) return 0f;
            
            int currentHour = timeSystem.CurrentHour;
            int currentMinute = timeSystem.CurrentMinute;
            int currentTimeInMinutes = currentHour * 60 + currentMinute;
            
            // 오늘 남은 스폰 시간 찾기
            foreach (int spawnHour in spawnTimes)
            {
                int spawnTimeInMinutes = spawnHour * 60;
                if (spawnTimeInMinutes > currentTimeInMinutes)
                {
                    DebugLog($"다음 스폰 시간: 오늘 {spawnHour}시 ({spawnTimeInMinutes}분)", false);
                    return spawnTimeInMinutes;
                }
            }
            
            // 오늘 남은 스폰 시간이 없으면 내일 첫 번째 스폰 시간 (하루 기준으로만)
            if (spawnTimes.Count > 0)
            {
                int nextSpawnTime = spawnTimes[0] * 60; // 내일 첫 번째 시간 (24시간 더하지 않음)
                DebugLog($"다음 스폰 시간: 내일 {spawnTimes[0]}시 ({nextSpawnTime}분)", false);
                return nextSpawnTime;
            }
            
            return 0f;
        }

        void OnDestroy()
        {
            if (timeSystem != null)
            {
                timeSystem.OnMinuteChanged -= CheckForSpawn;
            }
        }

        void OnValidate()
        {
            if (Application.isPlaying)
            {
                SetupSpawnTimes();
            }
        }
        
        #region 디버그 메서드
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
        }
        
        #endregion
    }
}