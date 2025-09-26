using UnityEngine;
using System.Collections.Generic;

namespace JY.AI
{
    /// <summary>
    /// AI의 설정 데이터를 담는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "New AIData", menuName = "AI/AI Data", order = 1)]
    public class AIData : ScriptableObject
    {
        [Header("기본 정보")]
        [Tooltip("AI 이름")]
        public string aiName = "New AI";
        
        [Tooltip("AI 설명")]
        [TextArea(3, 5)]
        public string description = "AI 설명을 입력하세요.";
        
        [Tooltip("AI 아이콘")]
        public Sprite icon;
        
        [Header("AI 타입")]
        [Tooltip("AI 타입")]
        public AIType aiType = AIType.Butler;
        
        [Header("비주얼 설정")]
        [Tooltip("AI 프리팹")]
        public GameObject aiPrefab;
        
        [Tooltip("애니메이션 컨트롤러")]
        public RuntimeAnimatorController animatorController;
        
        [Header("능력치")]
        [Tooltip("AI 능력치")]
        public AIStats stats = AIStats.Default;
        
        [Header("작업 설정")]
        [Tooltip("수행 가능한 작업 타입들")]
        public List<TaskType> availableTasks = new List<TaskType>();
        
        [Tooltip("기본 작업 우선순위")]
        public List<TaskType> taskPriority = new List<TaskType>();
        
        [Header("고급 설정")]
        [Tooltip("특수 능력 (선택사항)")]
        public List<string> specialAbilities = new List<string>();
        
        [Tooltip("AI가 선호하는 작업 시간대")]
        public List<int> preferredWorkHours = new List<int>();
        
        [Tooltip("AI가 회피하는 작업 시간대")]
        public List<int> avoidedWorkHours = new List<int>();
        
        /// <summary>
        /// AI 데이터가 유효한지 확인
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(aiName))
            {
                Debug.LogError($"AI 데이터 '{name}'의 이름이 비어있습니다.");
                return false;
            }
            
            if (aiPrefab == null)
            {
                Debug.LogError($"AI 데이터 '{aiName}'의 프리팹이 설정되지 않았습니다.");
                return false;
            }
            
            if (!stats.IsValid())
            {
                Debug.LogError($"AI 데이터 '{aiName}'의 능력치가 유효하지 않습니다.");
                return false;
            }
            
            if (availableTasks.Count == 0)
            {
                Debug.LogWarning($"AI 데이터 '{aiName}'에 수행 가능한 작업이 없습니다.");
            }
            
            return true;
        }
        
        /// <summary>
        /// 특정 작업을 수행할 수 있는지 확인
        /// </summary>
        public bool CanPerformTask(TaskType taskType)
        {
            return availableTasks.Contains(taskType);
        }
        
        /// <summary>
        /// 작업 우선순위를 반환
        /// </summary>
        public int GetTaskPriority(TaskType taskType)
        {
            int index = taskPriority.IndexOf(taskType);
            return index >= 0 ? index : int.MaxValue;
        }
        
        /// <summary>
        /// 특정 시간대에 작업 가능한지 확인
        /// </summary>
        public bool CanWorkAtHour(int hour)
        {
            // 회피 시간대에 포함되면 false
            if (avoidedWorkHours.Contains(hour))
                return false;
            
            // 선호 시간대가 설정되어 있으면 선호 시간대만 true
            if (preferredWorkHours.Count > 0)
                return preferredWorkHours.Contains(hour);
            
            // 기본적으로 모든 시간대 작업 가능
            return true;
        }
        
        private void OnValidate()
        {
            // 에디터에서 값이 변경될 때 유효성 검사
            if (Application.isPlaying)
                return;
                
            IsValid();
        }
    }
}
