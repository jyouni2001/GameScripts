using UnityEngine;

// 퀘스트 완료 조건 타입을 정의합니다.
public enum QuestCompletionType
{
    Tutorial,
    BuildObject, // 특정 오브젝트 건설
    EarnMoney,   // 특정 금액 벌기
    ReachReputation // 특정 평판 도달
    
}

// 퀘스트 보상 타입을 정의합니다.
public enum QuestRewardType
{
    Money,
    Reputation
}

[CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest")]
public class QuestData : ScriptableObject
{
    [Header("퀘스트 기본 정보")]
    public string questName; // 퀘스트 이름
    [TextArea(3, 10)]
    public string dialogue; // 퀘스트 대화 내용
    public Texture2D characterImage; // 퀘스트를 주는 인물 이미지

    [Header("퀘스트 발생 조건")]
    public int requiredDay; // 퀘스트가 발생하는 날짜
    public int requiredHour; // 퀘스트가 발생하는 시간
    public int requiredMin;

    [Header("퀘스트 완료 조건")]
    public QuestCompletionType completionType; // 완료 조건 타입
    public int completionTargetID; // (오브젝트 건설 시) 목표 오브젝트 ID
    public int completionAmount;   // 목표 수량 또는 금액, 평판 점수

    [Header("퀘스트 보상")]
    public QuestRewardType rewardType; // 보상 타입
    public int rewardAmount; // 보상 수량

    [Header("시간 제한")]
    public float timeLimitInMinutes; // 제한 시간 (게임 내 분 단위)
}