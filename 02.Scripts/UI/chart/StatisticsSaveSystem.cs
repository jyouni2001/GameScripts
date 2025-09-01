using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// 통계 데이터를 저장하고 로드하는 시스템
/// </summary>
public class StatisticsSaveSystem : MonoBehaviour
{
    [Header("Save Settings")]
    [SerializeField] private string statisticsFileName = "statistics.json";
    [SerializeField] private bool autoSave = true;
    [SerializeField] private float autoSaveInterval = 300f; // 5분
    
    private string statisticsFilePath;
    private float lastSaveTime;
    private bool isInitialized = false;

    private void Awake()
    {
        InitializeSaveSystem();
    }

    private void Start()
    {
        isInitialized = true;
        LoadStatisticsData();
    }

    private void Update()
    {
        if (autoSave && isInitialized)
        {
            lastSaveTime += Time.deltaTime;
            if (lastSaveTime >= autoSaveInterval)
            {
                SaveStatisticsData();
                lastSaveTime = 0f;
            }
        }
    }

    private void InitializeSaveSystem()
    {
        // 저장 파일 경로 설정
        statisticsFilePath = Path.Combine(Application.persistentDataPath, statisticsFileName);
        Debug.Log($"통계 데이터 저장 경로: {statisticsFilePath}");
    }

    /// <summary>
    /// 통계 데이터를 저장합니다.
    /// </summary>
    public async void SaveStatisticsData()
    {
        if (GameStatisticsData.Instance == null)
        {
            Debug.LogWarning("GameStatisticsData 인스턴스가 없습니다.");
            return;
        }

        try
        {
            StatisticsSaveData saveData = new StatisticsSaveData
            {
                dailyStatistics = GameStatisticsData.Instance.DailyStats,
                currentDaySpawnCount = GameStatisticsData.Instance.CurrentDaySpawnCount,
                currentDayGoldEarned = GameStatisticsData.Instance.CurrentDayGoldEarned,
                currentDayReputationGained = GameStatisticsData.Instance.CurrentDayReputationGained,
                lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                version = "1.0"
            };

            string json = JsonUtility.ToJson(saveData, true);
            await File.WriteAllTextAsync(statisticsFilePath, json);
            
            Debug.Log($"통계 데이터 저장 완료: {statisticsFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"통계 데이터 저장 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 통계 데이터를 로드합니다.
    /// </summary>
    public async void LoadStatisticsData()
    {
        if (!File.Exists(statisticsFilePath))
        {
            Debug.LogWarning($"통계 데이터 파일이 존재하지 않습니다: {statisticsFilePath}");
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(statisticsFilePath);
            StatisticsSaveData saveData = JsonUtility.FromJson<StatisticsSaveData>(json);
            
            if (saveData == null)
            {
                Debug.LogError("통계 데이터 역직렬화 실패");
                return;
            }

            RestoreStatisticsData(saveData);
            Debug.Log($"통계 데이터 로드 완료: {statisticsFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"통계 데이터 로드 오류: {e.Message}");
        }
    }

    private void RestoreStatisticsData(StatisticsSaveData saveData)
    {
        if (GameStatisticsData.Instance == null)
        {
            Debug.LogError("GameStatisticsData 인스턴스가 없습니다.");
            return;
        }

        // 데이터 복원 로직
        // 실제 구현에서는 GameStatisticsData에 복원 메서드를 추가해야 함
        Debug.Log($"통계 데이터 복원: {saveData.dailyStatistics.Count}일간의 데이터");
    }

    /// <summary>
    /// 통계 데이터를 백업합니다.
    /// </summary>
    public async void BackupStatisticsData()
    {
        if (!File.Exists(statisticsFilePath))
        {
            Debug.LogWarning("백업할 통계 데이터 파일이 없습니다.");
            return;
        }

        try
        {
            string backupFileName = $"statistics_backup_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            string backupFilePath = Path.Combine(Application.persistentDataPath, backupFileName);
            
            string json = await File.ReadAllTextAsync(statisticsFilePath);
            await File.WriteAllTextAsync(backupFilePath, json);
            
            Debug.Log($"통계 데이터 백업 완료: {backupFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"통계 데이터 백업 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 통계 데이터를 내보냅니다.
    /// </summary>
    public async void ExportStatisticsData(string exportPath)
    {
        if (GameStatisticsData.Instance == null)
        {
            Debug.LogWarning("GameStatisticsData 인스턴스가 없습니다.");
            return;
        }

        try
        {
            StatisticsSaveData exportData = new StatisticsSaveData
            {
                dailyStatistics = GameStatisticsData.Instance.DailyStats,
                currentDaySpawnCount = GameStatisticsData.Instance.CurrentDaySpawnCount,
                currentDayGoldEarned = GameStatisticsData.Instance.CurrentDayGoldEarned,
                currentDayReputationGained = GameStatisticsData.Instance.CurrentDayReputationGained,
                lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                version = "1.0"
            };

            string json = JsonUtility.ToJson(exportData, true);
            await File.WriteAllTextAsync(exportPath, json);
            
            Debug.Log($"통계 데이터 내보내기 완료: {exportPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"통계 데이터 내보내기 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 통계 데이터를 가져옵니다.
    /// </summary>
    public async void ImportStatisticsData(string importPath)
    {
        if (!File.Exists(importPath))
        {
            Debug.LogError($"가져올 통계 데이터 파일이 존재하지 않습니다: {importPath}");
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(importPath);
            StatisticsSaveData importData = JsonUtility.FromJson<StatisticsSaveData>(json);
            
            if (importData == null)
            {
                Debug.LogError("통계 데이터 역직렬화 실패");
                return;
            }

            RestoreStatisticsData(importData);
            Debug.Log($"통계 데이터 가져오기 완료: {importPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"통계 데이터 가져오기 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 통계 데이터 파일을 삭제합니다.
    /// </summary>
    public void DeleteStatisticsData()
    {
        if (File.Exists(statisticsFilePath))
        {
            try
            {
                File.Delete(statisticsFilePath);
                Debug.Log("통계 데이터 파일이 삭제되었습니다.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"통계 데이터 파일 삭제 오류: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("삭제할 통계 데이터 파일이 없습니다.");
        }
    }

    /// <summary>
    /// 통계 데이터 파일 정보를 가져옵니다.
    /// </summary>
    public StatisticsFileInfo GetStatisticsFileInfo()
    {
        if (!File.Exists(statisticsFilePath))
        {
            return new StatisticsFileInfo { exists = false };
        }

        try
        {
            FileInfo fileInfo = new FileInfo(statisticsFilePath);
            return new StatisticsFileInfo
            {
                exists = true,
                fileSize = fileInfo.Length,
                lastModified = fileInfo.LastWriteTime,
                filePath = statisticsFilePath
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"파일 정보 가져오기 오류: {e.Message}");
            return new StatisticsFileInfo { exists = false };
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveStatisticsData();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveStatisticsData();
        }
    }

    private void OnDestroy()
    {
        if (isInitialized)
        {
            SaveStatisticsData();
        }
    }
}

/// <summary>
/// 통계 데이터 저장용 구조체
/// </summary>
[System.Serializable]
public class StatisticsSaveData
{
    public List<DailyStatistics> dailyStatistics;
    public int currentDaySpawnCount;
    public int currentDayGoldEarned;
    public int currentDayReputationGained;
    public string lastSaveTime;
    public string version;
}

/// <summary>
/// 통계 파일 정보 구조체
/// </summary>
[System.Serializable]
public class StatisticsFileInfo
{
    public bool exists;
    public long fileSize;
    public System.DateTime lastModified;
    public string filePath;
}
