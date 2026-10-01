# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- 프로젝트 루트: `C:/Unity/Proejct_F`
- 현재 단계: 승인된 이동·전투에 적 자동 탐지·교전·복귀를 통합
- 현재 브랜치: `codex/enemy-ai` (승인 후 main에서 분기)
- 마지막 분석: 2026-10-01 (Asia/Seoul)
- 기준 커밋: `8bce2a0` (이동·기본 전투 main 병합) + 이번 적 AI 작업. 승인 상태는 `Docs/DevelopmentProgress.md` 참조

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
- 아군 4명·적 3명. `Project F → Open Gameplay`로 열기. 아군은 수동 선택·공격·이동, 적은 자동 탐지·교전·복귀
- 시작 흐름: Unity가 `BasicCombat`을 로드 → 유닛이 `NavigationWorld2D`에 등록 → 입력 액션과 적 AI 활성화 → 플레이어 명령·AI 명령을 기존 전투와 중앙 길찾기로 실행
- 저장된 주요 루트 오브젝트: `Mouse Commands`, `Main Camera`, `Lord`, `Test Unit 2~4`, `Navigation Obstacles`, `Command HUD`, `Command Destinations`, `Movement Test Ground`
- 별도의 씬 로더나 메뉴/게임 상태 전환 시스템은 확인되지 않음

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Scene composition root | `Mouse Commands` 오브젝트가 입력 컨트롤러와 중앙 길찾기 관리자를 소유 | Confirmed | `InputCamera.unity` |
| MonoBehaviour-centric | 입력, 카메라, 유닛, 길찾기 총괄은 MonoBehaviour 컴포넌트 | Confirmed | 런타임 코드 |
| Shared data asset | 모든 기본 유닛이 이동 속도 5의 `LordMovementSettings`를 공유 | Confirmed | 씬 참조와 데이터 에셋 |
| Combat composition | 선택적 `UnitCombat` 컴포넌트, 아군/적 공유 설정 에셋, 유닛별 런타임 체력, 이벤트 기반 체력 표시 | Confirmed | `Runtime/Combat/*.cs` |
| Command integration | 외부 `MoveTo`는 `MoveCommandIssued` 이벤트로 공격 취소, 전투 추적은 내부 `NavigateTo`로 같은 중앙 길찾기 사용 | Confirmed | `CommandableUnit`, `UnitCombat` |
| Enemy decisions | `EnemyCombatAI`가 적의 대상 선정과 경계/교전/복귀 전환을 담당. 활성 중 `UnitCombat`의 피격 반격을 억제해 명령 소유권을 유지 | Confirmed | `EnemyCombatAI`, `UnitCombat.SuppressIdleRetaliation` |
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
- PlayMode 테스트: 이동·카메라 29개 + 기본 전투 14개 + 적 AI 13개
- 기본 전투 테스트는 적 AI를 꺼서 수동 공격 규칙을 분리 검증. 적 AI 테스트는 실제 씬에 연결된 AI를 켜고 검증
- 이전 이동 검사: 29 통과, 0 실패, 0 건너뜀, 83.88초 (`Logs/gather-tests-final.json`)
- 이전 이동 빌드: Windows x64 성공, 약 132.67 MB (`Builds/InputCamera`)
- 기능 문서에 AI inference 셰이더 관련 500건과 Pipeline 안내 1건의 기존 빌드 경고가 기록됨
- 단계별 검증 결과는 `Docs/Features/BasicCombat.md`, `Docs/Features/EnemyAI.md` 참조

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity 프로젝트 파일 읽기 | available | 현재 워크스페이스 직접 확인 |
| Unity Editor 연결 | available | 현재 프로젝트의 Unity 6000.6.0f1, 설치된 Pipeline을 Unity CLI로 제어 |
| 과거 Unity 원격 명령 로그 | available as historical evidence | `Logs/gather-tests-final.json`에 `127.0.0.1:7801` 실행 기록 |
| 콘솔/씬/게임오브젝트 실시간 검사 | available | Unity CLI의 console, eval, editor_status |
| 테스트/Play Mode 실행 | available | Unity CLI의 run_tests, test_status, editor_play/stop |

## Important Constraints

- 현재 결과물은 이동·선택·카메라·충돌 회피·근접 전투·적 경계 AI가 연결된 초기 플레이 버전이다.
- 기본 근접 공격과 Player/Hostile/Neutral 진영을 추가했다. 채집, 건설, 외교 관계, 영웅/병과 규칙, 저장, 메인 메뉴, 편대 유지, Shift 추가 선택/명령 예약은 아직 없다.
- 유닛은 스프라이트가 아니라 런타임 메시와 단색 URP Unlit 재질을 조합한 임시 도형이다.
- `InputCameraSceneSetup.ConfigureMouseControls()`는 기존 씬을 업그레이드하지만 호출 자체가 자동 저장을 보장하지 않는다.
- 입력은 `Assets/Settings/InputSystem_Actions.inputactions`를 직접 쓰지 않고 `CommandInput`이 마우스 액션을 코드로 생성한다.
- 현재 이동 예측은 0.1초 간격 × 60단계 = 6초이며, 기본 0.5초마다 재계산한다.
- 64명 집결과 32명 혼합 교차는 통과했지만, 최대 재계산 시간이 수십 ms이므로 대규모 전투 성능은 완료 상태가 아니다.

## Unknowns And Confidence

- 위 이전 이동 성능·빌드 수치는 2026-09-21 기록이다. 이번 전투 단계에서 별도 검증하고 결과를 기능 문서에 기록한다.
- 수백~수천 명 전투, 저장·부활, 몬스터 생성·웨이브, 부대 전술은 아직 구현·검증 범위가 아니다.
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
