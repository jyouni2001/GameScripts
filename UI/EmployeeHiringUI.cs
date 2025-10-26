using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JY;

namespace JY
{
    /// <summary>
    /// 직원 고용 UI 시스템
    /// 플레이어가 직원을 고용할 수 있는 UI 인터페이스를 제공
    /// </summary>
    public class EmployeeHiringUI : MonoBehaviour
    {
        [Header("UI 패널")]
        [SerializeField] private GameObject hiringPanel;
        
        [Header("직원 목록")]
        [SerializeField] private Transform employeeListParent;
        [SerializeField] private GameObject employeeItemPrefab;
        
        [Header("직원 정보")]
        [SerializeField] private TextMeshProUGUI employeeCountText;
        [SerializeField] private TextMeshProUGUI hiringLimitText;
        
        [Header("확인 대화상자")]
        [SerializeField] private GameObject confirmationDialog;
        [SerializeField] private TextMeshProUGUI confirmationText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        
        [Header("알림")]
        [SerializeField] private GameObject notificationPanel;
        [SerializeField] private TextMeshProUGUI notificationText;
        [SerializeField] private float notificationDuration = 3f;
        
        [Header("디버그")]
        [SerializeField] private bool enableDebugLogs = true;
        
        // 시스템 참조
        private EmployeeHiringSystem hiringSystem;
        private PlayerWallet playerWallet;
        
        // UI 상태
        private EmployeeType selectedEmployeeType;
        private List<GameObject> employeeUIItems = new List<GameObject>();

        [SerializeField] private Button Btn_CloseUI;
        
        #region Unity Lifecycle
        
        private void Start()
        {
            InitializeUI();
            SetupEventListeners();
            RefreshUI();
            
            // 주기적으로 UI 업데이트 시작 (Kitchen/Counter 태그 변경 감지용)
            InvokeRepeating(nameof(RefreshUI), 1f, 1f);
        }
        
        private void OnDestroy()
        {
            RemoveEventListeners();
            CancelInvoke(nameof(RefreshUI));
        }
        
        #endregion
        
        #region 초기화
        
        private void InitializeUI()
        {
            // 시스템 참조 가져오기
            Btn_CloseUI.onClick.AddListener(CloseUI);
            hiringSystem = EmployeeHiringSystem.Instance;
            playerWallet = PlayerWallet.Instance;
            
            if (hiringSystem == null)
            {
                Debug.LogError("[EmployeeHiringUI] EmployeeHiringSystem을 찾을 수 없습니다!");
                return;
            }
            
            if (playerWallet == null)
            {
                Debug.LogError("[EmployeeHiringUI] PlayerWallet을 찾을 수 없습니다!");
                return;
            }
            
            // UI 초기 상태 설정
            if (hiringPanel != null)
            {
                hiringPanel.SetActive(true);
            }
            
            if (confirmationDialog != null)
            {
                confirmationDialog.SetActive(false);
            }
            
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
            
            // 직원 목록 UI 생성
            CreateEmployeeListUI();
            
            DebugLog("고용 UI 초기화 완료");
        }
        
        private void SetupEventListeners()
        {
            
            // 확인 대화상자 버튼
            if (confirmButton != null)
            {
                confirmButton.onClick.AddListener(ConfirmHiring);
            }
            
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(CancelHiring);
            }
            
            // 시스템 이벤트 구독
            if (playerWallet != null)
            {
                playerWallet.OnMoneyChanged += OnMoneyChanged;
            }
            
            EmployeeHiringSystem.OnEmployeeHired += OnEmployeeHired;
            EmployeeHiringSystem.OnEmployeeFired += OnEmployeeFired;
        }
        
