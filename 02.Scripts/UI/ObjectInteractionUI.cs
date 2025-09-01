using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using ZLinq;

public class ObjectInteractionUI : MonoBehaviour
{
    [Header("UI 관련 오브젝트")]
    [SerializeField] private GameObject interactionPanel;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button rotateButton;
    [SerializeField] private Button destroyButton;
    [SerializeField] private Canvas worldCanvas;

    [Header("컴포넌트")]
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private ObjectPlacer objectPlacer;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private Camera mainCamera;

    // 이동 시 필요한 원본 데이터 저장
    private PlacementData originalPlacementData;
    private GridData originalGridData;
    private Vector3Int originalGridPosition;
    
    // 변수
    private GameObject selectedObject;
    private int selectedObjectIndex;
    private bool isMoving = false;
    private bool isRotating = false;
    private bool justShown = false; // UI가 방금 표시되었는지 확인하는 플래그
    private bool isInteracting = false;  

    [Header("이동 모드 프리뷰")]
    [SerializeField] private GameObject cellIndicatorPrefab; // PlacementSystem과 동일한 프리팹 사용
    [SerializeField] private GameObject mouseIndicator; // 마우스 인디케이터

    // 이동 모드 프리뷰 관련
    private GameObject movePreviewObject;
    private List<GameObject> moveCellIndicators = new List<GameObject>();
    private Renderer previewRenderer;
    private Vector3Int currentGridPosition;

    private void Start()
    {
        InitializeUI();
        SetupButtonEvents();

        // PlacementSystem에서 프리팹 참조 가져오기
        if (cellIndicatorPrefab == null && placementSystem != null)
        {
            // PlacementSystem의 cellIndicatorPrefab을 참조
            cellIndicatorPrefab = placementSystem.cellIndicatorPrefab;
        }

        if (mouseIndicator == null && placementSystem != null)
        {
            mouseIndicator = placementSystem.mouseIndicator;
        }
    }

    private void InitializeUI()
    {
        if (interactionPanel != null)
            interactionPanel.SetActive(false);
    }

    private void SetupButtonEvents()
    {
        moveButton?.onClick.AddListener(StartMoveMode);
        rotateButton?.onClick.AddListener(RotateObject);
        destroyButton?.onClick.AddListener(DestroyObject);
    }

    private GameObject currentSelectedObject; // 현재 선택된 오브젝트 추적

    public bool IsUIActive()
    {
        return interactionPanel != null && interactionPanel.activeInHierarchy;
    }

    public bool IsSameObjectSelected(GameObject targetObject)
    {
        return currentSelectedObject == targetObject;
    }

    public void ShowInteractionUI(GameObject targetObject, Vector3 worldPosition)
    {

        Debug.Log("ShowInteractionUI 시작");
        selectedObject = targetObject;

        Debug.Log($"{selectedObject.name}이 선택되어있음");

        selectedObjectIndex = objectPlacer.GetObjectIndex(targetObject);

        Debug.Log($"{selectedObject.name}의 인덱스는  {selectedObjectIndex} 임");

        if (selectedObjectIndex < 0)
        {
            Debug.LogWarning("선택된 오브젝트가 ObjectPlacer에 등록되지 않음");
            return;
        }

        // 원본 데이터 저장
        StoreOriginalData();

        // 오브젝트의 경계 상단(Bounds)을 기준으로 머리 위 위치 계산
        Renderer renderer = selectedObject.GetComponentInChildren<Renderer>();
        Vector3 headPosition = worldPosition;
        if (renderer is not null)
        {
            headPosition = renderer.bounds.center + Vector3.up * (renderer.bounds.extents.y + 0.5f); ; // 상단에 약간 여유 추가
        }
        else
        {
            headPosition = worldPosition + Vector3.up * 2f; // 대체 위치
        }

        // 월드 좌표를 화면 좌표로 변환
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(headPosition);
        Debug.Log($"UI 위치: {screenPosition}");
        interactionPanel.transform.position = screenPosition;

        // UI 표시 플래그 설정
        justShown = true;

        // UI 애니메이션으로 표시
        interactionPanel.SetActive(true);
        interactionPanel.transform.localScale = Vector3.zero;
        interactionPanel.transform.DOScale(Vector3.one * 1.5f , 0.3f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => {
                // 애니메이션 완료 후 클릭 감지 활성화
                justShown = false;
            });
    }

    public void HideInteractionUI()
    {
        if (interactionPanel.activeInHierarchy)
        {
            interactionPanel.transform.DOScale(Vector3.zero, 0.1f)
                .SetEase(Ease.InBack)
                .OnComplete(() => interactionPanel.SetActive(false));
        }

        selectedObject = null;
        selectedObjectIndex = -1;
        StopAllModes();
    }

