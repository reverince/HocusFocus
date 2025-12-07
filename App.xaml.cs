namespace HocusFocus;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 단일 인스턴스 확인
        var mutex = new System.Threading.Mutex(true, "HocusFocus_SingleInstance", out bool createdNew);
        
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("HocusFocus가 이미 실행 중입니다.", "HocusFocus", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }
        
        // mutex를 앱 종료까지 유지
        GC.KeepAlive(mutex);
    }
}
