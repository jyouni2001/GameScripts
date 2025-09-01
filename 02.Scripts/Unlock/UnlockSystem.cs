using JY;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class UnlockableItem
{
    public string itemName; // 인스펙터에서 알아보기 쉽도록 이름 추가
    public int reputationThreshold; // 해금에 필요한 명성도
    public Button unlockableButton; // 해금할 버튼
}

public class UnlockSystem : MonoBehaviour
{
    [SerializeField] private ReputationSystem reputationSystem;

    [Header("해금 목록")]
    [Tooltip("명성도에 따라 해금될 버튼 목록")]
    [SerializeField] private List<UnlockableItem> unlockableItems;

    [SerializeField] private GameObject maskPrefab; // 잠금 이미지 프리팹
    // 생성된 마스크 인스턴스를 추적하기 위한 Dictionary
    private Dictionary<Button, GameObject> instantiatedMasks = new Dictionary<Button, GameObject>();

    private void Start()
    {
        if (reputationSystem == null)
        {
            reputationSystem = ReputationSystem.Instance;
        }

        if (reputationSystem == null)
        {
            Debug.LogError("ReputationSystem을 찾을 수 없습니다!");
            return;
        }

        // 게임 시작 시 버튼들을 모두 비활성화하고 초기 상태를 체크
        InitializeButtons();
        CheckUnlock(reputationSystem.CurrentReputation);

        // 명성도 변경 이벤트가 발생할 때마다 CheckUnlock 함수를 호출하도록 등록
        reputationSystem.OnReputationChanged += CheckUnlock;
    }

    private void OnDestroy()
    {
        // 오브젝트가 파괴될 때 이벤트 구독 해제 (메모리 누수 방지)
        if (reputationSystem != null)
        {
            reputationSystem.OnReputationChanged -= CheckUnlock;
        }
    } 

    /// <summary>
    /// 시작 시 모든 버튼을 비활성화합니다.
    /// </summary>
    private void InitializeButtons()
    {
        foreach (var item in unlockableItems)
        {
            if (item.unlockableButton != null)
            {
                // 버튼 상호작용 비활성화
                item.unlockableButton.interactable = false;

                // 마스크 프리팹을 버튼의 자식으로 생성
                GameObject maskInstance = Instantiate(maskPrefab, item.unlockableButton.transform);
                maskInstance.name = "LockMask"; // 오브젝트 이름 설정

                // 마스크 크기가 버튼을 완전히 덮도록 RectTransform 조정
                RectTransform maskRect = maskInstance.GetComponent<RectTransform>();
                if (maskRect != null)
                {
                    maskRect.anchorMin = Vector2.zero; // (0, 0)
                    maskRect.anchorMax = Vector2.one;  // (1, 1)
                    maskRect.offsetMin = Vector2.zero; // left, bottom
                    maskRect.offsetMax = Vector2.zero; // -right, -top
                    maskRect.localScale = Vector3.one;
                }

                // 생성된 마스크 인스턴스를 나중에 참조할 수 있도록 저장
                instantiatedMasks[item.unlockableButton] = maskInstance;
            }
        }
    }

    /// <summary>
    /// 현재 명성도를 기준으로 해금 조건을 확인하고 버튼을 활성화합니다.
    /// </summary>
    /// <param name="currentReputation">현재 명성도</param>
    private void CheckUnlock(int currentReputation)
    {
        List<UnlockableItem> itemsToUnlock = new List<UnlockableItem>();

        foreach (var item in unlockableItems)
        {
            if (item.unlockableButton != null && !item.unlockableButton.interactable && currentReputation >= item.reputationThreshold)
            {
                itemsToUnlock.Add(item);
            }
        }

        // 해금할 아이템들을 처리
        foreach (var item in itemsToUnlock)
        {
            // 버튼 상호작용 활성화
            item.unlockableButton.interactable = true;

            // 해당 버튼에 연결된 마스크 찾아서 파괴
            if (instantiatedMasks.TryGetValue(item.unlockableButton, out GameObject maskInstance))
            {
                Destroy(maskInstance);
                instantiatedMasks.Remove(item.unlockableButton); // Dictionary에서 제거
            }

            Debug.Log($"'{item.itemName}'이(가) 해금되었습니다! (필요 명성도: {item.reputationThreshold})");
        }
        
    }
}
