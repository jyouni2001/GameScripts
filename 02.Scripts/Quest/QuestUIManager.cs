using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening; // DOTween 사용

public class QuestUIManager : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject questPanel; // 퀘스트 UI 패널 전체
    public TextMeshProUGUI questDialogueText;
    public TextMeshProUGUI questConditionText;
    public RawImage characterImage;

    private Vector2 initialPosition;
    private Vector2 startDragPosition;
    private float swipeThreshold = 150f; // 스와이프 판정 거리
    private float maxDragDistance = 200f;

    private void Start()
    {
        initialPosition = questPanel.GetComponent<RectTransform>().anchoredPosition;
        questPanel.SetActive(false);
    }

    // 퀘스트 UI를 화면에 표시
    public void ShowQuest(QuestData quest)
    {
        questDialogueText.text = quest.dialogue;
        questConditionText.text = GetConditionString(quest);
        characterImage.texture = quest.characterImage;

        questPanel.SetActive(true);
        RectTransform rect = questPanel.GetComponent<RectTransform>();

        // 화면 밖 왼쪽에서 시작
        rect.anchoredPosition = new Vector2(-rect.rect.width, initialPosition.y);

        // DOTween을 사용하여 슬라이드 인 애니메이션
        rect.DOAnchorPosX(initialPosition.x, 0.5f).SetEase(Ease.OutBack);
    }

    // 퀘스트 UI를 화면 밖으로 사라지게 함
    public void HideQuest(bool accepted, System.Action onHideAnimationComplete)
    {
        RectTransform rect = questPanel.GetComponent<RectTransform>();
        float endPosX = accepted ? rect.rect.width * 2 : -rect.rect.width * 2; // 수락이면 오른쪽, 거절이면 왼쪽

        rect.DOAnchorPosX(endPosX, 0.5f).SetEase(Ease.InBack).OnComplete(() =>
        {
            questPanel.SetActive(false);
            rect.anchoredPosition = initialPosition; // 다음 퀘스트를 위해 위치 초기화
            onHideAnimationComplete?.Invoke();
        });
    }

    // 완료 조건 텍스트 생성
    public string GetConditionString(QuestData quest)
    {
        switch (quest.completionType)
        {
            case QuestCompletionType.Tutorial:
                return $"조건 : 당겨서 수락하세요.";
            case QuestCompletionType.BuildObject:
                // ObjectDatabaseSO를 참조하여 오브젝트 이름을 가져옵니다.
                string objectName = PlacementSystem.Instance.database.GetObjectData(quest.completionTargetID).Name;
                return $"조건: {objectName} {quest.completionAmount}개 건설";
            case QuestCompletionType.EarnMoney:
                return $"조건: {quest.completionAmount}원 벌기";
            case QuestCompletionType.ReachReputation:
                return $"조건: 평판 {quest.completionAmount}점 달성";
            default:
                return "";
        }
    }

    public bool IsVisible()
    {
        return questPanel.activeSelf;
    }

    // --- 스와이프 처리 ---
    public void OnBeginDrag(PointerEventData eventData)
    {
        startDragPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 드래그 중 패널을 따라 움직이게 함
        float difference = eventData.position.x - startDragPosition.x;

        // 드래그 최대치 지정
        float clampedDifference = Mathf.Clamp(difference, -maxDragDistance, maxDragDistance);
        questPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialPosition.x + clampedDifference, initialPosition.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float swipeDistance = eventData.position.x - startDragPosition.x;

        if (swipeDistance > swipeThreshold) // 오른쪽 스와이프 (수락)
        {
            QuestManager.Instance.AcceptQuest();
        }
        else if (swipeDistance < -swipeThreshold) // 왼쪽 스와이프 (거절)
        {
            QuestManager.Instance.DeclineQuest();
        }
        else // 원래 위치로 복귀
        {
            questPanel.GetComponent<RectTransform>().DOAnchorPos(initialPosition, 0.2f);
        }
    }
}