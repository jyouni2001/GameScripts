using JY;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class SaveData
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
}

[System.Serializable]
public class GridDataSave
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
            currentDay = TimeSystem.Instance.CurrentDay
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
                await LoadMainScene();
                return;
            }

            await LoadMainScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"세이브 파일 로드 오류: {e.Message}");
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
        if (scene.name == "MainScene" && loadedSaveData != null)
        {
            StartCoroutine(WaitAndRestoreData());
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
                          ObjectPlacer.Instance != null;

        if (!initialized)
        {
            Debug.LogWarning($"시스템 초기화 상태: " +
                             $"PlayerWallet: {PlayerWallet.Instance != null}, " +
                             $"PlacementSystem: {PlacementSystem.Instance != null}, " +
                             $"PaymentSystem: {PaymentSystem.Instance != null}, " +
                             $"ReputationSystem: {ReputationSystem.Instance != null}, " +
                             $"TimeSystem: {TimeSystem.Instance != null}, " +
                             $"ObjectPlacer: {ObjectPlacer.Instance != null}");
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

            PlacementSystem.Instance.UpdateGridBounds();
            PlacementSystem.Instance.ActivatePlanesByLevel(loadedSaveData.currentPurchaseLevel);

            Debug.Log("게임 데이터 복원 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"데이터 복원 중 오류 발생: {e.Message}\nStackTrace: {e.StackTrace}");
        }
        finally
        {
            loadedSaveData = null;
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

        gridData.placedObjects = new Dictionary<Vector3Int, List<PlacementData>>();
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
        }
    }

    private void ClearPlacedObjects()
    {
        if (ObjectPlacer.Instance != null)
        {
            for (int i = 0; i < ObjectPlacer.Instance.placedGameObjects.Count; i++)
            {
                if (ObjectPlacer.Instance.placedGameObjects[i] != null)
                {
                    ObjectPlacer.Instance.RemoveObject(i);
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
        switch(gridY) 
        {
            case 0:
                return 1;
            case 2:
                return 2;
            case 4:
                return 3;
            case 8:
                return 4;

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
                return 9.63405f;
            case 4:
                return 14.45f;

            default:
                return 0;
        }
    }
}