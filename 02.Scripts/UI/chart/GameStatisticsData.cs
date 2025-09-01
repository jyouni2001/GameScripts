using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DailyStatistics
{
    public int day;
    public int spawnCount;
    public int goldEarned;
    public int reputationGained;
    public DateTime date;

    public DailyStatistics(int day, int spawnCount, int goldEarned, int reputationGained)
    {
        this.day = day;
        this.spawnCount = spawnCount;
        this.goldEarned = goldEarned;
        this.reputationGained = reputationGained;
        this.date = DateTime.Now;
    }
}

public class GameStatisticsData : MonoBehaviour
{
    public static GameStatisticsData Instance { get; private set; }

    [Header("Statistics Data")]
    [SerializeField] private List<DailyStatistics> dailyStats = new List<DailyStatistics>();
    
    [Header("Current Day Stats")]
    [SerializeField] private int currentDaySpawnCount = 0;
    [SerializeField] private int currentDayGoldEarned = 0;
    [SerializeField] private int currentDayReputationGained = 0;

    public List<DailyStatistics> DailyStats => dailyStats;
    public int CurrentDaySpawnCount => currentDaySpawnCount;
    public int CurrentDayGoldEarned => currentDayGoldEarned;
    public int CurrentDayReputationGained => currentDayReputationGained;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // TimeSystem의 날짜 변경 이벤트 구독
        if (TimeSystem.Instance != null)
        {
            // TimeSystem에 날짜 변경 이벤트가 있다면 구독
            // 실제 구현에서는 TimeSystem의 이벤트를 구독해야 함
        }
    }

    /// <summary>
    /// 현재 날짜의 스폰 인원수 증가
    /// </summary>
    public void AddSpawnCount(int count = 1)
    {
        currentDaySpawnCount += count;
        Debug.Log($"스폰 인원수 증가: {count}, 총: {currentDaySpawnCount}");
    }

    /// <summary>
    /// 현재 날짜의 획득 골드 증가
    /// </summary>
    public void AddGoldEarned(int amount)
    {
        currentDayGoldEarned += amount;
        Debug.Log($"획득 골드 증가: {amount}, 총: {currentDayGoldEarned}");
    }

    /// <summary>
    /// 현재 날짜의 명성도 증가
    /// </summary>
    public void AddReputationGained(int amount)
    {
        currentDayReputationGained += amount;
        Debug.Log($"명성도 증가: {amount}, 총: {currentDayReputationGained}");
    }

    /// <summary>
    /// 새로운 날이 시작될 때 호출 (TimeSystem에서 호출)
    /// </summary>
    public void OnNewDay(int dayNumber)
    {
        // 이전 날의 통계를 저장
        if (currentDaySpawnCount > 0 || currentDayGoldEarned > 0 || currentDayReputationGained > 0)
        {
            DailyStatistics yesterdayStats = new DailyStatistics(
                dayNumber - 1,
                currentDaySpawnCount,
                currentDayGoldEarned,
                currentDayReputationGained
            );
            
            dailyStats.Add(yesterdayStats);
            Debug.Log($"새로운 날 시작: Day {dayNumber}, 이전 날 통계 저장 완료");
        }

        // 현재 날 통계 초기화
        currentDaySpawnCount = 0;
        currentDayGoldEarned = 0;
        currentDayReputationGained = 0;
    }

    /// <summary>
    /// 특정 날짜 범위의 통계 데이터 반환
    /// </summary>
    public List<DailyStatistics> GetStatsForRange(int startDay, int endDay)
    {
        return dailyStats.FindAll(stat => stat.day >= startDay && stat.day <= endDay);
    }

    /// <summary>
    /// 최근 N일간의 통계 데이터 반환
    /// </summary>
    public List<DailyStatistics> GetRecentStats(int days)
    {
        if (dailyStats.Count == 0) return new List<DailyStatistics>();
        
        int startIndex = Mathf.Max(0, dailyStats.Count - days);
        return dailyStats.GetRange(startIndex, dailyStats.Count - startIndex);
    }

    /// <summary>
    /// 모든 통계 데이터 초기화
    /// </summary>
    public void ClearAllStats()
    {
        dailyStats.Clear();
        currentDaySpawnCount = 0;
        currentDayGoldEarned = 0;
        currentDayReputationGained = 0;
        Debug.Log("모든 통계 데이터가 초기화되었습니다.");
    }

    /// <summary>
    /// 테스트용 더미 데이터 생성
    /// </summary>
    public void GenerateTestData()
    {
        ClearAllStats();
        
        for (int i = 1; i <= 30; i++)
        {
            DailyStatistics testStat = new DailyStatistics(
                i,
                UnityEngine.Random.Range(5, 25),
                UnityEngine.Random.Range(100, 1000),
                UnityEngine.Random.Range(1, 10)
            );
            dailyStats.Add(testStat);
        }
        
        Debug.Log("테스트 데이터가 생성되었습니다.");
    }
}
