using JY;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestLogItem : MonoBehaviour
{
    public TextMeshProUGUI questNameText;
    public TextMeshProUGUI questConditionText;
    public Button completeButton;

    private ActiveQuest associatedQuest;

    public void Setup(ActiveQuest quest)
    {
        associatedQuest = quest;
        questNameText.text = quest.data.questName;
        // QuestUIManager의 GetConditionString 재활용
        //questConditionText.text = QuestManager.Instance.questUIManager.GetConditionString(quest.data);

        completeButton.gameObject.SetActive(false); // 처음엔 완료 버튼 숨김
        completeButton.onClick.AddListener(OnCompleteButtonClicked);

        UpdateStatus();
    }

    public void UpdateStatus()
    {
        if (associatedQuest == null) return;

        if (associatedQuest.isCompleted)
        {
            questConditionText.text = "<b><color=green>완료 가능!</color></b>";
            questConditionText.fontStyle = FontStyles.Bold;
            completeButton.gameObject.SetActive(true);
        }
        else // 퀘스트가 진행 중일 때
        {
            string progressText = "";
            switch (associatedQuest.data.completionType)
            {
                case QuestCompletionType.BuildObject:
                    string objectName = PlacementSystem.Instance.database.GetObjectData(associatedQuest.data.completionTargetID).Name;
                    progressText = $"{objectName} 건설: {associatedQuest.currentAmount} / {associatedQuest.data.completionAmount}";
                    break;
                case QuestCompletionType.EarnMoney:
                    progressText = $"돈 벌기: {associatedQuest.currentAmount} / {associatedQuest.data.completionAmount} G";
                    break;
                case QuestCompletionType.ReachReputation:
                    // 평판은 현재 평판을 바로 보여주는 것이 직관적입니다.
                    int currentReputation = ReputationSystem.Instance.CurrentReputation;
                    progressText = $"평판 달성: {currentReputation} / {associatedQuest.data.completionAmount}";
                    break;
                case QuestCompletionType.Tutorial:
                    progressText = "튜토리얼을 진행하세요.";
                    break;
            }
            questConditionText.text = progressText;
        }
    }

    private void OnCompleteButtonClicked()
    {
        QuestManager.Instance.CompleteQuest(associatedQuest);
    }
}