using UnityEngine;

public class ChangeFloorSystem : MonoBehaviour
{
    [SerializeField] private CameraCon cameraCon;
    [SerializeField] private Grid grid;
    [SerializeField] private PlacementSystem placementSystem;
    [SerializeField] private InputManager inputManager;
    
    [SerializeField] private Camera mainCamera;
    
    public int currentFloor = 1;
    
    // 층 변경 메서드 (파라미터 없이 currentFloor 사용)
    private void ChangeFloor()
    {
        // 층 범위 제한
        currentFloor = Mathf.Clamp(currentFloor, 1, 4);

        Vector3 newCellSize = grid.cellSize;

        switch (currentFloor)
        {
            case 1:
                newCellSize.y = 0f;
                cameraCon.SetOffset(25);
                break;
            case 2:
                newCellSize.y = 1.927f;
                cameraCon.SetOffset(30);
                break;
            case 3:
                newCellSize.y = 2.154f - 0.0131f;//3.854f; // 1.927 * 2 (예상값, 필요 시 조정) 2.154f;/
                cameraCon.SetOffset(35);
                break;
            case 4:
                newCellSize.y = 1.7f; //5.781f; // 1.927 * 3 (예상값, 필요 시 조정) 
                cameraCon.SetOffset(40);
                
                break;
        }
        
        // 카메라 컬링 마스크 업데이트
        UpdateCameraCullingMask();

        OnBuildModeChanged();
        grid.cellSize = newCellSize;
    }

    // 카메라 컬링 마스크 설정: 현재 층과 그 아래 모든 층 표시
    private void UpdateCameraCullingMask()
    {
        if (mainCamera == null)
        {
            Debug.LogError("Main Camera is not assigned in ChangeFloorSystem!");
            return;
        }

        // 기본 레이어(예: Default, UI 등) 유지
        int cullingMask = mainCamera.cullingMask;

        // 모든 층 레이어 비우기
        for (int i = 1; i <= 4; i++)
        {
            int layer = LayerMask.NameToLayer($"{i}F");
            if (layer != -1)
            {
                cullingMask &= ~(1 << layer); // 해당 층 레이어 비활성화
            }
        }

        // 현재 층과 그 아래 층 레이어 활성화
        for (int i = 1; i <= currentFloor; i++)
        {
            int layer = LayerMask.NameToLayer($"{i}F");
            if (layer != -1)
            {
                cullingMask |= (1 << layer); // 해당 층 레이어 활성화
            }
            else
            {
                Debug.LogError($"Layer {i}F not found!");
            }
        }

        mainCamera.cullingMask = cullingMask;
    }
    
    // Up 버튼에서 호출
    public void IncreaseFloor()
    {
        currentFloor++;
        ChangeFloor();
    }

    // Down 버튼에서 호출
    public void DecreaseFloor()
    {
        currentFloor--;
        ChangeFloor();
    }

    private void CheckBuildMode()
    {
        if (inputManager.isBuildMode)
        {
            placementSystem.HidePlane(currentFloor); // 빌드 모드일 때 현재 층 표시
        }
        else
        {
            placementSystem.HideAllPlanes(); // 빌드 모드가 아닐 때 모든 플레인 비활성화
        }
    }

    // 빌드 모드 변경 시 호출 (InputManager에서 호출)
    public void OnBuildModeChanged()
    {
        CheckBuildMode();
    }
}
