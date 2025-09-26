using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using JY;

namespace JY
{
    /// <summary>
    /// 실시간 AI 수 표시 UI
    /// 기존 UI 디자인에 Text만 실시간으로 업데이트
    /// </summary>
    public class RealTimeAICounterUI : MonoBehaviour
    {
        [Header("UI 설정")]
        [Tooltip("AI 수를 표시할 TextMeshProUGUI 컴포넌트")]
        public TextMeshProUGUI aiCountText;
        
        [Tooltip("표시 형식 (예: \"실시간AI수.0m\")")]
        [SerializeField] private string displayFormat = "{0}.0m";
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        // 내부 변수
        private int lastAICount = -1;
        private AISpawner aiSpawner;
        
        void Start()
        {
            InitializeUI();
            SetupEventBasedUpdate();
        }
        
        // Update 메서드 제거 - 인원수 변경 시에만 업데이트
        
        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // TextMeshProUGUI 컴포넌트가 없으면 자동으로 찾기
            if (aiCountText == null)
            {
                // 1. 같은 오브젝트에서 찾기
                aiCountText = GetComponent<TextMeshProUGUI>();
                
                // 2. 자식 오브젝트에서 찾기
                if (aiCountText == null)
                {
                    aiCountText = GetComponentInChildren<TextMeshProUGUI>();
                }
                
                // 3. 부모 오브젝트에서 찾기
                if (aiCountText == null)
                {
                    aiCountText = GetComponentInParent<TextMeshProUGUI>();
                }
                
                // 4. 씬에서 "Str_Customer" 이름으로 찾기
                if (aiCountText == null)
                {
                    GameObject strCustomer = GameObject.Find("Str_Customer");
                    if (strCustomer != null)
                    {
                        aiCountText = strCustomer.GetComponent<TextMeshProUGUI>();
                        DebugLog($"Str_Customer 오브젝트에서 TextMeshProUGUI 찾음: {aiCountText != null}", true);
                    }
                }
            }
            
            if (aiCountText == null)
            {
                Debug.LogError("[RealTimeAICounterUI] TextMeshProUGUI 컴포넌트를 찾을 수 없습니다! Str_Customer 오브젝트에 TextMeshProUGUI가 있는지 확인해주세요.");
                return;
            }
            
            // 초기값 설정
            aiCountText.text = "0.0m";
            
            DebugLog($"실시간 AI 수 UI 초기화 완료 - 연결된 텍스트: {aiCountText.gameObject.name}", true);
        }
        
        /// <summary>
        /// 이벤트 기반 업데이트 설정
        /// </summary>
        private void SetupEventBasedUpdate()
        {
            // AISpawner 찾기
            aiSpawner = AISpawner.Instance;
            if (aiSpawner == null)
            {
                aiSpawner = FindFirstObjectByType<AISpawner>();
            }
            
            if (aiSpawner != null)
            {
                DebugLog("AISpawner 연결 완료 - 수동 업데이트 모드", true);
            }
            else
            {
                DebugLog("AISpawner를 찾을 수 없습니다.", true);
            }
            
            // 초기 AI 수 설정
            UpdateAICountDisplay();
        }
        
        /// <summary>
        /// AI 수 표시 업데이트
        /// </summary>
        private void UpdateAICountDisplay()
        {
            if (aiCountText == null) return;
            
            int currentAICount = GetCurrentAICount();
            
            // AI 수가 변경된 경우에만 UI 업데이트
            if (currentAICount != lastAICount)
            {
                lastAICount = currentAICount;
                string displayText = string.Format(displayFormat, currentAICount);
                aiCountText.text = displayText;
                
                DebugLog($"AI 수 업데이트: {displayText}", true);
            }
        }
        
        /// <summary>
        /// 현재 활성 AI 수 가져오기
        /// </summary>
        /// <returns>현재 활성 AI 수</returns>
        private int GetCurrentAICount()
        {
            if (aiSpawner != null)
            {
                return aiSpawner.GetActiveAICount();
            }
            
            // AISpawner가 null이면 다시 찾기
            aiSpawner = AISpawner.Instance;
            if (aiSpawner == null)
            {
                aiSpawner = FindFirstObjectByType<AISpawner>();
            }
            
            if (aiSpawner != null)
            {
                return aiSpawner.GetActiveAICount();
            }
            
            // AISpawner를 찾을 수 없으면 0 반환
            return 0;
        }
        
        /// <summary>
        /// 수동으로 AI 수 업데이트 (테스트용)
        /// </summary>
        public void ManualUpdate()
        {
            UpdateAICountDisplay();
            DebugLog("수동 업데이트 실행", true);
        }
        
        /// <summary>
        /// 표시 형식 변경
        /// </summary>
        /// <param name="newFormat">새로운 표시 형식</param>
        public void SetDisplayFormat(string newFormat)
        {
            displayFormat = newFormat;
            UpdateAICountDisplay(); // 즉시 업데이트
            DebugLog($"표시 형식 변경: {newFormat}", true);
        }
        
        /// <summary>
        /// AI 수 변경 시 호출되는 메서드 (외부에서 호출)
        /// </summary>
        public void OnAICountChanged()
        {
            UpdateAICountDisplay();
            DebugLog("AI 수 변경 감지 - UI 업데이트", true);
        }
        
        // 이벤트 핸들러 제거 - AISpawner에 이벤트가 없으므로 수동 호출 방식 사용
        
        /// <summary>
        /// 현재 AI 수 반환 (외부 접근용)
        /// </summary>
        /// <returns>현재 AI 수</returns>
        public int GetCurrentCount()
        {
            return GetCurrentAICount();
        }
        
        /// <summary>
        /// UI 상태 테스트 (디버그용)
        /// </summary>
        public void TestUIStatus()
        {
            DebugLog("=== UI 상태 테스트 ===", true);
            DebugLog($"TextMeshProUGUI 컴포넌트: {(aiCountText != null ? "존재" : "없음")}", true);
            DebugLog($"현재 AI 수: {GetCurrentAICount()}명", true);
            DebugLog($"표시 형식: {displayFormat}", true);
            DebugLog($"AISpawner 연결: {(aiSpawner != null ? "연결됨" : "연결 안됨")}", true);
            DebugLog($"현재 표시 텍스트: {aiCountText?.text}", true);
            DebugLog("======================", true);
        }
        
        #region 정리
        void OnDestroy()
        {
            // 참조 정리
            aiSpawner = null;
        }
        
        void OnDisable()
        {
            // 참조 정리
            aiSpawner = null;
        }
        #endregion
        
        #region 디버그 메서드
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        /// <param name="message">로그 메시지</param>
        /// <param name="isImportant">중요한 이벤트인지 여부</param>
        private void DebugLog(string message, bool isImportant = false)
        {
#if UNITY_EDITOR
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[RealTimeAICounterUI] {message}");
#endif
        }
        #endregion
    }
}
