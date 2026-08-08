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

    /// <summary>裁剪保存目录：固定在 Box\cutBox，不可修改</summary>
    public string ResolveCropDir() => AppPaths.DefaultCutboxPath;

    /// <summary>Tempbox 目录：固定在 Box\tempBox，不可修改</summary>
    public string ResolveTempboxDir() => AppPaths.DefaultTempboxPath;

    /// <summary>voboX 根目录：固定在 Box\voboX，不可修改</summary>
    public string ResolveVoboxDir() => AppPaths.DefaultSaveBoxPath;

    /// <summary>录音目录：固定在 Box\recordBox，不可修改</summary>
    public string ResolveRecordboxDir() => AppPaths.RecordingsDir;
}
