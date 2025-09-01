using System.Collections;
using UnityEngine;

namespace JY
{
    /// <summary>
    /// 개별 배의 움직임과 상태 제어
    /// 웨이포인트 기반 이동, 정박, 출발 시퀀스 관리
    /// </summary>
    public class ShipController : MonoBehaviour
    {
        [Header("배 상태")]
        [SerializeField] private ShipState currentState = ShipState.Inactive;
        [SerializeField] private string shipId;
        
        [Header("이동 설정")]
        [SerializeField] private float currentSpeed = 0f;
        [SerializeField] private int currentWaypointIndex = 0;
        [SerializeField] private bool hasCompletedRoute = false;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        [SerializeField] private bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        [SerializeField] private bool showImportantLogsOnly = true;
        
        [Tooltip("이동 상태 로그 표시")]
        [SerializeField] private bool showMovementLogs = false;
        
        // 시스템 참조
        private ShipRoute assignedRoute;
        private ShipSystem shipSystem;
        
        // 이동 관련
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Coroutine movementCoroutine;
        
        // 상태 속성
        public ShipState CurrentState => currentState;
        public string ShipId => shipId;
        public ShipRoute AssignedRoute => assignedRoute;
        public bool HasCompletedRoute => hasCompletedRoute;
        
        // 이벤트
        public System.Action<ShipController> OnWaypointReached;
        public System.Action<ShipController> OnDockingStarted;
        public System.Action<ShipController> OnDockingCompleted;
        public System.Action<ShipController> OnDepartureStarted;
        
        private void Awake()
        {
            shipId = System.Guid.NewGuid().ToString();
        }
        
        /// <summary>
        /// 배 초기화
        /// </summary>
        public void Initialize(ShipRoute route, ShipSystem system)
        {
            assignedRoute = route;
            shipSystem = system;
            currentState = ShipState.Inactive;
            currentWaypointIndex = 0;
            hasCompletedRoute = false;
            
            // 시작 위치로 이동 (첫 번째 웨이포인트 Way0)
            if (route.waypoints.Count > 0 && route.waypoints[0] != null)
            {
                Vector3 startPos = route.GetWaypointPosition(0);
                transform.position = new Vector3(startPos.x, 0.5f, startPos.z); // 바닥에서 약간 위로
                
                // 두 번째 웨이포인트 방향으로 회전
                if (route.waypoints.Count > 1 && route.waypoints[1] != null)
                {
                    Vector3 direction = (route.GetWaypointPosition(1) - startPos).normalized;
                    if (direction != Vector3.zero)
                    {
                        transform.rotation = Quaternion.LookRotation(direction);
                    }
                }
            }
            
            DebugLog($"배 초기화 완료: {route.routeId}, 시작 위치: Way0", true);
        }
        
        /// <summary>
        /// 여행 시작
        /// </summary>
        public void StartJourney()
        {
            if (assignedRoute == null || !assignedRoute.IsValid())
            {
                DebugLog("유효하지 않은 루트입니다.", true);
                return;
            }
            
            currentState = ShipState.Moving;
            currentWaypointIndex = 1; // Way0에서 시작하므로 다음은 Way1
            hasCompletedRoute = false;
            
            DebugLog("여행 시작 - Way1으로 이동", true);
            MoveToNextWaypoint();
        }
        
        /// <summary>
        /// 정박 시작 (자동 호출)
        /// </summary>
        public void StartDocking()
        {
            if (currentState != ShipState.Moving)
            {
                DebugLog($"정박 불가 - 현재 상태: {currentState}", true);
                return;
            }
            
            currentState = ShipState.Docking;
            
            // 정박지로 이동
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }
            
            movementCoroutine = StartCoroutine(MoveToDockingPoint());
            
            OnDockingStarted?.Invoke(this);
            DebugLog("정박 시작", true);
        }
        
        /// <summary>
        /// 출발 시작
        /// </summary>
        public void StartDeparture()
        {
            if (currentState != ShipState.Docked) 
            {
                DebugLog($"출발 불가 - 현재 상태: {currentState}", true);
                return;
            }
            
            currentState = ShipState.Departing;
            
            // 출발 애니메이션 시작
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }
            
            movementCoroutine = StartCoroutine(DepartureSequence());
            
