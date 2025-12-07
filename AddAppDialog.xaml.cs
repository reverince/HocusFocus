using System.Windows;
using System.Windows.Controls;
using HocusFocus.Services;
using HocusFocus.ViewModels;

namespace HocusFocus;

/// <summary>
/// 앱 추가 팝업 다이얼로그
/// </summary>
public partial class AddAppDialog : Window
{
    private readonly WindowTracker _windowTracker;
    private List<RunningAppViewModel> _allApps = new();
    private readonly HashSet<string> _trackedProcessNames;

    /// <summary>
    /// 선택된 앱 정보
    /// </summary>
    public RunningAppViewModel? SelectedApp { get; private set; }

    public AddAppDialog(IEnumerable<string> trackedProcessNames)
    {
        InitializeComponent();
        
        _windowTracker = new WindowTracker();
        _trackedProcessNames = new HashSet<string>(trackedProcessNames, StringComparer.OrdinalIgnoreCase);
        
        Loaded += AddAppDialog_Loaded;
    }

    private void AddAppDialog_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshApps();
        SearchTextBox.Focus();
    }

    private void RefreshApps()
    {
        var processes = _windowTracker.GetRunningProcesses();
        _allApps = processes
            .Select(p => new RunningAppViewModel
            {
                ProcessName = p.ProcessName,
                Title = p.Title,
                IsTracked = _trackedProcessNames.Contains(p.ProcessName)
            })
            .OrderBy(a => a.IsTracked)  // 추가되지 않은 앱이 먼저
            .ThenBy(a => a.ProcessName)
            .ToList();

        FilterApps();
    }

    private void FilterApps()
    {
        var searchText = SearchTextBox.Text?.Trim().ToLower() ?? "";
        
        var filtered = string.IsNullOrEmpty(searchText)
            ? _allApps
            : _allApps.Where(a => 
                a.ProcessName.ToLower().Contains(searchText) || 
                a.Title.ToLower().Contains(searchText)).ToList();

        AppListBox.ItemsSource = filtered;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterApps();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshApps();
    }

    private void AppListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = AppListBox.SelectedItem as RunningAppViewModel;
        AddButton.IsEnabled = selected != null && !selected.IsTracked;
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = AppListBox.SelectedItem as RunningAppViewModel;
        if (selected != null && !selected.IsTracked)
        {
            SelectedApp = selected;
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

