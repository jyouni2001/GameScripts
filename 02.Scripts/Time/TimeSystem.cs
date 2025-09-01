using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace JY
{
    /// <summary>
    /// 24시간 순환 시간 시스템을 관리하는 클래스
    /// 싱글톤 패턴으로 구현되어 전체 게임에서 접근 가능
    /// </summary>
    public class TimeSystem : MonoBehaviour
    {
        #region Enums
        // 현재 사용 중인 열거형 없음
        #endregion

        #region Fields & Properties

        [Header("시간 설정")]
        [Tooltip("1초당 게임 내 시간 흐름 배수")]
        public float timeMultiplier = 60f; // 기본값: 1초에 60초(1분)가 흐름
        
        [Tooltip("게임 시작 시 설정할 시간 (0-24)")]
        [Range(0, 24)] public float startingHour = 6f; // 게임 시작 시간 (06:00)
        
        [Header("시간 정보")]
        public float currentTime; // 현재 시간 (초 단위)
        
        [Header("날짜 설정")]
        [Tooltip("게임 시작 시 날짜 (1일부터 시작)")]
        [SerializeField] private int startingDay = 1;
        
        [Header("날짜 정보")]
        [SerializeField] private int currentDay; // 현재 일차
        

        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        public bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        public bool showImportantLogsOnly = true;
        
        // 시간 관련 속성들
        public int CurrentHour { get; private set; }
        public int CurrentMinute { get; private set; }
        public int CurrentSecond { get; private set; }
        public string CurrentTimeString { get; private set; }
        
        // 날짜 관련 속성들
        public int CurrentDay { get; set; }
        public string CurrentDateString { get; private set; }

        // 싱글톤 인스턴스
        private static TimeSystem _instance;

        #endregion

        #region Events & Delegates

        /// <summary>
        /// 시간 변경 이벤트 델리게이트
        /// </summary>
        public delegate void TimeChangeHandler(int hour, int minute);
        

        
        /// <summary>
        /// 특정 시간 이벤트 델리게이트
        /// </summary>
        public delegate void TimeEventHandler(float eventTime);
        
        /// <summary>
        /// 일차 변경 이벤트 델리게이트
        /// </summary>
        public delegate void DayChangeHandler(int newDay);
        
        // 이벤트 선언
        public event TimeChangeHandler OnHourChanged;
        public event TimeChangeHandler OnMinuteChanged; 
        public event TimeEventHandler OnTimeEvent;
        public event DayChangeHandler OnDayChanged;

        #endregion

        #region Singleton Implementation
        
        /// <summary>
        /// 싱글톤 인스턴스 접근자
        /// </summary>
        public static TimeSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<TimeSystem>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("TimeSystem");
                        _instance = obj.AddComponent<TimeSystem>();
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Unity Lifecycle Methods
        
        private void Awake()
        {
            Debug.Log("TimeSystem 초기화 시작");
            InitializeSingleton();
        }
        
        private void Start()
        {
            // 초기 상태 설정 완료
        }
        
        private void Update()
        {
            UpdateTime();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 싱글톤 패턴 초기화
        /// </summary>
        private void InitializeSingleton()
        {
            // 싱글톤 패턴 - 중복 인스턴스 방지
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            
            // 초기 시간 및 날짜 설정
            currentTime = startingHour * 3600f; // 시간을 초 단위로 변환
            currentDay = startingDay; // 초기 일차 설정
            CurrentDay = currentDay;
            UpdateTimeValues(); // 시간 값 초기화
            
            DebugLog($"시간 시스템 초기화 완료 - {CurrentDay}일차 {CurrentTimeString}", true);
        }

        #endregion

        #region Time Update Methods

        /// <summary>
        /// 매 프레임 시간 업데이트
        /// </summary>
        private void UpdateTime()
        {
            // 이전 시간 값 저장
            int prevHour = CurrentHour;
            int prevMinute = CurrentMinute;
            int prevDay = CurrentDay;
            
            // 시간 업데이트
            currentTime += Time.deltaTime * timeMultiplier;
            if (currentTime >= 86400f) // 하루는 86400초 (24시간)
            {
                currentTime -= 86400f;
                currentDay++; // 하루가 지나면 일차 증가
                CurrentDay = currentDay;
            }
            
            // 시간 값 업데이트
            UpdateTimeValues();
            

            
            // 시간 변경 이벤트 발생
            if (CurrentHour != prevHour)
            {
                OnHourChanged?.Invoke(CurrentHour, CurrentMinute);
                DebugLog($"시간 변경: {CurrentHour}시", showImportantLogsOnly);
            }
            
            if (CurrentMinute != prevMinute)
            {
                OnMinuteChanged?.Invoke(CurrentHour, CurrentMinute);
            }
            

            
            // 일차 변경 이벤트 발생
            if (CurrentDay != prevDay)
            {
                OnDayChanged?.Invoke(CurrentDay);
            }
        }
        
        /// <summary>
        /// 시간 값 업데이트 메서드
        /// </summary>
        private void UpdateTimeValues()
        {
            float hourTime = currentTime / 3600f;
            CurrentHour = Mathf.FloorToInt(hourTime) % 24;
            CurrentMinute = Mathf.FloorToInt((hourTime * 60) % 60);
            CurrentSecond = Mathf.FloorToInt((hourTime * 3600) % 60);
            
            CurrentTimeString = string.Format("{0:00}:{1:00}", CurrentHour, CurrentMinute);
            CurrentDateString = string.Format("{0}일차", CurrentDay);
        }
        

        


        #endregion

        #region Public Time Control Methods
        
        /// <summary>
        /// 시간 설정 메서드
        /// </summary>
        /// <param name="hour">시 (0-23)</param>
        /// <param name="minute">분 (0-59)</param>
        public void SetTime(int hour, int minute = 0)
        {
            currentTime = (hour * 3600f) + (minute * 60f);
            UpdateTimeValues();
            DebugLog($"시간 설정: {hour:00}:{minute:00}", true);
        }
        
        /// <summary>
        /// 일차 설정 메서드
        /// </summary>
        /// <param name="day">설정할 일차 (1 이상)</param>
        public void SetDay(int day)
        {            
            currentDay = Mathf.Max(1, day);
            CurrentDay = currentDay;
            UpdateTimeValues();
            DebugLog($"일차 설정: {CurrentDay}일차", true);
        }
        
        /// <summary>
        /// 시간과 일차를 동시에 설정
        /// </summary>
        /// <param name="day">일차 (1 이상)</param>
        /// <param name="hour">시 (0-23)</param>
        /// <param name="minute">분 (0-59)</param>
        public void SetDateTime(int day, int hour, int minute = 0)
        {
            Debug.Log($"현재 상태 = {day}");
            currentDay = Mathf.Max(1, day);
            CurrentDay = currentDay;
            currentTime = (hour * 3600f) + (minute * 60f);
            UpdateTimeValues();
            DebugLog($"날짜시간 설정: {CurrentDay}일차 {hour:00}:{minute:00}", true);
        }
        

        
        /// <summary>
        /// 현재 시간을 분 단위로 반환 (0-1439, 하루는 1440분)
        /// </summary>
        /// <returns>현재 시간의 분 단위 값</returns>
        public int GetCurrentTimeInMinutes()
        {
            return CurrentHour * 60 + CurrentMinute;
        }

        #endregion
        
        #region Debug Methods
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[TimeSystem] {message}");
        }
        
        #endregion
    }
}