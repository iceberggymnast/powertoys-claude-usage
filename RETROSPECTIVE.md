# PowerToys Command Palette Dock 위젯 개발 회고록

다음 위젯을 만들 때 이 문서만 보면 조사 없이 바로 시작 가능.

---

## 1. 전체 구조 한 눈에

```
MyWidget/
├── MyWidget.sln
└── MyWidget/
    ├── MyWidget.csproj          ← NuGet + MSIX 설정
    ├── Package.appxmanifest     ← COM 서버 + AppExtension 등록
    ├── app.manifest             ← DPI 인식 (boilerplate)
    ├── Program.cs               ← COM 서버 진입점
    ├── MyExtension.cs           ← IExtension 구현 (GUID 필수)
    ├── MyCommandProvider.cs     ← CommandProvider, GetDockBands()
    ├── MyDockBand.cs            ← WrappedDockItem (실제 UI)
    └── Assets/                  ← PNG 5개 (1×1 투명 PNG도 OK)
```

---

## 2. .csproj 정답 (검증됨)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows10.0.22000.0</TargetFramework>
    <WindowsSdkPackageVersion>10.0.26100.38</WindowsSdkPackageVersion>
    <RootNamespace>MyWidget</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <UseWinUI>false</UseWinUI>
    <EnableMsixTooling>true</EnableMsixTooling>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Platforms>x64</Platforms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
  </PropertyGroup>

  <ItemGroup>
    <!-- Toolkit이 이 패키지 안에 포함됨 — 별도 패키지 없음 -->
    <PackageReference Include="Microsoft.CommandPalette.Extensions" Version="0.9.260303001" />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.*" />
    <PackageReference Include="Shmuelie.WinRTServer" Version="2.2.1" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Assets\**">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
```

### 패키지 주의사항
| 패키지 | 올바른 ID | 함정 |
|--------|-----------|------|
| CmdPal SDK | `Microsoft.CommandPalette.Extensions` | `Extensions.Toolkit`은 별도 패키지 아님 — 위 패키지 안에 포함 |
| WinRT 서버 | `Shmuelie.WinRTServer` 2.2.1 | `Shmuelie.WinRTServer.CsWinRT`는 별도 패키지 아님 |
| TargetFramework | `net9.0-windows10.0.22000.0` | `net8.0` + WinAppSDK 1.6.x → System.Runtime 버전 충돌 발생 |
| WindowsSdkPackageVersion | `10.0.26100.38` | 버전이 낮으면 NETSDK 빌드 오류 |

---

## 3. Package.appxmanifest 정답 (검증됨)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
  xmlns:com="http://schemas.microsoft.com/appx/manifest/com/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap uap3 rescap">

  <Identity Name="MyWidget.Name" Publisher="CN=MyPublisher" Version="1.0.0.0" />

  <Properties>
    <DisplayName>My Widget</DisplayName>
    <PublisherDisplayName>MyPublisher</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <!-- Windows 11 이상 필수 (22000 = Windows 11 21H2) -->
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22000.0" MaxVersionTested="10.0.22000.0" />
  </Dependencies>

  <Resources><Resource Language="x-generate"/></Resources>

  <Applications>
    <Application Id="App" Executable="$targetnametoken$.exe" EntryPoint="$targetentrypoint$">
      <uap:VisualElements DisplayName="My Widget" Description="설명"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.scale-200.png"
        Square44x44Logo="Assets\Square44x44Logo.scale-200.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.scale-200.png" />
        <uap:SplashScreen Image="Assets\SplashScreen.scale-200.png" />
      </uap:VisualElements>
      <Extensions>

        <!-- COM 서버 등록 — GUID는 새로 생성 (Visual Studio > Tools > Create GUID) -->
        <com:Extension Category="windows.comServer">
          <com:ComServer>
            <com:ExeServer Executable="MyWidget.exe"
                           Arguments="-RegisterProcessAsComServer"
                           DisplayName="MyWidgetApp">
              <com:Class Id="YOUR-GUID-HERE" DisplayName="MyExtension" />
            </com:ExeServer>
          </com:ComServer>
        </com:Extension>

        <!-- PowerToys Command Palette 연결 -->
        <uap3:Extension Category="windows.appExtension">
          <uap3:AppExtension Name="com.microsoft.commandpalette"
            Id="ID" PublicFolder="Public"
            DisplayName="My Widget" Description="설명">
            <uap3:Properties>
              <CmdPalProvider>
                <Activation>
                  <!-- COM 서버와 동일한 GUID -->
                  <CreateInstance ClassId="YOUR-GUID-HERE" />
                </Activation>
                <SupportedInterfaces><Commands/></SupportedInterfaces>
              </CmdPalProvider>
            </uap3:Properties>
          </uap3:AppExtension>
        </uap3:Extension>

      </Extensions>
    </Application>
  </Applications>

  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
```

