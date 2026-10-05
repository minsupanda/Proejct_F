# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- 프로젝트 루트: `C:/Unity/Proejct_F`
- 현재 단계: 승인된 벽 전투 기반에 목재 채집·추가 건설 흐름을 통합
- 현재 브랜치: `codex/wood-gathering` (승인 후 main에서 분기)
- 마지막 분석: 2026-10-06 (Asia/Seoul)
- 기준 커밋: `ef99c05` (벽 전투 main 병합) + 이번 목재 채집 작업. 승인 상태는 `Docs/DevelopmentProgress.md` 참조

## Confirmed Environment

- Unity 버전: 6000.6.0f1, revision `f7f8ed4d1e24`
- 렌더 파이프라인: URP 17.6.0, 2D Renderer
- 입력: 새 Input System 전용(`activeInputHandler: 1`), 런타임에서 마우스 전용 액션 맵 생성
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
| `Assets/ProjectF/Runtime/Combat` | 전투·AI 설정, 체력·공격, 적 자율 교전·복귀, 시각 피드백 | Confirmed | `UnitCombat`, `EnemyCombatAI`, 설정과 표시 컴포넌트 |
| `Assets/ProjectF/Runtime/Invasion` | 1회 침공 설정·생성·종료 상태와 HUD | Confirmed | `PortalInvasion`, `InvasionSettings`, `InvasionHUD` |
| `Assets/ProjectF/Runtime/Construction` | 건설·철거 규칙, 비용·환급, 입력·대상 표시, 벽의 길찾기 반영, HUD | Confirmed | 건설 컴포넌트와 설정 |
| `Assets/ProjectF/Runtime/Economy` | 목재 재고·유한 자원·채집 명령과 표시 | Confirmed | `EstateStockpile`, `WoodResourceNode`, `WoodGatherer`, 설정·표시 컴포넌트 |
| `Assets/ProjectF/Prefabs` | 비활성 상태로 생성하고 길찾기 연결 후 활성화하는 몬스터 | Confirmed | `PortalRaider.prefab` |
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
- 초기 아군 4명·목재 80. `Project F → Open Gameplay`로 열기. BUILD WALL로 벽 건설(10/칸) → START INVASION → 3초 준비 → 1.5초 간격으로 몬스터 3명 생성 → 진격·교전 → 격퇴/패배 → RESTART
- 침공 전 REMOVE WALL로 직접 지은 벽 철거·환급 가능. 기본 환급률 100%, 건설 시 벽마다 환급액을 확정. 건설·철거 모두 우클릭/Esc 취소, 침공 시작 후 잠금.
- 벽 기본 체력 60과 메시 체력바. 진격 중인 적이 전방의 가까운 벽을 공격하면 BREAK WALL, 파괴 후 진격 재개. 전투 파괴에는 환급 없음.
- 영지 왼쪽 위 나무 3곳(각 40). 준비 상태에서 아군 선택 후 나무 우클릭으로 접근·2초마다 목재 5 획득. 이동·공격·침공 시작 시 취소. 소진 시 그루터기만 남고 충돌 제거. 전용 일꾼·운반·저장은 아직 없음.
- 시작 흐름: Unity가 `BasicCombat`을 로드 → 아군이 `NavigationWorld2D`에 등록 → 침공 시작 시 비활성 몬스터 프리팹을 생성·길찾기에 연결·활성화 → 기존 전투와 중앙 길찾기로 진격·교전
- 저장된 주요 루트 오브젝트: `Mouse Commands`, `Main Camera`, `Lord`, `Test Unit 2~4`, `Navigation Obstacles`, `Command HUD`, `Command Destinations`, `Movement Test Ground`
- 별도 메뉴나 범용 게임 상태 시스템은 없음. `InvasionHUD`의 결과 버튼은 현재 씬을 비동기로 다시 불러옴

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
| Finite wave | `PortalInvasion`이 생성 수와 소유 유닛을 관리. 출구 검사·생성 간격·구성원 정리·승패 판정, UI는 이벤트 구독 | Confirmed | `Runtime/Invasion/*.cs` |
| Construction | `WallConstruction`이 준비 단계·범위·겹침·목재를 검사하고 벽 생성. `EstateStockpile`이 자원 소유. 실제 충돌체로 기존 길찾기에 반영 | Confirmed | `Runtime/Construction`, `Runtime/Economy` |
| Demolition | 같은 `WallConstruction`이 소유 벽 검증·철거·환급을 처리. `WallStructure`에 환급액 보관, 비활성화로 충돌 즉시 제거. 입력 도구는 None/Build/Demolish 단일 모드 | Confirmed | 건설 런타임과 `DemolitionTests` |
| Wall combat | `WallStructure`가 체력과 파괴 소유. `UnitCombat.WallTarget`은 유닛 대상과 배타적이며 공격 주기 공유. AI Breaching 상태가 전방 벽 선택·실패 시 대기·진격 재개를 담당 | Confirmed | `WallStructure`, `UnitCombat`, `EnemyCombatAI`, `WallHealthView` |
| Wood gathering | `WoodResourceNode`의 유한 잔량을 `WoodGatherer`가 기존 재고로 이전. 접근·시야·작업 시간 검사, 명령 이벤트로 취소. 상태·잔량 이벤트로 표시 갱신 | Confirmed | `Runtime/Economy`, `UnitCommandController`, `UnitCombat.AttackCommandIssued` |
| Modal pointer ownership | 건설 입력이 소유자별 차단 API로 월드 클릭을 점유. 취소 후 버튼을 뗄 때까지 기존 명령 차단 | Confirmed | `UnitCommandController.SetPointerCommandsBlocked`, `WallPlacementController` |
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

