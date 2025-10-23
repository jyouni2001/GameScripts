using UnityEngine;
using System;

namespace JY.AI
{
    /// <summary>
    /// AI의 근무 스케줄을 관리하는 클래스
    /// </summary>
    [System.Serializable]
    public class AIWorkSchedule
    {
        [Header("근무 시간")]
        [Tooltip("근무 시작 시간 (시)")]
        [Range(0, 23)]
        public int workStartHour = 8;
        
        [Tooltip("근무 종료 시간 (시)")]
        [Range(0, 23)]
        public int workEndHour = 22;
        
        [Header("급여")]
        [Tooltip("일급 (골드)")]
        public int dailyWage = 100;
        
        [Tooltip("마지막 급여 지급일")]
        public DateTime lastPayDate = DateTime.MinValue;
        
        /// <summary>
        /// 현재 시간이 근무 시간인지 확인
        /// </summary>
        public bool IsWorkTime(DateTime currentTime)
        {
            int currentHour = currentTime.Hour;
            
            // 8시부터 22시까지 (14시간 근무)
            if (workStartHour <= workEndHour)
            {
                return currentHour >= workStartHour && currentHour < workEndHour;
            }
            else
            {
                // 자정을 넘나드는 경우 (예: 22시~8시)
                return currentHour >= workStartHour || currentHour < workEndHour;
            }
        }
        
        /// <summary>
        /// 급여를 지급해야 하는지 확인 (매일 0시)
        /// </summary>
        public bool ShouldPayWage(DateTime currentTime)
        {
            if (lastPayDate == DateTime.MinValue)
            {
                return true; // 첫 급여
            }
            
            // 날짜가 바뀌었고 0시인 경우
            return currentTime.Date > lastPayDate.Date && currentTime.Hour == 0;
        }
        
        /// <summary>
        /// 급여 지급 처리
        /// </summary>
        public void PayWage(DateTime currentTime)
        {
            lastPayDate = currentTime.Date;
        }
        
        /// <summary>
        /// 근무 시간까지 남은 시간 계산 (분 단위)
        /// </summary>
        public int GetMinutesUntilWorkStart(DateTime currentTime)
        {
            DateTime nextWorkStart = currentTime.Date.AddHours(workStartHour);
            
            // 오늘 근무시간이 이미 지났다면 내일 근무시간으로
            if (currentTime >= nextWorkStart)
            {
                nextWorkStart = nextWorkStart.AddDays(1);
            }
            
            return (int)(nextWorkStart - currentTime).TotalMinutes;
        }
        
        /// <summary>
        /// 근무 종료까지 남은 시간 계산 (분 단위)
        /// </summary>
        public int GetMinutesUntilWorkEnd(DateTime currentTime)
        {
            DateTime workEnd = currentTime.Date.AddHours(workEndHour);
            
            // 오늘 근무시간이 이미 지났다면 내일 근무시간으로
            if (currentTime >= workEnd)
            {
                workEnd = workEnd.AddDays(1);
            }
            
            return (int)(workEnd - currentTime).TotalMinutes;
        }
    }
}
