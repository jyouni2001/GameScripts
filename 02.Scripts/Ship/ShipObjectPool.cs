using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JY
{
    /// <summary>
    /// 배 오브젝트 풀링 시스템
    /// </summary>
    public class ShipObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject shipPrefab;
        [SerializeField] private int poolSize = 5;
        [SerializeField] private bool expandPool = true; // 풀 크기 자동 확장
        [SerializeField] private Transform poolParent; // 풀 오브젝트들의 부모
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        // 풀 관리
        private Queue<GameObject> availableShips = new Queue<GameObject>();
        private List<GameObject> allShips = new List<GameObject>();
        private HashSet<GameObject> activeShips = new HashSet<GameObject>();
        
        // 통계
        public int TotalShips => allShips.Count;
        public int AvailableShips => availableShips.Count;
        public int ActiveShips => activeShips.Count;
        
        private void Awake()
        {
            SetupPoolParent();
        }
        
        /// <summary>
        /// 풀 초기화
        /// </summary>
        public void Initialize(GameObject prefab, int size)
        {
            if (prefab == null)
            {
                Debug.LogError("[ShipObjectPool] Ship prefab이 null입니다!");
                return;
            }
            
            shipPrefab = prefab;
            poolSize = size;
            
            CreateInitialPool();
            DebugLog($"오브젝트 풀 초기화 완료: {poolSize}개");
        }
        
        /// <summary>
        /// 풀에서 배 가져오기
        /// </summary>
        public GameObject GetShip()
        {
            GameObject ship = null;
            
            // 사용 가능한 배가 있는지 확인
            if (availableShips.Count > 0)
            {
                ship = availableShips.Dequeue();
            }
            else if (expandPool)
            {
                // 풀 확장
                ship = CreateNewShip();
                DebugLog("풀 확장으로 새 배 생성");
            }
            else
            {
                DebugLog("사용 가능한 배가 없습니다.");
                return null;
            }
            
            if (ship != null)
            {
                // 배 활성화
                ship.SetActive(true);
                activeShips.Add(ship);
                
                DebugLog($"배 대여: {ship.name} (활성: {ActiveShips}, 대기: {AvailableShips})");
            }
            
            return ship;
        }
        
        /// <summary>
        /// 배를 풀로 반환
        /// </summary>
        public void ReturnShip(GameObject ship)
        {
            if (ship == null)
            {
                DebugLog("반환하려는 배가 null입니다.");
                return;
            }
            
            if (!activeShips.Contains(ship))
            {
                DebugLog($"이 배는 풀에서 관리되지 않습니다: {ship.name}");
                return;
            }
            
            // 배 리셋
            ShipController controller = ship.GetComponent<ShipController>();
            if (controller != null)
            {
                controller.ResetShip();
            }
            
            // 비활성화 및 풀로 반환
            ship.SetActive(false);
            ship.transform.SetParent(poolParent);
            ship.transform.position = Vector3.zero;
            ship.transform.rotation = Quaternion.identity;
            
            activeShips.Remove(ship);
            availableShips.Enqueue(ship);
            
            DebugLog($"배 반환: {ship.name} (활성: {ActiveShips}, 대기: {AvailableShips})");
        }
        
        /// <summary>
        /// 모든 활성 배를 풀로 반환
        /// </summary>
        public void ReturnAllShips()
        {
            var shipsToReturn = new List<GameObject>(activeShips);
            
            foreach (var ship in shipsToReturn)
            {
                ReturnShip(ship);
            }
            
            DebugLog($"모든 배 반환 완료: {shipsToReturn.Count}개");
        }
        
        /// <summary>
        /// 풀 크기 조정
        /// </summary>
        public void ResizePool(int newSize)
        {
            if (newSize < 0)
            {
                DebugLog("풀 크기는 0 이상이어야 합니다.");
                return;
            }
            
            int currentSize = allShips.Count;
            
            if (newSize > currentSize)
            {
                // 풀 확장
                int shipsToAdd = newSize - currentSize;
                for (int i = 0; i < shipsToAdd; i++)
                {
                    CreateNewShip();
                }
                DebugLog($"풀 확장: {shipsToAdd}개 추가");
            }
            else if (newSize < currentSize)
            {
                // 풀 축소 (비활성 배만 제거)
                int shipsToRemove = currentSize - newSize;
                int removed = 0;
                
                while (removed < shipsToRemove && availableShips.Count > 0)
                {
                    GameObject ship = availableShips.Dequeue();
                    allShips.Remove(ship);
                    DestroyImmediate(ship);
                    removed++;
                }
                
                DebugLog($"풀 축소: {removed}개 제거");
            }
            
            poolSize = newSize;
        }
        
        /// <summary>
        /// 풀 상태 정보 가져오기
        /// </summary>
        public PoolStatus GetPoolStatus()
        {
            return new PoolStatus
            {
                totalShips = TotalShips,
                activeShips = ActiveShips,
                availableShips = AvailableShips,
                poolUtilization = TotalShips > 0 ? (float)ActiveShips / TotalShips : 0f
            };
        }
        
        private void SetupPoolParent()
        {
            if (poolParent == null)
            {
                GameObject poolParentObj = new GameObject("Ship Pool");
                poolParentObj.transform.SetParent(transform);
                poolParent = poolParentObj.transform;
            }
        }
        
        private void CreateInitialPool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                CreateNewShip();
            }
        }
        
        private GameObject CreateNewShip()
        {
            if (shipPrefab == null)
            {
                DebugLog("Ship prefab이 설정되지 않았습니다.");
                return null;
            }
            
            GameObject newShip = Instantiate(shipPrefab, poolParent);
            newShip.name = $"Ship_{allShips.Count:D3}";
            newShip.SetActive(false);
            
            // ShipController 컴포넌트 확인
            if (newShip.GetComponent<ShipController>() == null)
            {
                newShip.AddComponent<ShipController>();
            }
            
            // 시각적 효과 컴포넌트 추가 (나중에 활성화 가능)
            /*
            // 파티클 시스템 추가
            if (newShip.GetComponentInChildren<ParticleSystem>() == null)
            {
                // 파티클 시스템 생성 및 설정
            }
            
            // 오디오 소스 추가
            if (newShip.GetComponent<AudioSource>() == null)
            {
                AudioSource audio = newShip.AddComponent<AudioSource>();
                // 오디오 설정
            }
            */
            
            allShips.Add(newShip);
            availableShips.Enqueue(newShip);
            
            return newShip;
        }
        
        private void DebugLog(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[ShipObjectPool] {message}");
            }
        }
        
        // 에디터에서 풀 상태 확인용
        private void OnValidate()
        {
            if (poolSize < 0)
            {
                poolSize = 0;
            }
        }
        
        // 시스템 정리
        private void OnDestroy()
        {
            ReturnAllShips();
            
            foreach (var ship in allShips)
            {
                if (ship != null)
                {
                    DestroyImmediate(ship);
                }
            }
            
            allShips.Clear();
            availableShips.Clear();
            activeShips.Clear();
        }
    }
    
    /// <summary>
    /// 풀 상태 정보
    /// </summary>
    [System.Serializable]
    public struct PoolStatus
    {
        public int totalShips;
        public int activeShips;
        public int availableShips;
        public float poolUtilization; // 0.0 ~ 1.0
    }
} 