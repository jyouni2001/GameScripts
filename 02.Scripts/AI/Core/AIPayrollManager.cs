using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JY.AI
{
    /// <summary>
    /// AI 급여 관리 시스템
    /// </summary>
    public class AIPayrollManager : MonoBehaviour
    {
        public static AIPayrollManager Instance { get; private set; }
        
        [Header("급여 설정")]
        [Tooltip("급여 지급 시간 (시)")]
        [Range(0, 23)]
        public int payrollHour = 0; // 0시에 급여 지급
        
        [Tooltip("급여 지급 시 로그 출력")]
        public bool enablePayrollLogs = true;
        
        private DateTime lastPayrollDate = DateTime.MinValue;
        
        void Awake()
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
        
        void Start()
        {
            // 게임 시간 시스템과 연결 (DayManager 등이 있다면)
            // DayManager.OnDayChanged += ProcessPayroll;
        }
        
        void Update()
        {
            CheckAndProcessPayroll();
        }
        
        /// <summary>
        /// 급여 지급을 확인하고 처리
        /// </summary>
        private void CheckAndProcessPayroll()
        {
            DateTime currentTime = DateTime.Now; // 실제 게임에서는 게임 시간 사용
            
            // 매일 설정된 시간에 급여 지급
            if (ShouldProcessPayroll(currentTime))
            {
                ProcessPayroll(currentTime);
            }
        }
        
        /// <summary>
        /// 급여를 지급해야 하는지 확인
        /// </summary>
        private bool ShouldProcessPayroll(DateTime currentTime)
        {
            // 날짜가 바뀌었고 설정된 시간인 경우
            return currentTime.Date > lastPayrollDate.Date && 
                   currentTime.Hour == payrollHour;
        }
        
        /// <summary>
        /// 모든 AI에게 급여 지급
        /// </summary>
        public void ProcessPayroll(DateTime currentTime)
        {
            // EmployeeHiringSystem에서 고용된 직원들 가져오기
            if (EmployeeHiringSystem.Instance == null)
            {
                Debug.LogWarning("[AIPayrollManager] EmployeeHiringSystem not found!");
                return;
            }
            
            var activeEmployees = EmployeeHiringSystem.Instance.GetHiredEmployees();
            int totalPayroll = 0;
            int paidAICount = 0;
            
            foreach (var employee in activeEmployees)
            {
                if (employee != null && employee.IsHired)
                {
                    int dailyWage = employee.dailyWage;
                    totalPayroll += dailyWage;
                    paidAICount++;
                    
                    if (enablePayrollLogs)
                    {
                        Debug.Log($"[AIPayrollManager] 급여 지급: {employee.employeeName} - {dailyWage}골드");
                    }
                }
            }
            
            // 플레이어 지갑에서 급여 차감
            if (totalPayroll > 0)
            {
                DeductPayrollFromPlayer(totalPayroll);
                
                if (enablePayrollLogs)
                {
                    Debug.Log($"[AIPayrollManager] 총 급여 지급 완료: {paidAICount}명, {totalPayroll}골드");
                }
            }
            
            lastPayrollDate = currentTime.Date;
        }
        
        /// <summary>
        /// 플레이어 지갑에서 급여 차감
        /// </summary>
        private void DeductPayrollFromPlayer(int totalAmount)
        {
            // PlayerWallet 시스템과 연결
            if (PlayerWallet.Instance != null)
            {
                if (PlayerWallet.Instance.money >= totalAmount)
                {
                    PlayerWallet.Instance.SpendMoney(totalAmount);
                    Debug.Log($"[AIPayrollManager] 급여 차감 완료: {totalAmount}골드. 남은 골드: {PlayerWallet.Instance.money}");
                }
                else
                {
                    Debug.LogWarning($"[AIPayrollManager] 급여 지급 실패: 골드 부족 ({totalAmount}골드 필요, 현재: {PlayerWallet.Instance.money}골드)");
                    // TODO: 급여를 지급할 수 없는 경우의 처리 (AI 해고, 경고 등)
                }
            }
            else
            {
                Debug.LogError("[AIPayrollManager] PlayerWallet 인스턴스를 찾을 수 없습니다!");
            }
        }
        
        /// <summary>
        /// 다음 급여까지 남은 시간 계산
        /// </summary>
        public int GetMinutesUntilNextPayroll(DateTime currentTime)
        {
            DateTime nextPayroll = currentTime.Date.AddHours(payrollHour);
            
            // 오늘 급여시간이 이미 지났다면 내일 급여시간으로
            if (currentTime >= nextPayroll)
            {
                nextPayroll = nextPayroll.AddDays(1);
            }
            
            return (int)(nextPayroll - currentTime).TotalMinutes;
        }
        
        /// <summary>
        /// 오늘 지급할 총 급여 계산
        /// </summary>
        public int CalculateTodayPayroll()
        {
            if (EmployeeHiringSystem.Instance == null) return 0;
            
            var activeEmployees = EmployeeHiringSystem.Instance.GetHiredEmployees();
            return activeEmployees.Sum(employee => employee?.dailyWage ?? 0);
        }
        
        /// <summary>
        /// 수동으로 급여 지급 (테스트용)
        /// </summary>
        [ContextMenu("Process Payroll Now")]
        public void ManualProcessPayroll()
        {
            ProcessPayroll(DateTime.Now);
        }
        
        void OnDestroy()
        {
            // 이벤트 구독 해제
            // DayManager.OnDayChanged -= ProcessPayroll;
        }
    }
}
