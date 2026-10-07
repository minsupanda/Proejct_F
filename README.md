# Proejct_F

Unity 6000.6.0f1 / 2D 전략 게임 프로토타입.

## 지금 확인할 기능

Unity 상단 **Project F → Open Gameplay → Play**.
**TRAIN SOLDIER**로 목재 20을 소비하면 준비 시간 5초 후 왼쪽 훈련소에서 병사 한 명이 나옵니다. 기본 생존 아군 상한은 8명입니다. 새 병사도 선택·이동·공격·채집할 수 있습니다.
한 번에 한 명씩 훈련하며, 침공 중에는 남은 시간을 보관했다가 다음 준비 단계에서 이어갑니다.
아군을 선택하고 왼쪽 위 나무를 **우클릭**하면 접근해 **2초마다 목재 5**를 채집합니다. 나무마다 40씩 있으며, 채집은 침공 시작 전까지 가능합니다.
**BUILD WALL**을 눌러 목재로 방어벽을 짓습니다. 초록 위치를 좌클릭해 배치하고 우클릭/Esc로 건설을 취소합니다.
**REMOVE WALL**을 누르고 주황색으로 표시된 벽을 좌클릭하면 철거·목재 환급을 받습니다. 침공 시작 전까지 방어선을 다시 배치할 수 있습니다.
아군을 좌클릭·드래그로 선택하고 적을 우클릭하면 공격합니다. 땅을 우클릭하면 이동합니다.
화면 아래 **START INVASION**을 누르면 포탈에서 몬스터 3명이 등장해 진격합니다.
벽에는 체력 60이 있으며, 몬스터가 전방의 벽을 공격하면 **BREAK WALL**로 표시됩니다. 체력이 0이 되면 환급 없이 파괴되고 길이 열립니다.
아군에게 직접 공격 명령을 내려 격퇴합니다. **NEXT ROUND**를 누르면 체력·목재·나무 잔량·벽을 유지하며 다음 침공을 준비합니다. 채집·건설·철거를 다시 하고 **START INVASION**으로 진행합니다.
기본 **3차 침공(3명 → 4명 → 5명)**을 모두 막거나 패배하면 **RESTART**로 처음부터 다시 시작합니다.

- [병력 충원 사용법과 검증](Docs/Features/UnitRecruitment.md)
- [반복 침공·다음 차수 준비 사용법과 검증](Docs/Features/InvasionRounds.md)
- [목재 채집·추가 건설 사용법과 검증](Docs/Features/WoodGathering.md)
- [목재 소비·방어벽 건설 사용법과 검증](Docs/Features/WallBuilding.md)
- [방어벽 철거·목재 환급 사용법과 검증](Docs/Features/WallDemolition.md)
- [방어벽 체력·몬스터 벽 공격 사용법과 검증](Docs/Features/WallCombat.md)
- [포탈 생성·첫 침공 사용법과 검증](Docs/Features/PortalInvasion.md)
- [적 자동 탐지·교전·복귀 사용법과 검증](Docs/Features/EnemyAI.md)
- [기본 근접 전투 사용법과 검증](Docs/Features/BasicCombat.md)
- [개발 진행·사용자 확인·브랜치 운영](Docs/DevelopmentProgress.md)
- [통합 기획서 원문](Docs/Design/GameConcept.md)
- [Unity 프로젝트 구조](Docs/AI/UnityProjectContext.md)

각 기능은 구현·검증 후 사용자의 최종 확인을 받고 다음 단계로 진행합니다.
