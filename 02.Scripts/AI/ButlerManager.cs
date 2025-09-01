using System.Collections.Generic;
using UnityEngine;
using LodgingSimulator.SO;
using LodgingSimulator.Economy;

namespace LodgingSimulator.AI
{
    /// <summary>
    /// 집사 AI들을 총괄 관리하는 매니저 클래스 (싱글톤 패턴)
    /// </summary>
    public class ButlerManager : MonoBehaviour
    {
        [Header("집사 AI 시스템 설정")]
        [Tooltip("집사 AI 설정 파일")]
        public ButlerSettingsSO settings;
        
        [Tooltip("집사 AI 프리팹")]
        public GameObject butlerPrefab;
        
        [Tooltip("집사 AI 스폰 위치")]
        public Transform butlerSpawnPoint;
        
        [Header("현재 상태")]
        [SerializeField] private List<ButlerAI> allButlers = new List<ButlerAI>();
        [SerializeField] private List<ButlerAI> idleButlers = new List<ButlerAI>();
        [SerializeField] private List<ButlerTask> pendingTasks = new List<ButlerTask>();
        
        // 싱글톤 인스턴스
        public static ButlerManager Instance { get; private set; }
        
        // 프로퍼티
        public int TotalButlerCount => allButlers.Count;
        public int IdleButlerCount => idleButlers.Count;
        public int PendingTaskCount => pendingTasks.Count;
        public ButlerSettingsSO Settings => settings;
        
        /// <summary>
        /// 싱글톤 인스턴스 설정
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 초기화
        /// </summary>
        private void Start()
        {
            // 설정 파일이 없으면 경고
            if (settings == null)
            {
                Debug.LogError("ButlerSettings가 설정되지 않았습니다!");
                return;
            }
            
            // 초기 집사 AI 생성 (선택사항)
            // CreateInitialButlers();
        }
        
        /// <summary>
        /// 매 프레임 업데이트
        /// </summary>
        private void Update()
        {
            // 작업 할당 처리
            ProcessTaskAssignment();
        }
        
        /// <summary>
        /// 새로운 집사 AI 구매 및 생성
        /// </summary>
        public bool PurchaseButler()
        {
            // 최대 집사 수 확인
            if (allButlers.Count >= settings.maxButlerCount)
            {
                Debug.LogWarning($"최대 집사 수({settings.maxButlerCount})에 도달했습니다.");
                return false;
            }
            
            // 플레이어 지갑에서 비용 차감
            PlayerWallet playerWallet = FindObjectOfType<PlayerWallet>();
            if (playerWallet != null)
            {
                if (playerWallet.SpendMoney(settings.butlerPurchaseCost))
                {
                    // 집사 AI 생성
                    CreateButler();
                    return true;
                }
                else
                {
                    Debug.LogWarning("집사 AI를 구매할 충분한 돈이 없습니다.");
                    return false;
                }
            }
            else
            {
                Debug.LogWarning("PlayerWallet을 찾을 수 없습니다.");
                return false;
            }
        }
        
        /// <summary>
        /// 집사 AI 생성
        /// </summary>
        private void CreateButler()
        {
            if (butlerPrefab == null)
            {
                Debug.LogError("집사 AI 프리팹이 설정되지 않았습니다!");
                return;
            }
            
            // 스폰 위치 결정
            Vector3 spawnPosition = butlerSpawnPoint != null ? 
                butlerSpawnPoint.position : 
                settings.counterPosition;
            
            // 집사 AI 인스턴스 생성
            GameObject butlerObject = Instantiate(butlerPrefab, spawnPosition, Quaternion.identity);
            ButlerAI butler = butlerObject.GetComponent<ButlerAI>();
            
            if (butler != null)
            {
                // 설정 적용
                butler.settings = settings;
                
                // 리스트에 추가
                allButlers.Add(butler);
                
                // 유휴 상태로 등록
                RegisterIdleButler(butler);
                
                Debug.Log($"새로운 집사 AI가 생성되었습니다. 총 집사 수: {allButlers.Count}");
            }
            else
            {
                Debug.LogError("집사 AI 프리팹에 ButlerAI 컴포넌트가 없습니다!");
                Destroy(butlerObject);
            }
        }
        
        /// <summary>
        /// 집사 AI 제거
        /// </summary>
        public void RemoveButler(ButlerAI butler)
        {
            // 모든 리스트에서 제거
            allButlers.Remove(butler);
            idleButlers.Remove(butler);
            
            // 할당된 작업이 있다면 해제
            if (butler.CurrentTask != null)
            {
                butler.CurrentTask.Unassign();
                pendingTasks.Remove(butler.CurrentTask);
            }
            
            Debug.Log($"집사 AI가 제거되었습니다. 총 집사 수: {allButlers.Count}");
        }
        
        /// <summary>
        /// 유휴 집사 등록
        /// </summary>
        public void RegisterIdleButler(ButlerAI butler)
        {
            if (!idleButlers.Contains(butler))
            {
                idleButlers.Add(butler);
            }
        }
        
