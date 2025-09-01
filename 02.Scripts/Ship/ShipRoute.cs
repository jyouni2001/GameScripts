using System.Collections.Generic;
using UnityEngine;

namespace JY
{
    /// <summary>
    /// 배의 이동 경로 정의
    /// </summary>
    [System.Serializable]
    public class ShipRoute
    {
        [Header("Route Information")]
        public string routeId = "Route_01";
        public string routeName = "기본 루트";
        
        [Header("Timing")]
        public float arrivalTime = 480f; // 게임 시간 (분) - 8:00 AM
        
        [Header("Arrival Waypoints")]
        public List<Transform> waypoints = new List<Transform>();
        
        [Header("Departure Waypoints")]
        public List<Transform> departureWaypoints = new List<Transform>();
        
        [Header("Docking")]
        public Transform dockingPoint;
        public Vector3 dockingRotation = Vector3.zero;
        
        [Header("Movement Settings")]
        public float movementSpeed = 5f; // 이동 속도
        public float rotationSpeed = 2f; // 회전 속도
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1); // 속도 곡선
        
        [Header("Visual Settings")]
        public Color routeColor = Color.blue;
        public Color departureRouteColor = Color.red;
        public bool showWaypoints = true;
        public bool showDockingArea = true;
        
        /// <summary>
        /// 루트가 유효한지 확인
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(routeId))
            {
                Debug.LogWarning("[ShipRoute] Route ID가 비어있습니다.");
                return false;
            }
            
            /*if (waypoints.Count < 2)
            {
                Debug.LogWarning($"[ShipRoute] {routeId}: 최소 2개의 웨이포인트가 필요합니다.");
                return false;
            }*/
            
            if (dockingPoint == null)
            {
                Debug.LogWarning($"[ShipRoute] {routeId}: 정박 지점이 설정되지 않았습니다.");
                return false;
            }
            
            // 웨이포인트 null 체크
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null)
                {
                    Debug.LogWarning($"[ShipRoute] {routeId}: 웨이포인트 {i}가 null입니다.");
                    return false;
                }
            }
            
            // 출발 웨이포인트 null 체크
            for (int i = 0; i < departureWaypoints.Count; i++)
            {
                if (departureWaypoints[i] == null)
                {
                    Debug.LogWarning($"[ShipRoute] {routeId}: 출발 웨이포인트 {i}가 null입니다.");
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// 특정 인덱스의 웨이포인트 위치 가져오기
        /// </summary>
        public Vector3 GetWaypointPosition(int index)
        {
            if (index >= 0 && index < waypoints.Count && waypoints[index] != null)
            {
                return waypoints[index].position;
            }
            return Vector3.zero;
        }
        
        /// <summary>
        /// 특정 인덱스의 출발 웨이포인트 위치 가져오기
        /// </summary>
        public Vector3 GetDepartureWaypointPosition(int index)
        {
            if (index >= 0 && index < departureWaypoints.Count && departureWaypoints[index] != null)
            {
                return departureWaypoints[index].position;
            }
            return Vector3.zero;
        }
        
        /// <summary>
        /// 정박 지점 위치 가져오기
        /// </summary>
        public Vector3 GetDockingPosition()
        {
            return dockingPoint != null ? dockingPoint.position : Vector3.zero;
        }
        
        /// <summary>
        /// 정박 지점 회전값 가져오기
        /// </summary>
        public Quaternion GetDockingRotation()
        {
            if (dockingPoint != null)
            {
                return dockingPoint.rotation;
            }
            return Quaternion.Euler(dockingRotation);
        }
        
        /// <summary>
        /// 루트의 총 거리 계산
        /// </summary>
        public float GetTotalDistance()
        {
            if (waypoints.Count < 2) return 0f;
            
            float totalDistance = 0f;
            
            // 도착 경로 거리
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    totalDistance += Vector3.Distance(waypoints[i].position, waypoints[i + 1].position);
                }
            }
            
            // 마지막 웨이포인트에서 정박지까지의 거리
            if (waypoints.Count > 0 && waypoints[waypoints.Count - 1] != null && dockingPoint != null)
            {
                totalDistance += Vector3.Distance(waypoints[waypoints.Count - 1].position, dockingPoint.position);
            }
            
            // 출발 경로 거리
            for (int i = 0; i < departureWaypoints.Count - 1; i++)
            {
                if (departureWaypoints[i] != null && departureWaypoints[i + 1] != null)
                {
                    totalDistance += Vector3.Distance(departureWaypoints[i].position, departureWaypoints[i + 1].position);
                }
            }
            
            return totalDistance;
        }
        
        /// <summary>
        /// 예상 이동 시간 계산 (분)
        /// </summary>
        public float GetEstimatedTravelTime()
        {
            float distance = GetTotalDistance();
            if (movementSpeed <= 0) return 0f;
            
            // 거리 / 속도 = 시간 (초) -> 분으로 변환
            return (distance / movementSpeed) / 60f;
        }
        
        /// <summary>
        /// 웨이포인트 추가
        /// </summary>
        public void AddWaypoint(Transform waypoint)
        {
            if (waypoint != null && !waypoints.Contains(waypoint))
            {
                waypoints.Add(waypoint);
            }
        }
        
        /// <summary>
        /// 웨이포인트 제거
        /// </summary>
        public void RemoveWaypoint(Transform waypoint)
        {
            waypoints.Remove(waypoint);
        }
        
        /// <summary>
        /// 웨이포인트 순서 변경
        /// </summary>
        public void MoveWaypoint(int fromIndex, int toIndex)
        {
            if (fromIndex >= 0 && fromIndex < waypoints.Count && 
                toIndex >= 0 && toIndex < waypoints.Count)
            {
                Transform waypoint = waypoints[fromIndex];
                waypoints.RemoveAt(fromIndex);
                waypoints.Insert(toIndex, waypoint);
            }
        }
        
        /// <summary>
        /// 기즈모 그리기
        /// </summary>
        public void DrawGizmos()
        {
            if (waypoints.Count < 2) return;
            
            // 도착 경로 그리기
            Gizmos.color = routeColor;
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] != null && waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
                    
                    // 방향 화살표
                    Vector3 direction = (waypoints[i + 1].position - waypoints[i].position).normalized;
                    Vector3 midPoint = Vector3.Lerp(waypoints[i].position, waypoints[i + 1].position, 0.5f);
                    DrawArrow(midPoint, direction, 2f);
                }
            }
            
            // 출발 경로 그리기
            Gizmos.color = departureRouteColor;
            for (int i = 0; i < departureWaypoints.Count - 1; i++)
            {
                if (departureWaypoints[i] != null && departureWaypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(departureWaypoints[i].position, departureWaypoints[i + 1].position);
                    
                    // 방향 화살표
                    Vector3 direction = (departureWaypoints[i + 1].position - departureWaypoints[i].position).normalized;
                    Vector3 midPoint = Vector3.Lerp(departureWaypoints[i].position, departureWaypoints[i + 1].position, 0.5f);
                    DrawArrow(midPoint, direction, 2f);
                }
            }
            
            // 마지막 웨이포인트에서 정박지까지 연결
            if (waypoints.Count > 0 && waypoints[waypoints.Count - 1] != null && dockingPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(waypoints[waypoints.Count - 1].position, dockingPoint.position);
            }
            
            // 웨이포인트 표시
            if (showWaypoints)
            {
                // 도착 웨이포인트
                Gizmos.color = routeColor;
                for (int i = 0; i < waypoints.Count; i++)
                {
                    if (waypoints[i] != null)
                    {
                        Gizmos.DrawWireSphere(waypoints[i].position, 1f);
                        
                        #if UNITY_EDITOR
                        UnityEditor.Handles.Label(waypoints[i].position + Vector3.up * 2f, $"WP{i}");
                        #endif
                    }
                }
                
                // 출발 웨이포인트
                Gizmos.color = departureRouteColor;
                for (int i = 0; i < departureWaypoints.Count; i++)
                {
                    if (departureWaypoints[i] != null)
                    {
                        Gizmos.DrawWireSphere(departureWaypoints[i].position, 1f);
                        
                        #if UNITY_EDITOR
                        UnityEditor.Handles.Label(departureWaypoints[i].position + Vector3.up * 2f, $"DP{i}");
                        #endif
                    }
                }
            }
            
            // 정박 지역 표시
            if (showDockingArea && dockingPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(dockingPoint.position, Vector3.one * 3f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(dockingPoint.position + Vector3.up * 3f, "DOCK");
                #endif
            }
        }
        
        private void DrawArrow(Vector3 position, Vector3 direction, float size)
        {
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized * size * 0.3f;
            Vector3 forward = direction * size * 0.5f;
            
            Gizmos.DrawLine(position, position + forward);
            Gizmos.DrawLine(position + forward, position + forward * 0.7f + right);
            Gizmos.DrawLine(position + forward, position + forward * 0.7f - right);
        }
    }
} 