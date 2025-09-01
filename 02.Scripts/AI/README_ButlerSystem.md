# 집사 AI 시스템 (Butler AI System)

Unity 2023.3 (6000.0.40f1) 기준으로 구현된 다중 집사 AI 시스템입니다.

## 🎯 주요 기능

### 1. 돈으로 집사 AI 구매 시스템
- 플레이어가 돈을 사용해서 집사 AI를 여러 마리 구매 가능
- 구매할 때마다 새로운 집사 AI 인스턴스 생성
- 구매한 집사 AI들의 리스트 관리

### 2. 방 상태 관리 시스템
- 방 사용 완료 시 해당 방을 대기 큐에 추가
- 여러 방이 동시에 사용 완료될 수 있음
- 사용 완료된 방들의 우선순위 큐 관리

### 3. 다중 집사 AI 행동 로직
- **유휴 상태**: 카운터에서 대기하며 새로운 작업 할당 기다림
- **작업 할당**: 사용 완료된 방이 있을 때 가장 가까운 유휴 집사에게 할당
- **이동 및 청소**: 할당받은 방으로 이동 → 청소 애니메이션 재생 → 설정된 시간(초) 동안 작업
- **복귀 및 반복**: 작업 완료 후 카운터로 복귀 → 다시 유휴 상태로 전환

### 4. 효율적인 작업 분배
- 여러 집사가 동시에 다른 방에서 작업 가능
- 같은 방에 중복 할당 방지
- 거리 기반 최적 집사 선택 알고리즘

## 🏗️ 시스템 구조

### 핵심 클래스들

#### 1. ButlerSettingsSO (ScriptableObject)
- 집사 AI 시스템의 모든 설정값 관리
- 구매 비용, 최대 집사 수, 이동 속도, 청소 시간 등

#### 2. ButlerManager (싱글톤)
- 집사 AI들을 총괄 관리하는 매니저
- 집사 생성/제거, 작업 할당, 상태 관리

#### 3. ButlerAI (MonoBehaviour)
- 개별 집사 AI의 동작을 관리
- 상태 패턴을 통한 행동 제어
- NavMesh를 이용한 이동

#### 4. ButlerState (상태 패턴)
- Idle: 유휴 상태
- Moving: 이동 상태
- Cleaning: 청소 상태

#### 5. ButlerTask
- 청소 작업 정보 관리
- 우선순위, 위치, 할당 상태 등

#### 6. ButlerPurchaseUI
- 집사 AI 구매를 위한 UI
- 현재 상태 및 구매 가능 여부 표시

## 🚀 빠른 시작

### 1. 설정 파일 생성
Unity 에디터에서 `LodgingSimulator/집사 AI/설정 파일 생성` 메뉴 실행

### 2. 프리팹 생성
`LodgingSimulator/집사 AI/프리팹 생성` 메뉴 실행

### 3. 씬에 배치
1. ButlerManager 컴포넌트를 가진 게임오브젝트 생성
2. ButlerSettings 참조 설정
3. ButlerAI 프리팹 참조 설정
4. 스폰 위치 설정

### 4. UI 설정
1. ButlerPurchaseUI 컴포넌트를 UI에 추가
2. 필요한 UI 요소들 연결 (버튼, 텍스트 등)

### 5. 방 시스템 연동
1. RoomManager의 `enableButlerSystem`을 true로 설정
2. `autoAddCleaningTask`를 true로 설정

## ⚙️ 설정 옵션

### ButlerSettings 주요 설정값

```csharp
[Header("집사 AI 기본 설정")]
public int butlerPurchaseCost = 1000;        // 구매 비용
public int maxButlerCount = 10;              // 최대 집사 수

[Header("집사 AI 성능 설정")]
public float butlerMoveSpeed = 3.5f;        // 이동 속도
public float cleaningDuration = 5.0f;       // 청소 시간 (초)
public float rotationSpeed = 180f;          // 회전 속도

[Header("작업 할당 설정")]
public float maxAssignmentDistance = 50f;    // 최대 할당 거리
public Vector3 counterPosition;              // 카운터 위치
```

## 🎮 사용법

### 집사 AI 구매
1. UI의 "집사 AI 구매" 버튼 클릭
2. 충분한 돈이 있고 최대 집사 수에 도달하지 않았다면 구매 성공
3. 새로운 집사 AI가 스폰 위치에 생성

### 자동 청소 시스템
1. AI가 방을 사용하고 나감
2. RoomManager가 자동으로 청소 작업을 ButlerManager에 추가
3. 유휴 상태의 집사가 가장 가까운 방으로 이동하여 청소
4. 청소 완료 후 방을 다시 사용 가능한 상태로 변경

### 수동 청소 작업 추가
```csharp
// 특정 방에 청소 작업 추가
ButlerManager.Instance.AddCleaningTask("Room101", roomPosition, priority);
```

## 🔧 커스터마이징

### 새로운 상태 추가
1. `IButlerState` 인터페이스를 구현하는 새 클래스 생성
2. `ButlerStateType` 열거형에 새 상태 추가
3. `ButlerAI` 클래스에 상태 전환 로직 추가

### 애니메이션 파라미터 변경
`ButlerSettingsSO`에서 애니메이션 파라미터 이름을 수정

### 작업 우선순위 시스템
`ButlerTask.GetPriorityScore()` 메서드를 수정하여 우선순위 계산 로직 변경

## 🐛 문제 해결

### 집사 AI가 이동하지 않음
1. NavMesh가 제대로 구워져 있는지 확인
2. NavMeshAgent 컴포넌트 설정 확인
3. 목적지가 NavMesh 위에 있는지 확인

### 청소 작업이 할당되지 않음
1. ButlerManager가 씬에 존재하는지 확인
2. 유휴 상태의 집사가 있는지 확인
3. 대기 중인 작업이 있는지 확인

### 애니메이션이 재생되지 않음
1. Animator Controller가 연결되어 있는지 확인
2. 애니메이션 파라미터 이름이 설정과 일치하는지 확인
3. 애니메이션 클립이 설정되어 있는지 확인

## 📝 로그 및 디버깅

### 디버그 정보 표시
- ButlerManager의 OnGUI에서 실시간 상태 확인 가능
- ButlerPurchaseUI의 OnGUI에서 상세 정보 확인 가능

### 로그 메시지
- 모든 중요한 이벤트는 Debug.Log로 출력
- 집사 AI 생성, 작업 할당, 청소 완료 등

## 🔗 기존 시스템과의 연동

### RoomManager 연동
- 방 사용 완료 시 자동으로 청소 작업 추가
- 청소 완료 시 방을 다시 사용 가능한 상태로 변경

### PlayerWallet 연동
- 집사 AI 구매 시 자동으로 비용 차감
- 구매 가능 여부 실시간 확인

### NavMesh 시스템
- Unity의 기본 NavMesh 시스템 활용
- 집사 AI의 자율적인 경로 찾기

## 📚 추가 정보

더 자세한 정보는 Unity 에디터에서 `LodgingSimulator/집사 AI/시스템 설정 가이드` 메뉴를 참조하세요.

---

**개발자**: LodgingSimulator Team  
**버전**: 1.0.0  
**Unity 버전**: 2023.3 (6000.0.40f1)
