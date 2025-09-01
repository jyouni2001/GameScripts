using UnityEngine;

namespace LodgingSimulator.AI
{
    /// <summary>
    /// 집사 AI의 상태를 정의하는 열거형
    /// </summary>
    public enum ButlerStateType
    {
        Idle,       // 유휴 상태 - 카운터에서 대기
        Moving,     // 이동 중 - 목적지로 이동
        Cleaning    // 청소 중 - 방에서 청소 작업 수행
    }
    
    /// <summary>
    /// 집사 AI 상태 패턴의 기본 인터페이스
    /// </summary>
    public interface IButlerState
    {
        void Enter(ButlerAI butler);
        void Update(ButlerAI butler);
        void Exit(ButlerAI butler);
        ButlerStateType GetStateType();
    }
    
    /// <summary>
    /// 유휴 상태 - 카운터에서 대기하며 새로운 작업 할당을 기다림
    /// </summary>
    public class ButlerIdleState : IButlerState
    {
        public void Enter(ButlerAI butler)
        {
            // 카운터 위치로 이동
            butler.SetDestination(butler.CounterPosition);
            
            // 애니메이션 설정
            butler.SetAnimation(butler.Settings.idleAnimationParam, true);
            butler.SetAnimation(butler.Settings.moveAnimationParam, false);
            butler.SetAnimation(butler.Settings.cleaningAnimationParam, false);
            
            // ButlerManager에 유휴 상태 등록
            ButlerManager.Instance.RegisterIdleButler(butler);
        }
        
        public void Update(ButlerAI butler)
        {
            // 카운터 위치에 도달했는지 확인
            if (Vector3.Distance(butler.transform.position, butler.CounterPosition) < 0.5f)
            {
                // 카운터 위치에 도달하면 정지
                butler.StopMovement();
            }
        }
        
        public void Exit(ButlerAI butler)
        {
            // ButlerManager에서 유휴 상태 해제
            ButlerManager.Instance.UnregisterIdleButler(butler);
            
            // 애니메이션 정리
            butler.SetAnimation(butler.Settings.idleAnimationParam, false);
        }
        
        public ButlerStateType GetStateType() => ButlerStateType.Idle;
    }
    
    /// <summary>
    /// 이동 상태 - 목적지로 이동
    /// </summary>
    public class ButlerMovingState : IButlerState
    {
        public void Enter(ButlerAI butler)
        {
            // 이동 애니메이션 시작
            butler.SetAnimation(butler.Settings.moveAnimationParam, true);
            butler.SetAnimation(butler.Settings.idleAnimationParam, false);
            butler.SetAnimation(butler.Settings.cleaningAnimationParam, false);
        }
        
        public void Update(ButlerAI butler)
        {
            // 목적지에 도달했는지 확인
            if (butler.HasReachedDestination())
            {
                // 목적지에 도달하면 다음 상태로 전환
                if (butler.CurrentTask != null)
                {
                    butler.ChangeState(new ButlerCleaningState());
                }
                else
                {
                    butler.ChangeState(new ButlerIdleState());
                }
            }
        }
        
        public void Exit(ButlerAI butler)
        {
            // 이동 애니메이션 정지
            butler.SetAnimation(butler.Settings.moveAnimationParam, false);
        }
        
        public ButlerStateType GetStateType() => ButlerStateType.Moving;
    }
    
    /// <summary>
    /// 청소 상태 - 방에서 청소 작업 수행
    /// </summary>
    public class ButlerCleaningState : IButlerState
    {
        private float cleaningTimer;
        
        public void Enter(ButlerAI butler)
        {
            // 청소 타이머 초기화
            cleaningTimer = butler.Settings.cleaningDuration;
            
            // 청소 애니메이션 시작
            butler.SetAnimation(butler.Settings.cleaningAnimationParam, true);
            butler.SetAnimation(butler.Settings.moveAnimationParam, false);
            butler.SetAnimation(butler.Settings.idleAnimationParam, false);
            
            // 청소 작업 시작
            butler.StartCleaning();
        }
        
        public void Update(ButlerAI butler)
        {
            // 청소 타이머 감소
            cleaningTimer -= Time.deltaTime;
            
            // 청소 완료 시
            if (cleaningTimer <= 0f)
            {
                // 청소 작업 완료 처리
                butler.CompleteCleaning();
                
                // 유휴 상태로 전환
                butler.ChangeState(new ButlerIdleState());
            }
        }
        
        public void Exit(ButlerAI butler)
        {
            // 청소 애니메이션 정지
            butler.SetAnimation(butler.Settings.cleaningAnimationParam, false);
            
            // 청소 작업 정리
            butler.StopCleaning();
        }
        
        public ButlerStateType GetStateType() => ButlerStateType.Cleaning;
    }
}