            OnDepartureStarted?.Invoke(this);
            DebugLog("출발 시작", true);
        }
        
        /// <summary>
        /// 배 리셋 (풀로 반환 시)
        /// </summary>
        public void ResetShip()
        {
            // 모든 코루틴 중지
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
                movementCoroutine = null;
            }
            
            // 상태 리셋
            currentState = ShipState.Inactive;
            currentWaypointIndex = 0;
            currentSpeed = 0f;
            hasCompletedRoute = false;
            
            DebugLog("배 리셋 완료", showImportantLogsOnly);
        }
        
        /// <summary>
        /// 다음 웨이포인트로 이동
        /// </summary>
        private void MoveToNextWaypoint()
        {
            if (assignedRoute == null || currentWaypointIndex >= assignedRoute.waypoints.Count)
            {
                DebugLog("웨이포인트 경로 완료 - 정박 시작", true);
                StartDocking();
                return;
            }
            
            Transform waypoint = assignedRoute.waypoints[currentWaypointIndex];
            if (waypoint == null)
            {
                DebugLog($"웨이포인트 {currentWaypointIndex}가 null입니다.", true);
                currentWaypointIndex++;
                MoveToNextWaypoint();
                return;
            }
            
            Vector3 destination = waypoint.position;
            Vector3 direction = (destination - transform.position).normalized;
            Quaternion targetRotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : transform.rotation;
            
            DebugLog($"웨이포인트 {currentWaypointIndex}로 이동 시작: {waypoint.name}", showMovementLogs);
            
            if (movementCoroutine != null)
            {
                StopCoroutine(movementCoroutine);
            }
            
            movementCoroutine = StartCoroutine(MoveToPosition(destination, targetRotation));
        }
        
        /// <summary>
        /// 지정된 위치로 부드럽게 이동
        /// </summary>
        private IEnumerator MoveToPosition(Vector3 destination, Quaternion rotation)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            
            float distance = Vector3.Distance(startPosition, destination);
            float baseSpeed = assignedRoute.movementSpeed;
            float journeyTime = distance / baseSpeed;
            
            float elapsedTime = 0f;
            
            while (elapsedTime < journeyTime)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / journeyTime;
                
                // 부드러운 이동 곡선 적용
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
                
                // 위치 보간
                Vector3 currentPos = Vector3.Lerp(startPosition, destination, smoothProgress);
                currentPos.y = 0.5f; // 바닥에서 약간 위로 유지
                transform.position = currentPos;
                
                // 회전 보간
                transform.rotation = Quaternion.Slerp(startRotation, rotation, smoothProgress);
                
                // 현재 속도 계산 (시각적 표시용)
                currentSpeed = baseSpeed * (1f - Mathf.Abs(0.5f - smoothProgress) * 0.5f);
                
                yield return null;
            }
            
            // 최종 위치 및 회전 설정
            destination.y = 0.5f;
            transform.position = destination;
            transform.rotation = rotation;
            currentSpeed = 0f;
            
            // 웨이포인트 도달 처리
            OnWaypointReached?.Invoke(this);
            
            // 현재 웨이포인트 인덱스가 유효한 범위 내인지 확인 후 로그 출력
            if (currentWaypointIndex >= 0 && currentWaypointIndex < assignedRoute.waypoints.Count && assignedRoute.waypoints[currentWaypointIndex] != null)
            {
                DebugLog($"웨이포인트 {currentWaypointIndex} 도달: {assignedRoute.waypoints[currentWaypointIndex].name}", showMovementLogs);
            }
            else
            {
                DebugLog($"웨이포인트 {currentWaypointIndex} 도달 (정박지로 이동 중)", showMovementLogs);
            }
            
            currentWaypointIndex++;
            
            // 다음 웨이포인트로 이동 또는 정박 시작
            yield return new WaitForSeconds(0.2f); // 짧은 대기
            MoveToNextWaypoint();
        }
        
        /// <summary>
        /// 정박지로 이동
        /// </summary>
        private IEnumerator MoveToDockingPoint()
        {
            if (assignedRoute.dockingPoint == null)
            {
                DebugLog("정박지가 설정되지 않았습니다.", true);
                yield break;
            }
            
            Vector3 dockingPosition = assignedRoute.dockingPoint.position;
            Vector3 direction = (dockingPosition - transform.position).normalized;
            Quaternion dockingRotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : transform.rotation;
            
            DebugLog("정박지로 이동 중", showMovementLogs);
            
            yield return StartCoroutine(MoveToPosition(dockingPosition, dockingRotation));
            
            // 정박 완료
            currentState = ShipState.Docked;
            OnDockingCompleted?.Invoke(this);
            DebugLog("정박 완료 - 대기 시작", true);
            
            // 정박 시간 대기 시작
            StartCoroutine(WaitForDockingDuration());
        }
        
        /// <summary>
        /// 정박 시간 대기
        /// </summary>
        private IEnumerator WaitForDockingDuration()
        {
            if (shipSystem == null)
            {
                DebugLog("ShipSystem 참조가 없습니다.", true);
                yield break;
            }
            
            // TimeSystem을 통해 실제 게임 시간으로 대기
            TimeSystem timeSystem = TimeSystem.Instance;
            if (timeSystem == null)
            {
                DebugLog("TimeSystem을 찾을 수 없습니다.", true);
                yield break;
            }
            
            float dockingDurationMinutes = 30f; // 30분 정박
            float startTime = (float)timeSystem.GetCurrentTimeInMinutes(); // int를 float로 변환
            float endTime = startTime + dockingDurationMinutes;
            
            DebugLog($"정박 대기 시작: {dockingDurationMinutes}분 (게임 시간)", true);
            DebugLog($"시작 시간: {startTime}분, 종료 시간: {endTime}분, 시간 배속: {timeSystem.timeMultiplier}", true);
            
            // 게임 시간으로 대기
            float checkInterval = 1f; // 1초마다 체크
            float lastCheckTime = Time.time;
            
            while ((float)timeSystem.GetCurrentTimeInMinutes() < endTime)
            {
                // 게임이 일시정지되거나 시간 배속이 0이면 대기
                if (timeSystem.timeMultiplier <= 0)
                {
                    DebugLog("시간이 정지되어 있어 대기 중...", false);
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }
                
                // 주기적으로 진행 상황 로그
                if (Time.time - lastCheckTime >= checkInterval)
                {
                    float currentTime = (float)timeSystem.GetCurrentTimeInMinutes();
                    float remainingTime = endTime - currentTime;
                    DebugLog($"정박 대기 중... 남은 시간: {remainingTime:F1}분", false);
                    lastCheckTime = Time.time;
                }
                
                yield return new WaitForSeconds(0.1f);
            }
            
            DebugLog("정박 시간 완료 - 출발 준비", true);
            
            // 자동으로 출발 시작
            StartDeparture();
        }
        
        /// <summary>
        /// 출발 시퀀스
        /// </summary>
        private IEnumerator DepartureSequence()
        {
            DebugLog("출발 시퀀스 시작", true);
            
            // 출발 웨이포인트가 있는지 확인
            if (assignedRoute.departureWaypoints == null || assignedRoute.departureWaypoints.Count == 0)
            {
                DebugLog("출발 웨이포인트가 설정되지 않았습니다. 기본 출발 처리를 진행합니다.", true);
                
                // 출발 웨이포인트가 없으면 현재 위치에서 약간 멀리 이동 후 비활성화
                Vector3 currentPos = transform.position;
                Vector3 exitDirection = Vector3.forward; // 기본 출발 방향
                
                // 도착 경로의 반대 방향으로 설정 (가능한 경우)
                if (assignedRoute.waypoints.Count >= 2)
                {
                    Vector3 lastDirection = (assignedRoute.waypoints[assignedRoute.waypoints.Count - 1].position - 
                                           assignedRoute.waypoints[assignedRoute.waypoints.Count - 2].position).normalized;
                    exitDirection = lastDirection;
                }
                
                Vector3 exitPosition = currentPos + exitDirection * 10f; // 10 유닛 멀리
                exitPosition.y = 0.5f;
                
                DebugLog($"기본 출발 지점으로 이동: {exitDirection}", true);
                
                // 출발 지점으로 이동
                yield return StartCoroutine(MoveToPosition(exitPosition, Quaternion.LookRotation(exitDirection)));
                
                // 출발 완료
                currentState = ShipState.Inactive;
                hasCompletedRoute = true;
                DebugLog("기본 출발 완료 - 배 비활성화", true);
                yield break;
            }
            
            // 출발 웨이포인트들을 순서대로 이동
            for (int i = 0; i < assignedRoute.departureWaypoints.Count; i++)
            {
                Transform departureWaypoint = assignedRoute.departureWaypoints[i];
                if (departureWaypoint == null)
                {
                    DebugLog($"출발 웨이포인트 {i}가 null입니다.", true);
                    continue;
                }
                
                Vector3 destination = departureWaypoint.position;
                Vector3 direction = (destination - transform.position).normalized;
                Quaternion targetRotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : transform.rotation;
                
                DebugLog($"출발 웨이포인트 {i}로 이동: {departureWaypoint.name}", showMovementLogs);
                
                yield return StartCoroutine(MoveToPosition(destination, targetRotation));
            
                // 각 웨이포인트 사이에 짧은 대기
                yield return new WaitForSeconds(0.2f);
            }
            
            DebugLog("모든 출발 웨이포인트 통과 완료", true);
            
            // 출발 완료
            currentState = ShipState.Inactive;
            hasCompletedRoute = true;
            
            DebugLog("출발 완료 - 배 비활성화", true);
        }
        
        #region 디버그 메서드
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[ShipController-{shipId[..8]}] {message}");
        }
        
        /// <summary>
        /// 기즈모 그리기 (씬 뷰에서 배 상태 시각화)
        /// </summary>
        private void OnDrawGizmos()
        {
            if (assignedRoute == null) return;
            
            // 현재 상태에 따른 색상 설정
            switch (currentState)
            {
                case ShipState.Moving:
                    Gizmos.color = Color.green;
                    break;
                case ShipState.Docking:
            Gizmos.color = Color.yellow;
                    break;
                case ShipState.Docked:
                    Gizmos.color = Color.red;
                    break;
                case ShipState.Departing:
                    Gizmos.color = Color.blue;
                    break;
                default:
                    Gizmos.color = Color.gray;
                    break;
            }
            
            // 배 위치에 구체 그리기
            Gizmos.DrawWireSphere(transform.position, 1f);
            
            // 목표 위치까지 선 그리기
            if (targetPosition != Vector3.zero)
            {
                Gizmos.DrawLine(transform.position, targetPosition);
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// 배의 상태 열거형
    /// </summary>
    public enum ShipState
    {
        Inactive,   // 비활성
        Moving,     // 이동 중
        Docking,    // 정박 중
        Docked,     // 정박 완료
        Departing   // 출발 중
    }
} 