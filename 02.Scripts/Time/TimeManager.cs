using JY;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

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

        public static TimeManager instance;

        [SerializeField] private LocalizedString dayCounterLocalizedString;
        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            Debug.Log("TimeManager 초기화 시작");
            InitializeTimeSystem();

            if(instance == null)
            {
                instance = this;
            }
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
        public void UpdateDayUI(int newDay)
        {
            string localizedString = LocalizationSettings.StringDatabase.GetLocalizedString("Locales", "Ingame_DayCounter", new object[] { newDay });
            dayText.text = localizedString;
            Debug.Log(localizedString);
        }

        
        #endregion
    }
}