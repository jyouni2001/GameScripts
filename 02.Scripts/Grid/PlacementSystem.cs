using System.Collections.Generic;
using ZLinq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlacementSystem : MonoBehaviour
{
    public static PlacementSystem Instance { get; set; }

    [Header("컴포넌트")]
    [SerializeField] private InputManager inputManager;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private GameObject previewObject;
    [SerializeField] private ChangeFloorSystem changeFloorSystem;
    private GridData selectedData; 
    
    private Renderer previewRenderer;
    public Vector3Int gridPosition;
    public Vector3 mousePosition;
    private Quaternion previewRotation = Quaternion.identity;
    public Grid grid;
    public ObjectsDatabaseSO database;
    public GridData floorData, furnitureData, wallData, decoData;
    
    [Header("변수")]
    public int indicatorMax = 100;
    [SerializeField] public GameObject mouseIndicator;
    [SerializeField] public GameObject cellIndicatorPrefab;
    private List<GameObject> cellIndicators = new List<GameObject>();
    private int selectedObjectIndex = -1;
    public int currentPurchaseLevel = 1;
    public bool FloorLock;
    
    // 가구 레이어 마스크 설정
    private int furnitureLayerMask;
    
    [Header("그리드 오브젝트")]
    [SerializeField] public List<GameObject> gridVisualization;
    public List<Bounds> planeBounds;
    
    [FormerlySerializedAs("plane")] [Header("플레인 리스트")]
    public List<GameObject> plane1f;
    public List<GameObject> plane2f;
    public List<GameObject> plane3f;
    public List<GameObject> plane4f;

    [Header("땅 구매 버튼")]
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Button purchase2FButton;
    
    [Header("증축 시스템 관련 버튼")]
    [SerializeField] private GameObject changeFloorButton;    
    
    [Header("미리보기 관련")]
    [SerializeField] private GameObject highlightedObject;
    public Renderer[] objRenderers;
    [SerializeField] private Material previewMaterialInstance;

    /// <summary>
    /// 싱글톤 패턴 사용
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StopPlacement();
        InitailizeGridDatas();
        InitializeGridBounds();
        InitializePlane();

        database.InitializeDictionary();
        furnitureLayerMask = LayerMask.GetMask("Furniture");
        if (purchaseButton is not null)
        {
            purchaseButton.onClick.AddListener(PurchaseNextLand);
        }

        if (purchase2FButton is not null)
        {
            purchase2FButton.onClick.AddListener(PurchaseOtherFloor);
        }
        
        changeFloorButton.SetActive(false);
        FloorLock = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T)) // 언제든 삭제 가능
        {
            FloorLock = true;
            Debug.Log($"증축 시스템 해금 상태 = {FloorLock}");
        }

        DeliteModeMouth();


        if (selectedObjectIndex < 0) return;

        IndicatorPos();
        PreviewObjectFunc();
        DragPlacement();

        mousePosition = inputManager.GetSelectedMapPosition();
        gridPosition = grid.WorldToCell(inputManager.GetSelectedMapPosition());
        GridPositionYandFloor();
    }

    #region GridPosition y값 변환

    private void GridPositionYandFloor()
    {
        switch(changeFloorSystem.currentFloor)
        {
            case 1:
                gridPosition.y = 0;
                break;
            case 2:
                gridPosition.y = 2;
                break;
            case 3:
                gridPosition.y = 4;
                break;
            case 4:
                gridPosition.y = 8;
                break;
        }

        
    }

    #endregion

    #region 플레인 초기화

    /// <summary>
    /// 시작 시 그리드를 전부 비활성화시켜 보이지않도록 한다.
    /// </summary>
    public void InitializePlane()
    {
        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(false);
        }
    }
    #endregion

    #region 인디케이터 위치

    /// <summary>
    /// 마우스와 셀 인디케이터의 좌표를 조절한다.
    /// </summary>
    private void IndicatorPos()
    {        
        mouseIndicator.transform.position = mousePosition;        
        UpdateCellIndicators();
    }

    /// <summary>
    /// 마우스의 위치를 통해 인디케이터의 좌표를 실시간으로 변경하고, 미리보기 중일 때 또한 변경 되도록 한다.
    /// </summary>
    private void UpdateCellIndicators()
    {
        if (selectedObjectIndex < 0 || selectedObjectIndex >= database.objectsData.Count) return;

        Vector2Int objectSize = database.objectsData[selectedObjectIndex].Size;
        int requiredIndicators = objectSize.x * objectSize.y;
        
        while (cellIndicators.Count < requiredIndicators && cellIndicators.Count < indicatorMax)
        {
            GameObject newIndicator = Instantiate(cellIndicatorPrefab, transform);
            cellIndicators.Add(newIndicator);
            if (previewRenderer is null)
            {
                previewRenderer = newIndicator.GetComponentInChildren<Renderer>();
                if (previewRenderer is null)
                {
                    Debug.LogError("cellIndicatorPrefab에 Renderer가 없습니다!");
                }
            }
        }
        for (int i = 0; i < cellIndicators.Count; i++)
        {
            if (i < requiredIndicators)
            {
                cellIndicators[i].SetActive(true);
                List<Vector3Int> positions = floorData.CalculatePosition(gridPosition, objectSize, previewRotation, grid);
                if (i < positions.Count)
                {
                    cellIndicators[i].transform.position = grid.GetCellCenterWorld(positions[i]) + new Vector3(0, 0.002f,0);
                    cellIndicators[i].transform.rotation = Quaternion.Euler(90, 0, 0);
                }
            }
            else
            {
                cellIndicators[i].SetActive(false);
            }
        }
    }
    #endregion

    #region 그리드 데이터 초기화

    /// <summary>
    /// 첫 그리드 데이터를 초기화한다.
    /// </summary>
    private void InitailizeGridDatas()
    {
        floorData = new GridData();
        furnitureData = new GridData();
        wallData = new GridData();
        decoData = new GridData();
    }
    #endregion

    #region 그리드 건설반경 초기화

    /// <summary>
    /// 그리드 건설 반경을 초기화하는 함수
    /// </summary>
    private void InitializeGridBounds()
    {
        if (plane1f is null || plane1f.Count == 0)
        {
            Debug.LogWarning("plane 리스트가 null이거나 비어 있습니다.");
            return;
        }

        planeBounds.Clear();

        if (plane1f[0] is not null)
        {
            Renderer firstPlaneRenderer = plane1f[0].GetComponent<Renderer>();
            if (firstPlaneRenderer is not null)
            {
                Bounds rendererBounds = firstPlaneRenderer.bounds;
                rendererBounds.Expand(new Vector3(0, 1, 0));
                planeBounds.Add(rendererBounds);
            }
        }
    }
    #endregion

    #region UpdateGridBounds (후에 리팩토링 필요)
    /// <summary>
    /// 그리드 반경에 따라 건축 가능한 지역 바운드 설정
    /// </summary>
    public void UpdateGridBounds()
    {
        foreach (GameObject planeRend in plane1f)
        {
            if (planeRend is not null && planeRend.activeSelf)
            {
                Renderer planeRenderer = planeRend.GetComponent<Renderer>();
                if (planeRenderer != null)
                {
                    Bounds rendererBounds = planeRenderer.bounds;
                    rendererBounds.Expand(new Vector3(0, 1, 0));

                    bool alreadyExists = planeBounds.Exists(b => b.center == rendererBounds.center && b.size == rendererBounds.size);
                    if (!alreadyExists)
                    {
                        planeBounds.Add(rendererBounds);
                        gridVisualization.Add(planeRend);
                    }
                }
            }
        }

        if (FloorLock)
        {
            foreach (GameObject planeRend in plane2f)
            {
                if (planeRend is not null)
                {
                    Renderer planeRenderer = planeRend.GetComponent<Renderer>();
                    if (planeRenderer is not null)
                    {
                        Bounds rendererBounds = planeRenderer.bounds;
                        rendererBounds.Expand(new Vector3(0, 1, 0));

                        bool alreadyExists = planeBounds.Exists(b => b.center == rendererBounds.center && b.size == rendererBounds.size);
                        if (!alreadyExists)
                        {
                            planeBounds.Add(rendererBounds);
                            gridVisualization.Add(planeRend);
                        }
                    }
                }
            }

            foreach (GameObject planeRend in plane3f)
            {
                if (planeRend is not null)
                {
                    Renderer planeRenderer = planeRend.GetComponent<Renderer>();
                    if (planeRenderer is not null)
                    {
                        Bounds rendererBounds = planeRenderer.bounds;
                        rendererBounds.Expand(new Vector3(0, 1, 0));

                        bool alreadyExists = planeBounds.Exists(b => b.center == rendererBounds.center && b.size == rendererBounds.size);
                        if (!alreadyExists)
                        {
                            planeBounds.Add(rendererBounds);
                            gridVisualization.Add(planeRend);
                        }
                    }
                }
            }

            foreach (GameObject planeRend in plane4f)
            {
                if (planeRend is not null)
                {
                    Renderer planeRenderer = planeRend.GetComponent<Renderer>();
                    if (planeRenderer is not null)
                    {
                        Bounds rendererBounds = planeRenderer.bounds;
                        rendererBounds.Expand(new Vector3(0, 1, 0));

                        bool alreadyExists = planeBounds.Exists(b => b.center == rendererBounds.center && b.size == rendererBounds.size);
                        if (!alreadyExists)
                        {
                            planeBounds.Add(rendererBounds);
                            gridVisualization.Add(planeRend);
                        }
                    }
                }
            }
        }
    }
    #endregion

    #region 건축 시작
    public void StartPlacement(int ID)
    {
        StopPlacement();
        StopDeleteMode();

        selectedObjectIndex = database.objectsData.FindIndex(data => data.ID == ID);
        if (selectedObjectIndex < 0)
        {
            Debug.LogError($"No Id Found{ID}");
            return;
        }

        CreatePreview();
        inputManager.OnClicked += PlaceStructure;
        inputManager.OnExit += StopPlacement;
        UpdateCellIndicators(); // 즉시 셀 인디케이터 업데이트
    }
    #endregion

    #region 프리뷰 (미리보기)
    public void CreatePreview()
    {
        if (selectedObjectIndex < 0 || selectedObjectIndex >= database.objectsData.Count) return;

        if (previewObject != null)
        {
            Destroy(previewObject);
        }

        previewObject = Instantiate(database.objectsData[selectedObjectIndex].Prefab);
        
        ApplyPreviewMaterial(previewObject);
    }

   
    private void ApplyPreviewMaterial(GameObject obj)
    {
        objRenderers = obj.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer objRenderer in objRenderers)
        {
            Material[] originalMat = objRenderer.materials;
            Material[] newMaterial = new Material[originalMat.Length];

            for (int i = 0; i < originalMat.Length; i++)
            {
                originalMat[i] = previewMaterialInstance;

                newMaterial[i] = originalMat[i];
            }
            objRenderer.materials = newMaterial;           
        }
    }
    #endregion

    #region 오브젝트 배치
    private void PlaceStructure()
    {
        // 드래그 중에는 단일 배치 실행하지 않음
        if (isDragging) return;
        if (inputManager.IsPointerOverUI())  return;

        
        
        //Vector3 mousePosition = inputManager.GetSelectedMapPosition();
        Vector3Int gridPosition = grid.WorldToCell(inputManager.GetSelectedMapPosition());

        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex, previewRotation);

        if (!placementValidity) return;
        
        Vector3 worldPosition = grid.GetCellCenterWorld(gridPosition);
        
        int index = objectPlacer.PlaceObject(database.objectsData[selectedObjectIndex].Prefab, worldPosition, previewRotation);
        
        PlayerWallet.Instance.SpendMoney(database.objectsData[selectedObjectIndex].BuildPrice);

        selectedData = GetSelectedGridData();

        bool isWall = database.objectsData[selectedObjectIndex].IsWall;
        selectedData.AddObjectAt( // <<< previewRotation이 전달되는지 확인
            gridPosition,
            database.objectsData[selectedObjectIndex].Size,
            database.objectsData[selectedObjectIndex].ID,
            index,
            database.objectsData[selectedObjectIndex].kindIndex,
            previewRotation, // <<< 여기!
            grid,
            isWall
        );
        //if (inputManager.hit.transform is not null) Debug.Log($"현재 설치된 오브젝트 : {index}, 선택한 오브젝트 : {inputManager.hit.transform.name}");
    
    }
    #endregion

    #region 점유상태 확인
    private bool CheckPlacementValidity(Vector3Int gridPosition, int selectedObjectIndex, Quaternion rotation)
    {
        ObjectData objectToPlace = database.objectsData[selectedObjectIndex];
        bool placingWall = objectToPlace.IsWall;
        GameObject prefab = database.objectsData[selectedObjectIndex].Prefab;

        // 1. 소지금 체크
        int buildPrice = objectToPlace.BuildPrice;
        if (PlayerWallet.Instance.money < buildPrice)
        {
            Debug.Log("배치 불가: 소지금 부족");
            return false; // 소지금 부족 시 배치 불가능
        }
        
        // 2. 배치 영역 확인 (planeBounds) - 기존 로직 유지
        List<Vector3Int> positionsToCheck = floorData.CalculatePosition(gridPosition, objectToPlace.Size, rotation, grid);
        foreach (Vector3Int pos in positionsToCheck)
        {
            Vector3 worldPos = grid.GetCellCenterWorld(pos);
            //bool isWithinBounds = planeBounds.Any(bound => bound.Contains(worldPos));
            bool isWithinBounds = planeBounds.AsValueEnumerable().Any(bound => bound.Contains(worldPos));
            if (!isWithinBounds)
            {
                Debug.Log($"그리드 반경을 벗어남: {pos}");
                return false;
            }
        }

        // 바닥 오브젝트 설치 시
        if (objectToPlace.kindIndex == 0)
        {
            Vector3 worldPosition = grid.GetCellCenterWorld(gridPosition); // 그리드 위치를 월드 좌표로 변환
            RaycastHit hit;

            Debug.Log("바닥플로어 설치 중");

            // 아래로 짧은 레이캐스트 발사 (0.1f 위에서 시작해 0.2f 거리만큼 검사)
            if (Physics.Raycast(worldPosition + Vector3.up * 0.1f, Vector3.down, out hit, 0.2f, LayerMask.GetMask("StairCollider")))
            {
                if (hit.collider != null)
                {
                    Debug.Log("설치 불가능: 계단과 충돌");
                    return false; // 계단 콜라이더와 충돌하면 설치 불가
                }
            }
            
            // 바닥 플로어를 배치하려는 경우, 해당 위치에 이미 다른 바닥 플로어가 있는지 확인
            if (!floorData.CanPlaceObjectAt(gridPosition, objectToPlace.Size, rotation, grid, placingWall))
            {
                Debug.Log("이미 그 자리에 바닥플로어가 있습니다.");
                return false;
            }
            
            return true;
        }

        // 장식품 설치 시
        if (objectToPlace.kindIndex == 3)
        {
            // 장식품을 배치하려는 경우, 해당 위치에 장식품이 있는지 확인
            if (!decoData.CanPlaceObjectAt(gridPosition, objectToPlace.Size, rotation, grid, placingWall))
            {
                return false;
            }
            return true;
        }

        // 가구를 설치 시
        if (!placingWall) 
        {
            // 가구의 실제 월드 좌표 기준 크기와 중심을 계산             
            GameObject tempObject = Instantiate(prefab, grid.GetCellCenterWorld(gridPosition), rotation);
        
             // 콜라이더를 가져옴
             Collider objCollider = tempObject.GetComponent<Collider>();
             if (objCollider is null)
             {
                 objCollider = tempObject.GetComponentInChildren<Collider>();
             }
    
             if (objCollider is null)
             {
                 Destroy(tempObject);
                 return furnitureData.CanPlaceObjectAt(gridPosition, objectToPlace.Size, rotation, grid, placingWall);
             }

            // 충돌 검사를 위해 콜라이더는 유지하고 렌더러는 비활성화
            Renderer[] renderers = tempObject.GetComponentsInChildren<Renderer>();
             foreach (Renderer renderer in renderers)
             {
                 renderer.enabled = false;
             }

            // 마진 값 조정 (더 작은 값으로 검사 영역 축소)
            float collisionMargin = 0.05f; // 5cm 마진
        
             Vector3 colliderCenter = objCollider.bounds.center;
             Vector3 colliderSize = objCollider.bounds.size - new Vector3(collisionMargin, collisionMargin, collisionMargin);
        
             // 디버그 시각화 - 실제 검사 영역 확인용
             Debug.DrawLine(colliderCenter - colliderSize/2, colliderCenter + colliderSize/2, Color.red, 2f);
        
             // 벽 레이어만 검사
             int wallLayerMask = LayerMask.GetMask("Wall");
             
             // 더 정확한 충돌 검사 방법 - 다중 Raycast 사용
             bool collision = false;
        
             // 콜라이더 경계 상자의 8개 코너에서 레이캐스트
             Vector3[] corners = new Vector3[8];
             corners[0] = new Vector3(-1, -1, -1);
             corners[1] = new Vector3(1, -1, -1);
             corners[2] = new Vector3(-1, 1, -1);
             corners[3] = new Vector3(1, 1, -1);
             corners[4] = new Vector3(-1, -1, 1);
             corners[5] = new Vector3(1, -1, 1);
             corners[6] = new Vector3(-1, 1, 1);
             corners[7] = new Vector3(1, 1, 1);
    
             // 콜라이더 경계에서 중심으로 레이캐스트
             foreach (Vector3 cornerDir in corners)
             {
                 Vector3 startPos = colliderCenter + Vector3.Scale(cornerDir, colliderSize / 2f);
                 Vector3 direction = -cornerDir.normalized;
                 float distance = colliderSize.magnitude / 2f;
            
                 Debug.DrawRay(startPos, direction * distance, Color.yellow, 2f);
            
                 if (Physics.Raycast(startPos, direction, distance, wallLayerMask))
                 {
                     collision = true;
                     Debug.DrawRay(startPos, direction * distance, Color.red, 2f);
                     break;
                 }
             }
    
             // 중심에서 6방향으로 추가 레이캐스트
             if (!collision)
             {
                 Vector3[] directions = new Vector3[] 
                 {
                     Vector3.right, Vector3.left, Vector3.up, 
                     Vector3.down, Vector3.forward, Vector3.back
                 };
            
                 foreach (Vector3 dir in directions)
                 {
                     float distance = Vector3.Scale(colliderSize / 2f, new Vector3(
                         Mathf.Abs(dir.x) > 0.01f ? .7f : 0,
                         Mathf.Abs(dir.y) > 0.01f ? .7f : 0,
                         Mathf.Abs(dir.z) > 0.01f ? .7f : 0
                     )).magnitude;
                
                     Debug.DrawRay(colliderCenter, dir * distance, Color.yellow, 2f);
                
                     if (Physics.Raycast(colliderCenter, dir, distance, wallLayerMask))
                     {
                         collision = true;
                         Debug.DrawRay(colliderCenter, dir * distance, Color.red, 2f);
                         break;
                     }
                 }
             }
    
             // 충돌이 감지되면 배치 불가
             if (collision)
             {
                 Debug.Log($"배치 불가: 가구가 벽과 충돌했습니다. 위치: {gridPosition}");
                 Destroy(tempObject);
                 return false;
             }
        
             // 오버랩박스로 마지막 확인 (더 작은 크기로)
             Collider[] hitColliders = Physics.OverlapBox(
                 colliderCenter,         
                 colliderSize / 4f,     // 더 작은 영역으로 검사
                 rotation,               
                 wallLayerMask          
             );
    
             // 충돌 감지 시 로그 출력 및 배치 금지
             if (hitColliders.Length > 0)
             {
                 //Debug.Log($"배치 불가: 가구가 벽과 충돌했습니다. 벽 개수: {hitColliders.Length}");
                 foreach (Collider hit in hitColliders)
                 {
                     Debug.Log($"- 충돌한 벽 오브젝트: {hit.gameObject.name}");
                 }
                 Destroy(tempObject);
                 return false;
             }
    
             // 임시 오브젝트 제거
             Destroy(tempObject);
        
             // GridData 기반 충돌 체크 - 다른 가구와의 충돌 확인
             if (!furnitureData.CanPlaceObjectAt(gridPosition, objectToPlace.Size, rotation, grid, placingWall))
             {
                 Debug.Log($"배치 불가: 해당 위치에 가구가 이미 존재합니다. 위치: {gridPosition}");
                 return false;
             }
        }

        if (placingWall)
        {
            // 1. 기존 다른 벽과의 충돌 검사
            if (!wallData.CanPlaceObjectAt(gridPosition, objectToPlace.Size, rotation, grid, placingWall))
            {
                Debug.Log($"벽 배치 불가: 같은 각도의 벽 충돌 at {gridPosition}");
                return false;
            }
            Debug.Log($"벽-벽 충돌 검사 통과 at {gridPosition}");

            // 2. 벽과 가구 간 충돌 검사 (Raycast 기반)
            GameObject tempObject = Instantiate(prefab, grid.GetCellCenterWorld(gridPosition), rotation);

            Collider wallCollider = tempObject.GetComponent<Collider>();
            if (wallCollider is null)
            {
                wallCollider = tempObject.GetComponentInChildren<Collider>();
            }

            if (wallCollider is null)
            {
                Debug.Log($"벽 배치 불가: 콜라이더 없음 at {gridPosition}");
                Destroy(tempObject);
                return false;
            }
            Debug.Log($"콜라이더 확인 완료: {wallCollider.gameObject.name}");

            // 충돌 검사를 위해 렌더러 비활성화
            Renderer[] renderers = tempObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }

            

            // 벽 콜라이더의 중심과 크기
            Vector3 colliderCenter = wallCollider.bounds.center;
            Vector3 colliderSize = wallCollider.bounds.size;

            // 벽의 바닥 위치 계산 (Raycast 발사 위치)
            Vector3 wallBottomCenter = colliderCenter - tempObject.transform.up * (colliderSize.y / 5f);

            // 디버그 시각화: 콜라이더 범위 및 발사 위치 표시
            Debug.DrawLine(colliderCenter - colliderSize / 2, colliderCenter + colliderSize / 2, Color.blue, 2f);
            Debug.DrawLine(wallBottomCenter - new Vector3(0.1f, 0, 0.1f), wallBottomCenter + new Vector3(0.1f, 0, 0.1f), Color.green, 2f);

            Vector3 leftRaycastOrigin1 = wallBottomCenter - tempObject.transform.forward * 1;
            Vector3 rightRaycastOrigin2 = wallBottomCenter + tempObject.transform.forward * 1;

            // Raycast 방향 설정 (벽의 로컬 아래 방향)
            Vector3 raycastDirection = -tempObject.transform.up;

            // Raycast 거리 계산: 벽 바닥에서 그리드 바닥까지의 거리
            float distance = Mathf.Abs(wallBottomCenter.y - grid.GetCellCenterWorld(gridPosition).y) + 1f;

            // 각 위치에서 Raycast 발사
            Vector3[] raycastOrigins = new Vector3[]
            {
        leftRaycastOrigin1, rightRaycastOrigin2
            };

            bool collision = false;
            foreach (Vector3 origin in raycastOrigins)
            {
                Debug.DrawRay(origin, raycastDirection * distance, Color.yellow, 2f);
                if (Physics.Raycast(origin, raycastDirection, distance, furnitureLayerMask))
                {
                    collision = true;
                    Debug.DrawRay(origin, raycastDirection * distance, Color.red, 2f);
                    Debug.Log($"벽 배치 불가: Raycast로 가구와 충돌 감지됨 at {gridPosition}, 방향: {raycastDirection}, 발사 위치: {origin}");
                    break;
                }
            }

            if (!collision)
            {
                Debug.Log($"벽-가구 충돌 검사 통과 at {gridPosition}");
            }

            // 충돌이 감지되면 벽 설치 차단
            if (collision)
            {
                Destroy(tempObject);
                return false;
            }

            Destroy(tempObject);
            Debug.Log($"벽 배치 가능: 모든 검사 통과 at {gridPosition}");
        }        

        Debug.Log("벽 검사 끝");
    
        // 모든 검사를 통과하면 배치 가능
        return true;
    }   
    
    #endregion

    #region 건축 종료
    private void StopPlacement()
    {
        selectedObjectIndex = -1;

        foreach (GameObject indicator in cellIndicators)
        {
            indicator.SetActive(false);
        }

        inputManager.OnClicked -= PlaceStructure;
        inputManager.OnExit -= StopPlacement;

        if (previewObject is not null)
        {
            Destroy(previewObject);
            previewObject = null;
        }

        // 드래그 상태 초기화
        isDragging = false;
        dragStartPosition = Vector3Int.zero;
    }
    #endregion    

    #region 프리뷰 변경
    private void PreviewObjectFunc()
    {
        bool placementValidity = CheckPlacementValidity(gridPosition, selectedObjectIndex, previewRotation);

        if (previewRenderer != null)
        {
            previewRenderer.material.color = placementValidity ? Color.white : Color.red;
        }
        else
        {
            Debug.LogWarning("previewRenderer가 null입니다.");
        }
        
        if (isDragging)
        {
            Vector3Int dragEndPosition = grid.WorldToCell(inputManager.GetSelectedMapPosition());
            UpdateDragPreview(dragStartPosition, dragEndPosition);
        }
        else if (previewObject is not null)
        {
            previewObject.transform.position = grid.GetCellCenterWorld(gridPosition);
            previewObject.transform.rotation = previewRotation;
            Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
            // 나중에 수정 - 프리뷰 점유 시 색 변경
            /*foreach (Renderer renderer in renderers)
            {
                renderer.material.color = placementValidity ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
            }*/
        }
        
        if (Input.GetKeyDown(KeyCode.R) && !isDragging)
        {
            previewRotation = Quaternion.Euler(0, previewRotation.eulerAngles.y + 90, 0);
            if (previewObject is not null) previewObject.transform.rotation = previewRotation;
            UpdateCellIndicators();
        }
        
        foreach (GameObject indicator in cellIndicators)
        {
            if (indicator.activeSelf)
            {
                Renderer renderer = indicator.GetComponentInChildren<Renderer>();
                if (renderer is not null)
                {
                    renderer.material.color = placementValidity ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 0f, 0f, 0.7f);
                }
            }
        }
    }
    #endregion

    #region 땅 구매

    /// <summary>
    /// 땅을 구매하는 버튼을 통해 실행되는 함수
    /// 버튼을 클릭하면 그리드 플레인이 활성화 + 그리드 건설 반경이 확대되며, 4번(최대 횟수) 클릭 시 버튼이 비활성화 된다.
    /// </summary>
    public void PurchaseNextLand()
    {
        currentPurchaseLevel++;
        Debug.Log(currentPurchaseLevel);

        ActivatePlanesByLevel(currentPurchaseLevel);
        UpdateGridBounds();

        if (currentPurchaseLevel >= 4)
        {
            Debug.Log("모든 땅이 구매되었습니다!");
            if (purchaseButton != null)
            {
                purchaseButton.gameObject.SetActive(false);
            }
            return;
        }

        Debug.Log($"현재 구매 단계: {currentPurchaseLevel}, 활성화된 Plane 수: {gridVisualization.Count}");
    }

    /// <summary>
    /// 플레인의 이름에서 뽑아낸 정수와 반환된 정수의 값이 같을 때, 그리드 플레인을 활성화한다. 
    /// </summary>
    /// <param name="level"></param>
    public void ActivatePlanesByLevel(int level)
    {
        foreach (GameObject planeObj in plane1f)
        {
            string planeName = planeObj.name;
            int planeLevel = ExtractLevelFromPlaneName(planeName);

            if (planeLevel == level)
            {
                planeObj.SetActive(true);
                Debug.Log($"활성화된 Plane: {planeName}");
            }
        }
    }

    /// <summary>
    /// 플레인의 이름에서 _를 기준으로 숫자를 뽑아내어 반환한다.
    /// </summary>
    /// <param name="planeName"></param>
    /// <returns></returns>
    private int ExtractLevelFromPlaneName(string planeName)
    {
        string[] parts = planeName.Split('_');
        if (parts.Length > 1 && int.TryParse(parts[1], out int level))
        {
            return level;
        }
        Debug.LogWarning($"Plane 이름에서 레벨을 추출할 수 없습니다: {planeName}");
        return 0;
    }
    #endregion

    #region 2층 구매

    // 땅이 전부 구매된 후에 2층 해금이 풀림
    // 해금 해제 후에는 2층의 땅을 모두 구입할 필요가 없기 때문에 전부다 비활성화 후 한꺼번에 활성화가 가능
    // 활성화가 된 후에 그리드 바운드가 업데이트 되어야만 함
    // 사라질 때 함께 사라지도록 테스트 필요
    // 오브젝트 활성화 후 그리드바운드만 작동되도록 테스트 = 성공
    // 그러면 ActivatePlanesByLevel 함수가 필요 없음
    // 따라서 2층을 구매하면 리스트들이 전부 활성화가 되며, 업데이트 그리드 바운드 함수가 실행되도록 테스트 시작 = 성공
    // 성공함으로서 2층 해금 시, 건축모드에서는 2층 그리드가 활성화가 됌
    // 조건은 무조건 땅을 모두 구매한 후에 해금 되도록 설정


    /// <summary>
    /// 버튼의 기능으로서, 함수가 실행되면 2층 플레인이 모두 활성화가 되며, 그리드 건설 반경이 확대된다.
    /// </summary>
    private void PurchaseOtherFloor()
    {
        if (!FloorLock) return;

        if (currentPurchaseLevel < 4)
        {
            Debug.LogWarning("모든 땅을 구매한 후에만 2층을 구매할 수 있습니다.");
            return;
        }
        
        changeFloorButton.SetActive(FloorLock);
        UpdateGridBounds();
    }

    #endregion

    #region 건설 상태
    /// <summary>
    /// 건설모드가 되었을 때, 건설UI와 그리드를 활성화한다.
    /// </summary>
    public void EnterBuildMode()
    {
        inputManager.isBuildMode = true;
        inputManager.BuildUI.SetActive(true);

        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(true);
        }

        Debug.Log("건설 상태 진입: BuildUI와 Grid 활성화");
    }

    /// <summary>
    /// 건설모드에서 해제 되었을 때, 건설UI와 그리드를 비활성화한다.
    /// </summary>
    public void ExitBuildMode()
    {
        inputManager.isBuildMode = false;
        StopPlacement();
        inputManager.BuildUI.SetActive(false);

        foreach (GameObject gridVisual in gridVisualization)
        {
            gridVisual.SetActive(false);
        }

        Debug.Log("건설 상태 종료: BuildUI와 Grid 비활성화");
    }

    #endregion
    
    #region 드래그 건축 
    
    private bool isDragging = false;
    private Vector3Int dragStartPosition;

    /// <summary>
    /// 드래그 건축이 가능하게 하는 인풋과 로직 함수
    /// </summary>
    void DragPlacement()
    {
        // 건축 모드가 아니면 드래그 중지
        if (!inputManager.isBuildMode)
        {
            if (isDragging)
            {
                isDragging = false;
                dragStartPosition = Vector3Int.zero;
                UpdateDragPreview(gridPosition, gridPosition); // 프리뷰 초기화
            }
            return;
        }

        

        // 드래그 시작
        if (Input.GetMouseButtonDown(0) && !inputManager.IsPointerOverUI())
        {
            isDragging = true;
            dragStartPosition = grid.WorldToCell(inputManager.GetSelectedMapPosition());
            inputManager.OnClicked -= PlaceStructure;
        }


        // 드래그 종료 및 배치
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            Vector3Int dragEndPosition = grid.WorldToCell(inputManager.GetSelectedMapPosition());

            if (inputManager.isBuildMode) // 건축 모드일 때만 배치 실행
            {
                PlaceLineStructure(dragStartPosition, dragEndPosition);
            }
            else
            {
                // 건축 모드가 종료된 경우 드래그 취소
                dragStartPosition = Vector3Int.zero;
                UpdateDragPreview(gridPosition, gridPosition); // 프리뷰 초기화
                inputManager.OnClicked += PlaceStructure;
            }
        }

    }
    
    /// <summary>
    /// 드래그로 인한 배치 시 직선 배치 로직 함수
    /// </summary>
    /// <param name="startPos">시작 시점</param>
    /// <param name="endPos">끝 시점</param>
    private void PlaceLineStructure(Vector3Int startPos, Vector3Int endPos)
    {
        if (selectedObjectIndex < 0 || !inputManager.isBuildMode) return;

        Vector2Int objectSize = database.objectsData[selectedObjectIndex].Size;
        bool isWall = database.objectsData[selectedObjectIndex].IsWall;
        selectedData = GetSelectedGridData();

        // X축 또는 Z축 중 더 긴 방향으로만 배치 (간단한 직선 드래그)
        int dx = Mathf.Abs(endPos.x - startPos.x);
        int dz = Mathf.Abs(endPos.z - startPos.z);
        bool alongX = dx >= dz;

        //int stepCount = alongX ? dx + 1 : dz + 1;
        int stepCount = (dx == 0 && dz == 0) ? 1 : (alongX ? dx + 1 : dz + 1);
        /*Vector3Int stepDirection = alongX ? new Vector3Int((int)Mathf.Sign(endPos.x - startPos.x), 0, 0) 
            : new Vector3Int(0, 0, (int)Mathf.Sign(endPos.z - startPos.z));*/
        Vector3Int stepDirection = (dx == 0 && dz == 0) ? Vector3Int.zero :
                               alongX ? new Vector3Int((int)Mathf.Sign(endPos.x - startPos.x), 0, 0) :
                               new Vector3Int(0, 0, (int)Mathf.Sign(endPos.z - startPos.z));

        Vector3Int currentPos = startPos;
        for (int i = 0; i < stepCount; i++)
        {
            if (CheckPlacementValidity(currentPos, selectedObjectIndex, previewRotation))
            {
                Vector3 worldPosition = grid.GetCellCenterWorld(currentPos);
                int index = objectPlacer.PlaceObject(database.objectsData[selectedObjectIndex].Prefab, worldPosition, previewRotation);
                PlayerWallet.Instance.SpendMoney(database.objectsData[selectedObjectIndex].BuildPrice);
                selectedData.AddObjectAt(currentPos, objectSize, database.objectsData[selectedObjectIndex].ID, index, 
                database.objectsData[selectedObjectIndex].kindIndex, previewRotation, grid, isWall);
            }
            currentPos += stepDirection;
        }
    }
    
    private GridData GetSelectedGridData()
    {
        if (selectedObjectIndex < 0 || selectedObjectIndex >= database.objectsData.Count) return null; // 예외 처리 추가

        switch (database.objectsData[selectedObjectIndex].kindIndex)
        {
            case 0: return floorData;
            case 1: return furnitureData;
            case 2: return wallData;
            case 3: return decoData;
            default:
                Debug.LogWarning($"Unknown kindIndex: {database.objectsData[selectedObjectIndex].kindIndex}");
                return furnitureData; // 기본값 또는 null 반환 등 결정 필요
        }
    }
    
    private void UpdateDragPreview(Vector3Int startPos, Vector3Int endPos)
    {
        Vector2Int objectSize = database.objectsData[selectedObjectIndex].Size;
        int dx = Mathf.Abs(endPos.x - startPos.x);
        int dz = Mathf.Abs(endPos.z - startPos.z);
        bool alongX = dx >= dz;
        int stepCount = alongX ? dx + 1 : dz + 1;
        Vector3Int stepDirection = alongX ? new Vector3Int((int)Mathf.Sign(endPos.x - startPos.x), 0, 0) 
            : new Vector3Int(0, 0, (int)Mathf.Sign(endPos.z - startPos.z));

        int requiredIndicators = stepCount * objectSize.x * objectSize.y;
        while (cellIndicators.Count < requiredIndicators)
        {
            GameObject newIndicator = Instantiate(cellIndicatorPrefab, transform);
            cellIndicators.Add(newIndicator);
        }

        Vector3Int currentPos = startPos;
        int indicatorIndex = 0;
        for (int i = 0; i < stepCount; i++)
        {
            List<Vector3Int> positions = floorData.CalculatePosition(currentPos, objectSize, previewRotation, grid);
            foreach (Vector3Int pos in positions)
            {
                if (indicatorIndex < cellIndicators.Count)
                {
                    cellIndicators[indicatorIndex].SetActive(true);
                    cellIndicators[indicatorIndex].transform.position = grid.GetCellCenterWorld(pos) + new Vector3(0, 0.002f,0);
                    cellIndicators[indicatorIndex].transform.rotation = Quaternion.Euler(90, 0, 0);
                    indicatorIndex++;
                }
            }
            currentPos += stepDirection;
        }

        for (int i = indicatorIndex; i < cellIndicators.Count; i++)
        {
            cellIndicators[i].SetActive(false);
        }
    }

    #endregion

    #region 삭제 모드

    //public bool isDeleteMode = false;

    public void StartDeleteMode()
    {
        StopPlacement(); // 기존 배치 모드 종료
        inputManager.isDeleteMode = true;
        inputManager.OnClicked += DeleteStructure; // 클릭 시 삭제 함수 호출
        inputManager.OnExit += StopDeleteMode;
        mouseIndicator.SetActive(true); // 인디케이터 활성화
        Debug.Log("삭제 모드 시작");
    }

    public void StopDeleteMode()
    {
        inputManager.isDeleteMode = false;
        inputManager.OnClicked -= DeleteStructure;
        inputManager.OnExit -= StopDeleteMode;
        mouseIndicator.SetActive(false); // 인디케이터 비활성화
        Debug.Log("삭제 모드 종료");
    }

    private void DeleteStructure()
    {
        if (inputManager.IsPointerOverUI())
            return;

        GameObject clickedObject = inputManager.GetClickedObject();

        Debug.Log($"삭제하려는 오브젝트 : {clickedObject}");

        if (clickedObject is null)
        {
            Debug.Log("삭제할 오브젝트가 없습니다.");
            return;
        }

        // ObjectPlacer에서 오브젝트 인덱스 찾기
        int objectIndex = objectPlacer.GetObjectIndex(clickedObject);
        Debug.Log($"삭제하려는 오브젝트의 인덱스 : {objectIndex}");
        if (objectIndex < 0)
        {
            Debug.LogWarning("클릭한 오브젝트가 ObjectPlacer에 등록되지 않음.");
            return;
        }

        // 적절한 GridData 선택
        GridData selectedData = FindGridDataByObjectIndex(objectIndex);
        Debug.Log($"삭제하려는 오브젝트 그리드 데이터 : {selectedData}");
        if (selectedData is null)
        {
            Debug.LogWarning("해당 오브젝트의 GridData를 찾을 수 없음.");
            return;
        }

        // GridData에서 오브젝트의 ID 가져오기
        int objectID = -1;
        foreach (var kvp in selectedData.placedObjects)
        {
            foreach (var data in kvp.Value)
            {
                if (data.PlacedObjectIndex == objectIndex)
                {
                    objectID = data.ID;
                    break;
                }
            }
            if (objectID != -1) break;
        }

        if (objectID == -1)
        {
            Debug.LogWarning("GridData에서 오브젝트의 ID를 찾을 수 없음.");
            return;
        }

        // ObjectsDatabaseSO에서 ObjectData 조회
        ObjectData objectData = database.GetObjectData(objectID);
        if (objectData is null)
        {
            Debug.LogWarning($"ID {objectID}에 해당하는 ObjectData를 찾을 수 없음.");
            return;
        }

        // GridData에서 데이터 제거
        if (selectedData.RemoveObjectByIndex(objectIndex))
        {
            // ObjectPlacer에서 오브젝트 제거
            objectPlacer.RemoveObject(objectIndex);
            PlayerWallet.Instance.AddMoney(objectData.BuildPrice);
            Debug.Log($"오브젝트 삭제 완료: 인덱스 {objectIndex}");
        }
        else
        {
            Debug.LogWarning("GridData에서 오브젝트 데이터를 제거하지 못함.");
        }
    }

    public GridData FindGridDataByObjectIndex(int objectIndex)
    {
        // floorData 확인
        if (floorData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return floorData;

        // furnitureData 확인
        if (furnitureData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return furnitureData;

        // wallData 확인
        if (wallData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return wallData;

        // decoData 확인
        if (decoData.placedObjects.AsValueEnumerable().Any(kvp => kvp.Value.AsValueEnumerable().Any(data => data.PlacedObjectIndex == objectIndex)))
            return decoData;

        return null;
    }

    // database를 public으로 접근 가능하게 하거나 getter 추가
    public ObjectsDatabaseSO GetDatabase()
    {
        return database;
    }
    #endregion

    #region 그리드바운드 시각화 (디버그 끝날 시 삭제 예정)

    // Gizmos 색상 설정 (Inspector에서 조정 가능)
    [Header("디버깅 설정")]
    [SerializeField] private Color boundsColor = Color.green;
    
    // Gizmos로 planeBounds 시각화
    private void OnDrawGizmos()
    {
        Gizmos.color = boundsColor; // 색상 설정

        foreach (Bounds bound in planeBounds)
        {
            Gizmos.DrawWireCube(bound.center, bound.size); // Bounds의 외곽선을 그림
        }
    }
    
    #endregion

    #region 그리드 층 가리기

    public void HidePlane(int floor)
    {
        // 플레인 비활성화
        foreach (GameObject planeObj in plane1f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(floor == 1);
            }
        }

        foreach (GameObject planeObj in plane2f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(floor == 2);
            }
        }

        foreach (GameObject planeObj in plane3f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(floor == 3);
            }
        }

        foreach (GameObject planeObj in plane4f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(floor == 4);
            }
        }
    }
    
    // 모든 플레인을 비활성화하는 메서드
    public void HideAllPlanes()
    {
        foreach (GameObject planeObj in plane1f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(false);
            }
        }

        foreach (GameObject planeObj in plane2f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(false);
            }
        }

        foreach (GameObject planeObj in plane3f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(false);
            }
        }

        foreach (GameObject planeObj in plane4f)
        {
            if (planeObj is not null)
            {
                planeObj.SetActive(false);
            }
        }
    }

    #endregion

    #region get,set

    public bool GetFloorLock()
    {
        return FloorLock;
    }

    // 또는 getter 메서드 추가
    public GameObject GetCellIndicatorPrefab()
    {
        return cellIndicatorPrefab;
    }

    public GameObject GetMouseIndicator()
    {
        return mouseIndicator;
    }

    #endregion

    #region 삭제 모드 시 마우스 상태

    private void DeliteModeMouth()
    {
        // 삭제 모드일 때 마우스 인디케이터 업데이트
        if (inputManager.isDeleteMode)
        {
            Vector3 mousePosition = inputManager.GetSelectedMapPosition();
            mouseIndicator.transform.position = mousePosition;
            
            // 마우스 위치로 오브젝트 감지
            GameObject newHighlightedObject = inputManager.GetClickedObject();
            if (newHighlightedObject != highlightedObject)
            {
                // 이전 오브젝트 비주얼 복원
                if (highlightedObject != null)
                {
                    ResetObjectVisual(highlightedObject);
                }

                // 새로운 오브젝트 하이라이트
                highlightedObject = newHighlightedObject;
                if (highlightedObject != null)
                {
                    HighlightObject(highlightedObject);
                }
            }
        }
    }

    #endregion
    
    #region 오브젝트 하이라이트
    // 오브젝트 하이라이트
    private void HighlightObject(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material.color = new Color(1f, 0.5f, 0.5f, 1f); // 약간 붉은색으로 변경
        }
    }

    // 오브젝트 비주얼 복원
    private void ResetObjectVisual(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material.color = Color.white; // 기본 색상으로 복원
        }
    }
    #endregion
}