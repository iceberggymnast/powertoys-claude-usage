# DockBar — Claude Usage for PowerToys Command Palette

**[한국어](#한국어) · [English](#english)**

---

## 한국어

### 소개

PowerToys Command Palette의 Dock 툴바에 **Claude 구독 사용량**을 실시간으로 표시하는 Windows 확장 프로그램입니다.

- **Session** — 5시간 롤링 세션 사용률 (⚡)
- **Weekly** — 7일 주간 사용률 (📅)

60초마다 자동 갱신되며, 네트워크 오류 시 마지막 캐시 데이터를 표시합니다.

### 미리보기

```
⚡  100%          📅  2%
   Session · resets in now     Weekly · resets in 6d 20h
```

### 요구 사항

| 항목 | 최소 버전 |
|------|----------|
| Windows | 11 (22000+) |
| PowerToys | 0.90+ (Command Palette 포함) |
| .NET SDK | 9.0+ |
| Claude Code | 로그인 상태 (`~/.claude/.credentials.json` 필요) |

### 설치 방법

1. **저장소 클론**
   ```powershell
   git clone https://github.com/YOUR_USERNAME/DockBar.git
   cd DockBar
   ```

2. **인증서 생성** (최초 1회)
   - Visual Studio에서 `DockBar.sln` 열기
   - 솔루션 탐색기에서 `DockBar` 프로젝트 우클릭
   - Publish → Create App Packages → Sideloading → 인증서 Create
   - 생성된 `.pfx` 파일 더블클릭하여 로컬에 설치

3. **빌드 & 배포**
   ```powershell
   # 빌드
   & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
     "DockBar\DockBar.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal

   # 등록
   Add-AppxPackage -Register `
     "DockBar\bin\x64\Debug\net9.0-windows10.0.22000.0\win-x64\AppxManifest.xml" `
     -ForceApplicationShutdown -ForceUpdateFromAnyVersion

   # PowerToys 재시작
   Stop-Process -Name 'PowerToys' -ErrorAction SilentlyContinue
   Start-Sleep 2
   Start-Process 'C:\Program Files\PowerToys\PowerToys.exe'
   ```

### 동작 원리

```
~/.claude/.credentials.json
        │  claudeAiOauth.accessToken
        ▼
GET https://api.anthropic.com/api/oauth/usage
  Authorization: Bearer <token>
  anthropic-beta: oauth-2025-04-20
        │
        ▼
  five_hour.utilization / resets_at
  seven_day.utilization / resets_at
        │
        ▼
PowerToys Dock Band (60s 자동 갱신)
```

### 기술 스택

- **언어**: C# / .NET 9
- **SDK**: Microsoft.CommandPalette.Extensions 0.9.x
- **패키징**: MSIX (사이드로드)
- **COM 호스팅**: Shmuelie.WinRTServer 2.2.1

---

## English

### Overview

A PowerToys Command Palette **Dock band extension** that displays your Claude subscription usage in real time — directly in the Windows taskbar toolbar.

- **Session** — 5-hour rolling session utilization (⚡)
- **Weekly** — 7-day weekly utilization (📅)

Auto-refreshes every 60 seconds. Shows cached data on network errors.

### Preview

```
⚡  100%          📅  2%
   Session · resets in now     Weekly · resets in 6d 20h
```

### Requirements

| Item | Minimum |
|------|---------|
| Windows | 11 (build 22000+) |
| PowerToys | 0.90+ (with Command Palette) |
| .NET SDK | 9.0+ |
| Claude Code | Signed in (`~/.claude/.credentials.json` must exist) |

### Installation

1. **Clone the repository**
   ```powershell
   git clone https://github.com/YOUR_USERNAME/DockBar.git
   cd DockBar
   ```

2. **Create a signing certificate** (once only)
   - Open `DockBar.sln` in Visual Studio
   - Right-click the `DockBar` project → Publish → Create App Packages
   - Choose **Sideloading** → Create a certificate
   - Double-click the generated `.pfx` to install it locally

3. **Build & Deploy**
   ```powershell
   # Build
   & "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
     "DockBar\DockBar.csproj" /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal

   # Register
   Add-AppxPackage -Register `
     "DockBar\bin\x64\Debug\net9.0-windows10.0.22000.0\win-x64\AppxManifest.xml" `
     -ForceApplicationShutdown -ForceUpdateFromAnyVersion

   # Restart PowerToys
   Stop-Process -Name 'PowerToys' -ErrorAction SilentlyContinue
   Start-Sleep 2
   Start-Process 'C:\Program Files\PowerToys\PowerToys.exe'
   ```

### How It Works

The extension reads the Claude Code OAuth token from `~/.claude/.credentials.json` and calls the Claude usage API every 60 seconds. Results are displayed as a two-item Dock band in the PowerToys Command Palette toolbar.

On HTTP 429 or transient errors, the last successful response is shown with a `·` suffix to indicate stale data.

### Tech Stack

- **Language**: C# / .NET 9
- **SDK**: Microsoft.CommandPalette.Extensions 0.9.x
- **Packaging**: MSIX (sideloaded)
- **COM hosting**: Shmuelie.WinRTServer 2.2.1

### License

MIT

---

## 면책 조항 / Disclaimer

**한국어**

이 프로그램은 Anthropic 또는 Claude와 공식적으로 관련이 없는 **비공식 서드파티 프로젝트**입니다.
Claude 및 Claude Code는 Anthropic의 상표입니다.

이 소프트웨어는 현재 공개된 비공식 API 엔드포인트를 사용하며, 해당 API는 사전 고지 없이 변경되거나 중단될 수 있습니다.
사용으로 인한 계정 제한, 데이터 손실, 또는 기타 문제에 대해 개발자는 일체의 책임을 지지 않습니다.
**사용은 전적으로 본인의 책임입니다.**

**English**

This is an **unofficial third-party project** and is not affiliated with, endorsed by, or associated with Anthropic or Claude in any way.
Claude and Claude Code are trademarks of Anthropic.

This software uses an unofficial, undocumented API endpoint that may change or be discontinued at any time without notice.
The developer assumes no responsibility for account restrictions, data loss, or any other issues arising from the use of this software.
**Use at your own risk.**
