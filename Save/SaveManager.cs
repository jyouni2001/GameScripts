using JY;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ZLinq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class AISaveData // AI 저장 데이터
{
    public string aiName;
    public Vector3 position;
    public Quaternion rotation;
    public int currentRoomIndex;
    public string currentState;  // AIState enum을 string으로 저장
    
    // AI 상태 플래그들
    public bool isUsingHealth;
    public bool isUsingWedding;
    public bool isUsingLounge;
    public bool isUsingHall;
    public bool isUsingSauna;
    public bool isSaunaSitting; // 사우나에서 앉기(true) 또는 눕기(false)
    public bool isUsingSunbed;
    public bool isEating;
    public bool isUsingBathtub;
    public bool isSleeping;
    public bool isNapping;
    
    // 시설 위치 (Transform 대신 위치로 저장)
    public Vector3 currentHealthPosition;
    public Vector3 currentWeddingPosition;
    public Vector3 currentLoungePosition;
    public Vector3 currentHallPosition;
    public Vector3 currentSaunaPosition;
    public Vector3 currentSunbedPosition;
    public bool hasHealthPosition;
    public bool hasWeddingPosition;
    public bool hasLoungePosition;
    public bool hasHallPosition;
    public bool hasSaunaPosition;
    public bool hasSunbedPosition;
}

[System.Serializable]
public class SaveData // 저장할 데이터
{
    public int playerMoney;
    public GridDataSave floorData;
    public GridDataSave furnitureData;
    public GridDataSave wallData;
    public GridDataSave decoData;
    public List<PaymentSystem.PaymentInfo> paymentQueue;
    public int currentReputation;
    public float currentTime;
    public int currentDay;
    public int currentPurchaseLevel;
    public bool floorLock;
    public bool isTutorialFinished;

    // 퀘스트 데이터
    public List<QuestSaveData> activeQuests;
    public List<string> pendingQuestNames;
    public List<string> availableQuestNames;
    public string currentQuestName;

    // 통계 데이터
    public List<DailyDataSave> dailyStatistics;
    
    // AI 데이터
    public List<AISaveData> aiAgents;
}

[System.Serializable]
public class DailyDataSave
{
    public int day;
    public int reputationGained;
    public int goldEarned;
    public int totalVisitors;
    public int startingReputation;
    public int startingGold;
    public int endingReputation;
    public int endingGold;
}

[System.Serializable]
public class QuestSaveData // 퀘스트 저장
{
    public string questName;
    public int currentAmount;
    public bool isCompleted;
    public int startingMoney;

}

[System.Serializable]
public class GridDataSave // 그리드 저장
{
    public List<GridEntry> placedObjects;
}

[System.Serializable]
public class GridEntry
{
    public Vector3Int key;
    public List<PlacementData> value;
}

public class SaveManager : MonoBehaviour
{
    private SaveData loadedSaveData;
    private string savePath;
    public static SaveManager Instance { get; private set; }

