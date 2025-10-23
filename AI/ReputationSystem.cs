using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace JY
{
    /// <summary>
    /// 플레이어의 명성도 시스템을 관리하는 클래스
    /// 명성도 증감, 등급 관리, UI 업데이트를 담당
    /// </summary>
    public class ReputationSystem : MonoBehaviour
    {
        [Header("명성도 설정")]
        [Tooltip("현재 플레이어의 명성도 점수")]
        [SerializeField] private int currentReputation = 0;
        
        [Header("UI 설정")]
        [Tooltip("명성도를 표시할 UI 텍스트 컴포넌트")]
        [SerializeField] private TextMeshProUGUI reputationText; // 인스펙터에서 할당
        [SerializeField] private TextMeshProUGUI currentGradeText;

        [Tooltip("UI 텍스트 형식")]
        [SerializeField] private string textFormat = "Grade: {0} {1}"; // {0}: 명성도, {1}: 등급

        [Tooltip("획득한 명성도 표시")]
        [SerializeField] private RectTransform reputationTextTransform;
        [SerializeField] private Color floatingTextColor;

        [Header("등급 설정")]
        [Tooltip("각 등급에 필요한 최소 명성도")]
        [SerializeField] private int[] gradeThresholds = {0, 100, 300, 500, 1000, 2000, 3000};

        [Header("등급별 텍스트 설정")]
        [Tooltip("이 등급 이상부터 '호텔'로 표시됩니다 (0부터 시작). Tier3 = 3")]
        [SerializeField] private int hotelGradeIndexThreshold = 3;

        [Header("등급 변경 알림 UI")]
        [Tooltip("등급 변경 시 나타날 UI 패널")]
        [SerializeField] private GameObject gradeChangePanel;
        [Tooltip("알림 메시지를 표시할 TextMeshPro 자식 오브젝트")]
        [SerializeField] private TextMeshProUGUI gradeChangeText;



        [Tooltip("등급 이름 목록 (Localization Table Key)")]
        [SerializeField] private string[] gradeNames = { "Grade_Ground", "Grade_Tier1", "Grade_Tier2", "Grade_Tier3", "Grade_Tier4", "Grade_Tier5", "Grade_Tier6" };

        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Tooltip("명성도 변경 기록 표시")]
        [SerializeField] private bool showReputationChanges = true;
        
        [Header("로그 정보")]
        [Tooltip("명성도 변경 기록")]
        [SerializeField] private List<string> reputationLogs = new List<string>();

        [Header("알림 애니메이션 설정")]
        [SerializeField] private float animStartY = -350f;
        [SerializeField] private float animEndY = -200f;
        [SerializeField] private float animDuration = 1.0f;
        [SerializeField] private float animDisplayTime = 2.0f; // 애니메이션 후 표시 유지 시간
        private Sequence gradeChangeSequence;

        [Tooltip("애니메이션을 위한 CanvasGroup 컴포넌트")]
        [SerializeField] private CanvasGroup gradeChangeCanvasGroup;

        // 싱글톤 인스턴스
        public static ReputationSystem Instance { get; set; }
        
        // 공개 속성
        public int CurrentReputation => currentReputation;
        public string CurrentGrade => GetCurrentGrade();

        // 명성도 변경 시 이벤트 추가
        public event Action<int> OnReputationChanged;

        // 캐싱 변수 (성능 최적화)
        private int lastReputation = -1; // 마지막으로 표시된 명성도
        private string lastFormattedReputation = ""; // 마지막으로 포맷된 명성도 문자열

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        private void Start()
        {
            if (gradeChangePanel != null)
            {
                gradeChangePanel.SetActive(false);
                if (gradeChangeCanvasGroup == null)
                    gradeChangeCanvasGroup = gradeChangePanel.GetComponent<CanvasGroup>();
                if (gradeChangeCanvasGroup == null)
                    gradeChangeCanvasGroup = gradeChangePanel.AddComponent<CanvasGroup>();
            }

            // 시작할 때 UI 업데이트
            UpdateUI();
            DebugLog("명성도 시스템 초기화 완료", true);
        }
        private void OnEnable()
        {
#pragma warning disable UDR0005 // Domain Reload Analyzer
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
#pragma warning restore UDR0005 // Domain Reload Analyzer
        }
        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            gradeChangeSequence?.Kill();
        }

        private void OnLocaleChanged(Locale newLocale)
        {
            DebugLog($"언어 변경 감지: {newLocale.Identifier.Code}. UI를 새로고침합니다.", true);
            UpdateUI(); // 언어가 변경되면 주 UI를 업데이트합니다.
        }

        /// <summary>
        /// 명성도 추가
        /// </summary>
        /// <param name="amount">추가할 명성도</param>
        /// <param name="reason">명성도 증가 이유</param>
        public void AddReputation(int amount, string reason = "")
        {
            if (amount <= 0) return;
            
            string prevGrade = GetCurrentGrade();
            currentReputation += amount;
            string newGrade = GetCurrentGrade();
            
            // 등급이 변경되었는지 확인
            bool gradeChanged = prevGrade != newGrade;
            
            if (showReputationChanges)
            {
                string logMessage = $"명성도 +{amount} (총 {currentReputation})";
                if (!string.IsNullOrEmpty(reason))
                {
                    logMessage += $" - {reason}";
                }
                
                reputationLogs.Add(logMessage);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (reputationLogs.Count > 20)
                {
                    reputationLogs.RemoveAt(0);
                }
            }
            
            DebugLog($"명성도 증가: +{amount} (총 {currentReputation}) - {reason}", gradeChanged || showImportantLogsOnly);
            
            if (gradeChanged)
            {
                DebugLog($"등급 상승! {prevGrade} → {newGrade}", true);
                ShowGradeChangeUI();
            }

            if (FloatingTextManager.Instance != null && reputationTextTransform != null && amount > 0)
            {
                FloatingTextManager.Instance.Show($"+{amount}", floatingTextColor, reputationTextTransform.position);
            }

            UpdateUI();
            OnReputationChanged?.Invoke(currentReputation);
        }
        
        /// <summary>
        /// 명성도 감소
        /// </summary>
        /// <param name="amount">감소할 명성도</param>
        /// <param name="reason">명성도 감소 이유</param>
        public void RemoveReputation(int amount, string reason = "")
        {
            if (amount <= 0) return;
            
            string prevGrade = GetCurrentGrade();
            currentReputation = Mathf.Max(0, currentReputation - amount);
            string newGrade = GetCurrentGrade();
            
            // 등급이 변경되었는지 확인
            bool gradeChanged = prevGrade != newGrade;
            
            if (showReputationChanges)
            {
                string logMessage = $"명성도 -{amount} (총 {currentReputation})";
                if (!string.IsNullOrEmpty(reason))
                {
                    logMessage += $" - {reason}";
                }
                
                reputationLogs.Add(logMessage);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (reputationLogs.Count > 20)
                {
                    reputationLogs.RemoveAt(0);
                }
            }
            
            DebugLog($"명성도 감소: -{amount} (총 {currentReputation}) - {reason}", gradeChanged || showImportantLogsOnly);
            
            if (gradeChanged)
            {
                DebugLog($"등급 하락! {prevGrade} → {newGrade}", true);
            }
            
            UpdateUI();
            OnReputationChanged?.Invoke(currentReputation);
        }
        
        /// <summary>
        /// 명성도 직접 설정
        /// </summary>
        /// <param name="amount">설정할 명성도</param>
        public void SetReputation(int amount)
        {
            string prevGrade = GetCurrentGrade();
            currentReputation = Mathf.Max(0, amount);
            string newGrade = GetCurrentGrade();
            
            bool gradeChanged = prevGrade != newGrade;
            
            DebugLog($"명성도 설정: {currentReputation}", true);
            
            if (gradeChanged)
            {
                DebugLog($"등급 변경: {prevGrade} → {newGrade}", true);
            }
            
            UpdateUI();
            OnReputationChanged?.Invoke(currentReputation);
        }

        /// <summary>
        /// 현재 등급의 현지화된 이름을 반환
        /// </summary>
        public string GetCurrentGrade()
        {
            string gradeKey = GetCurrentGradeKey();
            return LocalizationSettings.StringDatabase.GetLocalizedString("Tier", gradeKey);
            /*for (int i = gradeThresholds.Length - 1; i >= 0; i--)
            {
                if (currentReputation >= gradeThresholds[i])
                {
                    return LocalizationSettings.StringDatabase.GetLocalizedString("Locales", gradeNames[i]);
                    //return gradeNames[i];
                }
            }
            return LocalizationSettings.StringDatabase.GetLocalizedString("Locales", gradeNames[0]);
            //return gradeNames[0]; // 기본값*/
        }

        private string GetCurrentGradeKey()
        {
            for (int i = gradeThresholds.Length - 1; i >= 0; i--)
            {
                if (currentReputation >= gradeThresholds[i])
                {
                    return gradeNames[i];
                }
            }
            return gradeNames[0]; // 기본값
        }

        /// <summary>
        /// 다음 등급까지 필요한 명성도 반환
        /// </summary>
        public int GetReputationToNextGrade()
        {
            for (int i = 0; i < gradeThresholds.Length; i++)
            {
                if (currentReputation < gradeThresholds[i])
                {
                    return gradeThresholds[i] - currentReputation;
                }
            }
            return 0; // 최고 등급
        }
        
        /// <summary>
        /// 특정 등급에 필요한 명성도 반환
        /// </summary>
        public int GetRequiredReputationForGrade(string gradeName)
        {
            for (int i = 0; i < gradeNames.Length; i++)
            {
                if (gradeNames[i] == gradeName)
                {
                    return gradeThresholds[i];
                }
            }
            return 0;
        }

        /// <summary>
        /// 다음 등급의 이름을 반환합니다.
        /// </summary>
        private string GetNextGradeName()
        {
            int currentGradeIndex = -1;
            for (int i = gradeThresholds.Length - 1; i >= 0; i--)
            {
                if (currentReputation >= gradeThresholds[i])
                {
                    currentGradeIndex = i;
                    break;
                }
            }

            if (currentGradeIndex < gradeNames.Length - 1)
            {
                return LocalizationSettings.StringDatabase.GetLocalizedString("Tier", gradeNames[currentGradeIndex + 1]);
            }
            return ""; // 최고 등급
        }

        /// <summary>
        /// 다음 등급에 필요한 명성도 점수를 반환합니다.
        /// </summary>
        private int GetNextGradeReputation()
        {
            for (int i = 0; i < gradeThresholds.Length; i++)
            {
                if (currentReputation < gradeThresholds[i])
                {
                    return gradeThresholds[i];
                }
            }
            return -1; // 최고 등급
        }

        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (reputationText != null)
            {
                // 명성도 변경이 없으면 캐시된 텍스트 재사용
                if (currentReputation != lastReputation)
                {
                    lastReputation = currentReputation;
                    lastFormattedReputation = FormatReputation(currentReputation);
                }

                string grade = GetCurrentGrade();
                currentGradeText.text = grade;
                reputationText.text = string.Format(textFormat, lastFormattedReputation, grade);
            }
        }

        /// <summary>
        /// 명성도를 k, m, 단위로 포맷팅
        /// </summary>
        private string FormatReputation(int amount)
        {
            if (amount >= 1_000_000_000) // 10억 이상
            {
                return $"{(amount / 1_000_000_000f):F1}b"; // 소수점
            }
            else if (amount >= 1_000_000) // 100만 이상
            {
                return $"{(amount / 1_000_000f):F1}m";
            }
            else if (amount >= 1_000) // 1000 이상
            {
                return $"{(amount / 1000f):F1}k";
            }
            else
            {
                return amount.ToString(); // 1000 미만은 그대로 표시
            }
        }

        /// <summary>
        /// 등급 변경 알림 UI를 표시합니다.
        /// </summary>
        private void ShowGradeChangeUI()
        {
            if (gradeChangePanel == null || gradeChangeText == null || gradeChangeCanvasGroup == null) return;

            gradeChangeSequence?.Kill();

            int currentGradeIndex = GetCurrentGradeIndex();
            string currentGradeName = GetCurrentGrade();

            string finalMessage;
            int nextGradeRep = GetNextGradeReputation();

            if (nextGradeRep > 0) // 최고 등급이 아닐 경우
            {
                string key = (currentGradeIndex >= hotelGradeIndexThreshold) ? "GradeUp_Hotel_Next" : "GradeUp_Lodging_Next";
                string nextGradeName = GetNextGradeName();
                int neededRep = GetReputationToNextGrade();
                finalMessage = LocalizationSettings.StringDatabase.GetLocalizedString("Tier", key, new object[] { currentGradeName, nextGradeName, neededRep });
            }
            else // 최고 등급일 경우
            {
                string key = (currentGradeIndex >= hotelGradeIndexThreshold) ? "GradeUp_Hotel_Max" : "GradeUp_Lodging_Max";
                finalMessage = LocalizationSettings.StringDatabase.GetLocalizedString("Tier", key, new object[] { currentGradeName });
            }
            gradeChangeText.text = finalMessage;

            gradeChangePanel.SetActive(true);
            RectTransform rectTransform = gradeChangePanel.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, animStartY);
            gradeChangeCanvasGroup.alpha = 0f;

            //currentGradeNameText.text = GetCurrentGrade();

            gradeChangeSequence = DOTween.Sequence();
            gradeChangeSequence
                // Fade In & Move Up
                .Append(gradeChangeCanvasGroup.DOFade(1f, animDuration / 2))
                .Join(rectTransform.DOAnchorPosY(animEndY, animDuration).SetEase(Ease.OutCubic))
                // Display
                .AppendInterval(animDisplayTime)
                // Fade Out
                .Append(gradeChangeCanvasGroup.DOFade(0f, animDuration / 2))
                .OnComplete(() =>
                {
                    gradeChangePanel.SetActive(false); // 애니메이션 완료 후 비활성화
                }).SetUpdate(true); // Time.timeScale에 영향받지 않도록 설정
        }

        /// <summary>
        /// 현재 명성도에 해당하는 등급의 인덱스를 반환합니다.
        /// </summary>
        /// <returns>등급 인덱스 (0부터 시작)</returns>
        private int GetCurrentGradeIndex()
        {
            for (int i = gradeThresholds.Length - 1; i >= 0; i--)
            {
                if (currentReputation >= gradeThresholds[i])
                {
                    return i;
                }
            }
            return 0; // 기본값
        }

        #region 디버그 메서드

        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
        }
        
        #endregion
    }
}