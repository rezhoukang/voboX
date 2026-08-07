using System.IO;
using System.Text.Json;
using voboX.Models;

namespace voboX.Services;

/// <summary>设置持久化（JSON）</summary>
public class SettingsService
{
    private readonly string _path;
    public AppSettings Settings { get; private set; } = new();

    public SettingsService(string path)
    {
        _path = path;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // 忽略写入失败
        }
    }

    /// <summary>获取实际生效的裁剪保存目录（空 = 默认 Box\Cutbox）</summary>
    public string ResolveCropDir()
    {
        if (!string.IsNullOrWhiteSpace(Settings.CropSavePath)) return Settings.CropSavePath;
        return AppPaths.DefaultCutboxPath;
    }

    /// <summary>获取实际生效的 Tempbox 目录</summary>
    public string ResolveTempboxDir()
    {
        if (!string.IsNullOrWhiteSpace(Settings.TempboxPath)) return Settings.TempboxPath;
        return AppPaths.DefaultTempboxPath;
    }
}