    private void Awake()
    {
        // 세이브 파일 경로 설정 (예: Application.persistentDataPath)
        savePath = Path.Combine(Application.persistentDataPath, "saveData.json");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public async void SaveGame()
    {
        List<QuestSaveData> activeQuestsToSave = new List<QuestSaveData>();
        foreach (var activeQuest in QuestManager.Instance.activeQuests)
        {
            activeQuestsToSave.Add(new QuestSaveData
            {
                questName = activeQuest.data.questName,
                currentAmount = activeQuest.currentAmount,
                isCompleted = activeQuest.isCompleted,
                startingMoney = activeQuest.startingMoney
            });
        }

        // 통계 데이터 저장 준비
        List<DailyDataSave> statisticsToSave = new List<DailyDataSave>();
        if (DailyStatisticsManager.Instance != null && DailyStatisticsManager.Instance.StatisticsContainer != null)
        {
            Debug.Log($"[SaveManager] 저장할 통계 데이터 확인: statisticsContainer.dailyStatistics 개수={DailyStatisticsManager.Instance.StatisticsContainer.dailyStatistics.Count}");

            // 모든 일차의 통계를 순회
            foreach (var dailyStats in DailyStatisticsManager.Instance.StatisticsContainer.dailyStatistics)
            {
                Debug.Log($"  - Day {dailyStats.day}: dailyData 개수={dailyStats.dailyData?.Count ?? 0}");

                // 각 일차의 dailyData 리스트에서 최신 데이터만 저장 (또는 모든 데이터 저장)
                if (dailyStats.dailyData != null && dailyStats.dailyData.Count > 0)
                {
                    // 각 일차의 마지막 데이터만 저장 (가장 최신)
                    var latestData = dailyStats.dailyData[dailyStats.dailyData.Count - 1];
                    statisticsToSave.Add(new DailyDataSave
                    {
                        day = latestData.day,
                        reputationGained = latestData.reputationGained,
                        goldEarned = latestData.goldEarned,
                        totalVisitors = latestData.totalVisitors,
                        startingReputation = latestData.startingReputation,
                        startingGold = latestData.startingGold,
                        endingReputation = latestData.endingReputation,
                        endingGold = latestData.endingGold
                    });
                    Debug.Log($"    → Day {latestData.day} 저장: Gold={latestData.goldEarned}, Rep={latestData.reputationGained}");
                }
            }
            Debug.Log($"[SaveManager] 최종 저장될 통계 데이터 개수: {statisticsToSave.Count}");
        }

        // AI 데이터 저장 준비
        List<AISaveData> aiSaveDataList = new List<AISaveData>();
        
        // ✅ AISpawner를 통해 활성화된 AI만 가져오기
        if (JY.AISpawner.Instance != null)
        {
            List<GameObject> activeAIs = JY.AISpawner.Instance.GetActiveAIs();
            Debug.Log($"[SaveManager] 저장할 AI 확인: {activeAIs.Count}명 (AISpawner에서)");
            
            foreach (var aiObj in activeAIs)
            {
                if (aiObj != null)
                {
                    JY.AIAgent aiAgent = aiObj.GetComponent<JY.AIAgent>();
                    if (aiAgent != null)
                    {
                        AISaveData aiData = aiAgent.GetSaveData();
                        aiSaveDataList.Add(aiData);
                        Debug.Log($"  - {aiData.aiName}: 위치={aiData.position}, 방={aiData.currentRoomIndex}, 상태={aiData.currentState}");
                    }
                }
            }
            Debug.Log($"[SaveManager] AI {aiSaveDataList.Count}명 저장 준비 완료");
        }
        else
        {
            Debug.LogWarning("[SaveManager] AISpawner가 없어 AI 저장 불가");
        }

        SaveData saveData = new SaveData
        {
            playerMoney = PlayerWallet.Instance.money,
            currentPurchaseLevel = PlacementSystem.Instance.currentPurchaseLevel,
            floorLock = PlacementSystem.Instance.GetFloorLock(),
            floorData = ConvertGridData(PlacementSystem.Instance.floorData),
            furnitureData = ConvertGridData(PlacementSystem.Instance.furnitureData),
            wallData = ConvertGridData(PlacementSystem.Instance.wallData),
            decoData = ConvertGridData(PlacementSystem.Instance.decoData),
            paymentQueue = PaymentSystem.Instance.paymentQueue,
            currentReputation = ReputationSystem.Instance.CurrentReputation,
            currentTime = TimeSystem.Instance.currentTime,
            currentDay = TimeSystem.Instance.CurrentDay,
            activeQuests = activeQuestsToSave,
            pendingQuestNames = QuestManager.Instance.pendingQuests.AsValueEnumerable().Select(q => q.questName).ToList(),
            availableQuestNames = QuestManager.Instance.availableQuests.AsValueEnumerable().Select(q => q.questName).ToList(),
            currentQuestName = (QuestManager.Instance.CurrentQuest != null) ? QuestManager.Instance.CurrentQuest.questName : null,
            dailyStatistics = statisticsToSave,
            isTutorialFinished = NewTutorialGuide.Instance.isTutorialFinish,
            aiAgents = aiSaveDataList  // ✅ AI 데이터 추가
        };

        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            await File.WriteAllTextAsync(savePath, json);
            Debug.Log($"게임 저장 완료: {savePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"게임 저장 오류: {e.Message}");
        }
    }

    public async Task LoadGame()
    {
        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"세이브 파일이 존재하지 않습니다: {savePath}");

            // 새 게임 시작 - 통계 데이터 초기화
            if (DailyStatisticsManager.Instance != null)
            {
                DailyStatisticsManager.Instance.ClearAllStatistics();
                Debug.Log("새 게임 시작 - 통계 데이터 초기화됨");
            }

            await LoadMainScene();
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(savePath);
            loadedSaveData = JsonUtility.FromJson<SaveData>(json);
            if (loadedSaveData == null)
            {
                Debug.LogError("세이브 데이터 역직렬화 실패");

                // 데이터 로드 실패 - 통계 데이터 초기화
                if (DailyStatisticsManager.Instance != null)
                {
                    DailyStatisticsManager.Instance.ClearAllStatistics();
                    Debug.Log("세이브 데이터 로드 실패 - 통계 데이터 초기화됨");
                }

                await LoadMainScene();
                return;
            }

            await LoadMainScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"세이브 파일 로드 오류: {e.Message}");

