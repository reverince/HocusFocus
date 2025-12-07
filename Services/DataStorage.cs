using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HocusFocus.Models;

namespace HocusFocus.Services;

/// <summary>
/// JSON 파일로 데이터를 저장/로드하는 서비스
/// </summary>
public class DataStorage
{
    private readonly string _dataPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public DataStorage()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var hocusFocusPath = Path.Combine(appDataPath, "HocusFocus");
        
        if (!Directory.Exists(hocusFocusPath))
            Directory.CreateDirectory(hocusFocusPath);

        _dataPath = Path.Combine(hocusFocusPath, "data.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// 데이터 파일 경로
    /// </summary>
    public string DataPath => _dataPath;

    /// <summary>
    /// 데이터 저장
    /// </summary>
    public async Task SaveAsync(AppData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(_dataPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 데이터 로드
    /// </summary>
    public async Task<AppData> LoadAsync()
    {
        try
        {
            if (!File.Exists(_dataPath))
                return new AppData();

            var json = await File.ReadAllTextAsync(_dataPath);
            return JsonSerializer.Deserialize<AppData>(json, _jsonOptions) ?? new AppData();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"로드 실패: {ex.Message}");
            return new AppData();
        }
    }

    /// <summary>
    /// 동기 저장 (앱 종료 시 사용)
    /// </summary>
    public void Save(AppData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_dataPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"저장 실패: {ex.Message}");
        }
    }
}

