using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하루 통계 시스템의 데이터 구조를 정의하는 클래스
/// 일차별 데이터와 일일 통계를 관리합니다.
/// </summary>
[System.Serializable]
public class DailyData
{
    [Header("날짜 정보")]
    [Tooltip("일차")]
    public int day;
    
    [Header("하루 수익 데이터")]
    [Tooltip("하루 동안 획득한 명성도")]
    public int reputationGained;
    
    [Tooltip("하루 동안 획득한 골드")]
    public int goldEarned;
    
    [Tooltip("하루 총 방문객 수")]
    public int totalVisitors;
    
    [Header("시작/종료 값")]
    [Tooltip("하루 시작 시 명성도")]
    public int startingReputation;
    
    [Tooltip("하루 시작 시 골드")]
    public int startingGold;
    
    [Tooltip("하루 종료 시 명성도")]
    public int endingReputation;
    
    [Tooltip("하루 종료 시 골드")]
    public int endingGold;
    
    [Header("메타데이터")]
    [Tooltip("데이터 생성 시간")]
    public string createdTime;
    
    [Tooltip("마지막 업데이트 시간")]
    public string lastUpdatedTime;
    
    /// <summary>
    /// 일차별 데이터 생성자
    /// </summary>
    /// <param name="day">일차</param>
    /// <param name="reputationGained">획득한 명성도</param>
    /// <param name="goldEarned">획득한 골드</param>
    /// <param name="totalVisitors">총 방문객 수</param>
    /// <param name="startingReputation">시작 명성도</param>
    /// <param name="startingGold">시작 골드</param>
    /// <param name="endingReputation">종료 명성도</param>
    /// <param name="endingGold">종료 골드</param>
    public DailyData(int day, int reputationGained, int goldEarned, int totalVisitors, 
                     int startingReputation, int startingGold, int endingReputation, int endingGold)
    {
        this.day = day;
        this.reputationGained = reputationGained;
        this.goldEarned = goldEarned;
        this.totalVisitors = totalVisitors;
        this.startingReputation = startingReputation;
        this.startingGold = startingGold;
        this.endingReputation = endingReputation;
        this.endingGold = endingGold;
        this.createdTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        this.lastUpdatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 기본 생성자 (JSON 직렬화용)
    /// </summary>
    public DailyData()
    {
        day = 1;
        reputationGained = 0;
        goldEarned = 0;
        totalVisitors = 0;
        startingReputation = 0;
        startingGold = 0;
        endingReputation = 0;
        endingGold = 0;
        createdTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        lastUpdatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 데이터를 문자열로 반환 (디버그용)
    /// </summary>
    public override string ToString()
    {
        return $"Day {day}: Rep+{reputationGained}, Gold+{goldEarned}, Visitors={totalVisitors}";
    }
}

/// <summary>
/// 일일 통계 데이터를 관리하는 클래스
/// 24시간의 시간별 데이터와 하루 총계를 포함합니다.
/// </summary>
[System.Serializable]
public class DailyStatistics
{
    [Header("날짜 정보")]
    [Tooltip("일차")]
    public int day;
    
    [Header("일차별 데이터")]
    [Tooltip("일차별 데이터 리스트")]
    public List<DailyData> dailyData;
    
    [Header("하루 총계")]
    [Tooltip("하루 총 명성도 증가량")]
    public int totalReputationGained;
    
    [Tooltip("하루 총 골드 획득량")]
    public int totalGoldEarned;
    
    [Tooltip("하루 총 방문객 수")]
    public int totalVisitors;
    
    [Header("시작 값")]
    [Tooltip("하루 시작 시 명성도")]
    public int startingReputation;
    
    [Tooltip("하루 시작 시 골드")]
    public int startingGold;
    
    [Header("메타데이터")]
    [Tooltip("데이터 생성 시간")]
    public string createdTime;
    
    [Tooltip("마지막 업데이트 시간")]
    public string lastUpdatedTime;
    
    /// <summary>
    /// 일일 통계 생성자
    /// </summary>
    /// <param name="day">일차</param>
    public DailyStatistics(int day)
    {
        this.day = day;
        dailyData = new List<DailyData>();
        totalReputationGained = 0;
        totalGoldEarned = 0;
        totalVisitors = 0;
        startingReputation = 0;
        startingGold = 0;
        createdTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        lastUpdatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 기본 생성자 (JSON 직렬화용)
    /// </summary>
    public DailyStatistics()
    {
        day = 1;
        dailyData = new List<DailyData>();
        totalReputationGained = 0;
        totalGoldEarned = 0;
        totalVisitors = 0;
        startingReputation = 0;
        startingGold = 0;
        createdTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        lastUpdatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 특정 일의 데이터를 추가하거나 업데이트
    /// </summary>
    /// <param name="day">일차</param>
    /// <param name="reputationGained">획득한 명성도</param>
    /// <param name="goldEarned">획득한 골드</param>
    /// <param name="totalVisitors">총 방문객 수</param>
    /// <param name="startingReputation">시작 명성도</param>
    /// <param name="startingGold">시작 골드</param>
    /// <param name="endingReputation">종료 명성도</param>
    /// <param name="endingGold">종료 골드</param>
    public void UpdateDailyData(int day, int reputationGained, int goldEarned, int totalVisitors, 
                               int startingReputation, int startingGold, int endingReputation, int endingGold)
    {
        // 기존 데이터가 있으면 업데이트, 없으면 새로 추가
        var existingData = dailyData.Find(d => d.day == day);
        if (existingData != null)
        {
            existingData.reputationGained = reputationGained;
            existingData.goldEarned = goldEarned;
            existingData.totalVisitors = totalVisitors;
            existingData.startingReputation = startingReputation;
            existingData.startingGold = startingGold;
            existingData.endingReputation = endingReputation;
            existingData.endingGold = endingGold;
            existingData.lastUpdatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        else
        {
            dailyData.Add(new DailyData(day, reputationGained, goldEarned, totalVisitors, 
                                      startingReputation, startingGold, endingReputation, endingGold));
        }
        
        // 일차순으로 정렬
        dailyData.Sort((a, b) => a.day.CompareTo(b.day));
        
        // 총계 업데이트
        UpdateTotals();
        
        // 마지막 업데이트 시간 갱신
        lastUpdatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 총계 데이터를 업데이트
    /// </summary>
    private void UpdateTotals()
    {
        if (dailyData.Count == 0) return;
        
        // 현재 일의 데이터를 사용하여 총계 계산
        var currentDayData = dailyData.Find(d => d.day == this.day);
        if (currentDayData != null)
        {
            totalReputationGained = currentDayData.reputationGained;
            totalGoldEarned = currentDayData.goldEarned;
            totalVisitors = currentDayData.totalVisitors;
        }
    }
    
    /// <summary>
    /// 특정 일의 데이터를 가져옴
    /// </summary>
    /// <param name="day">일차</param>
    /// <returns>해당 일의 데이터, 없으면 null</returns>
    public DailyData GetDailyData(int day)
    {
        return dailyData.Find(d => d.day == day);
    }
    
    /// <summary>
    /// 모든 일차 데이터가 있는지 확인
    /// </summary>
    /// <returns>데이터가 있으면 true</returns>
    public bool HasData()
    {
        return dailyData.Count > 0;
    }
    
    /// <summary>
    /// 데이터를 문자열로 반환 (디버그용)
    /// </summary>
    public override string ToString()
    {
        return $"Day {day}: Rep+{totalReputationGained}, Gold+{totalGoldEarned}, Visitors={totalVisitors}";
    }
}

/// <summary>
/// 여러 일의 통계 데이터를 관리하는 컨테이너 클래스
/// </summary>
[System.Serializable]
public class StatisticsContainer
{
    [Header("통계 데이터")]
    [Tooltip("일별 통계 데이터 리스트")]
    public List<DailyStatistics> dailyStatistics;
    
    [Header("설정")]
    [Tooltip("최대 보관 일수")]
    public int maxDays = 7;
    
    [Tooltip("마지막 저장 시간")]
    public string lastSavedTime;
    
    /// <summary>
    /// 통계 컨테이너 생성자
    /// </summary>
    public StatisticsContainer()
    {
        dailyStatistics = new List<DailyStatistics>();
        maxDays = 7;
        lastSavedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>
    /// 특정 일의 통계를 추가하거나 업데이트
    /// </summary>
    /// <param name="day">일차</param>
    /// <returns>해당 일의 통계 객체</returns>
    public DailyStatistics GetOrCreateDailyStatistics(int day)
    {
        var existing = dailyStatistics.Find(d => d.day == day);
        if (existing != null)
        {
            return existing;
        }
        
        var newStats = new DailyStatistics(day);
        dailyStatistics.Add(newStats);
        
        // 오래된 데이터 정리
        CleanupOldData();
        
        return newStats;
    }
    
    /// <summary>
    /// 오래된 데이터를 정리 (maxDays 초과 시)
    /// </summary>
    private void CleanupOldData()
    {
        if (dailyStatistics.Count > maxDays)
        {
            // 일차순으로 정렬
            dailyStatistics.Sort((a, b) => a.day.CompareTo(b.day));
            
            // 오래된 데이터 제거
            int removeCount = dailyStatistics.Count - maxDays;
            dailyStatistics.RemoveRange(0, removeCount);
        }
    }
    
    /// <summary>
    /// 특정 일의 통계를 가져옴
    /// </summary>
    /// <param name="day">일차</param>
    /// <returns>해당 일의 통계, 없으면 null</returns>
    public DailyStatistics GetDailyStatistics(int day)
    {
        return dailyStatistics.Find(d => d.day == day);
    }
    
    /// <summary>
    /// 가장 최근 통계를 가져옴
    /// </summary>
    /// <returns>가장 최근 일의 통계, 없으면 null</returns>
    public DailyStatistics GetLatestStatistics()
    {
        if (dailyStatistics.Count == 0) return null;
        
        dailyStatistics.Sort((a, b) => b.day.CompareTo(a.day));
        return dailyStatistics[0];
    }
    
    /// <summary>
    /// 저장 시간을 업데이트
    /// </summary>
    public void UpdateSaveTime()
    {
        lastSavedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}