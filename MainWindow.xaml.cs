using System.Windows;
using System.Windows.Input;
using HocusFocus.ViewModels;

namespace HocusFocus;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isExiting;
    
    // 메인 모드 크기 저장
    private double _mainWidth = 1000;
    private double _mainHeight = 800;
    private double _mainLeft;
    private double _mainTop;
    private WindowState _mainWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        
        // 트레이 아이콘 설정
        UpdateTrayIcon();
        
        // 초기 위치 저장
        _mainLeft = Left;
        _mainTop = Top;
    }

    private void UpdateTrayIcon()
    {
        // 기본 아이콘 사용 (앱 아이콘)
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath != null)
            {
                TrayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            }
        }
        catch
        {
            // 아이콘 로드 실패 시 무시
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && !_viewModel.IsMiniMode)
        {
            Hide();
            TrayIcon.ShowBalloonTip("HocusFocus", "백그라운드에서 실행 중입니다.", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExiting && _viewModel.MinimizeToTrayOnClose)
        {
            // 트레이로 최소화 옵션이 켜져 있으면 숨기기
            e.Cancel = true;
            Hide();
            TrayIcon.ShowBalloonTip("HocusFocus", "백그라운드에서 실행 중입니다.", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
        else
        {
            // 완전 종료
            _viewModel.Shutdown();
            TrayIcon.Dispose();
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 미니 모드에서만 드래그 가능
        if (_viewModel.IsMiniMode && e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeWindow_Click(sender, e);
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TrayOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowMainMode();
    }

    private void TrayToggle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleTrackingCommand.Execute(null);
    }

    private void TrayMiniMode_Click(object sender, RoutedEventArgs e)
    {
        ShowMiniMode();
        Show();
        Activate();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        Close();
    }

    private void ToggleMiniMode_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsMiniMode)
        {
            ShowMainMode();
        }
        else
        {
            ShowMiniMode();
        }
    }

    private void ShowMiniMode()
    {
        if (_viewModel.IsMiniMode) return;
        
        // 현재 메인 모드 크기/위치 저장
        _mainWidth = Width;
        _mainHeight = Height;
        _mainLeft = Left;
        _mainTop = Top;
        _mainWindowState = WindowState;
        
        // 미니 모드로 전환
        _viewModel.IsMiniMode = true;
        
        WindowState = WindowState.Normal;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        
        // 먼저 MinWidth/MinHeight 설정
        MinWidth = 0;
        MinHeight = 0;
        
        // SizeToContent로 콘텐츠에 맞게 크기 조정
        SizeToContent = SizeToContent.WidthAndHeight;
        
        // 화면 오른쪽 하단에 배치
        var workArea = SystemParameters.WorkArea;
        
        // SizeToContent가 적용된 후 위치 설정
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Left = workArea.Right - ActualWidth - 20;
            Top = workArea.Bottom - ActualHeight - 20;
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ShowMainMode()
    {
        if (!_viewModel.IsMiniMode)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            return;
        }
        
        // 메인 모드로 전환
        _viewModel.IsMiniMode = false;
        
        // SizeToContent 해제
        SizeToContent = SizeToContent.Manual;
        
        // AllowsTransparency가 True이므로 WindowStyle은 None으로 유지
        ResizeMode = ResizeMode.CanResize;
        Topmost = false;
        
        MinWidth = 800;
        MinHeight = 750;
        Width = _mainWidth;
        Height = _mainHeight;
        Left = _mainLeft;
        Top = _mainTop;
        WindowState = _mainWindowState;
        
        Show();
        Activate();
    }

    private void OpenAddAppDialog_Click(object sender, RoutedEventArgs e)
    {
        // 현재 추적 중인 앱의 프로세스 이름 목록
        var trackedProcessNames = _viewModel.TrackedApps.Select(a => a.ProcessName);
        
        var dialog = new AddAppDialog(trackedProcessNames)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedApp != null)
        {
            _viewModel.AddTrackedAppCommand.Execute(dialog.SelectedApp);
        }
    }
}
