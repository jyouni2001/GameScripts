using UnityEngine;
using System.Collections.Generic;

public class QuestLogUI : MonoBehaviour
{
    public Transform questListContent; // 퀘스트 아이템이 추가될 Content 오브젝트
    public GameObject questLogItemPrefab; // 개별 퀘스트 UI 프리팹

    private Dictionary<ActiveQuest, GameObject> questItemObjects = new Dictionary<ActiveQuest, GameObject>();

    public void AddQuestToList(ActiveQuest quest)
    {
        GameObject itemGO = Instantiate(questLogItemPrefab, questListContent);
        QuestLogItem itemUI = itemGO.GetComponent<QuestLogItem>();
        itemUI.Setup(quest);

        questItemObjects.Add(quest, itemGO);
    }

    public void RemoveQuestFromList(ActiveQuest quest)
    {
        if (questItemObjects.TryGetValue(quest, out GameObject itemGO))
        {
            Destroy(itemGO);
            questItemObjects.Remove(quest);
        }
    }

    public void UpdateQuestStatus(ActiveQuest quest)
    {
        if (questItemObjects.TryGetValue(quest, out GameObject itemGO))
        {
            itemGO.GetComponent<QuestLogItem>().UpdateStatus();
        }
    }
}