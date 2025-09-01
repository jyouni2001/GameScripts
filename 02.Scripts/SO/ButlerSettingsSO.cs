using UnityEngine;

namespace LodgingSimulator.SO
{
    /// <summary>
    /// 집사 AI 시스템의 설정값들을 관리하는 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "ButlerSettings", menuName = "LodgingSimulator/Butler Settings")]
    public class ButlerSettingsSO : ScriptableObject
    {
        [Header("집사 AI 기본 설정")]
        [Tooltip("집사 AI 구매 비용")]
        public int butlerPurchaseCost = 1000;
        
        [Tooltip("최대 구매 가능한 집사 AI 수")]
        public int maxButlerCount = 10;
        
        [Header("집사 AI 성능 설정")]
        [Tooltip("집사 AI 이동 속도")]
        public float butlerMoveSpeed = 3.5f;
        
        [Tooltip("방 청소에 소요되는 시간 (초)")]
        public float cleaningDuration = 5.0f;
        
        [Tooltip("집사 AI 회전 속도")]
        public float rotationSpeed = 180f;
        
        [Header("작업 할당 설정")]
        [Tooltip("작업 할당 시 고려할 최대 거리")]
        public float maxAssignmentDistance = 50f;
        
        [Tooltip("카운터 위치 (집사들이 대기하는 위치)")]
        public Vector3 counterPosition = Vector3.zero;
        
        [Header("애니메이션 설정")]
        [Tooltip("이동 애니메이션 파라미터 이름")]
        public string moveAnimationParam = "IsMoving";
        
        [Tooltip("청소 애니메이션 파라미터 이름")]
        public string cleaningAnimationParam = "IsCleaning";
        
        [Tooltip("유휴 애니메이션 파라미터 이름")]
        public string idleAnimationParam = "IsIdle";
    }
}
