using UnityEngine;

namespace JY.AI
{
    /// <summary>
    /// AI의 직업과 행동 관련 능력치를 정의하는 구조체
    /// </summary>
    [System.Serializable]
    public struct AIStats
    {
        [Header("기본 능력치")]
        [Tooltip("작업 효율성 (0.0 ~ 2.0)")]
        [Range(0.0f, 2.0f)]
        public float efficiency;
        
        [Tooltip("이동 속도 배율 (0.5 ~ 2.0)")]
        [Range(0.5f, 2.0f)]
        public float speedMultiplier;
        
        [Header("경제 정보")]
        [Tooltip("일급 (골드) - 매일 0시에 지급")]
        public int dailyWage;
        
        [Header("작업 관련")]
        [Tooltip("최대 작업 시간 (분)")]
        public int maxWorkDuration;
        
        [Tooltip("휴식 필요 시간 (분)")]
        public int restDuration;
        
        /// <summary>
        /// 기본 능력치로 초기화
        /// </summary>
        public static AIStats Default => new AIStats
        {
            efficiency = 1.0f,
            speedMultiplier = 1.0f,
            dailyWage = 100,
            maxWorkDuration = 60,
            restDuration = 15
        };
        
        /// <summary>
        /// 능력치가 유효한지 확인
        /// </summary>
        public bool IsValid()
        {
            return efficiency > 0 && speedMultiplier > 0 && dailyWage >= 0 && 
                   maxWorkDuration > 0 && restDuration >= 0;
        }
    }
}
