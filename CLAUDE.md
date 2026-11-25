# Firework

음악(Scott Buckley – *Stars In Her Skies*, CC BY 4.0)에 맞춰 터지는 불꽃놀이 씬. Unity 6000.3, URP.

## 구성

- 씬: `Assets/Scenes/FireworkShow.unity` (설치기가 통째로 만든다. 씬에서 직접 고친 내용은 다시 만들면 사라진다.)
- 런타임: `Assets/Fireworks/Scripts` — `ShowConductor`(재생·발사 시각), `FireworkLauncher`/`FireworkShell`(풀·발사), `CameraDirector`, `PlanarReflection`
- 데이터: `Assets/Fireworks/Data/ShowData.asset` — `sections`·`cameraKeys`·`manualCues`는 사람이 고치는 부분, `autoCues`는 생성물
- 에디터 메뉴(`Assets/Fireworks/Editor`), 순서대로 실행:
  1. `Fireworks/1 Analyze Audio` → `AudioAnalysis.asset`, 요약은 `Temp/FireworksAnalysis.txt`
  2. `Fireworks/2 Build Prefabs` → 텍스처·머티리얼·프리팹 8종
  3. `Fireworks/3 Build Scene` → 씬 전체
  4. `Fireworks/4 Compose Show` → `autoCues`만 다시 생성
  - `Fireworks/Reset/…` → 구간 표·카메라 또는 수동 큐를 코드의 기본값으로 되돌림
- 곡의 박자: 100 BPM, 한 마디 2.4초, 첫 박 0.13초. 2:40부터 4.8초마다 큰 타격음.
- 하위 파티클 시스템은 처음 재생될 때 Burst를 한 번 내보낸다. 그래서 풀은 y=-1000에 만든다(`FireworkLauncher.Create`).

## Unity 에디터 작업

- 실행 중인 에디터는 Unity CLI(`unity`, 1.0.0)와 Pipeline 패키지(1.0.1)로 다룬다. 시작 전에 `unity status`로 연결을 확인한다. 명령은 `unity command <그룹> <이름> --인자 값` 형식이고, 목록은 `unity command`, `unity command <그룹>`으로 본다.
  - 컴파일: `unity --json recompile --project-path <경로>` (끝난 직후 잠깐 연결이 끊긴다. `unity status`가 `ready`가 될 때까지 기다린다.)
  - C# 실행: `unity command script eval --code "…"` 또는 `--file <절대경로>` (`--timeout 초`)
  - 메뉴: `unity command editor invoke_menu --path "…"`
  - 씬: `unity command scene list_open` / `scene open` / `scene save`
  - 콘솔: `unity command console get --tail 20 --level warn`
  - 캡처: `unity command capture game_view --source camera --width 1920 --height 1080 --output-path <프로젝트 밖 절대경로>`
- 씬·프리팹·설정 자산은 텍스트로 직접 고치지 않는다. 변경은 `Editor/`의 설치기로 작성해 실행한다. 설치기는 다시 실행해도 결과가 같아야 한다.
- 코드 변경 후 컴파일 오류 0을 확인한다. 씬 반영 후에는 콘솔 오류와 `scene list_open`의 dirty 여부를 확인한다.
- 조사·검증용 C#은 프로젝트 밖 임시 파일로 써서 `script eval --file`로 실행한다.
- Play 모드 진입, 자동 플레이 테스트, 반복 캡처는 사용자가 요청할 때만 한다. 플레이 확인은 사용자가 한다.
- 편집 모드에서 불꽃 모양을 보려면 프리팹을 임시로 만들어 `ParticleSystem.Simulate`로 돌린다. 이때 하위 시스템은 하나씩 `Simulate`를 한 번 더 불러야 렌더 경계가 갱신되고, 캡처 해상도는 반사 텍스처와 같은 비율로 맞춘다.
- 사용자가 에디터에서 작업 중일 수 있다. 저장 안 된 변경이 있으면 저장·폐기 전에 묻는다.
- 파일 작성, 컴파일 통과, 에디터 반영, 플레이 확인을 구분해 보고한다.
- 패키지 추가·업그레이드와 자산 삭제·대량 이동은 사용자 확인 후 한다.
