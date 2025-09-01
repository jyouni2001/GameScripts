using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LodgingSimulator.AI;
using LodgingSimulator.Economy;

namespace LodgingSimulator.UI
{
    /// <summary>
    /// 집사 AI 구매를 위한 UI 클래스
    /// </summary>
    public class ButlerPurchaseUI : MonoBehaviour
    {
        [Header("UI 요소")]
        [Tooltip("집사 구매 버튼")]
        public Button purchaseButton;
        
        [Tooltip("집사 수 표시 텍스트")]
        public TextMeshProUGUI butlerCountText;
        
        [Tooltip("구매 비용 표시 텍스트")]
        public TextMeshProUGUI costText;
        
        [Tooltip("최대 집사 수 표시 텍스트")]
        public TextMeshProUGUI maxButlerText;
        
        [Tooltip("구매 불가 메시지")]
        public GameObject insufficientFundsMessage;
        
        [Tooltip("최대 집사 수 도달 메시지")]
        public GameObject maxButlerMessage;
        
        [Header("설정")]
        [Tooltip("메시지 표시 시간")]
        public float messageDisplayTime = 3f;
        
        // 내부 변수
        private PlayerWallet playerWallet;
        private ButlerManager butlerManager;
        private float messageTimer = 0f;
        
        /// <summary>
        /// 초기화
        /// </summary>
        private void Start()
        {
            // 컴포넌트 참조 가져오기
            playerWallet = FindObjectOfType<PlayerWallet>();
            butlerManager = ButlerManager.Instance;
            
            // 버튼 이벤트 연결
            if (purchaseButton != null)
            {
                purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
            }
            
            // 초기 UI 업데이트
            UpdateUI();
        }
        
        /// <summary>
        /// 매 프레임 업데이트
        /// </summary>
        private void Update()
        {
            // UI 업데이트
            UpdateUI();
            
            // 메시지 타이머 처리
            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f)
                {
                    HideAllMessages();
                }
            }
        }
        
        /// <summary>
        /// 구매 버튼 클릭 처리
        /// </summary>
        private void OnPurchaseButtonClicked()
        {
            if (butlerManager == null)
            {
                Debug.LogError("ButlerManager를 찾을 수 없습니다!");
                return;
            }
            
            // 집사 AI 구매 시도
            bool success = butlerManager.PurchaseButler();
            
            if (success)
            {
                // 구매 성공 시 UI 업데이트
                UpdateUI();
                Debug.Log("집사 AI 구매가 완료되었습니다.");
            }
            else
            {
                // 구매 실패 시 적절한 메시지 표시
                if (butlerManager.TotalButlerCount >= butlerManager.Settings.maxButlerCount)
                {
                    ShowMessage(maxButlerMessage);
                }
                else
                {
                    ShowMessage(insufficientFundsMessage);
                }
            }
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (butlerManager == null) return;
            
            // 집사 수 표시
            if (butlerCountText != null)
            {
                butlerCountText.text = $"현재 집사 수: {butlerManager.TotalButlerCount}";
            }
            
            // 최대 집사 수 표시
            if (maxButlerText != null)
            {
                maxButlerText.text = $"최대 집사 수: {butlerManager.Settings.maxButlerCount}";
            }
            
            // 구매 비용 표시
            if (costText != null)
            {
                costText.text = $"구매 비용: {butlerManager.Settings.butlerPurchaseCost}원";
            }
            
            // 구매 버튼 활성화/비활성화
            if (purchaseButton != null)
            {
                bool canPurchase = CanPurchaseButler();
                purchaseButton.interactable = canPurchase;
                
                // 버튼 색상 변경
                ColorBlock colors = purchaseButton.colors;
                if (canPurchase)
                {
                    colors.normalColor = Color.white;
                    colors.selectedColor = Color.white;
                }
                else
                {
                    colors.normalColor = Color.gray;
                    colors.selectedColor = Color.gray;
                }
                purchaseButton.colors = colors;
            }
        }
        
        /// <summary>
        /// 집사 AI 구매 가능 여부 확인
        /// </summary>
        private bool CanPurchaseButler()
        {
            if (butlerManager == null || playerWallet == null)
                return false;
            
            // 최대 집사 수 확인
            if (butlerManager.TotalButlerCount >= butlerManager.Settings.maxButlerCount)
                return false;
            
            // 충분한 돈이 있는지 확인
            if (playerWallet.GetMoney() < butlerManager.Settings.butlerPurchaseCost)
                return false;
            
            return true;
        }
        
        /// <summary>
        /// 메시지 표시
        /// </summary>
        private void ShowMessage(GameObject messageObject)
        {
            if (messageObject != null)
            {
                messageObject.SetActive(true);
                messageTimer = messageDisplayTime;
            }
        }
        
        /// <summary>
        /// 모든 메시지 숨기기
        /// </summary>
        private void HideAllMessages()
        {
            if (insufficientFundsMessage != null)
                insufficientFundsMessage.SetActive(false);
            
            if (maxButlerMessage != null)
                maxButlerMessage.SetActive(false);
        }
        
        /// <summary>
        /// 집사 AI 정보 표시 (디버그용)
        /// </summary>
        private void OnGUI()
        {
            if (!Application.isPlaying || butlerManager == null) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 200));
            GUILayout.Label("=== 집사 AI 상세 정보 ===");
            
            var butlerInfoList = butlerManager.GetAllButlerInfo();
            foreach (var info in butlerInfoList)
            {
                GUILayout.Label($"집사: {info.butlerName}");
                GUILayout.Label($"상태: {info.currentState}");
                GUILayout.Label($"작업: {info.currentTask}");
                GUILayout.Label("---");
            }
            
            GUILayout.EndArea();
        }
    }
}
