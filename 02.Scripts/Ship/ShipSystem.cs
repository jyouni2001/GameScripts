using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace JY
{
    /// <summary>
    /// 배 시스템 메인 매니저
    /// 시간 시스템과 연동하여 배의 스폰, 이동, 정박을 관리
    /// </summary>
    public class ShipSystem : MonoBehaviour
    {
        [Header("배 시스템 설정")]
        [SerializeField] private bool enableShipSystem = true;
        [SerializeField] private GameObject shipPrefab;
        [SerializeField] private int maxShipCount = 5; // 최대 배 개수
        
        [Header("시간 설정")]
        [SerializeField] private float spawnTimeBeforeArrival = 5f; // 도착 5분 전 스폰 (분)
        
        [Header("루트 설정")]
        [SerializeField] private List<ShipRoute> shipRoutes = new List<ShipRoute>();
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Tooltip("기즈모 표시 여부")]
        [SerializeField] private bool showGizmos = true;
        
        // 시스템 참조
        private TimeSystem timeSystem;
        private ShipObjectPool shipPool;
        private AISpawner aiSpawner;
        
        // 활성 배 관리
        private List<ShipController> activeShips = new List<ShipController>();
        private Dictionary<string, ShipSchedule> shipSchedules = new Dictionary<string, ShipSchedule>();
        
        // 이벤트
        public event Action<ShipController> OnShipSpawned;
        
        private void Awake()
        {
            InitializeSystem();
        }
        
        private void Start()
        {
            SetupTimeSystemConnection();
            SetupAISpawnerConnection();
            GenerateShipSchedules();
        }
        
        private void Update()
        {
            if (!enableShipSystem) return;
            
            CheckShipSchedules();
            UpdateActiveShips();
        }
        
        /// <summary>
        /// 시스템 초기화
        /// </summary>
        private void InitializeSystem()
        {
            // 오브젝트 풀 초기화
            shipPool = GetComponent<ShipObjectPool>();
            if (shipPool == null)
            {
                shipPool = gameObject.AddComponent<ShipObjectPool>();
            }
            shipPool.Initialize(shipPrefab, maxShipCount);
            
            DebugLog("배 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// TimeSystem 연결 설정
        /// </summary>
        private void SetupTimeSystemConnection()
        {
            // TimeSystem 찾기
            timeSystem = FindFirstObjectByType<TimeSystem>();
            if (timeSystem == null)
            {
                DebugLog("TimeSystem을 찾을 수 없습니다!", true);
                enableShipSystem = false;
                return;
            }
            
            DebugLog("TimeSystem 연결 완료", true);
        }

        /// <summary>
        /// AISpawner 연결 설정
        /// </summary>
        private void SetupAISpawnerConnection()
        {
            // AISpawner 찾기
            aiSpawner = FindFirstObjectByType<AISpawner>();
            if (aiSpawner == null)
            {
                DebugLog("AISpawner를 찾을 수 없습니다!", true);
                enableShipSystem = false;
                return;
            }
            
            DebugLog("AISpawner 연결 완료", true);
        }
        
        /// <summary>
        /// 배 스케줄 생성
        /// </summary>
        private void GenerateShipSchedules()
        {
            if (shipRoutes.Count == 0)
            {
                DebugLog("설정된 배 루트가 없습니다.", true);
                return;
            }
            
            shipSchedules.Clear();
            
            // AI 스폰 시간에 맞춰 배 스케줄 생성
            foreach (var route in shipRoutes)
            {
                if (route.IsValid())
                {
                    // AI 스폰 시간에 맞춰 배 도착 시간 설정
                    float aiSpawnTime = aiSpawner.GetNextSpawnTime();
                    route.arrivalTime = aiSpawnTime;
                    
                    var schedule = new ShipSchedule(route);
                    
                    // 초기 스케줄 날짜 설정
                    if (timeSystem != null)
                    {
                        int currentDay = timeSystem.CurrentDay;
                        float currentTimeInMinutes = timeSystem.GetCurrentTimeInMinutes();
                        
                        // 오늘 시간이 지났으면 내일로 스케줄
                        if (aiSpawnTime <= currentTimeInMinutes)
                        {
                            schedule.scheduledDay = currentDay + 1;
                        }
                        else
                        {
                            schedule.scheduledDay = currentDay;
                        }
                    }
                    else
                    {
                        schedule.scheduledDay = 1; // 기본값
                    }
                    
                    shipSchedules[route.routeId] = schedule;
                    DebugLog($"배 스케줄 생성: {route.routeId} (도착 시간: {aiSpawnTime}분, 날짜: {schedule.scheduledDay}일)", showImportantLogsOnly);
                }
            }
            
            DebugLog($"총 {shipSchedules.Count}개의 배 스케줄 생성됨", true);
        }
        
        /// <summary>
        /// 배 스케줄 확인 및 처리
        /// </summary>
        private void CheckShipSchedules()
        {
            if (timeSystem == null) return;
            
            float currentGameTime = timeSystem.GetCurrentTimeInMinutes();
            
            foreach (var schedule in shipSchedules.Values)
            {
                // 스폰 시간 체크
                if (!schedule.isShipSpawned && ShouldSpawnShip(schedule, currentGameTime))
                {
                    SpawnShip(schedule);
                }
                
                // 배가 스폰되었지만 아직 활성 상태인지 확인
                if (schedule.isShipSpawned && schedule.shipController != null)
                {
                    // GameObject가 파괴되었는지 확인
                    if (schedule.shipController.gameObject == null)
                    {
                        DebugLog($"배 GameObject가 파괴됨 - 스케줄 리셋: {schedule.route.routeId}", true);
                        ResetAndUpdateSchedule(schedule);
                        continue;
                    }
                    
                    // 배가 비활성 상태가 되면 (출발 완료) 스케줄 리셋
                    if (schedule.shipController.CurrentState == ShipState.Inactive && schedule.shipController.HasCompletedRoute)
                    {
                        DebugLog($"배 출발 완료 감지 - 풀로 반환: {schedule.route.routeId}", true);
                        
                        // 활성 배 목록에서 제거
                        if (activeShips.Contains(schedule.shipController))
                        {
                            activeShips.Remove(schedule.shipController);
                        }
                        
                        // 풀로 반환
                        shipPool.ReturnShip(schedule.shipController.gameObject);
                        
                        // 스케줄 리셋 및 다음 AI 스폰 시간으로 업데이트
                        ResetAndUpdateSchedule(schedule);
                    }
                    else
                    {
                        // 현재 상태 로그 (디버그용)
                        DebugLog($"배 상태 체크: {schedule.route.routeId} - {schedule.shipController.CurrentState}, 완료: {schedule.shipController.HasCompletedRoute}", false);
                    }
                }
            }
        }
        
        /// <summary>
        /// 배를 스폰해야 하는지 확인
        /// </summary>
        private bool ShouldSpawnShip(ShipSchedule schedule, float currentTime)
        {
            // 날짜 검증
            if (timeSystem != null && schedule.scheduledDay > 0)
            {
                int currentDay = timeSystem.CurrentDay;
                if (currentDay != schedule.scheduledDay)
                {
                    return false; // 스케줄된 날짜가 아니면 스폰하지 않음
                }
            }
            
            float spawnTime = schedule.arrivalTime - spawnTimeBeforeArrival;
            return currentTime >= spawnTime && currentTime < schedule.arrivalTime;
        }
        
        /// <summary>
        /// 배 스폰 처리
        /// </summary>
        private void SpawnShip(ShipSchedule schedule)
        {
            GameObject shipObj = shipPool.GetShip();
            if (shipObj == null)
            {
                DebugLog("사용 가능한 배가 없습니다.", true);
                return;
            }
            
            ShipController shipController = shipObj.GetComponent<ShipController>();
            if (shipController == null)
            {
                shipController = shipObj.AddComponent<ShipController>();
            }
            
            // 배 초기화
            shipController.Initialize(schedule.route, this);
            shipController.StartJourney();
            
            // 스케줄 업데이트
            schedule.isShipSpawned = true;
            schedule.shipController = shipController;
            
            // 활성 배 목록에 추가
            activeShips.Add(shipController);
            
            DebugLog($"배 스폰됨: {schedule.route.routeId}", true);
            
            // 이벤트 발생
            OnShipSpawned?.Invoke(shipController);
        }
        
        /// <summary>
        /// 스케줄 리셋 및 업데이트
        /// </summary>
        private void ResetAndUpdateSchedule(ShipSchedule schedule)
        {
            // 스케줄 리셋
            schedule.Reset();
            
            // 다음 AI 스폰 시간으로 업데이트
            float nextAISpawnTime = aiSpawner.GetNextSpawnTime();
            schedule.route.arrivalTime = nextAISpawnTime;
            schedule.arrivalTime = nextAISpawnTime;
            
            // 현재 날짜 또는 다음 날짜로 스케줄 설정
            if (timeSystem != null)
            {
                int currentDay = timeSystem.CurrentDay;
                float currentTimeInMinutes = timeSystem.GetCurrentTimeInMinutes();
                
                // 오늘 시간이 지났으면 내일로 스케줄
                if (nextAISpawnTime <= currentTimeInMinutes)
                {
                    schedule.scheduledDay = currentDay + 1;
                    DebugLog($"스케줄 업데이트: {schedule.route.routeId} (내일 {nextAISpawnTime}분 도착 예정)", showImportantLogsOnly);
                }
                else
                {
                    schedule.scheduledDay = currentDay;
                    DebugLog($"스케줄 업데이트: {schedule.route.routeId} (오늘 {nextAISpawnTime}분 도착 예정)", showImportantLogsOnly);
                }
            }
            else
            {
                schedule.scheduledDay = 1; // 기본값
                DebugLog($"스케줄 업데이트: {schedule.route.routeId} (다음 도착 시간: {nextAISpawnTime}분)", showImportantLogsOnly);
            }
        }
        
        /// <summary>
        /// 활성 배 상태 업데이트
        /// </summary>
        private void UpdateActiveShips()
        {
            // 현재는 개별 배가 자체적으로 상태를 관리하므로 특별한 처리 없음
            // 필요시 여기서 배들의 상태를 모니터링하거나 추가 로직 구현 가능
        }
        
        /// <summary>
        /// 루트 추가
        /// </summary>
        public void AddRoute(ShipRoute route)
        {
            if (route != null && !shipRoutes.Contains(route))
            {
                shipRoutes.Add(route);
                DebugLog($"루트 추가됨: {route.routeId}", true);
            }
        }
        
        /// <summary>
        /// 루트 제거
        /// </summary>
        public void RemoveRoute(string routeId)
        {
            shipRoutes.RemoveAll(r => r.routeId == routeId);
            shipSchedules.Remove(routeId);
            DebugLog($"루트 제거됨: {routeId}", true);
        }
        
        /// <summary>
        /// 활성 배 목록 반환
        /// </summary>
        public List<ShipController> GetActiveShips()
        {
            return new List<ShipController>(activeShips);
        }
        
        /// <summary>
        /// 특정 루트의 스케줄 반환
        /// </summary>
        public ShipSchedule GetSchedule(string routeId)
        {
            return shipSchedules.ContainsKey(routeId) ? shipSchedules[routeId] : null;
        }
        
        #region 디버그 메서드
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[ShipSystem] {message}");
        }
        
        /// <summary>
        /// 기즈모 그리기
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            
            // 각 루트의 기즈모 그리기
            foreach (var route in shipRoutes)
            {
                if (route != null)
                {
                    route.DrawGizmos();
                }
            }
        }
        
        #endregion
        
        private void OnDestroy()
        {
            // 이벤트 정리
            OnShipSpawned = null;
        }
    }
    
    /// <summary>
    /// 배 스케줄 클래스
    /// </summary>
    [System.Serializable]
    public class ShipSchedule
    {
        public ShipRoute route;
        public float arrivalTime; // 게임 시간 (분, 하루 기준 0-1440)
        public int scheduledDay; // 스케줄된 날짜
        public bool isShipSpawned;
        public ShipController shipController;
        
        public ShipSchedule(ShipRoute shipRoute)
        {
            route = shipRoute;
            arrivalTime = shipRoute.arrivalTime;
            scheduledDay = -1; // 초기값: 날짜 미설정
            isShipSpawned = false;
            shipController = null;
        }
        
        /// <summary>
        /// 스케줄 리셋
        /// </summary>
        public void Reset()
        {
            isShipSpawned = false;
            shipController = null;
            // scheduledDay는 리셋하지 않음 (다음 업데이트에서 설정됨)
        }
    }
}
