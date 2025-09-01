using UnityEngine;
using UnityEngine.AI;
using LodgingSimulator.SO;

namespace LodgingSimulator.AI
{
    /// <summary>
    /// 개별 집사 AI의 동작을 관리하는 클래스
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class ButlerAI : MonoBehaviour
    {
        [Header("집사 AI 설정")]
        [Tooltip("집사 AI 설정 파일")]
        public ButlerSettingsSO settings;
        
        [Header("집사 AI 정보")]
        [Tooltip("집사 AI 고유 ID")]
        public string butlerId;
        
        [Tooltip("집사 AI 이름")]
        public string butlerName;
        
        [Header("현재 상태")]
        [Tooltip("현재 상태")]
        [SerializeField] private ButlerStateType currentStateType;
        
        [Tooltip("현재 할당된 작업")]
        [SerializeField] private ButlerTask currentTask;
        
        [Header("컴포넌트 참조")]
        [SerializeField] private NavMeshAgent navAgent;
        [SerializeField] private Animator animator;
        
        // 내부 변수
        private IButlerState currentState;
        private Vector3 targetDestination;
        private bool isMoving = false;
        
        // 프로퍼티
        public ButlerStateType CurrentStateType => currentStateType;
        public ButlerTask CurrentTask => currentTask;
        public Vector3 CounterPosition => settings.counterPosition;
        public ButlerSettingsSO Settings => settings;
        
        /// <summary>
        /// 초기화
        /// </summary>
        private void Awake()
        {
            // 컴포넌트 참조 가져오기
            navAgent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            
            // 고유 ID 생성
            butlerId = System.Guid.NewGuid().ToString();
            butlerName = $"집사_{Random.Range(1000, 9999)}";
            
            // NavMeshAgent 설정
            if (navAgent != null)
            {
                navAgent.speed = settings.butlerMoveSpeed;
                navAgent.angularSpeed = settings.rotationSpeed;
                navAgent.stoppingDistance = 0.5f;
            }
        }
        
        /// <summary>
        /// 시작 시 유휴 상태로 설정
        /// </summary>
        private void Start()
        {
            ChangeState(new ButlerIdleState());
        }
        
        /// <summary>
        /// 매 프레임 업데이트
        /// </summary>
        private void Update()
        {
            // 현재 상태 업데이트
            currentState?.Update(this);
            
            // 이동 상태 확인
            UpdateMovementState();
        }
        
        /// <summary>
        /// 상태 변경
        /// </summary>
        public void ChangeState(IButlerState newState)
        {
            // 이전 상태 종료
            currentState?.Exit(this);
            
            // 새 상태 설정
            currentState = newState;
            currentStateType = newState.GetStateType();
            
            // 새 상태 시작
            currentState?.Enter(this);
        }
        
        /// <summary>
        /// 목적지 설정
        /// </summary>
        public void SetDestination(Vector3 destination)
        {
            targetDestination = destination;
            
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(destination);
                isMoving = true;
            }
        }
        
        /// <summary>
        /// 이동 정지
        /// </summary>
        public void StopMovement()
        {
            if (navAgent != null)
            {
                navAgent.isStopped = true;
                isMoving = false;
            }
        }
        
        /// <summary>
        /// 이동 재개
        /// </summary>
        public void ResumeMovement()
        {
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                isMoving = true;
            }
        }
        
        /// <summary>
        /// 목적지 도달 여부 확인
        /// </summary>
        public bool HasReachedDestination()
        {
            if (navAgent == null || !navAgent.isOnNavMesh)
                return false;
                
            return !navAgent.pathPending && 
                   navAgent.remainingDistance <= navAgent.stoppingDistance;
        }
        
        /// <summary>
        /// 애니메이션 파라미터 설정
        /// </summary>
        public void SetAnimation(string paramName, bool value)
        {
            if (animator != null)
            {
                animator.SetBool(paramName, value);
            }
        }
        
        /// <summary>
        /// 작업 할당
        /// </summary>
        public void AssignTask(ButlerTask task)
        {
            if (currentTask != null)
            {
                Debug.LogWarning($"집사 {butlerName}에게 이미 작업이 할당되어 있습니다.");
                return;
            }
            
            currentTask = task;
            task.AssignToButler(this);
            
            // 이동 상태로 전환하여 작업 위치로 이동
            ChangeState(new ButlerMovingState());
        }
        
        /// <summary>
        /// 작업 완료 처리
        /// </summary>
        public void CompleteTask(ButlerTask task)
        {
            if (currentTask == task)
            {
                currentTask = null;
                task.Unassign();
                
                // 유휴 상태로 전환
                ChangeState(new ButlerIdleState());
            }
        }
        
        /// <summary>
        /// 청소 작업 시작
        /// </summary>
        public void StartCleaning()
        {
            // 청소 작업 시작 로직
            Debug.Log($"집사 {butlerName}이(가) 방 {currentTask?.roomId}에서 청소를 시작합니다.");
        }
        
        /// <summary>
        /// 청소 작업 완료
        /// </summary>
        public void CompleteCleaning()
        {
            // 청소 작업 완료 로직
            Debug.Log($"집사 {butlerName}이(가) 방 {currentTask?.roomId} 청소를 완료했습니다.");
            
            // 방 상태 업데이트 (RoomManager와 연동)
            if (currentTask != null)
            {
                // RoomManager에 청소 완료 알림
                var roomManager = FindObjectOfType<JY.RoomManager>();
                if (roomManager != null)
                {
                    roomManager.OnRoomCleaned(currentTask.roomId);
                }
            }
        }
        
        /// <summary>
        /// 청소 작업 정지
        /// </summary>
        public void StopCleaning()
        {
            // 청소 작업 정지 로직
            Debug.Log($"집사 {butlerName}이(가) 청소 작업을 중단했습니다.");
        }
        
        /// <summary>
        /// 이동 상태 업데이트
        /// </summary>
        private void UpdateMovementState()
        {
            if (navAgent == null) return;
            
            // 이동 중인지 확인
            bool wasMoving = isMoving;
            isMoving = navAgent.velocity.magnitude > 0.1f;
            
            // 이동 상태가 변경되었을 때 애니메이션 업데이트
            if (wasMoving != isMoving)
            {
                SetAnimation(settings.moveAnimationParam, isMoving);
            }
        }
        
        /// <summary>
        /// 집사 AI 제거
        /// </summary>
        public void DestroyButler()
        {
            // ButlerManager에서 제거
            ButlerManager.Instance.RemoveButler(this);
            
            // 게임오브젝트 제거
            Destroy(gameObject);
        }
        
        /// <summary>
        /// 디버그 정보 표시
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (currentTask != null)
            {
                // 현재 작업 위치 표시
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentTask.roomPosition, 1f);
                
                // 집사에서 작업 위치까지의 경로 표시
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, currentTask.roomPosition);
            }
            
            // 카운터 위치 표시
            if (settings != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(settings.counterPosition, 1f);
            }
        }
    }
}
