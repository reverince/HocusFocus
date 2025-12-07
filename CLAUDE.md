# CLAUDE.md - FocusClock 프로젝트 가이드

이 문서는 AI 어시스턴트가 FocusClock 프로젝트를 이해하고 작업하는 데 필요한 정보를 담고 있습니다.

## 프로젝트 개요

FocusClock은 특정 애플리케이션에서 활동한 누적 시간을 기록하는 Windows 데스크톱 앱입니다. 생산성 향상을 목표로 사용자가 지정한 앱의 사용 시간만 추적하며, 유휴 상태와 포커스를 자동으로 감지합니다.

## 기술 스택

- **프레임워크**: .NET 8.0, WPF (Windows Presentation Foundation)
- **아키텍처**: MVVM 패턴 (CommunityToolkit.Mvvm)
- **차트 라이브러리**: LiveCharts2 (SkiaSharp 기반)
- **시스템 트레이**: Hardcodet.NotifyIcon.Wpf
- **데이터 저장**: JSON 파일 (`%AppData%\FocusClock\data.json`)

## 프로젝트 구조

```
FocusClock/                     # 루트 디렉토리
├── FocusClock.sln             # 솔루션 파일
├── FocusClock.csproj          # 프로젝트 설정 파일
├── App.xaml(.cs)              # 애플리케이션 진입점
├── MainWindow.xaml(.cs)       # 메인 UI 윈도우
├── AddAppDialog.xaml(.cs)     # 앱 추가 팝업 다이얼로그
├── Models/
│   ├── AppData.cs             # 전체 앱 데이터 (저장/로드용)
│   ├── DailyRecord.cs         # 일별 기록 데이터
│   └── TrackedApp.cs          # 추적 대상 앱 정보
├── ViewModels/
│   └── MainViewModel.cs       # 메인 뷰모델 (+ 관련 ViewModel 클래스들)
├── Services/
│   ├── DataStorage.cs         # JSON 데이터 저장/로드
│   ├── IdleDetector.cs        # 유휴 시간 감지 (Win32 API)
│   ├── TimeRecorder.cs        # 시간 기록 메인 서비스
│   └── WindowTracker.cs       # 활성 창 추적 (Win32 API)
├── Converters/
│   ├── BoolToVisibilityConverter.cs
│   └── InverseBoolToVisibilityConverter.cs
├── Resources/                  # 리소스 파일들
└── .vscode/                    # VS Code/Cursor 설정
    ├── launch.json            # 디버그 실행 설정
    └── tasks.json             # 빌드 태스크 설정
```

## 핵심 컴포넌트

### Services (서비스 레이어)

1. **TimeRecorder**: 시간 기록 관리 메인 서비스
   - 1초 간격 DispatcherTimer로 활성 창 모니터링
   - 추적 대상 앱 + 비유휴 상태일 때만 시간 기록
   - 30초마다 자동 저장
   - 이벤트: `TrackingStatusChanged`, `TodayTimeUpdated`

2. **WindowTracker**: 현재 활성 창 정보 추적
   - Win32 API 사용 (`GetForegroundWindow`, `GetWindowThreadProcessId`)
   - 활성 프로세스 이름, 창 제목 조회

3. **IdleDetector**: 사용자 유휴 상태 감지
   - Win32 API 사용 (`GetLastInputInfo`)
   - 마지막 입력 이후 경과 시간 계산

4. **DataStorage**: JSON 파일 기반 데이터 영속화
   - 비동기/동기 저장/로드 지원
   - 저장 위치: `%AppData%\FocusClock\data.json`

### Models (데이터 모델)

1. **AppData**: 전체 앱 상태
   - `TrackedApps`: 추적 대상 앱 목록
   - `IdleThresholdSeconds`: 유휴 감지 임계값 (기본 300초)
   - `DailyRecords`: 일별 기록 딕셔너리

2. **TrackedApp**: 추적 대상 앱 정보
   - `ProcessName`: 프로세스 이름 (식별자)
   - `DisplayName`: 표시 이름
   - `IsEnabled`: 활성화 여부

3. **DailyRecord**: 일별 기록
   - `Date`: 날짜
   - `AppSeconds`: 앱별 사용 시간 (초)

### ViewModels

**MainViewModel**: 메인 화면 로직
- `ObservableProperty` 어트리뷰트로 바인딩 프로퍼티 자동 생성
- `RelayCommand` 어트리뷰트로 커맨드 자동 생성
- 주요 기능: 추적 토글, 앱 추가/제거, 차트 데이터 관리

## 빌드 및 실행

```bash
# 빌드
dotnet build

# 실행
dotnet run

# 또는 직접 실행
.\bin\Debug\net8.0-windows\FocusClock.exe

# VS Code/Cursor에서 F5로 디버그 실행 가능
```

## 주요 기능 흐름

### 시간 기록 흐름
1. `TimeRecorder` 1초 타이머 Tick
2. `WindowTracker`로 현재 활성 프로세스 확인
3. `IdleDetector`로 유휴 상태 확인
4. 추적 대상 앱 + 비유휴 → `DailyRecord.AddTime()` 호출
5. UI 업데이트 이벤트 발생

### 데이터 저장 흐름
1. 30초마다 자동 저장 (`TimeRecorder.OnTimerTick`)
2. 앱 종료 시 동기 저장 (`TimeRecorder.Stop`)
3. JSON 직렬화 → 파일 쓰기 (`DataStorage`)

## UI 테마

- **색상 스키마**: 다크 테마 (Slate 계열)
  - Background: `#0f172a`, `#1e293b`
  - Text: `#f1f5f9`, `#94a3b8`
  - Accent: Purple `#8b5cf6`, Indigo `#6366f1`, Green `#10b981`, Red `#ef4444`

- **스타일**: 모던 카드 기반 UI
  - 둥근 모서리 (CornerRadius 16px)
  - 호버 효과
  - Consolas 폰트 (시간 표시)

## 코딩 컨벤션

- **언어**: C# 12, 파일 범위 namespace 사용
- **Nullable**: 활성화 (`<Nullable>enable</Nullable>`)
- **비동기**: async/await 패턴
- **MVVM**: CommunityToolkit.Mvvm 소스 생성기 활용
- **한글 주석**: 한글로 주석 및 UI 텍스트 작성

## 알려진 제한사항

- Windows 전용 (Win32 API 의존)
- 관리자 권한 프로세스는 추적 불가
- 다중 모니터 환경에서 테스트 필요

## 기여 시 주의사항

1. MVVM 패턴 준수 - View에 비즈니스 로직 넣지 않기
2. Win32 API 호출 시 예외 처리 필수
3. DispatcherTimer 사용 - UI 스레드에서 안전하게 업데이트
4. 데이터 변경 후 항상 저장 호출 확인

