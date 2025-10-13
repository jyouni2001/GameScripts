using UnityEngine;

namespace JY
{
    /// <summary>
    /// 다층 건물 시스템을 위한 층 관련 상수 정의
    /// </summary>
    public static class FloorConstants 
{
    // 층간 높이 (건축 시스템과 동일하게 맞춤)
    public const float FLOOR_HEIGHT = 4.6f;
    
    // 방 내부 높이 (천장까지의 높이)
    public const float ROOM_HEIGHT = 4.0f;
    
    // 층 구분 허용 오차 (Y축 좌표 비교 시 사용)
    public const float FLOOR_TOLERANCE = 2.3f; // 층간 높이의 절반
    
    // 바닥 감지 오프셋
    public const float FLOOR_DETECTION_OFFSET = 0f;
    
    /// <summary>
    /// Y 좌표를 기반으로 층 레벨을 계산합니다.
    /// </summary>
    /// <param name="yPosition">월드 Y 좌표</param>
    /// <returns>층 번호 (1층부터 시작)</returns>
    public static int GetFloorLevel(float yPosition) 
    {
        return Mathf.FloorToInt(yPosition / FLOOR_HEIGHT) + 1;
    }
    
    /// <summary>
    /// 층 번호를 기반으로 해당 층의 기준 Y 좌표를 반환합니다.
    /// </summary>
    /// <param name="floorLevel">층 번호 (1층부터 시작)</param>
    /// <returns>해당 층의 기준 Y 좌표</returns>
    public static float GetFloorBaseY(int floorLevel)
    {
        return (floorLevel - 1) * FLOOR_HEIGHT;
    }
    
    /// <summary>
    /// 두 Y 좌표가 같은 층에 있는지 확인합니다.
    /// </summary>
    /// <param name="y1">첫 번째 Y 좌표</param>
    /// <param name="y2">두 번째 Y 좌표</param>
    /// <returns>같은 층에 있으면 true</returns>
    public static bool IsSameFloor(float y1, float y2)
    {
        return GetFloorLevel(y1) == GetFloorLevel(y2);
    }
    
    /// <summary>
    /// 특정 층의 방 범위를 계산합니다.
    /// </summary>
    /// <param name="floorLevel">층 번호</param>
    /// <param name="bounds">기존 bounds</param>
    /// <returns>층별로 조정된 bounds</returns>
    public static Bounds GetFloorBounds(int floorLevel, Bounds bounds)
    {
        float floorBaseY = GetFloorBaseY(floorLevel);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        
        min.y = floorBaseY;
        max.y = floorBaseY + ROOM_HEIGHT;
        
        Bounds adjustedBounds = new Bounds();
        adjustedBounds.SetMinMax(min, max);
        return adjustedBounds;
    }
    }
} 