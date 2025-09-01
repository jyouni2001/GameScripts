using UnityEngine;

namespace LodgingSimulator.AI
{
    /// <summary>
    /// 집사 AI가 수행할 청소 작업을 정의하는 클래스
    /// </summary>
    [System.Serializable]
    public class ButlerTask
    {
        [Header("작업 정보")]
        [Tooltip("작업 ID")]
        public string taskId;
        
        [Tooltip("청소할 방의 위치")]
        public Vector3 roomPosition;
        
        [Tooltip("방의 고유 식별자")]
        public string roomId;
        
        [Tooltip("작업 우선순위 (높을수록 우선)")]
        public int priority;
        
        [Tooltip("작업 생성 시간")]
        public float creationTime;
        
        [Header("작업 상태")]
        [Tooltip("작업이 할당되었는지 여부")]
        public bool isAssigned;
        
        [Tooltip("작업을 수행하는 집사 AI")]
        public ButlerAI assignedButler;
        
        /// <summary>
        /// 새로운 청소 작업 생성
        /// </summary>
        public ButlerTask(string roomId, Vector3 roomPosition, int priority = 1)
        {
            this.taskId = System.Guid.NewGuid().ToString();
            this.roomId = roomId;
            this.roomPosition = roomPosition;
            this.priority = priority;
            this.creationTime = Time.time;
            this.isAssigned = false;
            this.assignedButler = null;
        }
        
        /// <summary>
        /// 작업을 집사 AI에게 할당
        /// </summary>
        public void AssignToButler(ButlerAI butler)
        {
            this.isAssigned = true;
            this.assignedButler = butler;
        }
        
        /// <summary>
        /// 작업 할당 해제
        /// </summary>
        public void Unassign()
        {
            this.isAssigned = false;
            this.assignedButler = null;
        }
        
        /// <summary>
        /// 작업 완료 처리
        /// </summary>
        public void Complete()
        {
            if (assignedButler != null)
            {
                assignedButler.CompleteTask(this);
            }
        }
        
        /// <summary>
        /// 작업의 대기 시간 계산
        /// </summary>
        public float GetWaitTime()
        {
            return Time.time - creationTime;
        }
        
        /// <summary>
        /// 작업 우선순위 점수 계산 (대기 시간과 우선순위 고려)
        /// </summary>
        public float GetPriorityScore()
        {
            float waitTimeScore = GetWaitTime() * 0.1f; // 대기 시간당 0.1점
            return priority + waitTimeScore;
        }
    }
}
