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
        
        [Tooltip("시설 가격 설정 (ScriptableObject)")]
        public FacilityPriceConfigSO facilityPriceConfig;
        
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
            
            
            if (allRooms.Count == 0)
            {
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
                }
                else
                {
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
            }
        }

        /// <summary>
        /// AI가 방을 사용했을 때 호출
        /// </summary>
        public void ReportRoomUsage(string aiName, RoomContents room)
        {
            if (room == null)
            {
                Debug.LogError($"[ReportRoomUsage] AI {aiName}: room이 null입니다.");
                return;
            }
            
            Debug.Log($"[ReportRoomUsage] AI {aiName}: 시작 - 방 {room.roomID}, IsRoomUsed={room.IsRoomUsed}, TotalRoomPrice={room.TotalRoomPrice}");
            
            // 방이 이미 사용 중인지 확인
            if (room.IsRoomUsed)
            {
                Debug.LogWarning($"[ReportRoomUsage] AI {aiName}: 방 {room.roomID}은 이미 사용 중입니다 (중복 호출 방지)");
                return;
            }
            
            // 방 요금 계산 (방 가격 * 오늘의 배율)
            int basePrice = room.TotalRoomPrice;
            Debug.Log($"[ReportRoomUsage] AI {aiName}: UseRoom() 호출 전 - 기본 가격: {basePrice}원");
            
            int useRoomReturn = room.UseRoom();
            Debug.Log($"[ReportRoomUsage] AI {aiName}: UseRoom() 반환값: {useRoomReturn}원");
            
            int finalPrice = Mathf.RoundToInt(useRoomReturn * priceMultiplier);
            
            // 방 명성도 가져오기
            int roomReputation = room.TotalRoomReputation;
            
            Debug.Log($"[방 가격 정보] AI: {aiName}, 방 ID: {room.roomID}, 기본 가격: {basePrice}원, 가격 배율: {priceMultiplier:F2}, 최종 가격: {finalPrice}원, 명성도: {roomReputation}");
            
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
                Debug.Log($"[ReportRoomUsage] AI {aiName}: PaymentSystem에 결제 추가 - 금액: {finalPrice}원, 명성도: {roomReputation}");
                paymentSystem.AddPayment(aiName, finalPrice, room.roomID, roomReputation);
            }
            else
            {
                Debug.LogError($"[ReportRoomUsage] AI {aiName}: PaymentSystem이 null입니다!");
            }
        }
        
        /// <summary>
        /// AI가 카운터에서 방 사용 요금을 지불할 때 호출
        /// </summary>
        public int ProcessRoomPayment(string aiName)
        {
            if (paymentSystem == null) return 0;
            
            Debug.Log($"[결제 처리 시작] AI: {aiName} - PaymentSystem으로 결제 요청");
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
                
                Debug.Log($"[결제 완료] AI: {aiName}, 결제 금액: {amount}원");
            }
            else if (amount == 0)
            {
                Debug.Log($"[결제 경고] AI: {aiName}, 결제 금액이 0원입니다.");
            }
            
            return amount;
        }

        /// <summary>
        /// 선베드 사용 요금 처리 (FacilityPriceConfig 사용)
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        /// <param name="isInRoom">방 안 선베드 여부 (true면 무료)</param>
        public void ProcessSunbedPayment(string aiName, bool isInRoom)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[선베드 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetSunbedFinalPrice(isInRoom);
            int reputation = facilityPriceConfig.sunbedReputation;
            
            // 방 안 선베드는 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[선베드 무료] {aiName}: 방 안 선베드 (방 가격에 포함)");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[선베드 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[선베드 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 선베드 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "Sunbed", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) 선베드 {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[선베드 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// 운동 시설 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        public void ProcessHealthFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[운동 시설 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetHealthFacilityFinalPrice();
            int reputation = facilityPriceConfig.healthFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[운동 시설 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[운동 시설 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[운동 시설 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 운동 시설 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "HealthFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) 운동 시설 {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[운동 시설 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// 예식장 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        public void ProcessWeddingFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[예식장 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetWeddingFacilityFinalPrice();
            int reputation = facilityPriceConfig.weddingFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[예식장 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[예식장 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[예식장 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 예식장 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "WeddingFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) 예식장 {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[예식장 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// 라운지 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        public void ProcessLoungeFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[라운지 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetLoungeFacilityFinalPrice();
            int reputation = facilityPriceConfig.loungeFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[라운지 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[라운지 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[라운지 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 라운지 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "LoungeFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) 라운지 {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[라운지 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// 연회장 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        public void ProcessHallFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[연회장 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetHallFacilityFinalPrice();
            int reputation = facilityPriceConfig.hallFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[연회장 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[연회장 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[연회장 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 연회장 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "HallFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) 연회장 {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[연회장 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// 사우나 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        /// <param name="aiName">AI 이름</param>
        public void ProcessSaunaFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[사우나 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetSaunaFacilityFinalPrice();
            int reputation = facilityPriceConfig.saunaFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[사우나 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[사우나 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[사우나 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 사우나 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "SaunaFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) 사우나 {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[사우나 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// 카페 시설 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        public void ProcessCafeFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[카페 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetCafeFacilityFinalPrice();
            int reputation = facilityPriceConfig.cafeFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[카페 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[카페 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[카페 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) 카페 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "CafeFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) 카페 {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[카페 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// Bath 시설 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        public void ProcessBathFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[Bath 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetBathFacilityFinalPrice();
            int reputation = facilityPriceConfig.bathFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[Bath 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[Bath 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[Bath 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) Bath 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "BathFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) Bath {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[Bath 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
        }

        /// <summary>
        /// Hos(고급식당) 시설 사용 요금 처리 (FacilityPriceConfig 사용 - 방 유무 상관없이 유료)
        /// </summary>
        public void ProcessHosFacilityPayment(string aiName)
        {
            if (facilityPriceConfig == null)
            {
                Debug.LogError($"[Hos 결제 실패] {aiName}: FacilityPriceConfig가 설정되지 않았습니다.");
                return;
            }
            
            // FacilityPriceConfig에서 가격 및 명성도 가져오기
            int finalPrice = facilityPriceConfig.GetHosFacilityFinalPrice();
            int reputation = facilityPriceConfig.hosFacilityReputation;
            
            // 가격이 0원이면 무료
            if (finalPrice == 0)
            {
                Debug.Log($"[Hos 무료] {aiName}: 설정에 따라 무료 제공");
                return;
            }
            
            if (paymentSystem == null)
            {
                Debug.LogError($"[Hos 결제 실패] {aiName}: PaymentSystem이 null입니다.");
                return;
            }
            
            if (facilityPriceConfig.showPriceLogs)
            {
                Debug.Log($"[Hos 가격 정보] AI: {aiName}, 가격: {finalPrice}원, 명성도: {reputation}");
            }
            
            // 로그 추가
            if (showUsageLogs)
            {
                string usageLog = $"{aiName}이(가) Hos(고급식당) 사용: {finalPrice}원, 명성도: {reputation}";
                usedRoomLogs.Add(usageLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (usedRoomLogs.Count > 20)
                {
                    usedRoomLogs.RemoveAt(0);
                }
            }
            
            // 결제 시스템에 요금과 명성도 추가
            paymentSystem.AddPayment(aiName, finalPrice, "HosFacility", reputation);
            
            // 즉시 결제 처리
            int amount = paymentSystem.ProcessPayment(aiName);
            
            if (amount > 0 && showUsageLogs)
            {
                string paymentLog = $"{aiName}이(가) Hos(고급식당) {amount}원 결제 완료";
                paymentLogs.Add(paymentLog);
                
                // 로그 목록 크기 제한 (최근 20개만 유지)
                if (paymentLogs.Count > 20)
                {
                    paymentLogs.RemoveAt(0);
                }
                
                Debug.Log($"[Hos 결제 완료] AI: {aiName}, 결제 금액: {amount}원, 명성도: {reputation}");
            }
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
        }
        
        /// <summary>
        /// 방 청소 요청
        /// </summary>
        /// <param name="roomIndex">청소할 방 인덱스</param>
        public void RequestRoomCleaning(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= allRooms.Count)
            {
                return;
            }
            
            if (roomIndex < roomCleaningStatus.Count)
            {
                roomCleaningStatus[roomIndex] = true;
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
                return;
            }
            
            if (roomIndex < roomCleaningStatus.Count)
            {
                roomCleaningStatus[roomIndex] = false;
                allRooms[roomIndex].ReleaseRoom(); // 방을 사용 가능 상태로 변경
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
        }
        
        #endregion
    }
}