---

## 4. C# 파일 5개 패턴 (검증됨)

### Program.cs
```csharp
using Shmuelie.WinRTServer;
using Shmuelie.WinRTServer.CsWinRT;  // namespace는 여기, 패키지는 Shmuelie.WinRTServer

[MTAThread]
public static async Task Main(string[] args)
{
    if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
    {
        await using ComServer server = new();
        ManualResetEvent disposed = new(false);
        MyExtension instance = new(disposed);
        server.RegisterClass<MyExtension, IExtension>(() => instance);
        server.Start();
        disposed.WaitOne();
    }
}
```

### MyExtension.cs
```csharp
[ComVisible(true)]
[Guid("YOUR-GUID-HERE")]           // appxmanifest와 동일
[ComDefaultInterface(typeof(IExtension))]
public sealed partial class MyExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _disposed;
    private readonly MyCommandProvider _provider = new();

    public MyExtension(ManualResetEvent disposed) => _disposed = disposed;

    public object GetProvider(ProviderType t) =>
        t == ProviderType.Commands ? _provider : null!;

    public void Dispose() => _disposed.Set();
}
```

### MyCommandProvider.cs
```csharp
public partial class MyCommandProvider : CommandProvider
{
    private readonly MyDockBand _band = new();
    public override ICommandItem[] TopLevelCommands() => [];
    public override ICommandItem[] GetDockBands() => [_band];
}
```

### MyDockBand.cs — 핵심 UI
```csharp
internal sealed partial class MyDockBand : WrappedDockItem
{
    private readonly ListItem _item1;
    private readonly ListItem _item2;
    private readonly Timer _timer;

    public MyDockBand()
        : base([], "com.mywidget.unique.id", "My Widget")
    {
        // Title = 큰 글자, Subtitle = 작은 글자, Icon = 이모지 or Segoe 글리프
        _item1 = new ListItem(new NoOpCommand())
        {
            Title    = "Loading…",
            Subtitle = "Label 1",
            Icon     = new IconInfo("⚡"),
        };
        _item2 = new ListItem(new NoOpCommand())
        {
            Title    = "Loading…",
            Subtitle = "Label 2",
            Icon     = new IconInfo("📅"),
        };
        Items = [_item1, _item2];

        // dueTime=0 → 즉시 실행, period=60s
        _timer = new Timer(_ => _ = RefreshAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(60));
    }

    private async Task RefreshAsync()
    {
        // 데이터 가져와서 _item1.Title / _item1.Subtitle 업데이트
        // ListItem 프로퍼티를 직접 변경하면 UI가 자동 갱신됨 (INotifyPropertyChanged 내장)
    }
}
```

### NoOpCommand.cs
```csharp
// ListItem 생성자에 command가 필수라서 빈 명령 제공
internal sealed partial class NoOpCommand : InvokableCommand
{
    public override CommandResult Invoke() => CommandResult.KeepOpen();
}
```

---

## 5. ListItem 표시 규칙

| 프로퍼티 | Dock에서 역할 | 예시 |
|---------|--------------|------|
| `Title` | **큰 글자** (강조값) | `"100%"`, `"42°C"` |
| `Subtitle` | **작은 글자** (레이블+부가정보) | `"Session · resets in 2h"` |
| `Icon` | 아이콘 | `new IconInfo("⚡")` — 이모지 그대로 사용 가능 |

