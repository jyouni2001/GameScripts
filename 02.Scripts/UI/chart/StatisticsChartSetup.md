# Unity 통계 차트 시스템 설정 가이드

## 개요
이 문서는 Unity 6000.0.40.f1에서 게임 통계 차트 시스템을 설정하는 방법을 설명합니다.

## 필요한 스크립트
1. `GameStatisticsData.cs` - 통계 데이터 관리
2. `ChartRenderer.cs` - 차트 렌더링
3. `StatisticsChartController.cs` - UI 컨트롤러
4. `StatisticsSystemAdapter.cs` - 기존 시스템 연동
5. `StatisticsSaveSystem.cs` - 데이터 저장/로드

## Unity UI Canvas 설정

### 1. 메인 Canvas 생성
```
1. Hierarchy에서 우클릭 → UI → Canvas
2. Canvas 이름을 "StatisticsCanvas"로 변경
3. Canvas Scaler 설정:
   - UI Scale Mode: Scale With Screen Size
   - Reference Resolution: 1920 x 1080
   - Screen Match Mode: Match Width Or Height
   - Match: 0.5 (중간값)
```

### 2. 차트 컨테이너 생성
```
1. Canvas 하위에 Empty GameObject 생성
2. 이름을 "ChartContainer"로 변경
3. RectTransform 설정:
   - Anchor: Center
   - Width: 800
   - Height: 600
   - Position: (0, 0, 0)
```

### 3. 차트 타입 버튼들 생성
```
1. ChartContainer 하위에 3개의 Button 생성:
   - "SpawnCountButton" (스폰 인원수)
   - "GoldEarnedButton" (획득 골드)
   - "ReputationButton" (명성도)

2. 각 버튼의 RectTransform 설정:
   - Anchor: Top Left
   - Width: 150
   - Height: 40
   - Position: 각각 (10, -10, 0), (170, -10, 0), (330, -10, 0)

3. 버튼 텍스트 설정:
   - SpawnCountButton: "스폰 인원수"
   - GoldEarnedButton: "획득 골드"
   - ReputationButton: "명성도"
```

### 4. 차트 정보 텍스트 생성
```
1. ChartContainer 하위에 3개의 TextMeshPro - Text (UI) 생성:
   - "ChartTitle" (차트 제목)
   - "CurrentValue" (현재 값)
   - "TotalValue" (총합)

2. ChartTitle 설정:
   - Anchor: Top Center
   - Position: (0, -60, 0)
   - Font Size: 24
   - Text: "일별 스폰 인원수"

3. CurrentValue 설정:
   - Anchor: Bottom Left
   - Position: (10, 10, 0)
   - Font Size: 16
   - Text: "현재: 0명"

4. TotalValue 설정:
   - Anchor: Bottom Right
   - Position: (-10, 10, 0)
   - Font Size: 16
   - Text: "총합: 0명"
```

### 5. 차트 렌더링 영역 생성
```
1. ChartContainer 하위에 Empty GameObject 생성
2. 이름을 "ChartRenderer"로 변경
3. RectTransform 설정:
   - Anchor: Stretch
   - Left, Right, Top, Bottom: 10, 10, -100, 50
4. ChartRenderer 스크립트 컴포넌트 추가
```

## 스크립트 컴포넌트 설정

### 1. GameStatisticsData 설정
```
1. 빈 GameObject 생성 (이름: "GameStatisticsData")
2. GameStatisticsData 스크립트 컴포넌트 추가
3. DontDestroyOnLoad 설정으로 전역 관리
```

### 2. StatisticsChartController 설정
```
1. ChartContainer에 StatisticsChartController 스크립트 컴포넌트 추가
2. Inspector에서 다음 항목들을 연결:
   - Chart Renderer: ChartRenderer 오브젝트
   - Chart Container: ChartContainer 오브젝트
   - Spawn Count Button: SpawnCountButton
   - Gold Earned Button: GoldEarnedButton
   - Reputation Button: ReputationButton
   - Chart Title Text: ChartTitle
   - Current Value Text: CurrentValue
   - Total Value Text: TotalValue
```

### 3. StatisticsSystemAdapter 설정
```
1. 빈 GameObject 생성 (이름: "StatisticsSystemAdapter")
2. StatisticsSystemAdapter 스크립트 컴포넌트 추가
3. Auto Connect 체크
```

### 4. StatisticsSaveSystem 설정
```
1. 빈 GameObject 생성 (이름: "StatisticsSaveSystem")
2. StatisticsSaveSystem 스크립트 컴포넌트 추가
3. Auto Save 체크
4. Auto Save Interval: 300 (5분)
```

## 색상 설정

### 차트 색상
```
- Spawn Count Color: Green (0, 1, 0, 1)
- Gold Earned Color: Yellow (1, 1, 0, 1)
- Reputation Color: Blue (0, 0, 1, 1)
- Background Color: Dark Gray (0.1, 0.1, 0.1, 0.8)
- Line Color: White (1, 1, 1, 1)
- Point Color: Red (1, 0, 0, 1)
```

## 테스트 및 디버깅

### 1. 테스트 데이터 생성
```
1. Play 모드에서 StatisticsChartController의 GenerateTestData() 메서드 호출
2. 30일간의 더미 데이터가 생성되어 차트에 표시됨
```

### 2. 차트 전환 테스트
```
1. 각 버튼을 클릭하여 차트 타입 전환 확인
2. 색상 변경 및 제목 변경 확인
3. 데이터 포인트와 라인 렌더링 확인
```

### 3. 자동 업데이트 테스트
```
1. Auto Update 체크 확인
2. Update Interval 설정으로 업데이트 주기 조정
3. 실시간 데이터 변경 시 차트 업데이트 확인
```

## 성능 최적화

### 1. 차트 렌더링 최적화
```
- Max Data Points: 30 (기본값)
- Line Width: 2
- Point Size: 6
- Update Interval: 5초
```

### 2. 메모리 관리
```
- 차트 요소 자동 정리
- 불필요한 GameObject 생성 방지
- 이벤트 리스너 적절한 해제
```

## 문제 해결

### 1. 차트가 표시되지 않는 경우
```
- ChartRenderer 컴포넌트 확인
- ChartContainer RectTransform 설정 확인
- 데이터 존재 여부 확인
```

### 2. 버튼이 작동하지 않는 경우
```
- Button 컴포넌트 연결 확인
- OnClick 이벤트 리스너 확인
- StatisticsChartController 컴포넌트 확인
```

### 3. 데이터가 저장되지 않는 경우
```
- StatisticsSaveSystem 컴포넌트 확인
- 파일 경로 권한 확인
- JSON 직렬화 오류 확인
```

## 추가 기능 구현

### 1. 커스텀 차트 스타일
```
- ChartRenderer의 SetChartColors() 메서드 활용
- 라인 스타일 및 포인트 스타일 커스터마이징
```

### 2. 데이터 필터링
```
- 날짜 범위 선택 기능
- 통계 데이터 검색 기능
- 데이터 정렬 기능
```

### 3. 차트 내보내기
```
- PNG 이미지로 차트 저장
- CSV 데이터 내보내기
- PDF 리포트 생성
```

## 참고 사항

- Unity 6000.0.40.f1 버전에서 테스트됨
- TextMeshPro 패키지 필요
- 기존 게임 시스템과의 연동을 위한 추가 설정 필요
- 모바일 플랫폼에서의 성능 최적화 권장
