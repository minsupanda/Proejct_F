# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- 프로젝트 루트: `C:/Unity/Proejct_F`
- 현재 단계: 영주 성장 포인트 사용자 승인·main 병합 완료. 다음 기능 미착수
- 현재 브랜치: `main`
- 마지막 분석: 2026-10-16 (Asia/Seoul)
- 기준 구현: `7e5d869` (영주 성장), main 병합 완료. 승인 상태는 `Docs/DevelopmentProgress.md` 참조

## Confirmed Environment

- Unity 버전: 6000.6.0f1, revision `f7f8ed4d1e24`
- 렌더 파이프라인: URP 17.6.0, 2D Renderer
- 입력: 새 Input System 전용(`activeInputHandler: 1`), 런타임에서 마우스·키보드 명령 액션 맵 생성
- 물리: Rigidbody2D, CircleCollider2D, BoxCollider2D
- 확인된 빌드 대상: Windows x64

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | Universal Render Pipeline 17.6.0과 Renderer2D 사용 | Confirmed | `Packages/manifest.json`, `ProjectSettings/QualitySettings.asset`, `Assets/Settings/Renderer2D.asset` |
| Input | Input System 1.20.0 사용, 게임 명령은 `CommandInput`이 자체 액션을 생성 | Confirmed | `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`, `CommandInput.cs` |
| UI | uGUI 2.6.0으로 안내문과 드래그 선택 상자 표시 | Confirmed | `Packages/manifest.json`, `InputCamera.unity` |
| Physics | Unity 2D Physics로 유닛 몸통과 지형 충돌 검사 | Confirmed | 런타임 코드와 `InputCamera.unity` |
| Tests | Unity Test Framework 1.8.0, Input System Test Framework 사용 | Confirmed | 패키지 잠금 파일과 `ProjectF.PlayModeTests.asmdef` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/ProjectF/Runtime/Input` | 마우스 입력, 선택, 이동 명령 | Confirmed | `CommandInput.cs`, `UnitCommandController.cs` |
| `Assets/ProjectF/Runtime/Camera` | 휠 줌, 가운데 버튼 패닝, 지도 경계 제한 | Confirmed | `StrategyCamera2D.cs` |
| `Assets/ProjectF/Runtime/Player` | 유닛 상태, 2D 격자, 협력형 A*, 시공간 예약, 이동 총괄 | Confirmed | 해당 폴더의 6개 런타임 스크립트 |
| `Assets/ProjectF/Runtime/Combat` | 전투·AI·병력 충원 설정, 체력·공격·훈련, 시각 피드백 | Confirmed | `UnitCombat`, `EnemyCombatAI`, `UnitRecruitment`, 설정과 표시 컴포넌트 |
| `Assets/ProjectF/Runtime/Invasion` | 침공 차수·생성·승패·다음 준비와 HUD | Confirmed | `PortalInvasion`, `InvasionSettings`, `InvasionHUD` |
| `Assets/ProjectF/Runtime/Construction` | 건설·철거·수리 규칙, 비용·환급, 입력·대상 표시, 벽의 길찾기 반영, HUD | Confirmed | 건설 컴포넌트와 설정 |
| `Assets/ProjectF/Runtime/Economy` | 목재 재고·유한 자원·채집 명령과 표시 | Confirmed | `EstateStockpile`, `WoodResourceNode`, `WoodGatherer`, 설정·표시 컴포넌트 |
| `Assets/ProjectF/Prefabs` | 비활성 상태로 생성하고 씬 의존성을 연결한 뒤 활성화하는 몬스터·아군·벽 | Confirmed | `PortalRaider`, `PlayerSoldier`, `PalisadeWall` |
| `Assets/ProjectF/Editor` | 씬 생성/열기, 적 AI 통합, 설정 에셋 선택 메뉴 | Confirmed | `InputCameraSceneSetup`, `CombatSceneSetup`, `EnemyAISetup` |
| `Assets/ProjectF/Tests/PlayMode` | 실제 씬과 가상 입력을 이용한 자동 검사 | Confirmed | `MouseCommandTests.cs`, `NavigationTests.cs` |
| `Assets/ProjectF/Scenes` | 플레이 씬 `BasicCombat`, 기존 이동 검사 씬 `InputCamera` | Confirmed | 두 씬과 빌드 설정 |
| `Assets/ProjectF/Data` | 공유 이동·아군/적 전투·적 AI ScriptableObject | Confirmed | 각 Settings 에셋 |
| `Docs/Features` | 기능별 사용법·설정·검증 기록 | Confirmed | 입력·이동·전투·적 AI 문서 |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `ProjectF.Runtime` | 빌드에 포함되는 게임 코드 | `Unity.InputSystem`, `Unity.ugui` | 루트 네임스페이스 `ProjectF` |
| `ProjectF.Editor` | 에디터 메뉴와 시험 씬 구성 | `ProjectF.Runtime`, Input System, uGUI | Editor 플랫폼 전용 |
| `ProjectF.PlayModeTests` | 입력·카메라·길찾기 PlayMode 검사 | `ProjectF.Runtime`, Input System test framework | 테스트 어셈블리 |

## Scenes And Startup Flow

- 빌드 씬 0: `Assets/ProjectF/Scenes/BasicCombat.unity` (활성, 현재 플레이·빌드 시작 씬)
- 빌드 씬 1: `Assets/ProjectF/Scenes/InputCamera.unity` (활성, 기존 이동 회귀 검사)
- 빌드 씬 2: `Assets/Scenes/SampleScene.unity` (활성, 테스트 종료 시 사용)
- 초기 아군 4명·목재 80. `Project F → Open Gameplay`로 열기. 채집·건설(10/칸) → START INVASION → 3초 준비 → 1.5초 간격 생성 → 격퇴 → NEXT ROUND → 다음 준비. 기본 3차(3명→4명→5명), 최종 승리·패배·취소 후 RESTART.
- 다음 차수 준비에서 아군 체력·사망·위치, 목재·나무 잔량, 벽·체력을 유지하며 채집·건설·철거·수리를 재개. 첫 시작에 차수 수·기본 병력·증가량 확정. 자동 회복·부활·보상·나무 재생 없음.
- TRAIN SOLDIER: 목재 20 선결제·준비 시간 5초 후 훈련소 출구에 아군 한 명 배치. 초기 아군 포함 생존 8명 상한, 훈련 슬롯 하나. 침공·결과·일시정지에서는 진행을 보관하고 다음 준비에서 재개. 준비 단계의 CANCEL +20은 미배치 슬롯을 취소하고 최초 지불 목재를 전액 환불. 출구·상한 대기도 취소 가능하며 이미 나온 병사는 유지.
- SET RALLY → 빈 땅 클릭으로 준비 단계의 집결 지점 지정, CLEAR RALLY로 해제. 배치 시점의 지점으로 새 병사에게 이동 명령을 한 번 부여. 이미 나온 병사의 명령은 유지. 다음 차수까지 보관하고 재시작 시 초기화. 목적지 주변의 지형은 검사하지만 전체 경로 연결은 보장하지 않음.
- 충원 병사는 선택·이동·공격·채집 가능. `PortalInvasion`에 방어 병력으로 등록하므로 초기 아군이 전멸해도 충원 병사가 살아 있으면 전투 지속. 막힌 출구·배치 시 상한은 대기 후 재검사.
- 침공 전 REMOVE WALL로 직접 지은 벽 철거·환급 가능. 온전한 벽의 기본 환급률 100%. 구매 시 최대 환급액을 보관하고 실제 환급은 남은 체력 비율을 곱해 내림. 건설·철거·수리 모두 우클릭/Esc 취소, 침공 시작 후 잠금.
- REPAIR WALL: 준비 단계에서 손상된 소유 벽을 좌클릭하면 구매 비용 × 손상 비율을 올림한 목재를 내고 즉시 완전 수리. 기본 30/60 벽은 5목재. 구매 비용·최대 체력·최대 환급액은 건설 당시 값 유지. 작업자·수리 시간·전투 중 수리는 없음.
- 벽 기본 체력 60과 메시 체력바. 진격 중인 적이 전방의 가까운 벽을 공격하면 BREAK WALL, 파괴 후 진격 재개. 전투 파괴에는 환급 없음.
- 영지 왼쪽 위 나무 3곳(각 40). 준비 상태에서 아군 선택 후 나무 우클릭으로 접근·2초마다 목재 5 획득. 이동·공격·침공 시작 시 취소. 소진 시 그루터기만 남고 충돌 제거. 전용 일꾼·운반·저장은 아직 없음.
- 시작 흐름: Unity가 `BasicCombat`을 로드 → 아군이 `NavigationWorld2D`에 등록 → 침공 시작 시 비활성 몬스터 프리팹을 생성·길찾기에 연결·활성화 → 기존 전투와 중앙 길찾기로 진격·교전
- 저장된 주요 루트 오브젝트: `Mouse Commands`, `Main Camera`, `Lord`, `Test Unit 2~4`, `Navigation Obstacles`, `Command HUD`, `Command Destinations`, `Movement Test Ground`
- Space/PAUSE로 일시정지, Space/RESUME으로 재개. 씬 소유 GamePause가 시간·명령 차단을 관리하고 PauseHUD가 전체 화면 입력 차단·버튼을 표시. 이동·공격·채집·훈련·침공 시간과 선택을 보관. 카메라 정지, 건설·집결 편집 도구 종료. 비활성·씬 종료 시 자신이 멈춘 시간 복구.
- A를 누른 채 우클릭하면 선택 병사에게 공격 이동. 6 범위의 가까운 보이는 적과 교전하고 대상 상실·8 추격 한계·3초 정체 시 원래 목적지로 재개. 0.25초 탐지, 교전 종료 후 1초 진행. 기본 병사와 충원 병사에 적용. 도착·경로 실패·일반 명령 교체 시 종료. 선택 패널 ADVANCE는 해당 명령 인원이며 다른 명령 집계와 배타적.
- 메인 메뉴나 범용 게임 상태 시스템은 없음. `InvasionHUD`의 NEXT ROUND는 같은 씬에서 준비 상태로 전환하고, RESTART는 현재 씬을 비동기로 다시 불러옴
- 일반 병사(초기 3명·충원 병사)는 적 명중 1회당 숙련도 1. 기본 5회 TRAINED +2, 15회 VETERAN +4 공격력, 최고 단계 누적 상한 15. 해당 단계에 도달한 타격 다음부터 보너스 적용. 생존 병사는 다음 침공까지 유지, 충원·재시작은 0. 영주/적은 제외. 선택 패널에 개인 진행도 또는 R/T/V 인원 표시. 연구·시설 기반 병종 전환과 별개.

- 생존 영주가 침공 격퇴 시 차수당 성장 포인트 1개. 준비·격퇴 상태에서 무력(영주 공격력 +2) 또는 통솔(병력 상한 +1)에 1포인트씩 투자. 각 3단계. 다음 차수까지 유지, RESTART 초기화. 영주 사망·일시정지·침공 진행 중에는 투자 불가. 구매한 상한은 영주 사망에도 유지. 오른쪽 위 LORD GROWTH 패널.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Scene composition root | `Mouse Commands` 오브젝트가 입력 컨트롤러와 중앙 길찾기 관리자를 소유 | Confirmed | `InputCamera.unity` |
| MonoBehaviour-centric | 입력, 카메라, 유닛, 길찾기 총괄은 MonoBehaviour 컴포넌트 | Confirmed | 런타임 코드 |
| Shared data asset | 모든 기본 유닛이 이동 속도 5의 `LordMovementSettings`를 공유 | Confirmed | 씬 참조와 데이터 에셋 |
| Combat composition | 선택적 `UnitCombat` 컴포넌트, 아군/적 공유 설정 에셋, 유닛별 런타임 체력, 이벤트 기반 체력 표시 | Confirmed | `Runtime/Combat/*.cs` |
| Command integration | 외부 `MoveTo`는 `MoveCommandIssued` 이벤트로 공격 취소, 전투 추적은 내부 `NavigateTo`로 같은 중앙 길찾기 사용 | Confirmed | `CommandableUnit`, `UnitCombat` |
| Enemy decisions | `EnemyCombatAI`가 적의 대상 선정과 경계/교전/복귀 전환을 담당. 활성 중 `UnitCombat`의 피격 반격을 억제해 명령 소유권을 유지 | Confirmed | `EnemyCombatAI`, `UnitCombat.SuppressIdleRetaliation` |
| Invasion orders | `AdvanceTo`가 기존 AI에 목적지를 부여. 이동 중 교전하며 대상 상실 시 진격 재개, 도착 후 기존 경계 행동 | Confirmed | `EnemyCombatAI` |
| Invasion rounds | `PortalInvasion`이 차수·생성 수·소유 유닛·승패·다음 준비를 관리. 기존 출구 검사와 구성원 정리 재사용. UI·건설·채집은 상태 이벤트 구독 | Confirmed | `Runtime/Invasion/*.cs` |
| Construction | `WallConstruction`이 준비 단계·범위·겹침·목재를 검사하고 벽 생성. `EstateStockpile`이 자원 소유. 실제 충돌체로 기존 길찾기에 반영 | Confirmed | `Runtime/Construction`, `Runtime/Economy` |
| Demolition | 같은 `WallConstruction`이 소유 벽 검증·철거·환급을 처리. `WallStructure`에 최대 환급액 보관, 비활성화로 충돌 즉시 제거. 입력 도구는 None/Build/Demolish/Repair 단일 모드 | Confirmed | 건설 런타임과 `DemolitionTests` |
| Wall repair | `WallConstruction`이 검증·작업 잠금, `WallStructure`가 체력·비용 계산, `EstateStockpile` 내부 결제가 목재 차감·체력 복구 후 자원 알림을 담당. 기존 입력과 0.1초 대상 점검 재사용 | Confirmed | `WallRepairTests`, 건설·경제 런타임 |
| Wall combat | `WallStructure`가 체력과 파괴 소유. `UnitCombat.WallTarget`은 유닛 대상과 배타적이며 공격 주기 공유. AI Breaching 상태가 전방 벽 선택·실패 시 대기·진격 재개를 담당 | Confirmed | `WallStructure`, `UnitCombat`, `EnemyCombatAI`, `WallHealthView` |
| Wood gathering | `WoodResourceNode`의 유한 잔량을 `WoodGatherer`가 기존 재고로 이전. 접근·시야·작업 시간 검사, 명령 이벤트로 취소. 상태·잔량 이벤트로 표시 갱신 | Confirmed | `Runtime/Economy`, `UnitCommandController`, `UnitCombat.AttackCommandIssued` |
| Recruitment | `UnitRecruitment`이 결제·훈련 슬롯·시간·충원 병력 생성과 사망 정리 소유. 비활성 프리팹에 길찾기·영지를 연결하고 활성화 후 침공 방어 병력 등록. HUD는 표시·버튼 담당 | Confirmed | `UnitRecruitment`, `RecruitmentHUD`, `RecruitmentSettings`, `RecruitmentSetup` |
| Recruitment cancellation | 최초 지불 금액을 슬롯에 보관. 준비 단계·전액 환불 가능 여부를 검증하고 자원 변경 이벤트 전에 슬롯을 정리. 결제·취소 잠금으로 중복 환불과 이벤트 재진입 차단. 기존 HUD 버튼 재사용 | Confirmed | `RecruitmentCancelTests`, `UnitRecruitment`, `EstateStockpile`, `RecruitmentHUD` |
| Recruitment rally | `UnitRecruitment`이 지점·검증·배치 시 이동을 소유. 입력·깃발은 `RecruitmentRallyController`, 버튼·안내는 `RecruitmentRallyHUD`. 기존 길찾기·포인터 점유 재사용, 건설 상태 이벤트로 도구 전환 | Confirmed | `RecruitmentRallyTests`, `RecruitmentRallySetup` |
| Modal pointer ownership | 건설 입력이 소유자별 차단 API로 월드 클릭을 점유. 취소 후 버튼을 뗄 때까지 기존 명령 차단 | Confirmed | `UnitCommandController.SetPointerCommandsBlocked`, `WallPlacementController` |
| Game pause | CommandInput의 Space 입력, GamePause의 시간·입력 점유, PauseHUD의 이벤트 기반 화면. 기존 FixedUpdate와 scaled time을 재사용하며 카메라에는 시간 정지 검사를 추가 | Confirmed | `GamePause`, `PauseHUD`, `GamePauseSetup`, `GamePauseTests` |
| Attack move | AttackMoveOrder가 목적지·탐지·추격 전환 소유. UnitCombat과 중앙 길찾기 재사용. 주기적 주변 물리 조회와 재사용 목록, 첫 검사 시점 분산. 기존 이동/정지/공격/채집 알림으로 명령 대체 | Confirmed | `AttackMoveOrder`, `AttackMoveSettings`, `AttackMoveSetup`, `AttackMoveTests` |
| Soldier proficiency | SoldierProficiency가 개인 진행도·단계·추가 피해 소유, ProficiencySettings에 밸런스 분리. UnitCombat.Attacked 구독으로 실제 명중만 누적하고 AttackDamage에서 기본값과 보너스 합산. 공유 에셋 변경·별도 프레임 검사 없음 | Confirmed | `SoldierProficiency`, `ProficiencySettings`, `ProficiencySetup`, `ProficiencyTests` |
| Lord growth | LordGrowth가 차수별 격퇴 보상·포인트·투자를 소유. Invasion 변경 이벤트 재사용, 보상 중복·투자 재진입 차단. UnitCombat.AttackDamage와 UnitRecruitment.MaximumAllies에서 개인/영지 보너스 반영 | Confirmed | `LordGrowth`, `LordGrowthSettings`, `LordGrowthHUD`, `LordGrowthSetup`, `LordGrowthTests` |
| Local perception | 2D 물리 영역 조회와 재사용 목록, 기본 0.25초 판단, 첫 탐지 시점 분산, 프로파일러 `ProjectF.EnemyAI.Detect` | Confirmed | `EnemyCombatAI.FindTarget` |
| Central cooperative planning | `NavigationWorld2D`가 모든 유닛을 한 번에 우선순위별 계획하고 예약 | Confirmed | `NavigationWorld2D.cs` |
| Space-time A* | 위치 노드뿐 아니라 0.1초 시간 단계를 상태에 포함하며 대기 행동 허용 | Confirmed | `CooperativePathfinder.cs` |
| Broad phase + exact collision | 2단위 공간 버킷으로 후보를 줄이고 swept-disc 거리로 정확 검사 | Confirmed | `ReservationTable2D.cs` |
| Size-class terrain cache | 유닛 반경을 0.05단위 크기 등급으로 나눠 지형 격자 공유 | Confirmed | `NavigationWorld2D.Replan()` |

## Coding Conventions

- 네임스페이스: `ProjectF.<Feature>` 형식
- 타입: PascalCase, private 필드: camelCase
- Inspector 연결: `[SerializeField] private` 필드와 `SerializedObject` 기반 에디터 구성
- 런타임 클래스는 가능한 경우 `sealed`; 필수 컴포넌트는 `[RequireComponent]`
- 업데이트 분리: 일반 입력은 `Update`, 카메라는 `LateUpdate`, 물리 이동은 `FixedUpdate`
- 비동기 코드는 없음; 테스트는 `IEnumerator` 기반 UnityTest 코루틴
- 공개 API보다 씬 내부 협력을 위한 `internal` 메서드와 읽기 전용 프로퍼티 사용

## Testing And Validation

- 2026-10-16 영주 성장 최종 전체 PlayMode 279개 통과(기존 266개 + 전용 13개), 실패·건너뜀·판정 불가 0개, 346.03초. 전용 13개 4.41초, 선택 패널 9개 6.88초. 씬 종료 중 불필요한 성장 알림 제거와 정지 커서의 재시작 입력 초기화 수정 포함. 실제 화면의 통솔 클릭으로 포인트 1→0·병력 상한 8→9 확인. `Docs/Features/LordGrowth.md` 참조.
- 영주 성장 Windows x64 빌드·시작 검사 통과. 오류 0개·기존 경고 유형 501건, 18.579초, 132,890,650바이트. BasicCombat 저장·Play 중지, 누락 스크립트·성장/HUD 참조 0개, 현재 Console 오류 0개. LordGrowth와 HUD 각 1개, 훈련소 연결·충원 프리팹 제외·설치 메뉴 중복 방지 확인. 사용자 승인·main 병합 완료. 사용자가 병합까지만 요청했으므로 다음 기능은 별도 요청을 기다립니다.
- 2026-10-15 병사 숙련도 최종 전체 PlayMode 266개 통과(기존 255개 + 전용 11개), 실패·건너뜀·판정 불가 0개, 343.37초. 부대 번호 검사 좌표의 UI 겹침을 보완하고 관련 13개 재검사 통과. 기본 설정 실제 Play의 0→5→15회·공격력 15→17→19·패널 갱신 확인. `Docs/Features/SoldierProficiency.md` 참조.
- 병사 숙련도 Windows x64 빌드·시작 검사 통과, 오류 0개·기존 경고 유형 501건, 19.076초, 132,881,914바이트. Editor 재실행 후 BasicCombat 저장·Play 중지, 누락 스크립트·숙련도/선택 패널 참조 0개, 현재 콘솔 오류 0개. 초기 일반 병사 3명·충원 프리팹 1개 연결, 영주 제외. 사용자 승인·main 병합 완료 (`f84079a`).

- EditMode 테스트: 없음
- 2026-10-07 방어벽 수리 전체 159개 통과, 실패·건너뜀 0개, 211.62초. 새 수리 14개 포함. 실제 기본 설정 Play에서 검증용 피해로 만든 체력 30/60 벽을 클릭해 체력 60·목재 70→65 확인. `Docs/Features/WallRepair.md` 참조. 승인 후 main 병합 완료 (`b8ad011`).
- 방어벽 수리 Windows x64 빌드 성공, 오류 0개·기존 경고 501건, 12.343초, 132,850,658바이트. `Builds/WallRepair/ProjectF_WallRepair.exe`의 화면 없는 시작 검사 통과, 시작 로그 오류·예외 없음.
- 2026-10-07 병력 충원 전체 145개 통과, 실패·건너뜀 0개, 209.62초. 실제 기본 설정의 5초 훈련·목재 차감·아군 생성과 화면 확인. 새 병사의 입력·전투·채집·적 AI 탐지·승패 참여는 자동 검사로 검증. `Docs/Features/UnitRecruitment.md` 참조. 승인 후 main 병합 완료 (`09c0d14`).
- 병력 충원 Windows x64 빌드 성공, 오류 0개·기존 경고 유형 501건, 14.569초, 132,846,242바이트. `Builds/UnitRecruitment/ProjectF_UnitRecruitment.exe`의 화면 없는 시작 검사 통과, 시작 로그 오류·예외 없음.
- 2026-10-07 반복 침공 전체 133개 통과, 실패·건너뜀 0개, 199.44초. 새 반복 침공 10개 포함. 실제 기본 설정 3차 격퇴·중간 준비·체력과 자원 유지 확인. `Docs/Features/InvasionRounds.md` 참조. 승인 후 main 병합 완료 (`fc016ca`).
- 반복 침공 Windows x64 빌드 성공, 오류 0개·기존 경고 501개, 15.306초, 132,828,770바이트. `Builds/InvasionRounds/ProjectF_InvasionRounds.exe`의 화면 없는 시작 검사 통과, 시작 로그 오류·예외 없음.
- 훈련소 집결 최종 전체 PlayMode 174개 통과, 실패·건너뜀 0개, 259.86초. 전용 14개와 벽 돌파 판단 시점 회귀 1개를 추가했다. 실제 기본 설정 Play에서 클릭·훈련·깃발 도착을 확인했다. 승인 후 main 병합 완료 (`aab0e15`). `Docs/Features/RecruitmentRally.md` 참조.
- 2026-10-09 집결 Windows x64 빌드 성공, 오류 0개·이전 빌드와 같은 경고 유형 501건, 29.506초, 132,860,882바이트. `Builds/RecruitmentRally/ProjectF_RecruitmentRally.exe` 시작 검사 통과, 시작 로그 오류·예외 없음. BasicCombat 저장·Play 중지, 누락 스크립트·집결 참조 0개, EventSystem 1개.
- Shift 추가·해제 선택 전체 PlayMode 186개 통과, 실패·건너뜀 0개, 257.65초. 전용 12개는 양쪽 Shift, 추가·해제·합집합, 도구 점유, UI 보호, 기존 명령 유지, 충원 병사, 비활성·파괴·재시작을 검증한다. 실제 Play의 선택 1→3→2명과 표시 갱신 확인. 승인 후 main 병합 완료 (`2c8ea7c`). `Docs/Features/AdditiveSelection.md` 참조.
- 2026-10-11 추가 선택 Windows x64 빌드 성공, 오류 0개·이전 빌드와 같은 경고 유형 501건, 15.658초, 132,861,410바이트. `Builds/AdditiveSelection/ProjectF_AdditiveSelection.exe` 시작 검사 통과, 로그 오류·예외 없음. BasicCombat 저장·Play 중지, 누락 스크립트·선택 참조 0개, EventSystem 1개.
- 정지 명령 전체 PlayMode 197개 통과, 실패·건너뜀 0개, 270.00초. 새 전용 11개 포함. 실제 Play의 이동 중 S로 Idle·속도 0·목적지 제거·선택 유지 확인. 사용자 승인·main 병합 완료 (`567f4ee`). `Docs/Features/StopCommand.md` 참조.
- 2026-10-11 정지 명령 Windows x64 빌드 성공, 오류 0개·기존 경고 유형 501건, 12.914초, 132,861,954바이트. `Builds/StopCommand/ProjectF_StopCommand.exe` 시작 검사 통과, 시작 로그 오류·예외 없음. BasicCombat 저장·Play 중지, 누락 스크립트·선택 참조 0개.
- 부대 번호 전체 PlayMode 210개 통과, 실패·건너뜀·판정 불가 0개, 273.49초. 전용 13개 포함. 실제 Play에서 Ctrl+1 저장·다른 선택·1 호출 후 두 병사의 선택 원 복원과 안내문을 확인했다. `Docs/Features/ControlGroups.md` 참조.
- 2026-10-11 부대 번호 Windows x64 빌드 성공, 오류 0개·기존 경고 유형 501건, 12.695초, 132,863,058바이트. `Builds/ControlGroups/ProjectF_ControlGroups.exe` 시작 검사 통과, 로그 오류·예외 없음. BasicCombat 저장·Play 중지, 누락 스크립트·선택 참조 0개, EventSystem 1개. 사용자 승인·main 병합 완료 (`8f6eaa3`).
- 선택 상태 패널 최종 전체 PlayMode 219개 통과, 실패·건너뜀·판정 불가 0개, 279.46초. 전용 9개 포함. 새 패널 아래에 놓인 기존 빈 지도 드래그 검사 좌표를 보완하고 UI 밖 시작 여부를 검증했습니다. 관련 선택 12개 재검사 통과. 실제 Play에서 4명·400/400 → 375/400 체력 갱신과 이동 4명 표시를 확인했습니다. `Docs/Features/SelectionStatus.md` 참조.
- 2026-10-12 선택 상태 Windows x64 빌드 성공, 오류 0개·기존 경고 유형 501건, 14.238초, 132,866,130바이트. `Builds/SelectionStatus/ProjectF_SelectionStatus.exe` 시작 검사 통과, 로그 오류·예외 없음. Editor 재실행 후 BasicCombat 저장·Play 중지, HUD 1개, 누락 스크립트·HUD/선택 참조 0개 확인. 사용자 승인·main 병합 완료 (`54feab2`). `Assets/_Recovery`의 이전 세션 백업은 로컬 보존·커밋 제외.
- 훈련 취소 전용 PlayMode 12개 통과, 실패·건너뜀·판정 불가 0개, 4.82초. 최종 전체 231개 통과, 실패·건너뜀·판정 불가 0개, 280.57초. CLI 상태가 실행 중으로 남아 Unity 최종 XML과 Editor 완료 로그로 확인했습니다. 기본 설정의 실제 버튼으로 목재 80→60→80·병력 4명 유지·훈련 버튼 복귀를 확인. `Docs/Features/RecruitmentCancel.md` 참조.
- 2026-10-12 훈련 취소 Windows x64 빌드 성공, 오류 0개·기존 경고 유형 501건, 12.470초, 132,866,674바이트. `Builds/RecruitmentCancel/ProjectF_RecruitmentCancel.exe` 시작 검사 통과, 로그 오류·예외 없음. BasicCombat 저장·Play 중지, 누락 스크립트·선택/HUD/훈련 참조 0개. 사용자 승인·main 병합 완료 (`0bf400a`).
- 2026-10-13 일시정지 전용 PlayMode 11개 통과, 실패·건너뜀·판정 불가 0개, 12.57초. 최종 전체 242개 통과, 실패·건너뜀·판정 불가 0개, 297.52초. 실제 Space 입력으로 남은 훈련 시간 보관·재개와 정지 화면 확인. `Docs/Features/GamePause.md` 참조.
- 일시정지 Windows x64 빌드 성공, 오류 0개·기존 경고 유형 501건, 13.798초, 132,873,114바이트. `Builds/GamePause/ProjectF_GamePause.exe` 시작 검사 통과, 로그 오류·예외 없음. BasicCombat 저장·Play 중지, 누락 스크립트·선택/HUD/훈련/일시정지 참조 0개, GamePause와 EventSystem 각 1개. 사용자 승인·main 병합 완료 (`a3b892d`).
- 공격 이동 전용 PlayMode 13개 통과(14.85초), 최종 전체 255개 통과(321.76초), 실패·건너뜀·판정 불가 0개. 상태 패널 아래의 기존 선택 검사 좌표를 모두 보완하고 공통 지도 클릭의 UI 밖 여부를 검증. 실제 Play의 자동 교전·원래 목적지 도착·ADVANCE 1→0 확인. `Docs/Features/AttackMove.md` 참조.
- 공격 이동 Windows x64 빌드 성공, 오류 0개·기존 경고 유형 501건, 13.696초, 132,878,314바이트. `Builds/AttackMove/ProjectF_AttackMove.exe` 시작 검사 통과, 로그 오류·예외 없음. 2026-10-14 최종 확인: BasicCombat 저장·Play 중지, 누락 스크립트·공격 이동/선택 참조 0개, 초기 병사 4개·충원 프리팹 1개, EventSystem 1개, 현재 Console 오류 0개. 사용자 승인·main 병합 완료 (`2a2dfe0`).
- PlayMode 테스트: 이동·카메라 29개 + 기본 전투 14개 + 적 AI 13개 + 침공 12개 + 건설 12개 + 철거 13개 + 벽 전투 16개 + 목재 채집 15개 + 반복 침공 10개 + 병력 충원 12개 + 벽 수리 14개 + 집결 지점 14개 + 추가 선택 12개 + 정지 명령 11개 + 부대 번호 13개 + 상태 패널 9개 + 훈련 취소 12개 + 일시정지 11개 + 공격 이동 13개 + 병사 숙련도 11개 + 영주 성장 13개
- 벽 돌파의 도착 검사는 AI 설정 `ReturnTolerance`를 사용한다. 이전 고정 `x < -5.5` 조건은 기본 허용 거리 0.6과 맞지 않아 정상 Guarding에서도 간헐적으로 실패했다. 집결 통합 과정에서 바로잡고 판단 시점 13가지를 검사하는 회귀 사례를 추가했다. 전투 런타임 변경 없음.
- 2026-10-06 목재 채집 최종 전체 123개 통과, 실패·건너뜀 0개, 199.91초. 실제 기본 설정의 자원 증가·소진·추가 건설·침공 취소 확인. `Docs/Features/WoodGathering.md` 참조. 승인 후 main 병합 완료 (`e5e4642`).
- 목재 채집 Windows x64 빌드 성공, 오류 0개·기존 경고 501개, 24.410초, 132,827,202바이트. `Builds/WoodGathering/ProjectF_WoodGathering.exe` 시작 검사에서 엔진·입력 초기화와 프로세스 유지, 시작 로그 오류·예외 없음 확인.
- 기본 전투 테스트는 적 AI를 꺼서 수동 공격 규칙을 분리 검증. 적 AI 테스트는 실제 씬에 연결된 AI를 켜고 검증
- 이전 이동 검사: 29 통과, 0 실패, 0 건너뜀, 83.88초 (`Logs/gather-tests-final.json`)
- 이전 이동 빌드: Windows x64 성공, 약 132.67 MB (`Builds/InputCamera`)
- 기능 문서에 AI inference 셰이더 관련 500건과 Pipeline 안내 1건의 기존 빌드 경고가 기록됨
- 이전 전투·AI 테스트는 `CombatTestScene`에서 실제 몬스터 프리팹 3개를 기존 배치로 생성. 새 침공은 기본 플레이 씬을 그대로 검증
- 단계별 검증 결과는 `Docs/Features/BasicCombat.md`, `Docs/Features/EnemyAI.md`, `Docs/Features/PortalInvasion.md` 참조
- 이전 침공 단계: 전체 68개 통과, 포탈 최종 배치 이후 침공 12개 재검사 통과, Windows x64 빌드 오류 0개. 실제 전투 격퇴·재시작과 Player의 화면 없는 시작 확인
- 건설 단계: 전체 80개 통과, 실패·건너뜀 0개, 149.17초. Play 화면에서 벽 5칸·목재 30·미리보기·침공 건설 잠금·적 우회 확인. 세부 결과: `Docs/Features/WallBuilding.md`
- 건설 Windows x64 빌드 성공, 오류 0개, 기존 경고 501개, 132,799,806바이트. `Builds/WallBuilding/ProjectF_WallBuilding.exe`의 화면 없는 시작 검사 통과. 승인 후 main 병합 완료 (`2d05ba2`).
- 철거 단계: 전체 93개 통과, 실패·건너뜀 0개, 155.87초. Play에서 벽 5→4칸과 목재 30→40, 주황색 대상·환급 안내 확인. 세부 결과: `Docs/Features/WallDemolition.md`.
- 철거 Windows x64 빌드 성공, 오류 0개, 기존 경고 501개, 132,804,686바이트. `Builds/WallDemolition/ProjectF_WallDemolition.exe`의 화면 없는 시작 검사 통과. 승인 후 main 병합 완료 (`e1fda3b`).
- 벽 전투 단계: 전체 108개 통과, 실패·건너뜀 0개, 180.66초. 새 벽 전투 15개를 포함하며 실제 침공의 포위된 아군 돌파·결과 후 공격 정지도 확인. 세부 결과: `Docs/Features/WallCombat.md`.
- 기본 설정 Play 화면에서 벽 체력 60→30·파괴·통로 진입·아군 교전 확인. 전투 파괴 후 목재 0 유지.
- 벽 전투 Windows x64 빌드 성공, 오류 0개, 기존 경고 501개, 132,810,658바이트. `Builds/WallCombat/ProjectF_WallCombat.exe`의 화면 없는 시작 검사에서 엔진 초기화와 프로세스 유지 확인. Unity 통계 서버 DNS 오류 3건이 기록됐으나 예외나 종료는 관찰되지 않음. 승인 후 main 병합 완료 (`ef99c05`).

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity 프로젝트 파일 읽기 | available | 현재 워크스페이스 직접 확인 |
| Unity Editor 연결 | available | 현재 프로젝트의 Unity 6000.6.0f1, 설치된 Pipeline을 Unity CLI로 제어 |
| 과거 Unity 원격 명령 로그 | available as historical evidence | `Logs/gather-tests-final.json`에 `127.0.0.1:7801` 실행 기록 |
| 콘솔/씬/게임오브젝트 실시간 검사 | available | Unity CLI의 console, eval, editor_status |
| 테스트/Play Mode 실행 | available | Unity CLI의 run_tests, test_status, editor_play/stop |

## Important Constraints

- 현재 결과물은 이동·선택·카메라·충돌 회피·근접 전투·적 AI·반복 포탈 침공·방어벽 건설·철거·체력·파괴·목재 채집·기본 병력 충원·방어벽 수리·훈련소 집결 지점이 연결된 초기 플레이 버전이다.
- 기본 근접 공격과 Player/Hostile/Neutral 진영, 방어벽 건설·철거·체력·적의 근접 벽 공격, 준비 단계 목재 채집을 추가했다. 전용 일꾼·운반·생산 시설, 공성 장비·최적 돌파 경로·작업자 수리, 외교 관계, 영웅/병과 규칙, 저장, 메인 메뉴, 편대 유지, 명령 예약은 아직 없다.
- Shift 추가·해제 선택은 기존 CommandInput/UnitCommandController 안에 통합했다. 좌클릭 시작 시 Shift를 저장하고, 클릭은 토글·드래그는 합집합으로 처리한다. List와 HashSet을 함께 유지해 중복을 방지한다. 선택 변경은 기존 명령을 취소하지 않는다.
- S 정지는 `CommandableUnit.StopCommand`와 `StopCommandIssued` 이벤트로 이동·공격·채집을 취소한다. 내부 `Stop`은 조용한 도착·접근 처리에 유지한다. UI·드래그·패닝·건설/집결 도구·일시정지에서는 입력을 무시한다. 길게 누르기는 반복 명령을 만들지 않는다. 위치 사수 태세는 아니다.
- 부대 번호는 기존 CommandInput/UnitCommandController에 통합했다. Ctrl+1~9로 선택의 스냅샷을 저장하고 1~9로 교체 선택한다. 9개 목록은 씬 수명 동안 보관하며 임시 컴포넌트 비활성화에는 유지, 씬 재시작에는 초기화한다. 호출 때 선택 불가 참조를 정리하며 기존 이동·공격·채집을 변경하지 않는다.
- `SelectionStatusHUD`는 Command HUD 아래에서 기존 선택을 읽어 인원·체력 합계·명령별 인원을 표시한다. 선택 목록만 0.1초 간격의 unscaled time으로 확인하고 숫자가 바뀔 때만 텍스트를 갱신한다. 선택 해제·HUD 비활성화 시 패널과 클릭 차단을 숨긴다. 공격/채집은 접근·추격을 포함하는 명령 기준이며 각 병사는 한 범주에만 포함한다. `SelectionStatusSetup`은 중복 생성을 막고 기존 Canvas에 UI를 구성한다.
- 유닛은 스프라이트가 아니라 런타임 메시와 단색 URP Unlit 재질을 조합한 임시 도형이다.
- 병력 충원은 고정 훈련소·기본 병사·임시 목재 비용으로 구현했다. 병영 건설·식량·인구·금화·영웅별 지휘력은 아직 없다. 현재 생존 아군 상한은 영웅 지휘력 규칙이 아니다.
- `InputCameraSceneSetup.ConfigureMouseControls()`는 기존 씬을 업그레이드하지만 호출 자체가 자동 저장을 보장하지 않는다.
- 입력은 `Assets/Settings/InputSystem_Actions.inputactions`를 직접 쓰지 않고 `CommandInput`이 마우스·키보드 액션을 코드로 생성한다.
- 현재 이동 예측은 0.1초 간격 × 60단계 = 6초이며, 기본 0.5초마다 재계산한다.
- 64명 집결과 32명 혼합 교차는 통과했지만, 최대 재계산 시간이 수십 ms이므로 대규모 전투 성능은 완료 상태가 아니다.

## Unknowns And Confidence

- 위 이전 이동 성능·빌드 수치는 2026-09-21 기록이다. 이번 전투 단계에서 별도 검증하고 결과를 기능 문서에 기록한다.
- 수백~수천 명 전투, 저장·부활, 캠페인 일정, 부대 전술은 아직 구현·검증 범위가 아니다. 반복 침공은 수동 준비를 거치는 유한 차수이며 장기 밸런스는 프로토타입 값이다.
- `SampleScene`은 빌드 목록에 있지만 현재 게임 흐름에서 용도가 확인되지 않았다.
- CI 설정과 자동 배포 파이프라인은 확인되지 않았다.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/QualitySettings.asset`, `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `Assets/ProjectF/Runtime/**/*.cs`, 세 개의 `.asmdef`
- `Assets/ProjectF/Editor/InputCameraSceneSetup.cs`
- `Assets/ProjectF/Tests/PlayMode/*.cs`
- `Assets/ProjectF/Scenes/InputCamera.unity`
- `Assets/ProjectF/Data/LordMovementSettings.asset`
- `Assets/Settings/UniversalRP.asset`, `Assets/Settings/Renderer2D.asset`
- `Docs/Features/*.md`, `Logs/gather-tests-final.json`

<!-- unity-onboarding:generated:end -->