- 프로퍼티를 직접 수정하면 UI 자동 갱신 (별도 RaisePropertyChanged 불필요)
- `Items` 배열은 생성자에서 한 번만 설정

---

## 6. 빌드 & 배포 (검증됨)

### 최초 1회: 인증서 생성
Visual Studio → 프로젝트 우클릭 → Publish → Create App Packages → Sideloading → 인증서 Create → 생성된 `.pfx` 더블클릭 설치

### 매번 배포
```powershell
# 1. 빌드
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "MyWidget\MyWidget.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal

# 2. 등록 (이미 설치된 경우 -ForceUpdateFromAnyVersion 추가)
Add-AppxPackage -Register `
  "MyWidget\bin\x64\Debug\net9.0-windows10.0.22000.0\win-x64\AppxManifest.xml" `
  -ForceApplicationShutdown -ForceUpdateFromAnyVersion

# 3. PowerToys 재시작
Stop-Process -Name 'PowerToys' -ErrorAction SilentlyContinue
Start-Sleep 2
Start-Process 'C:\Program Files\PowerToys\PowerToys.exe'
```

### Visual Studio에서 배포
빌드(Build) 메뉴 → **[프로젝트명] 배포(Deploy)**
(플랫폼이 `x64`로 설정되어야 메뉴 활성화)

---

## 7. 겪은 오류와 해결법

| 오류 | 원인 | 해결 |
|------|------|------|
| `NU1101` Microsoft.CommandPalette.Extensions.Toolkit | 패키지 이름 틀림 | `Microsoft.CommandPalette.Extensions` 사용 (Toolkit 포함) |
| `NU1101` Shmuelie.WinRTServer.CsWinRT | 패키지 이름 틀림 | `Shmuelie.WinRTServer`만 사용 (`using` namespace는 `.CsWinRT`) |
| `NU1202` Shmuelie.WinRTServer 호환 안됨 | TargetFramework가 19041 | `net9.0-windows10.0.22000.0`으로 변경 |
| `NETSDK` Windows SDK 버전 오류 | WindowsSdkPackageVersion 낮음 | `10.0.26100.38`로 설정 |
| System.Runtime 버전 충돌 | .NET 8 + WinAppSDK 1.6.x | .NET 9으로 올리면 해결 |
| `0x80073CFB` 배포 실패 | 이미 동일 버전 설치됨 | `-ForceUpdateFromAnyVersion` 플래그 추가 |

---

## 8. GUID 관리

- **3곳이 반드시 동일한 GUID**:
  1. `Package.appxmanifest` → `com:Class Id="..."`
  2. `Package.appxmanifest` → `CreateInstance ClassId="..."`
  3. `MyExtension.cs` → `[Guid("...")]`

- 새 GUID 생성: Visual Studio → Tools → Create GUID → Registry Format 복사

---

## 9. 토큰/인증 패턴 (이번 프로젝트)

Claude Code 토큰 위치:
```
~/.claude/.credentials.json
→ .claudeAiOauth.accessToken
```

Windows Credential Manager 대신 파일에서 직접 읽는 방식이 더 단순하고 신뢰성 높음.
PasswordVault는 AppContainer 전용이므로 일반 데스크톱 앱에서 사용 불가.

---

## 10. 체크리스트 (새 위젯 시작 시)

- [ ] 새 GUID 생성 → 3곳에 동일하게 입력
- [ ] `Publisher="CN=XXX"` → 인증서 생성 시 이름과 정확히 일치
- [ ] `TargetFramework`: `net9.0-windows10.0.22000.0`
- [ ] `WindowsSdkPackageVersion`: `10.0.26100.38`
- [ ] Assets 폴더에 PNG 5개 (1×1 투명 PNG라도 있어야 빌드 통과)
- [ ] `base([], "com.고유ID", "표시이름")` — ID는 전역 고유값
- [ ] 배포 전 PowerToys 종료 확인
