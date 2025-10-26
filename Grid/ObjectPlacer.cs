using DG.Tweening;
using JY;
using System;
using System.Collections.Generic;
using UnityEngine;
public class ObjectPlacer : MonoBehaviour
{
    // 25-10-16 영상 찍기용 변수
    public bool videoTaking = false;
    //
    

    public float fallHeight = 5f; // 오브젝트가 떨어질 시작 높이
    public float fallDuration = 0.5f; // 떨어지는 애니메이션 시간
    public Ease fallEase = Ease.OutBounce; // 애니메이션 이징(부드러움) 효과
    public Ease destroyEase = Ease.OutBounce;
    public static ObjectPlacer Instance { get; set; }

    private ObjectPoolManager objectPool;

    private void Awake()
    {
        // 싱글톤 설정
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }
    private void Start()
    {
        // 싱글톤 인스턴스 참조
        objectPool = ObjectPoolManager.Instance;
        if (objectPool == null)
        {
            Debug.LogError("씬에 ObjectPoolManager가 존재하지 않습니다!");
        }
    }

    [SerializeField] public List<GameObject> placedGameObjects = new();
    [SerializeField] private ChangeFloorSystem changeFloorSystem;
    [SerializeField] private AutoNavMeshBaker navMeshBaker;
    [SerializeField] private SpawnEffect spawnEffect;

    /// <summary>
    /// 매개 변수의 오브젝트들을 배치한다.
    /// </summary> 
    /// <param name="prefab"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public int PlaceObject(GameObject prefab, Vector3 position, Quaternion rotation, int? floorOverride = null)
    {
        //GameObject newObject = Instantiate(prefab); //, BatchedObj.transform, true);
        GameObject newObject = objectPool.Get(prefab, position, rotation);

        if (videoTaking)
        {         
            // 2. 프리펩 이름을 '_' 기준으로 분리합니다.
            string[] nameParts = prefab.name.Split('_');

            // 3. 이름이 "pv_furniture_name" 형식인지 확인하고 레이어를 설정합니다.
            // nameParts.Length > 1 : '_'가 포함되어 분리된 요소가 2개 이상인지 확인
            if (nameParts.Length > 1)
            {
                // 두 번째 요소(furniture)를 레이어 이름으로 사용합니다.
                string layerNamePart = nameParts[1];

                // LayerMask는 대소문자를 구분하므로 첫 글자를 대문자로 변경해줍니다. (e.g., "furniture" -> "Furniture")
                string layerName2 = char.ToUpper(layerNamePart[0]) + layerNamePart.Substring(1);

                // 해당 이름의 레이어가 존재하는지 확인합니다.
                int layerIndex = LayerMask.NameToLayer(layerName2);
                if (layerIndex != -1) // -1은 해당 이름의 레이어가 없다는 의미입니다.
                {
                    newObject.layer = layerIndex;
                }
                else
                {
                    // 규칙에 맞는 이름을 가졌지만, 실제 프로젝트에 해당 레이어가 없는 경우 경고를 출력하고 기본 레이어로 설정합니다.
                    Debug.LogWarning($"레이어를 찾을 수 없습니다: '{layerName2}'. '{newObject.name}'에 기본 레이어를 설정합니다.");
                    newObject.layer = 0; // 0은 Default 레이어입니다.
                }
            }
            else
            {
                // 이름이 규칙에 맞지 않는 경우, 기본 레이어로 설정합니다.
                newObject.layer = 0; // 0은 Default 레이어입니다.
            }
        }
        

        // DOTween 애니메이션을 위해 오브젝트의 시작 위치를 목표 위치보다 높게 설정
        Vector3 startPosition = new Vector3(position.x, position.y + fallHeight, position.z);
        newObject.transform.position = startPosition;
        newObject.transform.rotation = rotation;

        // 반환 시 이상하게 되면 이부분 삭제 
        newObject.transform.localScale = Vector3.one;

        newObject.transform.DOMove(position, fallDuration)
                 .SetEase(fallEase).SetUpdate(true);

        if (SFXManager.Instance != null) SFXManager.PlaySound(SoundType.Build, 0.1f);

        spawnEffect.OnBuildingPlaced(position);

        // 현재 층에 따라 레이어 설정
        int floorToSet = floorOverride ?? changeFloorSystem.currentFloor;
        string layerName = $"{floorToSet}F";
        int layer = LayerMask.NameToLayer(layerName);
        int stairColliderLayer = LayerMask.NameToLayer("StairCollider");

        if (layer != -1)
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

        // 주방 감지기에게 실제 배치된 오브젝트 알림
        if (JY.KitchenDetector.Instance != null)
        {
            Debug.Log($"✅ KitchenDetector 인스턴스 발견! 배치 알림 전송: {newObject.name}");
            JY.KitchenDetector.Instance.OnFurnitureePlaced(newObject, position);
        }
        else
        {
            Debug.Log("❌ KitchenDetector.Instance가 null입니다! 씬에 KitchenDetector가 있는지 확인하세요.");
        }

        PlacementSystem.Instance.MarkNavMeshDirty();
        //navMeshBaker?.RebuildNavMesh();
        return index;
    }

    /// <summary>
    /// 오브젝트들을 삭제한다.
    /// </summary>
    /// <param name="index"></param>
    public void RemoveObject(int index)
    {
        PlacementSystem.Instance.MarkNavMeshDirty();
        //navMeshBaker?.RebuildNavMesh();

        if (index >= 0 && index < placedGameObjects.Count)
        {

            GameObject obj = placedGameObjects[index];
            if (obj != null)
            {
                string objectName = obj.name;
                bool hasCounterTag = obj.CompareTag("Counter");
                
                if (!hasCounterTag)
                {
                    // 자식 오브젝트들 중에 Counter 태그가 있는지 확인
                    Transform[] children = obj.GetComponentsInChildren<Transform>();
                    foreach (Transform child in children)
                    {
                        if (child.CompareTag("Counter"))
                        {
                            hasCounterTag = true;
                            break;
                        }
                    }
                }
                
                obj.transform.DOScale(Vector3.zero, 0.3f).SetEase(destroyEase).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        
                        if (hasCounterTag && EmployeeHiringSystem.Instance != null)
                        {
                            EmployeeHiringSystem.Instance.OnCounterDestroyed(obj);
                        }
                        
                        if (KitchenDetector.Instance != null)
                        {
                            KitchenDetector.Instance.OnFurnitureRemoved(obj, obj.transform.position);
                        }

                        objectPool.Return(obj);

                        spawnEffect.OnBuildingPlaced(obj.transform.position);
                    });
            }
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

    /// <summary>
    /// 애니메이션 없이 오브젝트를 즉시 풀로 반환합니다. (세이브/로드용)
    /// </summary>
    /// <param name="index"></param>
    public void RemoveObjectImmediate(int index)
    {
        if (index >= 0 && index < placedGameObjects.Count)
        {
            GameObject obj = placedGameObjects[index];
            if (obj != null)
            {
                // 주방 감지기에 제거 알림 (필요 시)
                if (KitchenDetector.Instance != null)
                {
                    KitchenDetector.Instance.OnFurnitureRemoved(obj, obj.transform.position);
                }

                // DOTween 애니메이션 없이 바로 풀에 반환
                objectPool.Return(obj);
            }
            placedGameObjects[index] = null; // 리스트에서 참조만 제거
        }
    }

}