- EditMode 테스트: 없음
- PlayMode 테스트: 이동·카메라 29개 + 기본 전투 14개 + 적 AI 13개 + 침공 12개 + 건설 12개 + 철거 13개 + 벽 전투 15개 + 목재 채집 15개
- 2026-10-06 목재 채집 최종 전체 123개 통과, 실패·건너뜀 0개, 199.91초. 실제 기본 설정의 자원 증가·소진·추가 건설·침공 취소 확인. `Docs/Features/WoodGathering.md` 참조. 사용자 최종 확인 대기.
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

- 현재 결과물은 이동·선택·카메라·충돌 회피·근접 전투·적 AI·첫 포탈 침공·방어벽 건설·철거·체력·파괴가 연결된 초기 플레이 버전이다.
- 기본 근접 공격과 Player/Hostile/Neutral 진영, 방어벽 건설·철거·체력·적의 근접 벽 공격, 준비 단계 목재 채집을 추가했다. 전용 일꾼·운반·생산 시설, 공성 장비·최적 돌파 경로·수리, 외교 관계, 영웅/병과 규칙, 저장, 메인 메뉴, 편대 유지, Shift 추가 선택/명령 예약은 아직 없다.
- 유닛은 스프라이트가 아니라 런타임 메시와 단색 URP Unlit 재질을 조합한 임시 도형이다.
- `InputCameraSceneSetup.ConfigureMouseControls()`는 기존 씬을 업그레이드하지만 호출 자체가 자동 저장을 보장하지 않는다.
- 입력은 `Assets/Settings/InputSystem_Actions.inputactions`를 직접 쓰지 않고 `CommandInput`이 마우스 액션을 코드로 생성한다.
- 현재 이동 예측은 0.1초 간격 × 60단계 = 6초이며, 기본 0.5초마다 재계산한다.
- 64명 집결과 32명 혼합 교차는 통과했지만, 최대 재계산 시간이 수십 ms이므로 대규모 전투 성능은 완료 상태가 아니다.

## Unknowns And Confidence

- 위 이전 이동 성능·빌드 수치는 2026-09-21 기록이다. 이번 전투 단계에서 별도 검증하고 결과를 기능 문서에 기록한다.
- 수백~수천 명 전투, 저장·부활, 반복 웨이브와 캠페인 일정, 부대 전술은 아직 구현·검증 범위가 아니다.
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
