# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- 프로젝트 루트: `C:/Unity/Proejct_F`
- 현재 단계: 마우스로 유닛을 선택하고 집결 이동 명령을 내리는 2D 전략 게임 이동 프로토타입
- 현재 브랜치: `Input_Camera`
- 마지막 분석: 2026-09-21 (Asia/Seoul)
- 마지막 분석 커밋: `6ef5fc3` + 아직 커밋되지 않은 `Assets/ProjectF`, `Docs`, 프로젝트 설정 변경

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
| `Assets/ProjectF/Editor` | 시험 씬 생성/열기와 설정 에셋 선택 메뉴 | Confirmed | `InputCameraSceneSetup.cs` |
| `Assets/ProjectF/Tests/PlayMode` | 실제 씬과 가상 입력을 이용한 자동 검사 | Confirmed | `MouseCommandTests.cs`, `NavigationTests.cs` |
| `Assets/ProjectF/Scenes` | 현재 주 기능을 합친 `InputCamera` 씬 | Confirmed | `InputCamera.unity` |
| `Assets/ProjectF/Data` | 공유 이동 속도 ScriptableObject | Confirmed | `LordMovementSettings.asset` |
| `Docs/Features` | 입력/카메라, 길찾기, 집결 이동 설명과 검증 기록 | Confirmed | 세 개의 기능 문서 |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `ProjectF.Runtime` | 빌드에 포함되는 게임 코드 | `Unity.InputSystem`, `Unity.ugui` | 루트 네임스페이스 `ProjectF` |
| `ProjectF.Editor` | 에디터 메뉴와 시험 씬 구성 | `ProjectF.Runtime`, Input System, uGUI | Editor 플랫폼 전용 |
| `ProjectF.PlayModeTests` | 입력·카메라·길찾기 PlayMode 검사 | `ProjectF.Runtime`, Input System test framework | 테스트 어셈블리 |

## Scenes And Startup Flow

- 빌드 씬 0: `Assets/ProjectF/Scenes/InputCamera.unity` (활성, 시작 씬)
- 빌드 씬 1: `Assets/Scenes/SampleScene.unity` (활성, 현재 코드에서 자동 전환 없음)
- 시작 흐름: Unity가 `InputCamera`를 로드 → 각 유닛이 `NavigationWorld2D`에 등록 → `CommandInput`이 입력 액션 활성화 → 좌클릭 선택/우클릭 명령 → 중앙 길찾기 관리자가 FixedUpdate에서 모든 유닛의 경로와 속도를 함께 계산
- 저장된 주요 루트 오브젝트: `Mouse Commands`, `Main Camera`, `Lord`, `Test Unit 2~4`, `Navigation Obstacles`, `Command HUD`, `Command Destinations`, `Movement Test Ground`
- 별도의 씬 로더나 메뉴/게임 상태 전환 시스템은 확인되지 않음

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Scene composition root | `Mouse Commands` 오브젝트가 입력 컨트롤러와 중앙 길찾기 관리자를 소유 | Confirmed | `InputCamera.unity` |
| MonoBehaviour-centric | 입력, 카메라, 유닛, 길찾기 총괄은 MonoBehaviour 컴포넌트 | Confirmed | 런타임 코드 |
| Shared data asset | 모든 기본 유닛이 이동 속도 5의 `LordMovementSettings`를 공유 | Confirmed | 씬 참조와 데이터 에셋 |
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
- PlayMode 테스트: 29개. 마우스/카메라 11개, 길찾기/집결 18개
- 마지막 저장 로그: 29 통과, 0 실패, 0 건너뜀, 83.88초 (`Logs/gather-tests-final.json`)
- 마지막 기록 빌드: Windows x64 성공, 약 132.67 MB (`Builds/InputCamera`)
- 기능 문서에 AI inference 셰이더 관련 500건과 Pipeline 안내 1건의 기존 빌드 경고가 기록됨
- 이번 온보딩에서는 테스트나 빌드를 다시 실행하지 않았으며 저장된 결과와 소스를 확인함

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity 프로젝트 파일 읽기 | available | 현재 워크스페이스 직접 확인 |
| Unity Editor 연결/MCP | unavailable | 현재 세션에 Unity 도구가 노출되지 않고 Unity Editor 프로세스도 없음 |
| 과거 Unity 원격 명령 로그 | available as historical evidence | `Logs/gather-tests-final.json`에 `127.0.0.1:7801` 실행 기록 |
| 콘솔/씬/게임오브젝트 실시간 검사 | unavailable | 현재 연결된 Unity provider 없음 |
| 테스트/Play Mode/Profiler 실시간 실행 | unavailable | 현재 연결된 Unity provider 없음 |

## Important Constraints

- 현재 결과물은 완성 게임이 아니라 이동·선택·카메라·충돌 회피를 검증하는 프로토타입이다.
- 공격, 채집, 건설, 진영, 영웅/병과 규칙, 저장, 메인 메뉴, 편대 유지, Shift 추가 선택/명령 예약은 아직 없다.
- 유닛은 스프라이트가 아니라 런타임 메시와 단색 URP Unlit 재질을 조합한 임시 도형이다.
- `InputCameraSceneSetup.ConfigureMouseControls()`는 기존 씬을 업그레이드하지만 호출 자체가 자동 저장을 보장하지 않는다.
- 입력은 `Assets/Settings/InputSystem_Actions.inputactions`를 직접 쓰지 않고 `CommandInput`이 마우스 액션을 코드로 생성한다.
- 현재 이동 예측은 0.1초 간격 × 60단계 = 6초이며, 기본 0.5초마다 재계산한다.
- 64명 집결과 32명 혼합 교차는 통과했지만, 최대 재계산 시간이 수십 ms이므로 대규모 전투 성능은 완료 상태가 아니다.

## Unknowns And Confidence

- 현재 Unity Editor를 실시간 검사하지 않았으므로 Hierarchy의 펼침 상태나 Inspector UI 표시 방식은 저장된 YAML을 기준으로 설명한다.
- 저장된 테스트/빌드 결과는 2026-09-21의 로그이며 이번 분석에서 재실행하지 않았다.
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
