using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치된 오브젝트의 데이터를 저장하는 클래스
/// </summary>
/// 
/*[System.Serializable]
public class PlacementData 
{
    public List<Vector3Int> occupiedPositions;
    public int ID { get;  set; }
    public int PlacedObjectIndex { get; set; }


    // KindIndex
    // 0 = 바닥 오브젝트
    // 1 = 가구 오브젝트
    // 2 = 벽 오브젝트
    // 3 = 장식품 오브젝트
    
    public int kindIndex { get; set; }
    public Quaternion Rotation { get; set; } // <<< 회전 정보 추가

    public PlacementData(List<Vector3Int> occupiedPositions, int id, int placedObjectIndex, int kindOfIndex, Quaternion rotation)
    {
        this.occupiedPositions = occupiedPositions;
        ID = id;
        PlacedObjectIndex = placedObjectIndex;
        kindIndex = kindOfIndex;
        Rotation = rotation;
    }
}*/

[System.Serializable]
public class PlacementData
{
    public List<Vector3Int> occupiedPositions;
    [SerializeField] private int id;
    [SerializeField] private int placedObjectIndex;
    [SerializeField] private int kindIndex;
    [SerializeField] private Quaternion rotation;

    public int ID => id;
    public int PlacedObjectIndex
    {
        get => placedObjectIndex;
        set => placedObjectIndex = value;
    }
    public int KindIndex => kindIndex;
    public Quaternion Rotation => rotation;

    public PlacementData(List<Vector3Int> occupiedPositions, int id, int placedObjectIndex, int kindOfIndex, Quaternion rotation)
    {
        this.occupiedPositions = occupiedPositions;
        foreach(Vector3Int pos in occupiedPositions)
        {
            Debug.Log($"설치 = {pos}");
        }
        
        this.id = id;
        this.placedObjectIndex = placedObjectIndex;
        this.kindIndex = kindOfIndex;
        this.rotation = rotation;
    }
}