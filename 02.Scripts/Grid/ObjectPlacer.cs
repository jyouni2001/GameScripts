using System;
using System.Collections.Generic;
using UnityEngine;
using JY;
public class ObjectPlacer : MonoBehaviour
{
    public static ObjectPlacer Instance { get; set; }
    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [SerializeField] public List<GameObject> placedGameObjects = new();
    [SerializeField] private InputManager inputManager;
    [SerializeField] private ChangeFloorSystem changeFloorSystem;

    [SerializeField] private AutoNavMeshBaker navMeshBaker;

    /// <summary>
    /// 매개 변수의 오브젝트들을 배치한다.
    /// </summary> 
    /// <param name="prefab"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation, int? floorOverride = null)
    {
        GameObject newObject = Instantiate(prefab); //, BatchedObj.transform, true);
        newObject.transform.position = position;

        Debug.Log($"여기에 설치 {position}");

        newObject.transform.rotation = rotation;
        SoundManager.PlaySound(SoundType.Build, 0.1f);

        // 현재 층에 따라 레이어 설정
        int floorToSet = floorOverride ?? changeFloorSystem.currentFloor;
        string layerName = $"{floorToSet}F";

        //int currentFloor = changeFloorSystem.currentFloor;
        //string layerName = $"{currentFloor}F"; // 예: "1F", "2F", "3F", "4F"

        int layer = LayerMask.NameToLayer(layerName);
        int stairColliderLayer = LayerMask.NameToLayer("StairCollider");

        if (layer == -1)
        {
            Debug.LogError($"Layer {layerName} not found!");
        }
        else
        {
            // 모든 자손 오브젝트의 레이어 변경
            foreach (Transform child in newObject.transform.GetComponentsInChildren<Transform>(true))
            {
                if (child != newObject.transform && child.gameObject.layer != stairColliderLayer)
                {
                    child.gameObject.layer = layer;
                }
            }
        }
        
        // 비어 있는 인덱스 찾기
        int index = -1;
        for (int i = 0; i < placedGameObjects.Count; i++)
        {
            if (placedGameObjects[i] == null)
            {
                index = i;
                break;
            }
        }

        // 비어 있는 인덱스가 없으면 끝에 추가
        if (index == -1)
        {
            placedGameObjects.Add(newObject);
            index = placedGameObjects.Count - 1;
        }
        else
        {
            placedGameObjects[index] = newObject;
        }

        navMeshBaker?.RebuildNavMesh();

        return index;
        
        //placedGameObjects.Add(newObject);
        //return placedGameObjects.Count - 1;
    }

    /// <summary>
    /// 오브젝트들을 삭제한다.
    /// </summary>
    /// <param name="index"></param>
    public void RemoveObject(int index)
    {
        navMeshBaker?.RebuildNavMesh();

        if (index >= 0 && index < placedGameObjects.Count)
        {
            GameObject obj = placedGameObjects[index];
            if (obj != null)
            {
                Destroy(obj);
                
            }
            //placedGameObjects.RemoveAt(index);
            placedGameObjects[index] = null; // 참조 제거 (선택적으로 리스트에서 완전히 제거 가능)            
        }
    }

    /// <summary>
    /// 오브젝트의 인덱스를 추출한다.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public int GetObjectIndex(GameObject obj)
    {
        return placedGameObjects.IndexOf(obj);
    }
}