        /// <summary>
        /// 유휴 집사 해제
        /// </summary>
        public void UnregisterIdleButler(ButlerAI butler)
        {
            idleButlers.Remove(butler);
        }
        
        /// <summary>
        /// 새로운 청소 작업 추가
        /// </summary>
        public void AddCleaningTask(string roomId, Vector3 roomPosition, int priority = 1)
        {
            // 이미 대기 중인 작업인지 확인
            foreach (var task in pendingTasks)
            {
                if (task.roomId == roomId)
                {
                    Debug.LogWarning($"방 {roomId}에 대한 청소 작업이 이미 대기 중입니다.");
                    return;
                }
            }
            
            // 새로운 작업 생성
            ButlerTask newTask = new ButlerTask(roomId, roomPosition, priority);
            pendingTasks.Add(newTask);
            
            Debug.Log($"새로운 청소 작업이 추가되었습니다. 방: {roomId}, 대기 작업 수: {pendingTasks.Count}");
        }
        
        /// <summary>
        /// 작업 할당 처리
        /// </summary>
        private void ProcessTaskAssignment()
        {
            // 대기 중인 작업과 유휴 집사가 모두 있을 때만 처리
            if (pendingTasks.Count == 0 || idleButlers.Count == 0)
                return;
            
            // 우선순위에 따라 작업 정렬
            pendingTasks.Sort((a, b) => b.GetPriorityScore().CompareTo(a.GetPriorityScore()));
            
            // 각 작업에 대해 가장 적합한 집사 할당
            for (int i = pendingTasks.Count - 1; i >= 0; i--)
            {
                ButlerTask task = pendingTasks[i];
                
                if (task.isAssigned)
                    continue;
                
                // 가장 적합한 집사 찾기
                ButlerAI bestButler = FindBestButlerForTask(task);
                
                if (bestButler != null)
                {
                    // 작업 할당
                    bestButler.AssignTask(task);
                    pendingTasks.RemoveAt(i);
                    
                    Debug.Log($"집사 {bestButler.butlerName}에게 방 {task.roomId} 청소 작업이 할당되었습니다.");
                }
            }
        }
        
        /// <summary>
        /// 작업에 가장 적합한 집사 찾기
        /// </summary>
        private ButlerAI FindBestButlerForTask(ButlerTask task)
        {
            if (idleButlers.Count == 0)
                return null;
            
            ButlerAI bestButler = null;
            float bestScore = float.MaxValue;
            
            foreach (var butler in idleButlers)
            {
                // 거리 계산
                float distance = Vector3.Distance(butler.transform.position, task.roomPosition);
                
                // 최대 할당 거리 확인
                if (distance > settings.maxAssignmentDistance)
                    continue;
                
                // 거리 기반 점수 계산 (거리가 가까울수록 낮은 점수)
                float score = distance;
                
                // 점수가 더 낮은 집사 선택
                if (score < bestScore)
                {
                    bestScore = score;
                    bestButler = butler;
                }
            }
            
            return bestButler;
        }
        
        /// <summary>
        /// 특정 방의 청소 작업 제거
        /// </summary>
        public void RemoveCleaningTask(string roomId)
        {
            for (int i = pendingTasks.Count - 1; i >= 0; i--)
            {
                if (pendingTasks[i].roomId == roomId)
                {
                    pendingTasks.RemoveAt(i);
                    Debug.Log($"방 {roomId}의 청소 작업이 제거되었습니다.");
                    break;
                }
            }
        }
        
        /// <summary>
        /// 모든 집사 AI의 상태 정보 반환
        /// </summary>
        public List<ButlerInfo> GetAllButlerInfo()
        {
            List<ButlerInfo> infoList = new List<ButlerInfo>();
            
            foreach (var butler in allButlers)
            {
                infoList.Add(new ButlerInfo
                {
                    butlerId = butler.butlerId,
                    butlerName = butler.butlerName,
                    currentState = butler.CurrentStateType,
                    currentTask = butler.CurrentTask?.roomId ?? "없음",
                    position = butler.transform.position
                });
            }
            
            return infoList;
        }
        
        /// <summary>
        /// 디버그 정보 표시
        /// </summary>
        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== 집사 AI 시스템 상태 ===");
            GUILayout.Label($"총 집사 수: {TotalButlerCount}");
            GUILayout.Label($"유휴 집사 수: {IdleButlerCount}");
            GUILayout.Label($"대기 작업 수: {PendingTaskCount}");
            GUILayout.Label($"최대 집사 수: {settings.maxButlerCount}");
            GUILayout.Label($"집사 구매 비용: {settings.butlerPurchaseCost}");
            
            if (GUILayout.Button("집사 AI 구매"))
            {
                PurchaseButler();
            }
            
            GUILayout.EndArea();
        }
    }
    
    /// <summary>
    /// 집사 AI 정보를 담는 구조체
    /// </summary>
    [System.Serializable]
    public struct ButlerInfo
    {
        public string butlerId;
        public string butlerName;
        public ButlerStateType currentState;
        public string currentTask;
        public Vector3 position;
    }
}