    private void StartMoveMode()
    {
        if (isInteracting) return;

        isInteracting = true;
        isMoving = true;
        isRotating = false;
        Debug.Log("이동 모드 시작");
        StartMovePreview();
        interactionPanel.SetActive(false);
    }

    private void StartMovePreview()
    {
        if (originalPlacementData == null) return;

        // 오브젝트 데이터 가져오기
        var objectData = placementSystem.database.GetObjectData(originalPlacementData.ID);
        if (objectData == null) return;

        // 프리뷰 오브젝트 생성
        movePreviewObject = Instantiate(objectData.Prefab);
        ApplyPreviewMaterial(movePreviewObject);

        // 마우스 인디케이터 활성화
        if (mouseIndicator != null)
            mouseIndicator.SetActive(true);

        Debug.Log("이동 프리뷰 시작");
    }
    private void StopMovePreview()
    {
        // 프리뷰 오브젝트 제거
        if (movePreviewObject != null)
        {
            Destroy(movePreviewObject);
            movePreviewObject = null;
        }

        // 셀 인디케이터 비활성화
        foreach (GameObject indicator in moveCellIndicators)
        {
            if (indicator != null)
                indicator.SetActive(false);
        }

        // 마우스 인디케이터 비활성화
        if (mouseIndicator != null)
            mouseIndicator.SetActive(false);
        
        isInteracting = false;
        isMoving = false;
        

        Debug.Log("이동 프리뷰 종료");
    }

