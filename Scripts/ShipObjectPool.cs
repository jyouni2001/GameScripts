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
        
        public Vector3 FirstShipPos; 

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        // 풀 관리 (프리펩별로 분리)
        private Dictionary<GameObject, Queue<GameObject>> availableShipsByPrefab = new Dictionary<GameObject, Queue<GameObject>>();
        private Dictionary<GameObject, List<GameObject>> allShipsByPrefab = new Dictionary<GameObject, List<GameObject>>();
        private HashSet<GameObject> activeShips = new HashSet<GameObject>();
        
        // 기존 호환성을 위한 레거시 풀 (기본 프리펩용)
        private Queue<GameObject> availableShips = new Queue<GameObject>();
        private List<GameObject> allShips = new List<GameObject>();
        
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
        /// 특정 프리펩으로 배 가져오기
        /// </summary>
        /// <param name="prefab">사용할 배 프리펩</param>
        /// <returns>배 오브젝트</returns>
        public GameObject GetShipByPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                DebugLog("프리펩이 null입니다.");
                return null;
            }
            
            // 해당 프리펩의 풀이 없으면 생성
            if (!availableShipsByPrefab.ContainsKey(prefab))
            {
                InitializePrefabPool(prefab, poolSize);
            }
            
            GameObject ship = null;
            
            // 사용 가능한 배 가져오기
            if (availableShipsByPrefab[prefab].Count > 0)
            {
                ship = availableShipsByPrefab[prefab].Dequeue();
            }
            else if (expandPool)
            {
                // 풀 확장 - 새 배 생성
                ship = CreateNewShipFromPrefab(prefab);
                DebugLog($"풀 확장으로 새 배 생성: {prefab.name}");
            }
            else
            {
                DebugLog($"사용 가능한 배가 없습니다 ({prefab.name}).");
                return null;
            }
            
            if (ship != null)
            {
                // 배 활성화
                ship.SetActive(true);
                activeShips.Add(ship);
                
                DebugLog($"배 대여: {ship.name} (프리펩: {prefab.name}, 활성: {ActiveShips})");
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
            //ship.transform.position = Vector3.zero;
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
            
            FirstShipPos.y = newShip.transform.localPosition.y;
            newShip.transform.position = FirstShipPos;

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
        
        /// <summary>
        /// 특정 프리펩용 풀 초기화
        /// </summary>
        /// <param name="prefab">프리펩</param>
        /// <param name="size">초기 크기</param>
        private void InitializePrefabPool(GameObject prefab, int size)
        {
            if (!availableShipsByPrefab.ContainsKey(prefab))
            {
                availableShipsByPrefab[prefab] = new Queue<GameObject>();
                allShipsByPrefab[prefab] = new List<GameObject>();
            }
            
            // 초기 풀 크기만큼 배 생성
            for (int i = 0; i < size; i++)
            {
                GameObject newShip = CreateNewShipFromPrefab(prefab);
                allShipsByPrefab[prefab].Add(newShip);
                availableShipsByPrefab[prefab].Enqueue(newShip);
            }
            
            DebugLog($"프리펩 '{prefab.name}' 풀 초기화 완료: {size}개 생성");
        }
        
        /// <summary>
        /// 특정 프리펩으로 새 배 생성
        /// </summary>
        /// <param name="prefab">프리펩</param>
        /// <returns>생성된 배</returns>
        private GameObject CreateNewShipFromPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            
            GameObject newShip = Instantiate(prefab, FirstShipPos, Quaternion.identity);
            newShip.name = $"{prefab.name}_{GetNextShipId()}";
            newShip.transform.SetParent(poolParent);
            newShip.SetActive(false);
            
            // ShipController 컴포넌트 확인 및 추가
            if (newShip.GetComponent<ShipController>() == null)
            {
                newShip.AddComponent<ShipController>();
            }
            
            // 프리펩별 리스트에 추가
            if (!allShipsByPrefab.ContainsKey(prefab))
            {
                allShipsByPrefab[prefab] = new List<GameObject>();
            }
            allShipsByPrefab[prefab].Add(newShip);
            
            DebugLog($"새 배 생성: {newShip.name} (프리펩: {prefab.name})");
            
            return newShip;
        }
        
        /// <summary>
        /// 다음 배 ID 생성
        /// </summary>
        /// <returns>고유 ID</returns>
        private int GetNextShipId()
        {
            int totalShips = allShips.Count;
            foreach (var ships in allShipsByPrefab.Values)
            {
                totalShips += ships.Count;
            }
            return totalShips;
        }
        
        // 시스템 정리
        private void OnDestroy()
        {
            ReturnAllShips();
            
            // 기존 풀 정리
            foreach (var ship in allShips)
            {
                if (ship != null)
                {
                    DestroyImmediate(ship);
                }
            }
            
            // 프리펩별 풀 정리
            foreach (var ships in allShipsByPrefab.Values)
            {
                foreach (var ship in ships)
                {
                    if (ship != null)
                    {
                        DestroyImmediate(ship);
                    }
                }
            }
            
            allShips.Clear();
            availableShips.Clear();
            activeShips.Clear();
            availableShipsByPrefab.Clear();
            allShipsByPrefab.Clear();
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