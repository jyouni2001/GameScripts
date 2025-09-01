using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건축물들의 데이터를 저장하는 스크립터블오브젝트 
/// </summary>
[CreateAssetMenu]
public class ObjectsDatabaseSO : ScriptableObject
{
    public List<ObjectData> objectsData;
    //최적화 시도
    private Dictionary<int, ObjectData> objectDataDict;
    
    public void InitializeDictionary()
    {
        objectDataDict = new Dictionary<int, ObjectData>();
        foreach (var data in objectsData)
        {
            objectDataDict[data.ID] = data;
        }
    }
    
    public ObjectData GetObjectData(int id)
    {
        return objectDataDict.TryGetValue(id, out var data) ? data : null;
    }
}

[Serializable]
public class ObjectData
{
    [field : SerializeField]
    public string Name { get;  set; }
    
    [field : SerializeField]
    public int ID { get;  set; }

    [field : SerializeField]
    public int kindIndex { get;  set; }

    [field: SerializeField]
    public bool IsWall; 

    [field: SerializeField] 
    public Vector2Int Size { get;  set; } = Vector2Int.one;
    
    [field : SerializeField]
    public GameObject Prefab { get;  set; }

    [field : SerializeField]
    public int BuildPrice { get;  set; }

    [field: SerializeField]
    public int BasePrice { get;  set; }
    
    [field: SerializeField]
    public int ReputationValue { get; set; }
}