    private void ApplyPreviewMaterial(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material[] originalMat = renderer.materials;
            Material[] newMaterial = new Material[originalMat.Length];
            for (int i = 0; i < originalMat.Length; i++)
            {
                // URP 투명 설정
                originalMat[i].SetFloat("_Surface", 1);
                originalMat[i].SetFloat("_Blend", 1);
                newMaterial[i] = originalMat[i];
            }
            renderer.materials = newMaterial;
        }
    }

    private void UpdateMovePreview()
    {
        if (!isMoving || movePreviewObject == null || originalPlacementData == null) return;

        // 마우스 위치를 그리드 좌표로 변환
        Vector3 mousePosition = GetMouseWorldPosition();
        currentGridPosition = placementSystem.grid.WorldToCell(mousePosition);

        // 디버그 로그 추가
        Debug.Log($"마우스 월드 위치: {mousePosition}, 그리드 위치: {currentGridPosition}");

        // 마우스 인디케이터 위치 업데이트
        if (mouseIndicator != null)
        { 
            mouseIndicator.transform.position = mousePosition;
            Debug.Log($"마우스 인디케이터 위치: {mouseIndicator.transform.position}");
        }

        // 프리뷰 오브젝트 위치 업데이트
        Vector3 previewWorldPosition = placementSystem.grid.GetCellCenterWorld(currentGridPosition);
        movePreviewObject.transform.position = previewWorldPosition;
        movePreviewObject.transform.rotation = originalPlacementData.Rotation;

        Debug.Log($"프리뷰 오브젝트 위치: {movePreviewObject.transform.position}");
        // 배치 가능 여부 확인
        bool canPlace = CanMoveToPosition(currentGridPosition);

        // 셀 인디케이터 업데이트
        UpdateMoveCellIndicators(canPlace);

        // 프리뷰 색상 업데이트
        UpdatePreviewColor(canPlace);
    }

    private void UpdateMoveCellIndicators(bool canPlace)
    {
        if (originalPlacementData == null) return;

        var objectData = placementSystem.database.GetObjectData(originalPlacementData.ID);
        if (objectData == null) return;

        // 필요한 인디케이터 개수 계산
        int requiredIndicators = objectData.Size.x * objectData.Size.y;

        // 인디케이터 부족하면 생성
        while (moveCellIndicators.Count < requiredIndicators)
        {
            if (cellIndicatorPrefab != null)
            {
                GameObject newIndicator = Instantiate(cellIndicatorPrefab, transform);
                moveCellIndicators.Add(newIndicator);
            }
        }

        // 점유 위치 계산
        List<Vector3Int> positions = originalGridData.CalculatePosition(
            currentGridPosition,
            objectData.Size,
            originalPlacementData.Rotation,
            placementSystem.grid
        );

        // 인디케이터 위치 및 색상 설정
        for (int i = 0; i < moveCellIndicators.Count; i++)
        {
            if (i < positions.Count)
            {
                moveCellIndicators[i].SetActive(true);
                moveCellIndicators[i].transform.position = placementSystem.grid.GetCellCenterWorld(positions[i]) + new Vector3(0, 0.002f, 0);
                moveCellIndicators[i].transform.rotation = Quaternion.Euler(90, 0, 0);

                // 색상 설정
                Renderer renderer = moveCellIndicators[i].GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = canPlace ? new Color(1f, 1f, 1f, 0.35f) : new Color(1f, 0f, 0f, 0.5f);
                }
            }
            else
            {
                moveCellIndicators[i].SetActive(false);
            }
        }
    }

    private void UpdatePreviewColor(bool canPlace)
    {
        if (movePreviewObject == null) return;

        Renderer[] renderers = movePreviewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material.color = canPlace ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 0f, 0f, 0.5f);
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        return inputManager.GetSelectedMapPosition();
    }

    private void RotateObject()
    {
        if (isInteracting) return; // 인터랙션 중이면 무시
        if (selectedObject == null) return;

        isInteracting = true;
        isRotating = true;

        // 90도씩 회전
        Quaternion newRotation = selectedObject.transform.rotation * Quaternion.Euler(0, 90, 0);
        selectedObject.transform.DORotate(newRotation.eulerAngles, 0.5f)
        .SetEase(Ease.OutQuad)
        .OnComplete(() =>
        {
            isRotating = false;
            isInteracting = false; // 회전 완료 후 인터랙션 해제
            UpdateObjectRotationInGridData(newRotation);
            ShowInteractionUI(selectedObject, selectedObject.transform.position); // UI 다시 표시
        });
        
        // selectedObject.transform.DORotate(newRotation.eulerAngles, 0.5f).SetEase(Ease.OutQuad);
       
        // GridData에서 회전 정보 업데이트
        //UpdateObjectRotationInGridData(newRotation);
    }

    private void DestroyObject()
    {
        if (isInteracting) return;
        if (selectedObject == null || selectedObjectIndex < 0) return;

        isInteracting = true;
        
        // 기존 삭제 로직 활용
        GridData selectedData = FindGridDataByObjectIndex(selectedObjectIndex);
        if (selectedData != null)
        {
            // 환불 처리를 위한 ObjectData 조회
            int objectID = GetObjectIDFromGridData(selectedData, selectedObjectIndex);
            if (objectID != -1)
            {
                var objectData = placementSystem.database.GetObjectData(objectID);
                if (objectData != null)
                {
                    // 삭제 애니메이션
                    selectedObject.transform.DOScale(Vector3.zero, 0.3f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() =>
                        {
                            selectedData.RemoveObjectByIndex(selectedObjectIndex);
                            objectPlacer.RemoveObject(selectedObjectIndex);
                            PlayerWallet.Instance.AddMoney(objectData.BuildPrice);
                            isInteracting = false;
                            HideInteractionUI();
                        });
                }
            }
        }
    }

    private void Update()
    {
        HandleMovement();
        HandleUIPositionUpdate();
        HandleClickOutside();

        // 이동 모드일 때 프리뷰 업데이트
        if (isMoving)
        {
            UpdateMovePreview(); // 여기서 호출됨
        }
    }

    [SerializeField] private LayerMask objectLayer;

    private void HandleMovement()
    {
        if (!isMoving || selectedObject == null) return;

        foreach (GameObject gridVisual in placementSystem.gridVisualization)
        {
            gridVisual.SetActive(true);
        }

        Vector3 mousePosition = Input.mousePosition;
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        Debug.Log("인터렉션UI 에서 클릭했음");


        if (Input.GetMouseButtonDown(0))
        { 
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, objectLayer))
            {
                //Vector3Int newGridPosition = placementSystem.grid.WorldToCell(hit.point);
                //Vector3 newWorldPosition = placementSystem.grid.GetCellCenterWorld(newGridPosition);

                Debug.Log("인터렉션UI-HandleMovement 에서클릭했음");

                // 이동 가능 여부 검사
                if (CanMoveToPosition(currentGridPosition))
                {
                    Debug.Log("인터렉션UI-CanMoveToPosition(newGridPosition) 에서클릭했음");
                    // 기존 위치에서 GridData 제거
                    RemoveFromCurrentGridData();

                    // 새 위치로 이동
                    Vector3 newWorldPosition = placementSystem.grid.GetCellCenterWorld(currentGridPosition);
                    selectedObject.transform.DOMove(newWorldPosition, 0.5f).SetEase(Ease.OutQuad);

                    // 새 위치에 GridData 추가
                    AddToNewGridData(currentGridPosition);
                    // 새 위치에 GridData 추가
                    //AddToNewGridData(newGridPosition);

                    isMoving = false;
                    StopMovePreview();
                    //ResetObjectHighlight(selectedObject);

                    // 상호작용 UI 다시 표시
                    ShowInteractionUI(selectedObject, newWorldPosition);

                    foreach (GameObject gridVisual in placementSystem.gridVisualization)
                    {
                        gridVisual.SetActive(false);
                    }
                }
            }
        }


        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isMoving = false;
            StopMovePreview();
            //ResetObjectHighlight(selectedObject);

            // 상호작용 UI 다시 표시
            ShowInteractionUI(selectedObject, selectedObject.transform.position);

            foreach (GameObject gridVisual in placementSystem.gridVisualization)
            {
                gridVisual.SetActive(false);
            }
        }
    }

    private void HandleUIPositionUpdate()
    {
        if (selectedObject != null && interactionPanel.activeInHierarchy && !isMoving)
        {
            // 오브젝트의 머리 위 위치 계산
            Renderer renderer = selectedObject.GetComponentInChildren<Renderer>();
            Vector3 headPosition = selectedObject.transform.position;
            if (renderer != null)
            {
                headPosition = renderer.bounds.center + Vector3.up * (renderer.bounds.extents.y + 0.5f);
            }
            else
            {
                headPosition = selectedObject.transform.position + Vector3.up * 2f;
            }

            // 월드 좌표를 화면 좌표로 변환
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(headPosition);
            interactionPanel.transform.position = screenPosition;

            // 카메라를 향하도록 회전 (필요 시)
            if (mainCamera != null)
            {
                Vector3 lookDirection = mainCamera.transform.position - selectedObject.transform.position;
                lookDirection.y = 0;
                if (lookDirection != Vector3.zero)
                {
                    interactionPanel.transform.rotation = Quaternion.identity;
                }
            }
        }
    }

    /*private void HandleUIPositionUpdate()
    {
        if (selectedObject != null && interactionPanel.activeInHierarchy)
        {
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(
                selectedObject.transform.position + Vector3.up * 2f
            );
            interactionPanel.transform.position = screenPosition;
        }
    }*/

    private void HandleClickOutside()
    {
        if (justShown) return;

        if (Input.GetMouseButtonDown(0) && !isMoving && interactionPanel.activeInHierarchy)
        {
            Vector2 mousePosition = Input.mousePosition;
            if (!RectTransformUtility.RectangleContainsScreenPoint(
                interactionPanel.GetComponent<RectTransform>(),
                mousePosition))
            {
                HideInteractionUI();
            }
        }
    }

    private void StopAllModes()
    {
        isMoving = false;
        isRotating = false;
        StopMovePreview();


        if (selectedObject != null)
            ResetObjectHighlight(selectedObject);
    }

    private void HighlightObject(GameObject obj, Color color)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material.color = color;
        }
    }

    private void ResetObjectHighlight(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            renderer.material.color = Color.white;
        }
    }

    // 원본 데이터 저장
    private void StoreOriginalData()
    {
        originalGridData = FindGridDataByObjectIndex(selectedObjectIndex);
        if (originalGridData != null)
        {
            // 현재 그리드 위치 계산
            originalGridPosition = placementSystem.grid.WorldToCell(selectedObject.transform.position);

            // PlacementData 찾기
            foreach (var kvp in originalGridData.placedObjects)
            {
                foreach (var data in kvp.Value)
                {
                    if (data.PlacedObjectIndex == selectedObjectIndex)
                    {
                        originalPlacementData = data;
                        return;
                    }
                }
            }
        }
    }

    // 기존 PlacementSystem의 메서드들을 참조하여 구현
    private bool CanMoveToPosition(Vector3Int gridPosition)
    {
        if (originalPlacementData == null) return false;

        // 오브젝트 데이터 가져오기
        var objectData = placementSystem.database.GetObjectData(originalPlacementData.ID);
        if (objectData == null) return false;

        // 현재 위치와 같으면 이동 불가
        if (gridPosition == originalGridPosition) return false;
        
        // 전체 점유 셀 계산
        List<Vector3Int> positions = originalGridData.CalculatePosition(
            gridPosition,
            objectData.Size,
            originalPlacementData.Rotation,
            placementSystem.grid
        );
        
        // 모든 점유 셀이 planeBounds 내에 있는지 확인
        bool isWithinBounds = positions.AsValueEnumerable().All(pos =>
        {
            Vector3 worldPos = placementSystem.grid.GetCellCenterWorld(pos);
            return placementSystem.planeBounds.AsValueEnumerable().Any(bound => bound.Contains(worldPos));
        });

        if (!isWithinBounds)
        {
            return false;
        }

        // 임시로 현재 위치에서 제거
        RemoveFromCurrentGridData();

        // 새 위치에 배치 가능한지 확인
        bool canPlace = originalGridData.CanPlaceObjectAt(
            gridPosition,
            objectData.Size,
            originalPlacementData.Rotation,
            placementSystem.grid,
            objectData.IsWall
        );

        // 다시 원래 위치에 추가 (검사용이므로)
        if (originalPlacementData != null)
        {
            foreach (var pos in originalPlacementData.occupiedPositions)
            {
                if (!originalGridData.placedObjects.ContainsKey(pos))
                    originalGridData.placedObjects[pos] = new System.Collections.Generic.List<PlacementData>();
                originalGridData.placedObjects[pos].Add(originalPlacementData);
            }
        }

        return canPlace;
    }

    private void RemoveFromCurrentGridData()
    {
        if (originalGridData == null || originalPlacementData == null) return;

        // 현재 위치에서 PlacementData 제거
        foreach (var pos in originalPlacementData.occupiedPositions)
        {
            if (originalGridData.placedObjects.ContainsKey(pos))
            {
                originalGridData.placedObjects[pos].RemoveAll(data =>
                    data.PlacedObjectIndex == selectedObjectIndex);

                // 빈 리스트면 키 제거
                if (originalGridData.placedObjects[pos].Count == 0)
                {
                    originalGridData.placedObjects.Remove(pos);
                }
            }
        }
    }

    private void AddToNewGridData(Vector3Int newPosition)
    {
        if (originalGridData == null || originalPlacementData == null) return;

        // 오브젝트 데이터 가져오기
        var objectData = placementSystem.database.GetObjectData(originalPlacementData.ID);
        if (objectData == null) return;

        // 새 위치 계산
        var newPositions = originalGridData.CalculatePosition(
            newPosition,
            objectData.Size,
            originalPlacementData.Rotation,
            placementSystem.grid
        );

        // 새 PlacementData 생성
        PlacementData newPlacementData = new PlacementData(
            newPositions,
            originalPlacementData.ID,
            originalPlacementData.PlacedObjectIndex,
            originalPlacementData.KindIndex,
            originalPlacementData.Rotation
        );

        // 새 위치에 추가
        foreach (var pos in newPositions)
        {
            if (!originalGridData.placedObjects.ContainsKey(pos))
                originalGridData.placedObjects[pos] = new System.Collections.Generic.List<PlacementData>();
            originalGridData.placedObjects[pos].Add(newPlacementData);
        }

        // 원본 데이터 업데이트
        originalPlacementData = newPlacementData;
        originalGridPosition = newPosition;
    }

    private void UpdateObjectRotationInGridData(Quaternion newRotation)
    {
        if (originalGridData == null || originalPlacementData == null) return;

        // 현재 위치에서 제거
        RemoveFromCurrentGridData();

        // 오브젝트 데이터 가져오기
        var objectData = placementSystem.database.GetObjectData(originalPlacementData.ID);
        if (objectData == null) return;

        // 새 회전으로 위치 재계산
        var newPositions = originalGridData.CalculatePosition(
            originalGridPosition,
            objectData.Size,
            newRotation,
            placementSystem.grid
        );

        // 새 PlacementData 생성
        PlacementData newPlacementData = new PlacementData(
            newPositions,
            originalPlacementData.ID,
            originalPlacementData.PlacedObjectIndex,
            originalPlacementData.KindIndex,
            newRotation
        );

        // 새 위치에 추가
        foreach (var pos in newPositions)
        {
            if (!originalGridData.placedObjects.ContainsKey(pos))
                originalGridData.placedObjects[pos] = new System.Collections.Generic.List<PlacementData>();
            originalGridData.placedObjects[pos].Add(newPlacementData);
        }

        // 원본 데이터 업데이트
        originalPlacementData = newPlacementData;
    }

    private GridData FindGridDataByObjectIndex(int objectIndex)
    {
        // PlacementSystem의 기존 메서드 활용
        return placementSystem.FindGridDataByObjectIndex(objectIndex);
    }

    private int GetObjectIDFromGridData(GridData gridData, int objectIndex)
    {
        foreach (var kvp in gridData.placedObjects)
        {
            foreach (var data in kvp.Value)
            {
                if (data.PlacedObjectIndex == objectIndex)
                    return data.ID;
            }
        }
        return -1;
    }
}
