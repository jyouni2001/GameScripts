using Unity.Cinemachine;
using UnityEngine;
using DG.Tweening;

public class CameraCon : MonoBehaviour
{   
    public float     zoomMultiplier = 10f;  // 줌 조절 속도
    public float     minFOV         = 20f;  // 최소 줌
    public float     maxFOV         = 60f;  // 최대 줌
    public float     fovSmoothTime  = 0.2f; // 줌 부드럽게 변경되는 시간
    public float     targetFOV;             // 목표 줌 값
    public float     fovVelocity;           // 현재 FOV 변경 속도
    public float     moveSpeed      = 10f;  // WASD 이동 속도
    public float     rotationSpeed  = 5f;   // 회전 속도
    public float startHeight = 25f;
    
    public Transform target;         // 카메라 위치
    public Collider  boundingVolume; // 카메라 경계선

    private Vector3 offset;
    private Tween smoothTweener;
    private float duration = 1f;

    [SerializeField] private float             yaw   = -90f;    // 좌우 회전 (Y축)
    [SerializeField] private float             pitch = 60f;     // 상하 회전 (X축)
    [SerializeField] private CinemachineCamera cam;             // 카메라 참조
    [SerializeField] private Camera cam2;
    [SerializeField] private InputManager inputManager;
    
    
    private void Start()
    {
        //Debug.Log($"{target.transform.position}부터 시작");
        if (cam is null)
        {
            Debug.LogError("CinemachineVirtualCamera가 할당되지 않았습니다!");
            enabled = false; // 컴포넌트 비활성화
            return;
        }

        if (cam2 is null)
        {
            cam2 = Camera.main;
            return;
        }

        cam2 = Camera.main;

        //카메라 값 초기화
        offset = cam.transform.position;
        offset.y = startHeight;
        
        //target.transform.position = offset;
        targetFOV = cam.Lens.FieldOfView; 
        
        // 초기 컬링 마스크 설정 (1F만 표시)
        int layerMask = 1 << LayerMask.NameToLayer("1F");
        cam2.cullingMask |= layerMask;
    }

    private void LateUpdate()
    {
        // 카메라 위치 및 회전 적용
        Vector3 targetPosition = new Vector3(
            target is not null ? target.position.x : 0f,
            offset.y,
            target is not null ? target.position.z : 0f
        );

        if (target is not null) target.transform.position = targetPosition;
        
        if (inputManager.IsPointerOverUI())  return;
        
        SmoothWheel();       // 줌 처리
        HandleMovement();    // WASD 이동
        HandleRotation();    // 마우스 우클릭 회전
        CameraConfiner();    // 카메라가 경계에서 끼이는 버그 해결 
    }

    /// <summary>
    /// 카메라가 경계에서 멈추는 버그 해결
    /// </summary>
    private void CameraConfiner()
    {
        CinemachineConfiner3D confiner = cam.GetComponent<CinemachineConfiner3D>();
        if (confiner is not null && boundingVolume is not null)
        {
            Vector3 cameraPos = cam.transform.position;
            if (!boundingVolume.bounds.Contains(cameraPos))
            {
                // 카메라가 경계 밖에 있을 때, 경계 안으로 강제 이동
                Vector3 closestPoint = boundingVolume.ClosestPoint(cameraPos);
                cam.transform.position = closestPoint;
            }
        }
    }
    
    /// <summary>
    /// 마우스 휠 조절로 줌인, 줌아웃 기능
    /// </summary>
    private void SmoothWheel()
    {
        float scrollInput = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scrollInput is not 0)
        {
            targetFOV -= scrollInput * zoomMultiplier;
            targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
        } 
        // FOV를 부드럽게 업데이트
        cam.Lens.FieldOfView = Mathf.SmoothDamp(
            cam.Lens.FieldOfView, // 현재 값
            targetFOV,                      // 목표 값
            ref fovVelocity,                // 참조 속도 변수
            fovSmoothTime,                   // 부드럽게 만드는 시간
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );

    }

    /// <summary>
    /// W/A/S/D 방향키로 상하좌우 이동
    /// </summary>
    private void HandleMovement()
    {
        // WASD 입력 처리
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S

        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        if (moveDirection.magnitude > 0)
        {
            // 카메라의 로컬 방향을 기준으로 이동
            Vector3 move = transform.TransformDirection(moveDirection) * (moveSpeed * Time.unscaledDeltaTime);
            move.y = 0f; // y축 이동은 줌으로만 제어
            target.transform.position += move;
        }
    }

    /// <summary>
    /// 우클릭 드래그로 화면 회전
    /// </summary>
    private void HandleRotation()
    {
        // 마우스 우클릭 중일 때만 회전
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;

            // 상하 회전 각도 제한
            pitch = Mathf.Clamp(pitch, -40f, 70f);

            // 회전 적용
            target.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    
    public void SetOffset(int offsetY)
    {
        Debug.Log("SetOffset 작동 ");

        // 기존 Tween이 있으면 종료
        if (smoothTweener != null)
        {
            smoothTweener.Kill();
        }

        // DOTween으로 offset.y를 부드럽게 변경
        smoothTweener = DOVirtual.Float(offset.y, offsetY, duration, value =>
        {
            offset.y = value;
        }).SetEase(Ease.InOutQuad).SetUpdate(true).OnComplete(() =>
        {
            smoothTweener = null; // 완료 시 Tween 정리
        });
    }
}