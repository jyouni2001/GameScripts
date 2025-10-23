using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Unity.AI.Navigation;

namespace JY
{
public class AutoNavMeshBaker : MonoBehaviour
{
    public NavMeshSurface _navsurface;
    
    [Header("NavMesh 설정")]
    [Tooltip("NavMesh를 생성할 태그들")]
    public string[] tagsToBake;
    
    [Tooltip("NavMesh 생성 시 사용할 높이")]
    public float agentHeight = 2f;
    
    [Tooltip("NavMesh 생성 시 사용할 반경")]
    public float agentRadius = 0.5f;
    
    [Tooltip("NavMesh 생성 시 사용할 경사")]
    public float agentSlope = 45f;
    
    [Tooltip("NavMesh 생성 시 사용할 스텝 높이")]
    public float agentStepHeight = 0.4f;
    
    [Tooltip("NavMesh 생성 시 사용할 최대 거리")]
    public float maxJumpDistance = 2f;
    
    [Tooltip("NavMesh 생성 시 사용할 최소 거리")]
    public float minJumpDistance = 0.5f;

    [Header("자동 업데이트 설정")]
    [Tooltip("실행 중에 NavMesh를 자동으로 업데이트할지 여부")]
    public bool autoUpdate = true;
    
    [Tooltip("자동 업데이트 간격 (초)")]
    public float updateInterval = 1f;

    [Header("디버그 설정")]
    [Tooltip("디버그 로그를 표시할지 여부")]
    public bool showDebugLogs = false;

    // 문제가 되는 부분들을 주석 처리하거나 수정
    // private NavMeshData navMeshData;
    // private NavMeshDataInstance navMeshInstance;
    private float nextUpdateTime;
    private Dictionary<string, List<GameObject>> tagObjectCache = new Dictionary<string, List<GameObject>>();
    private bool isInitialized = false;
    private bool isBaking = false;

    void Start()
    {
        InitializeNavMesh();
    }

    void InitializeNavMesh()
    {
        if (_navsurface == null)
        {
            return;
        }

        if (tagsToBake == null || tagsToBake.Length == 0)
        {
            return;
        }
        
        // 태그별 오브젝트 캐시 초기화
        CacheTaggedObjects();

        // NavMeshSurface를 사용하여 초기 빌드
        StartCoroutine(BuildNavMeshAsync());
        
        isInitialized = true;
    }

    void CacheTaggedObjects()
    {
        tagObjectCache.Clear();

        foreach (var tag in tagsToBake)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            
            var taggedObjects = GameObject.FindGameObjectsWithTag(tag);
            tagObjectCache[tag] = new List<GameObject>(taggedObjects);

            if (showDebugLogs)
            {
            }
        }
    }
    
    void Update()
    {
        if (!isInitialized) return;

        if (autoUpdate && Time.time >= nextUpdateTime && !isBaking)
        {
            // 태그된 오브젝트들이 변경되었는지 확인
            if (HasTaggedObjectsChanged())
            {
                CacheTaggedObjects(); // 캐시 업데이트
                StartCoroutine(BuildNavMeshAsync());
                
                if (showDebugLogs)
                {
                }
            }
            
            nextUpdateTime = Time.time + updateInterval;
        }
    }
    
    // 태그된 오브젝트들이 변경되었는지 확인하는 메서드
    bool HasTaggedObjectsChanged()
    {
        foreach (var tag in tagsToBake)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            
            var currentObjects = GameObject.FindGameObjectsWithTag(tag);
            
            if (!tagObjectCache.ContainsKey(tag) || 
                tagObjectCache[tag].Count != currentObjects.Length)
            {
                return true;
            }
            
            // 실제 오브젝트들이 같은지 확인
            var cachedObjects = tagObjectCache[tag];
            for (int i = 0; i < currentObjects.Length; i++)
            {
                if (!cachedObjects.Contains(currentObjects[i]))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    IEnumerator BuildNavMeshAsync()
    {
        if (_navsurface == null)
        {
            yield break;
        }

        // *** 수정된 부분 시작 ***
        isBaking = true; // 베이킹 시작 플래그
        if (showDebugLogs) { }

        // NavMesh 데이터가 없으면 새로 빌드
        if (_navsurface.navMeshData == null)
        {
            _navsurface.BuildNavMesh();
        }
        else
        {
            AsyncOperation operation = _navsurface.UpdateNavMesh(_navsurface.navMeshData);

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        // NavMesh 통계 출력
        var triangulation = NavMesh.CalculateTriangulation();

        if (showDebugLogs)
        {
        }

        isBaking = false; // 베이킹 완료 플래그
        // *** 수정된 부분 끝 ***        

        //isBaking = true;

        // NavMeshSurface의 설정을 동적으로 업데이트
        /*var agent = NavMesh.GetSettingsByID(0);
        agent.agentRadius = agentRadius;
        agent.agentHeight = agentHeight;
        agent.agentSlope = agentSlope;
        agent.agentClimb = agentStepHeight;*/

        /*// NavMeshSurface를 사용하여 비동기 빌드
        AsyncOperation operation = _navsurface.UpdateNavMesh(_navsurface.navMeshData);
        
        while (!operation.isDone)
        {
            yield return null;
        }
        
        if (showDebugLogs)
        {
        }*/        
        //isBaking = false;       
    }

    // 수동으로 NavMesh를 다시 빌드하는 공개 메서드
    public void RebuildNavMesh()
    {
        // *** 수정된 부분 시작 ***
        // 이미 베이킹 중이면 재요청 무시
        if (isBaking)
        {
            if (showDebugLogs) { }
            return;
        }
        StartCoroutine(BuildNavMeshAsync());
        // *** 수정된 부분 끝 ***
    }

        // 수동으로 NavMesh를 다시 빌드하는 공개 메서드
        /*public void RebuildNavMesh()
        { // 드래그를 해서 여러개 설치, 여러번 반복해. 코루틴도 여러번 반복이야. 여기 코루틴에 isBaking이 false일때만 작동을 해
                // 여러개가 한꺼번에 실행이 되니까, 이미 코루틴 하나가 실행중이라서 isBaking true 상태야.
                // false가 되어야 코루틴이 실행이되는데, true인 상태에서 여러개가 실행이 안되니까 한번만 실행하고 끝내는거야
            //if (!isBaking)
            //{
                CacheTaggedObjects();
                StartCoroutine(BuildNavMeshAsync());
            //}
        }*/

        // 특정 태그의 오브젝트들만 업데이트하는 메서드
        public void UpdateTaggedObjects(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        
        var taggedObjects = GameObject.FindGameObjectsWithTag(tag);
        tagObjectCache[tag] = new List<GameObject>(taggedObjects);
        
        if (showDebugLogs)
        {
        }
    }

    void OnDestroy()
    {
        // NavMeshSurface를 사용하므로 수동으로 정리할 필요 없음
        tagObjectCache.Clear();
    }

    void OnDisable()
    {
        // 컴포넌트가 비활성화될 때 진행 중인 코루틴 정리
        StopAllCoroutines();
        isBaking = false;
    }
    }
}