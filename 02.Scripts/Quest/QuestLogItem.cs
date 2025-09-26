using JY;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
        questNameText.text = (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? quest.data.questName_en : quest.data.questName;

        //questNameText.text = quest.data.questName;
        // QuestUIManager의 GetConditionString 재활용
        //questConditionText.text = QuestManager.Instance.questUIManager.GetConditionString(quest.data);

        completeButton.gameObject.SetActive(false); // 처음엔 완료 버튼 숨김
        completeButton.onClick.AddListener(OnCompleteButtonClicked);

        Refresh();

        //UpdateStatus();
    }

    /// <summary>
    /// UI 텍스트를 현재 언어 설정에 맞게 새로고침하는 함수
    /// </summary>
    public void Refresh()
    {
        if (associatedQuest == null) return;

        // 퀘스트 이름 업데이트
        questNameText.text = (LocalizationSettings.SelectedLocale.Identifier.Code == "en")
            ? associatedQuest.data.questName_en
            : associatedQuest.data.questName;

        // 퀘스트 상태(진행도) 업데이트
        UpdateStatus();
    }

    public void UpdateStatus()
    {
        if (associatedQuest == null) return;

        if (associatedQuest.isCompleted)
        {
            questConditionText.text = (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? "<b><color=green>Completable!</color></b>" : "<b><color=green>완료 가능!</color></b>";
            questConditionText.fontStyle = FontStyles.Bold;
            completeButton.gameObject.SetActive(true);
        }
        else
        {
            string progressText = "";
            switch (associatedQuest.data.completionType)
            {
                case QuestCompletionType.BuildObject:
                    string objectName = PlacementSystem.Instance.database.GetObjectData(associatedQuest.data.completionTargetID).LocalizedName;
                    progressText = (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? $"Build {objectName}: {associatedQuest.currentAmount} / {associatedQuest.data.completionAmount}" : $"{objectName} 건설: {associatedQuest.currentAmount} / {associatedQuest.data.completionAmount}";
                    break;
                case QuestCompletionType.EarnMoney:
                    progressText = (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? $"Earn Money: {associatedQuest.currentAmount} / {associatedQuest.data.completionAmount} G" : $"돈 벌기: {associatedQuest.currentAmount} / {associatedQuest.data.completionAmount} G";
                    break;
                case QuestCompletionType.ReachReputation:
                    int currentReputation = ReputationSystem.Instance.CurrentReputation;
                    progressText = (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? $"Reach Reputation: {currentReputation} / {associatedQuest.data.completionAmount}" : $"평판 달성: {currentReputation} / {associatedQuest.data.completionAmount}";
                    break;
                case QuestCompletionType.Tutorial:
                    progressText = (LocalizationSettings.SelectedLocale.Identifier.Code == "en") ? "Proceed with the tutorial." : "튜토리얼을 진행하세요.";
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