        private void RemoveEventListeners()
        {
            
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(ConfirmHiring);
            }
            
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelHiring);
            }
            
            if (playerWallet != null)
            {
                playerWallet.OnMoneyChanged -= OnMoneyChanged;
            }
            
            EmployeeHiringSystem.OnEmployeeHired -= OnEmployeeHired;
            EmployeeHiringSystem.OnEmployeeFired -= OnEmployeeFired;
        }
        
        #endregion
        
        #region UI 생성
        
        private void CreateEmployeeListUI()
        {
            if (hiringSystem == null || employeeListParent == null || employeeItemPrefab == null)
            {
                return;
            }
            
            // 기존 UI 아이템 제거
            ClearEmployeeListUI();
            
            // 고용 가능한 직원 타입들 가져오기
            var employeeTypes = hiringSystem.GetAvailableEmployeeTypes();
            
            for (int i = 0; i < employeeTypes.Count; i++)
            {
                CreateEmployeeUIItem(employeeTypes[i], i);
            }
            
            DebugLog($"직원 목록 UI 생성 완료: {employeeTypes.Count}개");
        }
        
        private void CreateEmployeeUIItem(EmployeeType employeeType, int index)
        {
            GameObject uiItem = Instantiate(employeeItemPrefab, employeeListParent);
            employeeUIItems.Add(uiItem);
            
            // UI 요소들 설정
            SetupEmployeeUIItem(uiItem, employeeType, index);
        }
        
        private void SetupEmployeeUIItem(GameObject uiItem, EmployeeType employeeType, int index)
        {
            // 이름 설정
            var nameText = uiItem.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = employeeType.typeName;
            }
            
            // 설명 설정
            var descriptionText = uiItem.transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
            if (descriptionText != null)
            {
                descriptionText.text = employeeType.description;
            }
            
            // 비용 설정
            var costText = uiItem.transform.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            if (costText != null)
            {
                costText.text = $"고용비: {employeeType.hiringCost:N0}골드";
            }
            
            // 일급 설정
            var wageText = uiItem.transform.Find("WageText")?.GetComponent<TextMeshProUGUI>();
            if (wageText != null)
            {
                wageText.text = $"일급: {employeeType.dailyWage:N0}골드";
            }
            
            // 근무시간 설정
            var workTimeText = uiItem.transform.Find("WorkTimeText")?.GetComponent<TextMeshProUGUI>();
            if (workTimeText != null)
            {
                workTimeText.text = $"근무: {employeeType.workStartHour}시 - {employeeType.workEndHour}시";
            }
            
            // 아이콘 설정
            var iconImage = uiItem.transform.Find("IconImage")?.GetComponent<Image>();
            if (iconImage != null && employeeType.iconSprite != null)
            {
                iconImage.sprite = employeeType.iconSprite;
            }
            
            // 고용 버튼 설정
            var hireButton = uiItem.transform.Find("HireButton")?.GetComponent<Button>();
            if (hireButton != null)
            {
                hireButton.onClick.AddListener(() => RequestHiring(employeeType));
                
                // 버튼 상태 업데이트
                UpdateHireButtonState(hireButton, employeeType);
            }
        }
        
        private void ClearEmployeeListUI()
        {
            foreach (var item in employeeUIItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            employeeUIItems.Clear();
        }

        #endregion

        #region UI 이벤트

        public void CloseUI()
        {
            this.gameObject.SetActive(false);
        }

        private void RequestHiring(EmployeeType employeeType)
        {
            selectedEmployeeType = employeeType;
            
            if (confirmationDialog != null)
            {
                confirmationDialog.SetActive(true);
                
                if (confirmationText != null)
                {
                    confirmationText.text = $"{employeeType.typeName}을(를) {employeeType.hiringCost:N0}골드에 고용하시겠습니까?\n\n" +
                                          $"설명: {employeeType.description}\n" +
                                          $"일급: {employeeType.dailyWage:N0}골드\n" +
                                          $"근무시간: {employeeType.workStartHour}시 - {employeeType.workEndHour}시";
                }
            }
            
            DebugLog($"{employeeType.typeName} 고용 확인 요청");
        }
        
        private void ConfirmHiring()
        {
            if (selectedEmployeeType != null && hiringSystem != null)
            {
                DebugLog($"UI에서 고용 시도: {selectedEmployeeType.typeName}");
                DebugLog($"workPositionTag: '{selectedEmployeeType.workPositionTag}'");
                
                bool success = hiringSystem.HireEmployee(selectedEmployeeType);
                
                DebugLog($"고용 결과: {success}");
                
                if (success)
                {
                    ShowNotification($"{selectedEmployeeType.typeName}이(가) 고용되었습니다!", Color.green);
                }
                else
                {
                    ShowNotification("고용에 실패했습니다. 골드가 부족하거나 오류가 발생했습니다.", Color.red);
                }
            }
            
            CancelHiring();
        }
        
        private void CancelHiring()
        {
            selectedEmployeeType = null;
            
            if (confirmationDialog != null)
            {
                confirmationDialog.SetActive(false);
            }
        }
        
        #endregion
        
        #region UI 업데이트
        
        private void RefreshUI()
        {
            UpdatePlayerInfo();
            UpdateEmployeeButtons();
        }
        
        private void UpdatePlayerInfo()
        {
            if (hiringSystem != null && employeeCountText != null)
            {
                employeeCountText.text = $"고용된 직원: {hiringSystem.GetEmployeeCount()}명";
            }
            
            // 고용 제한 정보 표시
            if (hiringSystem != null && hiringLimitText != null)
            {
                hiringLimitText.text = hiringSystem.GetHiringStatusInfo();
            }
        }
        
        private void UpdateEmployeeButtons()
        {
            if (hiringSystem == null || playerWallet == null) return;
            
            var employeeTypes = hiringSystem.GetAvailableEmployeeTypes();
            
            for (int i = 0; i < employeeUIItems.Count && i < employeeTypes.Count; i++)
            {
                var hireButton = employeeUIItems[i].transform.Find("HireButton")?.GetComponent<Button>();
                if (hireButton != null)
                {
                    UpdateHireButtonState(hireButton, employeeTypes[i]);
                }
            }
        }
        
        private void UpdateHireButtonState(Button hireButton, EmployeeType employeeType)
        {
            if (hireButton == null || playerWallet == null || hiringSystem == null) return;
            
            bool canAfford = playerWallet.CanAfford(employeeType.hiringCost);
            bool hasPrefab = employeeType.employeePrefab != null;
            bool canHireAtPosition = CanHireEmployeeAtPosition(employeeType);
            
            // 주방 직원인 경우 추가 디버그 로그
            if (employeeType.workPositionTag == "WorkPosition_Kitchen")
            {
                Debug.Log($"====================================");
                Debug.Log($"[UI 업데이트] 주방 직원 버튼 상태 확인");
                Debug.Log($"[UI 업데이트] 골드 충분: {canAfford}");
                Debug.Log($"[UI 업데이트] 프리팹 있음: {hasPrefab}");
                Debug.Log($"[UI 업데이트] 고용 가능: {canHireAtPosition}");
                Debug.Log($"====================================");
            }
            
            // 디버그 로그 추가
            DebugLog($"UI 버튼 상태 확인 - {employeeType.typeName}: 골드={canAfford}, 프리팹={hasPrefab}, 위치={canHireAtPosition}");
            
            hireButton.interactable = canAfford && hasPrefab && canHireAtPosition;
            
            // 버튼 텍스트 업데이트
            var buttonText = hireButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                if (!hasPrefab)
                {
                    buttonText.text = "프리팹 없음";
                }
                else if (!canAfford)
                {
                    buttonText.text = "골드 부족";
                }
                else if (!canHireAtPosition)
                {
                    buttonText.text = "고용 불가";
                }
                else
                {
                    buttonText.text = "고용하기";
                }
            }
        }
        
        /// <summary>
        /// 특정 위치에서 직원을 고용할 수 있는지 확인 (UI용)
        /// </summary>
        private bool CanHireEmployeeAtPosition(EmployeeType employeeType)
        {
            // EmployeeHiringSystem의 실제 조건 체크 메서드 사용
            return hiringSystem.CanHireEmployeeAtPosition(employeeType);
        }
        
        #endregion
        
        #region 이벤트 핸들러
        
        private void OnMoneyChanged(int newAmount)
        {
            UpdateEmployeeButtons();
        }
        
        private void OnEmployeeHired(AIEmployee employee)
        {
            RefreshUI();
            DebugLog($"UI 업데이트: {employee.employeeName} 고용됨");
        }
        
        private void OnEmployeeFired(AIEmployee employee)
        {
            RefreshUI();
            DebugLog($"UI 업데이트: {employee.employeeName} 해고됨");
        }
        
        #endregion
        
        #region 알림 시스템
        
        private void ShowNotification(string message, Color color)
        {
            if (notificationPanel == null || notificationText == null) return;
            
            notificationText.text = message;
            notificationText.color = color;
            notificationPanel.SetActive(true);
            
            // 일정 시간 후 알림 숨기기
            Invoke(nameof(HideNotification), notificationDuration);
            
            DebugLog($"알림 표시: {message}");
        }
        
        private void HideNotification()
        {
            if (notificationPanel != null)
            {
                notificationPanel.SetActive(false);
            }
        }
        
        #endregion
        
        #region 유틸리티
        
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[EmployeeHiringUI] {message}");
            }
        }
        
        #endregion
        
        #region 에디터 전용
        
        #if UNITY_EDITOR
        [ContextMenu("테스트 - UI 새로고침")]
        private void TestRefreshUI()
        {
            if (Application.isPlaying)
            {
                RefreshUI();
            }
        }
        
        [ContextMenu("테스트 - 직원 목록 재생성")]
        private void TestRecreateEmployeeList()
        {
            if (Application.isPlaying)
            {
                CreateEmployeeListUI();
            }
        }
        #endif
        
        #endregion
    }
}