            // 로드 오류 - 통계 데이터 초기화
            if (DailyStatisticsManager.Instance != null)
            {
                DailyStatisticsManager.Instance.ClearAllStatistics();
                Debug.Log("세이브 파일 로드 오류 - 통계 데이터 초기화됨");
            }

            await LoadMainScene();
        }
    }

    private async Task LoadMainScene()
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("MainScene");
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            await Task.Yield();
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            await Task.Yield();
        }

        await Task.Delay(100);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainScene")
        {
            if (loadedSaveData != null)
            {
                StartCoroutine(WaitAndRestoreData());
            }
            else
            {
                // 새 게임 시작 시 통계 데이터 초기화
                if (DailyStatisticsManager.Instance != null)
                {
                    DailyStatisticsManager.Instance.ClearAllStatistics();
                    Debug.Log("메인 씬 로드 - 새 게임 시작, 통계 데이터 초기화됨");
                }
            }
        }
    }

    private IEnumerator WaitAndRestoreData()
    {
        // 시스템 초기화 대기
        yield return new WaitUntil(() => AreAllSystemsInitialized());
        yield return new WaitForEndOfFrame(); // 추가 프레임 대기

        if (!AreAllSystemsInitialized())
        {
            Debug.LogError("시스템 초기화 실패: 하나 이상의 인스턴스가 null입니다.");
            loadedSaveData = null;
            yield break;
        }

        RestoreGameData();
    }

    private bool AreAllSystemsInitialized()
    {
        bool initialized = PlayerWallet.Instance != null &&
                          PlacementSystem.Instance != null &&
                          PaymentSystem.Instance != null &&
                          ReputationSystem.Instance != null &&
                          TimeSystem.Instance != null &&
                          ObjectPlacer.Instance != null &&
                          QuestManager.Instance != null;

        if (!initialized)
        {
            Debug.LogWarning($"시스템 초기화 상태: " +
                             $"PlayerWallet: {PlayerWallet.Instance != null}, " +
                             $"PlacementSystem: {PlacementSystem.Instance != null}, " +
                             $"PaymentSystem: {PaymentSystem.Instance != null}, " +
                             $"ReputationSystem: {ReputationSystem.Instance != null}, " +
                             $"TimeSystem: {TimeSystem.Instance != null}, " +
                             $"ObjectPlacer: {ObjectPlacer.Instance != null}, " +
                             $"QuestManager: {QuestManager.Instance != null}");
        }
        else
        {
            Debug.Log("초기화 완료");
        }

        return initialized;
    }

    private void RestoreGameData()
    {
        try
        {
            // 데이터 복원
            if (PlayerWallet.Instance == null) throw new System.Exception("PlayerWallet.Instance is null");
            PlayerWallet.Instance.money = loadedSaveData.playerMoney;

            if (PlacementSystem.Instance == null) throw new System.Exception("PlacementSystem.Instance is null");
            PlacementSystem.Instance.currentPurchaseLevel = loadedSaveData.currentPurchaseLevel;
            PlacementSystem.Instance.FloorLock = loadedSaveData.floorLock;
            PlacementSystem.Instance.UpdatePurchaseUI();


            if (QuestManager.Instance != null && loadedSaveData.activeQuests != null)
            {
                QuestManager.Instance.LoadQuestData(
                    loadedSaveData.activeQuests,
                    loadedSaveData.pendingQuestNames,
                    loadedSaveData.availableQuestNames,
                    loadedSaveData.currentQuestName
                );
            }


            ClearPlacedObjects();

            LoadGridData(PlacementSystem.Instance.floorData, loadedSaveData.floorData);
            LoadGridData(PlacementSystem.Instance.furnitureData, loadedSaveData.furnitureData);
            LoadGridData(PlacementSystem.Instance.wallData, loadedSaveData.wallData);
            LoadGridData(PlacementSystem.Instance.decoData, loadedSaveData.decoData);

            if (PaymentSystem.Instance == null) throw new System.Exception("PaymentSystem.Instance is null");
            PaymentSystem.Instance.paymentQueue = loadedSaveData.paymentQueue;

            if (ReputationSystem.Instance == null) throw new System.Exception("ReputationSystem.Instance is null");
            ReputationSystem.Instance.SetReputation(loadedSaveData.currentReputation);

            if (TimeSystem.Instance == null) throw new System.Exception("TimeSystem.Instance is null");
            Debug.Log($"저장된 날짜 : {loadedSaveData.currentDay}");
            TimeSystem.Instance.SetDateTime(loadedSaveData.currentDay, (int)(loadedSaveData.currentTime / 3600), (int)((loadedSaveData.currentTime % 3600) / 60));
            TimeManager.instance.UpdateDayUI(loadedSaveData.currentDay);

            // 통계 데이터 복원
            if (DailyStatisticsManager.Instance != null && loadedSaveData.dailyStatistics != null
                && DailyStatisticsManager.Instance.StatisticsContainer != null)
            {
                Debug.Log($"[SaveManager 로드] 불러올 통계 데이터 개수: {loadedSaveData.dailyStatistics.Count}");

                foreach (var savedData in loadedSaveData.dailyStatistics)
                {
                    Debug.Log($"  - Day {savedData.day} 복원 시작: Gold={savedData.goldEarned}, Rep={savedData.reputationGained}, Visitors={savedData.totalVisitors}");

                    // 해당 일차의 통계를 가져오거나 생성
                    var dailyStats = DailyStatisticsManager.Instance.StatisticsContainer.GetOrCreateDailyStatistics(savedData.day);

                    // DailyData 생성
                    DailyData dailyData = new DailyData(
                        savedData.day,
                        savedData.reputationGained,
                        savedData.goldEarned,
                        savedData.totalVisitors,
                        savedData.startingReputation,
                        savedData.startingGold,
                        savedData.endingReputation,
                        savedData.endingGold
                    );

                    // dailyData 리스트에 추가 (중복 방지)
                    if (dailyStats.dailyData == null)
                    {
                        dailyStats.dailyData = new List<DailyData>();
                    }

                    // 이미 존재하는 데이터인지 확인
                    var existingData = dailyStats.dailyData.Find(d => d.day == savedData.day);
                    if (existingData == null)
                    {
                        dailyStats.dailyData.Add(dailyData);
                        Debug.Log($"    → Day {savedData.day} dailyData 추가: Visitors={savedData.totalVisitors}");
                    }
                    else
                    {
                        // 기존 데이터 업데이트
                        existingData.reputationGained = savedData.reputationGained;
                        existingData.goldEarned = savedData.goldEarned;
                        existingData.totalVisitors = savedData.totalVisitors;
                        existingData.startingReputation = savedData.startingReputation;
                        existingData.startingGold = savedData.startingGold;
                        existingData.endingReputation = savedData.endingReputation;
                        existingData.endingGold = savedData.endingGold;
                        Debug.Log($"    → Day {savedData.day} 기존 dailyData 업데이트: Visitors={savedData.totalVisitors}");
                    }

                    // 총계 업데이트
                    dailyStats.totalReputationGained = savedData.reputationGained;
                    dailyStats.totalGoldEarned = savedData.goldEarned;
                    dailyStats.totalVisitors = savedData.totalVisitors;
                    
                    // 시작 값도 복원
                    dailyStats.startingReputation = savedData.startingReputation;
                    dailyStats.startingGold = savedData.startingGold;
                }

                // ✅ 중요: 현재 날짜의 DailyStatisticsManager 런타임 변수 복원
                int currentDay = loadedSaveData.currentDay;
                var currentDayData = loadedSaveData.dailyStatistics.Find(d => d.day == currentDay);
                if (currentDayData != null)
                {
                    // 현재 날의 방문자 수 복원
                    DailyStatisticsManager.Instance.RestoreCurrentDayData(
                        currentDayData.totalVisitors,
                        currentDayData.startingReputation,
                        currentDayData.startingGold
                    );
                    Debug.Log($"[SaveManager] 현재 날({currentDay}일차) 런타임 변수 복원: Visitors={currentDayData.totalVisitors}, StartGold={currentDayData.startingGold}, StartRep={currentDayData.startingReputation}");
                }
                else
                {
                    Debug.Log($"[SaveManager] 현재 날({currentDay}일차) 데이터가 없어 런타임 변수를 기본값으로 초기화");
                }

                Debug.Log($"통계 데이터 복원 완료: {loadedSaveData.dailyStatistics.Count}일치");
                Debug.Log($"[SaveManager 로드 완료] statisticsContainer.dailyStatistics 최종 개수: {DailyStatisticsManager.Instance.StatisticsContainer.dailyStatistics.Count}");
                foreach (var stats in DailyStatisticsManager.Instance.StatisticsContainer.dailyStatistics)
                {
                    Debug.Log($"  - Day {stats.day}: dailyData 개수={stats.dailyData?.Count ?? 0}, totalVisitors={stats.totalVisitors}");
                }
            }

            PlacementSystem.Instance.ActivatePlanesByLevel(loadedSaveData.currentPurchaseLevel);
            PlacementSystem.Instance.UpdateGridBounds();
            PlacementSystem.Instance.HideAllPlanes();

            NewTutorialGuide.Instance.isTutorialFinish = loadedSaveData.isTutorialFinished;

            // ✅ AI 데이터 복원은 코루틴으로 지연 (오브젝트 배치 완료 대기)
            StartCoroutine(RestoreAIDataDelayed());

            Debug.Log("게임 데이터 복원 완료 (AI 복원은 코루틴에서 처리)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"데이터 복원 중 오류 발생: {e.Message}\nStackTrace: {e.StackTrace}");
        }
    }

    /// <summary>
    /// 오브젝트 배치 완료 후 AI 복원 (지연 실행)
    /// </summary>
    private IEnumerator RestoreAIDataDelayed()
    {
        // ✅ 2프레임 대기 (오브젝트 생성 및 태그 등록 완료 보장)
        yield return null;
        yield return null;
        
        Debug.Log("[SaveManager] 오브젝트 배치 완료 - AI 복원 시작");
        
        try
        {
            // AI 데이터 복원
            if (loadedSaveData != null && loadedSaveData.aiAgents != null && loadedSaveData.aiAgents.Count > 0)
            {
                Debug.Log($"[SaveManager] AI {loadedSaveData.aiAgents.Count}명 복원 시작");
                
                // ✅ AISpawner를 통해 모든 AI (활성화 + 비활성화) 가져오기
                if (JY.AISpawner.Instance != null)
                {
                    // ✅ 먼저 기존 활성 AI를 모두 비활성화
                    JY.AISpawner.Instance.DeactivateAllAIs();
                    Debug.Log($"[SaveManager] 기존 AI 모두 비활성화 완료");
                    
                    List<GameObject> allAIs = JY.AISpawner.Instance.GetAllAIs();
                    Debug.Log($"[SaveManager] 사용 가능한 AI: {allAIs.Count}명 (AISpawner 풀)");
                    
                    // AI 이름으로 매칭하여 복원
                    int restoredCount = 0;
                    foreach (var savedAI in loadedSaveData.aiAgents)
                    {
                        GameObject matchingAIObj = allAIs.Find(ai => ai.name == savedAI.aiName);
                        if (matchingAIObj != null)
                        {
                            JY.AIAgent aiAgent = matchingAIObj.GetComponent<JY.AIAgent>();
                            if (aiAgent != null)
                            {
                                // ✅ AISpawner를 통해 AI 활성화 및 리스트 관리
                                JY.AISpawner.Instance.RestoreAI(matchingAIObj);
                                
                                // 데이터 복원
                                aiAgent.LoadFromSaveData(savedAI);
                                restoredCount++;
                                Debug.Log($"  - {savedAI.aiName} 복원 완료: 위치={savedAI.position}, 방={savedAI.currentRoomIndex}, 상태={savedAI.currentState}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[SaveManager] AI를 찾을 수 없음: {savedAI.aiName}");
                        }
                    }
                    Debug.Log($"[SaveManager] AI 복원 완료: {restoredCount}/{loadedSaveData.aiAgents.Count}명");
                }
                else
                {
                    Debug.LogError("[SaveManager] AISpawner가 없어 AI 복원 불가");
                }
            }
            else
            {
                Debug.Log("[SaveManager] 저장된 AI 데이터 없음");
                
                // ✅ 저장된 AI가 없으면 기존 AI도 모두 비활성화
                if (JY.AISpawner.Instance != null)
                {
                    JY.AISpawner.Instance.DeactivateAllAIs();
                    Debug.Log($"[SaveManager] 저장된 AI 없음 - 모든 AI 비활성화");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AI 복원 중 오류 발생: {e.Message}\nStackTrace: {e.StackTrace}");
        }
        finally
        {
            // ✅ 모든 복원이 완료된 후 loadedSaveData 정리
            loadedSaveData = null;
            Debug.Log("[SaveManager] 모든 데이터 복원 완료 (AI 포함)");
        }
    }

    private GridDataSave ConvertGridData(GridData gridData)
    {
        if (gridData == null) return null;

        GridDataSave saveData = new GridDataSave { placedObjects = new List<GridEntry>() };

        foreach (var kvp in gridData.placedObjects)
        {
            saveData.placedObjects.Add(new GridEntry { key = kvp.Key, value = kvp.Value });
        }

        return saveData;
    }

    private void LoadGridData(GridData gridData, GridDataSave saveData)
    {
        if (gridData == null || saveData == null || saveData.placedObjects == null)
        {
            Debug.LogError("필수 데이터가 null입니다.");
            return;
        }
        gridData.placedObjects.Clear();

        var processedObjectIndices = new HashSet<int>();

        foreach (var entry in saveData.placedObjects)
        {
            if (entry.value == null || entry.value.Count == 0)
            {
                Debug.LogWarning($"GridEntry's value is null or empty. key: {entry.key}");
                continue;
            }

            // An object occupies multiple grid cells, but the PlacementData is the same.
            // We only need to process it once based on its unique PlacedObjectIndex.
            var placementData = entry.value[0];

            // [BUG FIX] Check if this object's index has already been processed. If so, skip.
            if (processedObjectIndices.Contains(placementData.PlacedObjectIndex))
            {
                continue;
            }

            ObjectData objectData = PlacementSystem.Instance.GetDatabase().GetObjectData(placementData.ID);
            if (objectData != null)
            {
                Vector3 worldPosition = PlacementSystem.Instance.grid.GetCellCenterWorld(entry.key);

                int floor = ConvertGridYToFloorNumber(entry.key.y);
                float floorHeight = GetFloorHeight(floor);
                worldPosition.y = floorHeight;

                // Place the object and get its new index in the runtime list.
                int newIndex = ObjectPlacer.Instance.PlaceObject(objectData.Prefab, worldPosition, placementData.Rotation, floor);

                if (newIndex != -1)
                {
                    // [BUG FIX] Add the original (saved) index to the processed set to prevent duplicates.
                    processedObjectIndices.Add(placementData.PlacedObjectIndex);

                    // Re-calculate all grid positions this object occupies.
                    List<Vector3Int> occupiedPositions = PlacementSystem.Instance.floorData.CalculatePosition(entry.key, objectData.Size, placementData.Rotation, PlacementSystem.Instance.grid);

                    // Create new PlacementData with the NEW index from ObjectPlacer.
                    PlacementData dataToAdd = new PlacementData(occupiedPositions, placementData.ID, newIndex, placementData.KindIndex, placementData.Rotation);

                    // Add the new PlacementData to the dictionary for all occupied positions.
                    foreach (var pos in occupiedPositions)
                    {
                        if (!gridData.placedObjects.ContainsKey(pos))
                        {
                            gridData.placedObjects[pos] = new List<PlacementData>();
                        }
                        gridData.placedObjects[pos].Add(dataToAdd);
                    }
                    Debug.Log($"Load Success - New Index: {newIndex}, ID: {placementData.ID}, Pos: {entry.key}");
                }
                else
                {
                    Debug.LogError($"Load Fail - key: {entry.key}, ID: {placementData.ID}");
                }
            }
        }

        /*gridData.placedObjects = new Dictionary<Vector3Int, List<PlacementData>>();
        var processedObjects = new HashSet<int>();

        foreach (var entry in saveData.placedObjects)
        {
            if (entry.value == null || entry.value.Count == 0)
            {
                Debug.LogWarning($"GridEntry의 value가 null 또는 비어 있습니다. key: {entry.key}");
                continue;
            }

            var placementData = entry.value[0]; // 첫 번째 PlacementData를 기준으로
            if (processedObjects.Contains(placementData.PlacedObjectIndex))
            {
                continue; // 이미 처리된 객체 무시
            }

            ObjectData objectData = PlacementSystem.Instance.GetDatabase().GetObjectData(placementData.ID);
            if (objectData != null)
            {
                Vector3 worldPosition = PlacementSystem.Instance.grid.GetCellCenterWorld(entry.key);
                Debug.Log($"현재 저장된 데이터의 월드 포지션 값 = {worldPosition}");

                // ▼▼▼ [핵심 수정] ▼▼▼
                // entry.key.y (정수 그리드 좌표)를 사용하여 정확한 층 번호를 계산합니다.
                int floor = ConvertGridYToFloorNumber(entry.key.y);
                Debug.Log($"현재 저장된 데이터의 y값 = {floor}와 기존 값 ={entry.key.y}");

                // floor 가 1, 2, 3, 4 (층) 값이 나올때, worldPosition.y 의 값 변화
                // 1층 0, 2층 4.8175, 3층 9.63405, 4층 14.45                

                float floorheight = GetFloorHeight(floor);

                worldPosition.y = floorheight;

                // PlaceObject 호출 시 계산된 층 번호를 세 번째 인자로 전달합니다.
                int index = ObjectPlacer.Instance.PlaceObject(objectData.Prefab, worldPosition, placementData.Rotation, floor);
                // ▲▲▲ [핵심 수정] ▲▲▲

                //int index = ObjectPlacer.Instance.PlaceObject(objectData.Prefab, worldPosition, placementData.Rotation);

                if (index != -1)
                {
                    // ▼ [수정] 새로 생성된 오브젝트의 인덱스를 processedObjects에 추가합니다.
                    processedObjects.Add(index);

                    // 원본 PlacementData의 인덱스를 새로 받은 인덱스로 갱신합니다.
                    placementData.PlacedObjectIndex = index;

                    // 오브젝트가 점유하는 모든 위치에 올바른 데이터를 다시 추가합니다.
                    List<Vector3Int> occupiedPositions = PlacementSystem.Instance.floorData.CalculatePosition(entry.key, objectData.Size, placementData.Rotation, PlacementSystem.Instance.grid);
                    PlacementData dataToAdd = new PlacementData(occupiedPositions, placementData.ID, index, placementData.KindIndex, placementData.Rotation);

                    foreach (var pos in occupiedPositions)
                    {
                        if (!gridData.placedObjects.ContainsKey(pos))
                        {
                            gridData.placedObjects[pos] = new List<PlacementData>();
                        }
                        gridData.placedObjects[pos].Add(dataToAdd);
                    }
                    Debug.Log($"로드 성공 - Index: {index}, ID: {placementData.ID}, Pos: {entry.key}");
                }
                else
                {
                    Debug.LogError($"로드 실패 - key: {entry.key}, ID: {placementData.ID}");
                }
            }
        }*/
    }

    private void ClearPlacedObjects()
    {
        if (ObjectPlacer.Instance != null)
        {
            for (int i = ObjectPlacer.Instance.placedGameObjects.Count - 1; i >= 0; i--)
            {
                if (ObjectPlacer.Instance.placedGameObjects[i] != null)
                {
                    // 애니메이션이 있는 RemoveObject 대신 즉시 삭제하는 RemoveObjectImmediate를 호출합니다.
                    ObjectPlacer.Instance.RemoveObjectImmediate(i);
                }
            }
            ObjectPlacer.Instance.placedGameObjects.Clear();
        }
        else
        {
            Debug.LogError("ObjectPlacer.Instance가 null입니다.");
        }

        if (PlacementSystem.Instance != null)
        {
            PlacementSystem.Instance.floorData.placedObjects.Clear();
            PlacementSystem.Instance.furnitureData.placedObjects.Clear();
            PlacementSystem.Instance.wallData.placedObjects.Clear();
            PlacementSystem.Instance.decoData.placedObjects.Clear();
        }
        else
        {
            Debug.LogError("PlacementSystem.Instance가 null입니다.");
        }
    }

    /// <summary>
    /// 그리드의 Y좌표(정수)를 실제 층 번호로 변환합니다.
    /// </summary>
    /// <param name="gridY">GridData에 저장된 Vector3Int의 y값</param>
    /// <returns>계산된 층 번호 (예: 1, 2, 3...)</returns>
    private int ConvertGridYToFloorNumber(int gridY)
    {
        switch (gridY)
        {
            case 0:
                return 1;
            case 2:
                return 2;
            case 4:
                return 3;
            case 8:
                return 4;
            case 16:
                return 5;
            case 32:
                return 6;

            default:
                return 1;
        }
    }

    private float GetFloorHeight(int floor)
    {
        switch (floor)
        {
            case 1:
                return 0;
            case 2:
                return 4.8175f;
            case 3:
                return 9.635401f;
            case 4:
                return 14.45425f;
            case 5:
                return 19.28355f; // 14.45 + 4.8175
            case 6:
                return 24.115f; // 24.109 변경 예정 / 현재 24.07925 24.255 - 24.109
            default:
                return 0;
                /*case 1:
                    return 0;
                case 2:
                    return 4.8175f;
                case 3:
                    return 9.63405f;
                case 4:
                    return 14.45f;
                case 5:
                    return 19.2675f; // 14.45 + 4.8175
                case 6:
                    return 24.085f;
                default:
                    return 0;*/
        }
    }
}