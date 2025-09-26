using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace JY
{
    /// <summary>
    /// 방 관리 및 요금 청구를 담당하는 매니저 클래스
    /// AI의 방 사용, 결제 처리, 명성도 관리를 통합적으로 처리
    /// </summary>
    public class RoomManager : MonoBehaviour
    {
        [Header("방 관리 설정")]
        [Tooltip("모든 방 내용물 관리 컴포넌트")]
        public List<RoomContents> allRooms = new List<RoomContents>();
        
        [Tooltip("방 결제 시스템 참조")]
        public PaymentSystem paymentSystem;
        
        [Tooltip("명성도 시스템 참조")]
        public ReputationSystem reputationSystem;
        
        [Header("방 설정")]
        [Tooltip("방을 찾을 때 사용할 태그")]
        public string roomTag = "Room";
        
        [Header("가격 설정")]
        [Tooltip("오늘의 방 요금 배율")]
        public float priceMultiplier = 1.0f;
        
        [Header("디버그 설정")]
        [Tooltip("디버그 로그 표시 여부")]
        public bool showDebugLogs = false;
        
        [Tooltip("중요한 이벤트만 로그 표시")]
        public bool showImportantLogsOnly = true;
        
        [Tooltip("방 사용 기록 표시")]
        public bool showUsageLogs = true;
        
        [Header("로그 정보")]
        [Tooltip("사용된 방 정보")]
        [SerializeField] private List<string> usedRoomLogs = new List<string>();
        
        [Tooltip("결제 내역")]
        [SerializeField] private List<string> paymentLogs = new List<string>();
        
        [Header("청소 시스템")]
        [Tooltip("방 청소 상태 추적")]
        [SerializeField] private List<bool> roomCleaningStatus = new List<bool>();
        
        /// <summary>
        /// 시스템 초기화 및 방 자동 검색
        /// </summary>
        private void Start()
        {
            FindAllRooms();
            InitializeCleaningSystem();
            
            // 명성도 시스템이 참조되지 않았다면 자동으로 찾기
            if (reputationSystem == null)
            {
                reputationSystem = ReputationSystem.Instance;
                if (reputationSystem == null)
                {
                    reputationSystem = FindFirstObjectByType<ReputationSystem>();
                }
            }
            
            DebugLog("방 관리 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// 씬의 모든 방 검색
        /// </summary>
        public void FindAllRooms()
        {
            allRooms.Clear();
            
            // 태그로 방 찾기
            GameObject[] roomObjects = GameObject.FindGameObjectsWithTag(roomTag);
            foreach (GameObject roomObj in roomObjects)
            {
                RoomContents roomContents = roomObj.GetComponent<RoomContents>();
                if (roomContents != null)
                {
                    allRooms.Add(roomContents);
                }
            }
            
            // 방 번호 할당
            for (int i = 0; i < allRooms.Count; i++)
            {
                if (string.IsNullOrEmpty(allRooms[i].roomID))
                {
                    allRooms[i].roomID = (i + 101).ToString(); // 101, 102, 103...
                }
            }
            
            DebugLog($"총 {allRooms.Count}개의 방이 감지되었습니다.", true);
            
            if (allRooms.Count == 0)
            {
                DebugLog($"'{roomTag}' 태그를 가진 방을 찾을 수 없습니다. 방 오브젝트에 태그가 설정되어 있는지 확인하세요.", true);
            }
        }
        
        /// <summary>
        /// 새로운 방이 생성되었을 때 호출
        /// </summary>
        public void RegisterNewRoom(RoomContents room)
        {
            if (room != null && !allRooms.Contains(room))
            {
                allRooms.Add(room);
                if (string.IsNullOrEmpty(room.roomID))
                {
                    room.roomID = (allRooms.Count + 100).ToString();
                }
                
                // Sunbed 방인지 확인하고 설정
                if (room.isSunbedRoom && room.fixedPrice > 0)
                {
                    room.SetAsSunbedRoom(room.fixedPrice, room.fixedReputation);
                    DebugLog($"새로운 Sunbed 방 {room.roomID}이(가) 등록되었습니다. 고정 가격: {room.fixedPrice}원, 고정 명성도: {room.fixedReputation}", true);
                }
                else
                {
                    DebugLog($"새로운 방 {room.roomID}이(가) 등록되었습니다.", showImportantLogsOnly);
                }
            }
        }
        
        /// <summary>
        /// RoomDetector에서 방 정보를 받아 RoomContents를 생성하는 메서드
        /// </summary>
        public void RegisterRoomFromDetector(RoomDetector.RoomInfo roomInfo, GameObject roomGameObject)
        {
            RoomContents roomContents = roomGameObject.GetComponent<RoomContents>();
            if (roomContents == null)
            {
                roomContents = roomGameObject.AddComponent<RoomContents>();
            }
            
            roomContents.roomID = roomInfo.roomId;
            roomContents.SetRoomBounds(roomInfo.bounds);
            
            // Sunbed 방인지 확인하고 설정
            if (roomInfo.isSunbedRoom)
            {
                roomContents.SetAsSunbedRoom(roomInfo.fixedPrice, roomInfo.fixedReputation);
                DebugLog($"Sunbed 방 {roomInfo.roomId} 생성 완료. 고정 가격: {roomInfo.fixedPrice}원, 고정 명성도: {roomInfo.fixedReputation}", true);
            }
            
            RegisterNewRoom(roomContents);
        }
        
        /// <summary>
        /// 방이 제거되었을 때 호출
        /// </summary>
        public void UnregisterRoom(RoomContents room)
        {
            if (room != null && allRooms.Contains(room))
            {
                allRooms.Remove(room);
                DebugLog($"방 {room.roomID}이(가) 제거되었습니다.", showImportantLogsOnly);
            }
        }

        /// <summary>
        /// AI가 방을 사용했을 때 호출
        /// </summary>
        public void ReportRoomUsage(string aiName, RoomContents room)
        {
            if (room == null) return;
            
            // 방이 이미 사용 중인지 확인
            if (room.IsRoomUsed)
            {
                DebugLog($"{aiName}가 이미 사용 중인 방 {room.roomID}에 접근했습니다.");
                return;
            }
            
            // 방 요금 계산 (방 가격 * 오늘의 배율)
            int finalPrice = Mathf.RoundToInt(room.UseRoom() * priceMultiplier);
            
            // 방 명성도 가져오기
            int roomReputation = room.TotalRoomReputation;
            
            DebugLog($"방 사용 보고 - AI: {aiName}, 방: {room.roomID}, 가격: {finalPrice}원, 방 명성도: {roomReputation}", true);
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 방 {room.roomID}을(를) 사용: {finalPrice}원, 명성도: {roomReputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            if (paymentSystem != null)
            {
                DebugLog($"PaymentSystem에 결제 정보 추가 - AI: {aiName}, 방: {room.roomID}, 가격: {finalPrice}, 명성도: {roomReputation}");
                paymentSystem.AddPayment(aiName, finalPrice, room.roomID, roomReputation);
            }
            else
            {
                DebugLog("PaymentSystem을 찾을 수 없습니다!", true);
            }
        }
        
        /// <summary>
        /// AI가 카운터에서 방 사용 요금을 지불할 때 호출
        /// </summary>
        public int ProcessRoomPayment(string aiName)
        {
            if (paymentSystem == null) return 0;
            
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                DebugLog(paymentLog, true);
            }
            
            return amount;
        }

        /// <summary>
        /// 방 목록 업데이트 (빌딩 시스템에서 호출)
        /// </summary>
        public void UpdateRooms()
        {
            FindAllRooms();
        }

        /// <summary>
        /// 가격 범위로 방 찾기
        /// </summary>
        public List<RoomContents> FindRoomsInPriceRange(int minPrice, int maxPrice)
        {
            return allRooms.Where(r => r.TotalRoomPrice >= minPrice && r.TotalRoomPrice <= maxPrice).ToList();
        }

        /// <summary>
        /// 사용 가능한 방 목록 반환
        /// </summary>
        public List<RoomContents> GetAvailableRooms()
        {
            return allRooms.Where(r => !r.IsRoomUsed).ToList();
        }
        
        #region 청소 시스템
        
        /// <summary>
        /// 청소 시스템 초기화
        /// </summary>
        private void InitializeCleaningSystem()
        {
            roomCleaningStatus.Clear();
            for (int i = 0; i < allRooms.Count; i++)
            {
                roomCleaningStatus.Add(false);
            }
            DebugLog("청소 시스템 초기화 완료", true);
        }
        
        /// <summary>
        /// 방 청소 요청
        /// </summary>
        /// <param name="roomIndex">청소할 방 인덱스</param>
        public void RequestRoomCleaning(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= allRooms.Count)
            {
                DebugLog($"잘못된 방 인덱스: {roomIndex}", true);
                return;
            }
            
            if (roomIndex < roomCleaningStatus.Count)
            {
                roomCleaningStatus[roomIndex] = true;
                DebugLog($"방 {allRooms[roomIndex].roomID} 청소 요청됨", true);
            }
        }
        
        /// <summary>
        /// 방 청소 완료 처리
        /// </summary>
        /// <param name="roomIndex">청소 완료된 방 인덱스</param>
        public void CompleteRoomCleaning(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= allRooms.Count)
            {
                DebugLog($"잘못된 방 인덱스: {roomIndex}", true);
                return;
            }
            
            if (roomIndex < roomCleaningStatus.Count)
            {
                roomCleaningStatus[roomIndex] = false;
                allRooms[roomIndex].ReleaseRoom(); // 방을 사용 가능 상태로 변경
                DebugLog($"방 {allRooms[roomIndex].roomID} 청소 완료", true);
            }
        }
        
        /// <summary>
        /// 방이 청소 중인지 확인
        /// </summary>
        /// <param name="roomIndex">확인할 방 인덱스</param>
        /// <returns>청소 중이면 true</returns>
        public bool IsRoomBeingCleaned(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= roomCleaningStatus.Count)
                return false;
                
            return roomCleaningStatus[roomIndex];
        }
        
        /// <summary>
        /// 청소 중인 방 목록 반환
        /// </summary>
        /// <returns>청소 중인 방 목록</returns>
        public List<RoomContents> GetCleaningRooms()
        {
            List<RoomContents> cleaningRooms = new List<RoomContents>();
            for (int i = 0; i < allRooms.Count && i < roomCleaningStatus.Count; i++)
            {
                if (roomCleaningStatus[i])
                {
                    cleaningRooms.Add(allRooms[i]);
                }
            }
            return cleaningRooms;
        }
        
        /// <summary>
        /// 청소 가능한 방 목록 반환 (사용 중이지 않고 청소 중이지 않은 방)
        /// </summary>
        /// <returns>청소 가능한 방 목록</returns>
        public List<RoomContents> GetAvailableRoomsForCleaning()
        {
            List<RoomContents> availableRooms = new List<RoomContents>();
            for (int i = 0; i < allRooms.Count && i < roomCleaningStatus.Count; i++)
            {
                if (!allRooms[i].IsRoomUsed && !roomCleaningStatus[i])
                {
                    availableRooms.Add(allRooms[i]);
                }
            }
            return availableRooms;
        }
        
        #endregion
        
        #region 디버그 메서드
        
        /// <summary>
        /// 디버그 로그 출력
        /// </summary>
        private void DebugLog(string message, bool isImportant = false)
        {
            if (!showDebugLogs) return;
            
            if (showImportantLogsOnly && !isImportant) return;
            
            Debug.Log($"[RoomManager] {message}");
        }
        
        #endregion
    }
}