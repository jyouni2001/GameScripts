namespace JY.AI
{
    /// <summary>
    /// AI 타입 열거형
    /// </summary>
    public enum AIType
    {
        Butler,         // 집사 - 청소, 정리, 서비스
        Security,       // 경비원 - 순찰, 보안, 위험 감지
        Receptionist,   // 접수원 - 고객 응대, 예약 관리, 전화 응답
        Maintenance,    // 정비원 - 수리, 점검, 교체
        Chef,           // 요리사 - 요리 준비, 서빙, 재고 관리
        Cleaner,        // 청소부 - 일반 청소, 정리
        Waiter,         // 웨이터 - 서빙, 고객 응대
        Manager,        // 매니저 - 관리, 감독, 보고
        Custom          // 사용자 정의
    }
    
    /// <summary>
    /// 작업 타입 열거형
    /// </summary>
    public enum TaskType
    {
        // 집사 작업
        CleanRoom,          // 방 청소
        Organize,           // 정리
        Restock,            // 재료 보충
        CustomerService,    // 고객 서비스
        
        // 경비원 작업
        Patrol,             // 순찰
        ThreatDetection,    // 위험 감지
        Report,             // 보고
        Emergency,          // 비상 대응
        
        // 접수원 작업
        CheckIn,            // 체크인
        CheckOut,           // 체크아웃
        BookingManagement,  // 예약 관리
        PhoneResponse,      // 전화 응답
        
        // 정비원 작업
        Repair,             // 수리
        Inspection,         // 점검
        Replacement,        // 교체
        Maintenance,        // 정비
        
        // 요리사 작업
        FoodPrep,           // 요리 준비
        Cooking,            // 조리
        Serving,            // 서빙
        InventoryManagement, // 재고 관리
        
        // 청소부 작업
        GeneralCleaning,    // 일반 청소
        DeepCleaning,       // 심층 청소
        WasteDisposal,      // 쓰레기 처리
        
        // 웨이터 작업
        TableService,       // 테이블 서비스
        OrderTaking,        // 주문 접수
        FoodDelivery,       // 음식 배달
        
        // 매니저 작업
        Supervision,        // 감독
        Management,         // 관리
        ReportGeneration,   // 보고서 생성
        StaffCoordination,  // 직원 조율
        
        // 공통 작업
        Idle,               // 대기
        Rest,               // 휴식
        Move,               // 이동
        Custom              // 사용자 정의
    }
    
    /// <summary>
    /// AI 상태 열거형
    /// </summary>
    public enum AIState
    {
        Idle,           // 대기
        Working,        // 작업 중
        Moving,         // 이동 중
        Resting,        // 휴식 중
        Assigned,       // 작업 할당됨
        Completed,      // 작업 완료
        Failed,         // 작업 실패
        Disabled        // 비활성화
    }
    
    /// <summary>
    /// 작업 상태 열거형
    /// </summary>
    public enum TaskStatus
    {
        Pending,        // 대기 중
        InProgress,     // 진행 중
        Completed,      // 완료
        Failed,         // 실패
        Cancelled       // 취소됨
    }
}
