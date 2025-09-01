/*using UnityEngine;

public class CameraCon : MonoBehaviour
{
    public float zoomMultiplier = 10f;
    public float moveSpeed = 10f;        // WASD 이동 속도
    public float rotationSpeed = 5f;     // 회전 속도
    public Transform target;             // 카메라가 따라갈 타겟 (선택사항)
    public float minZoom = 2f;
    public float maxZoom = 10f;
    
    
    private float targetOffsetY;         // 목표 y 값
    private float smoothTime = 0.5f;     // 부드러운 이동을 위한 감속 시간
    private float yVelocity = 0f;        // 현재 속도 (SmoothDamp에서 필요)
    private Vector3 offset;
    private float yaw = 0f;              // 좌우 회전 (Y축)
    private float pitch = 60f;            // 상하 회전 (X축)
    
    private void Start()
    {
        offset = new Vector3(0f, 5f, -4f);
        targetOffsetY = offset.y;
        transform.position = offset;
    }

    private void LateUpdate()
    {
        SmoothWheel();       // 줌 처리
        HandleMovement();    // WASD 이동
        HandleRotation();    // 마우스 우클릭 회전
        
        // 카메라 위치 및 회전 적용
        Vector3 targetPosition = new Vector3(
            target is not null ? target.position.x : 0f,
            offset.y,
            target is not null ? target.position.z : 0f
        );
        
        transform.position = targetPosition;
    }

    private void SmoothWheel()
    {
        float scrollInput = Input.GetAxisRaw("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            targetOffsetY -= scrollInput * zoomMultiplier;
            targetOffsetY = Mathf.Clamp(targetOffsetY, minZoom, maxZoom);
        }
        offset.y = Mathf.SmoothDamp(offset.y, targetOffsetY, ref yVelocity, smoothTime);
    }

    private void HandleMovement()
    {
        // WASD 입력 처리
        float horizontal = Input.GetAxisRaw("Horizontal"); // A/D
        float vertical = Input.GetAxisRaw("Vertical");     // W/S
        
        Vector3 moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        if (moveDirection.magnitude > 0)
        {
            // 카메라의 로컬 방향을 기준으로 이동
            Vector3 move = transform.TransformDirection(moveDirection) * moveSpeed * Time.deltaTime;
            move.y = 0f; // y축 이동은 줌으로만 제어
            transform.position += move;
        }
    }

    private void HandleRotation()
    {
        // 마우스 우클릭 중일 때만 회전
        if (Input.GetMouseButton(1)) // 1은 우클릭
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;

            // 상하 회전 각도를 제한 (예: -45도 ~ 45도)
            pitch = Mathf.Clamp(pitch, 10f, 60f);

            // 회전 적용
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

}*/