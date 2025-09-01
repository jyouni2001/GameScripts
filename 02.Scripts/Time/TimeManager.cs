using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JY;

namespace JY
{
    /// <summary>
    /// 게임 내 시간 UI 및 이벤트를 관리하는 클래스
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        #region Fields & Properties

        [Header("UI 요소")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI dayText;
        
        private TimeSystem timeSystem;

        #endregion

        #region Unity Lifecycle Methods
        
        private void Start()
        {
            InitializeTimeSystem();
        }
        
        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// TimeSystem 초기화 및 이벤트 구독
        /// </summary>
        private void InitializeTimeSystem()
        {
            // TimeSystem 참조 가져오기
            timeSystem = TimeSystem.Instance;
            
            // 이벤트 구독
            SubscribeEvents();
            
            // UI 초기화
            UpdateTimeUI(timeSystem.CurrentHour, timeSystem.CurrentMinute);
            UpdateDayUI(timeSystem.CurrentDay);
        }

        /// <summary>
        /// 모든 이벤트 구독
        /// </summary>
        private void SubscribeEvents()
        {
            if (timeSystem != null)
            {
                timeSystem.OnMinuteChanged += UpdateTimeUI;
                timeSystem.OnDayChanged += UpdateDayUI;
            }
        }

        /// <summary>
        /// 모든 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (timeSystem != null)
            {
                timeSystem.OnMinuteChanged -= UpdateTimeUI;
                timeSystem.OnDayChanged -= UpdateDayUI;
            }
        }

        #endregion

        #region UI Update Methods
        
        /// <summary>
        /// 시간 UI 업데이트
        /// </summary>
        private void UpdateTimeUI(int hour, int minute)
        {
            if (timeText != null)
            {
                timeText.text = string.Format("{0:00}:{1:00}", hour, minute);
            }
        }
        

        
        /// <summary>
        /// 일차 UI 업데이트
        /// </summary>
        private void UpdateDayUI(int newDay)
        {
            if (dayText != null)
            {
                dayText.text = $"{newDay}일차";
            }
        }

        #endregion
    }
}