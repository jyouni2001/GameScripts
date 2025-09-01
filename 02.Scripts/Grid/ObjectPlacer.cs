using DG.Tweening;
using JY;
using System;
using System.Collections.Generic;
using UnityEngine;
public class ObjectPlacer : MonoBehaviour
{
    public float fallHeight = 5f; // 오브젝트가 떨어질 시작 높이
    public float fallDuration = 0.5f; // 떨어지는 애니메이션 시간
    public Ease fallEase = Ease.OutBounce; // 애니메이션 이징(부드러움) 효과
    public Ease destroyEase = Ease.InElastic;
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

    private void Start()
    {
        sequence = DOTween.Sequence();
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
        GameObject newObject = Instantiate(prefab); //, BatchedObj.transform, true);

        // DOTween 애니메이션을 위해 오브젝트의 시작 위치를 목표 위치보다 높게 설정
        Vector3 startPosition = new Vector3(position.x, position.y + fallHeight, position.z);
        newObject.transform.position = startPosition;

        /*newObject.transform.position = position;*/

        Debug.Log($"여기에 설치 {position}");

        newObject.transform.rotation = rotation;
        

        newObject.transform.DOMove(position, fallDuration)
                 .SetEase(fallEase);

        SFXManager.PlaySound(SoundType.Build, 0.1f);

        spawnEffect.OnBuildingPlaced(position);

        // 현재 층에 따라 레이어 설정
        int floorToSet = floorOverride ?? changeFloorSystem.currentFloor;
        string layerName = $"{floorToSet}F";

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
    }

    // Sequence 풀링을 위한 리스트
    private List<Sequence> activeSequences = new List<Sequence>();
    // Sequence 생성 및 풀링 관리
    Sequence sequence;

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
                activeSequences.Add(sequence);

                // DOScale 애니메이션 추가 (0.3초 동안 스케일 0으로 축소)
                sequence.Append(obj.transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack));

                // 애니메이션 완료 후 실행될 콜백 추가
                sequence.OnComplete(() =>
                {
                    // GameObject 파괴
                    Destroy(obj);
                    // 효과 재생
                    spawnEffect.OnBuildingPlaced(obj.transform.position);
                    activeSequences.Remove(sequence);
                    sequence.Kill();
                });

                /*obj.transform.DOScale(Vector3.zero, 0.3f).SetEase(destroyEase);
                Destroy(obj);
                spawnEffect.OnBuildingPlaced(obj.transform.position);*/
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

    // 모든 Sequence 정리 (필요 시 호출)
    public void CleanupSequences()
    {
        foreach (var sequence in activeSequences)
        {
            sequence.Kill();
        }
        activeSequences.Clear();
    }

    private void OnDestroy()
    {
        CleanupSequences(); // MonoBehaviour 파괴 시 모든 Sequence 정리
    }
}
