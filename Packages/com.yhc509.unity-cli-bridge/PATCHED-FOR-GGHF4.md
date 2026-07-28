# 로컬 패치 노트 (embedded 전환 사유)

원본: https://github.com/yhc509/unity-cli-bridge `v0.4.1`
(`unity-package/com.yhc509.unity-cli-bridge`, CLI 프로토콜 5 / unity-cli 0.4.1 과 페어)
v0.4.0 → v0.4.1 업그레이드: 2026-07-15 (아래 2개 패치 재적용).

git URL 참조 대신 embedded 로 전환하고 아래 2가지를 패치했다.
`com.unity.purchasing` 5.4.1 과의 공존 문제 — 업스트림에 이슈로 올릴 만한 내용.
(참고: gghf4 는 2026-07-14 Android 빌드 이슈로 purchasing 을 제거했지만,
Unity Newtonsoft 포크 정합은 여전히 안전한 구성이라 패치를 유지한다.)

1. **`Editor/Plugins/Newtonsoft.Json.dll` 삭제 + `com.unity.nuget.newtonsoft-json: 3.2.2` 의존성 추가**
   동봉된 공식 Newtonsoft 13.0 dll 이 Unity 포크(`com.unity.nuget.newtonsoft-json`)와
   어셈블리 이름이 겹쳐 에디터에서 이름 해석을 선점 → 포크에만 있는
   `Newtonsoft.Json.Utilities.AotHelper` 가 사라져 `com.unity.services.core` /
   `com.unity.purchasing` 생성 코드가 CS0103 으로 전멸했다.
   Editor asmdef 는 `precompiledReferences: ["Newtonsoft.Json.dll"]` (파일명 참조)라
   포크 dll 로 자연히 재해석된다. 코드 수정 없음. (빈 `Editor/Plugins` 폴더와 meta 도 제거)

2. **`Editor/PackageCommandHandler.cs.meta` GUID 재발급**
   업스트림 GUID `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6` (수제 placeholder)가
   `com.unity.purchasing/Runtime/Stores/Android/GooglePlay/AndroidDeviceInfoBuilder.cs`
   의 GUID 와 정면 충돌 → 한쪽 에셋이 무시됐다.
   → `eec5b5ffa2044ba69134c758a75b61a7` 로 교체. (v0.4.1 업스트림도 여전히 placeholder — 재적용함)

업그레이드 시: 새 버전을 다시 embedded 로 받고 위 2개 패치를 재적용할 것.
CLI 바이너리와 패키지 버전은 반드시 함께 올릴 것(프로토콜 정합).
v0.4.1 부터 CLI 는 `~/.unity-cli-bridge/versions/<버전>/` 에 버전별 설치되고
`~/.unity-cli-bridge/unity-cli/` 는 PATH 타깃(디스패처)이다. 구버전 브리지 프로젝트(gghf2 0.3.5-gghf2)는
프로토콜 hand-off 로 공존한다. 설치는 Window > Unity CLI Manager 또는
`CliInstallerState.FinalizeInstall("<버전>")` (versions/ 에 바이너리 배치 후).
