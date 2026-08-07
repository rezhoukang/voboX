namespace voboX.Models;

/// <summary>应用设置（持久化到 JSON）</summary>
public class AppSettings
{
    /// <summary>录制设备索引</summary>
    public int RecordDeviceIndex { get; set; }

    /// <summary>文件排序规则：none | time | timeAsc | name | nameDesc | duration | durationAsc</summary>
    public string SortRule { get; set; } = "time";

    /// <summary>开机自启动</summary>
    public bool AutoStart { get; set; }

    /// <summary>裁剪保存目录（空 = 默认 Box\cutBox）</summary>
    public string CropSavePath { get; set; } = "";

    /// <summary>tempBox 目录（空 = 默认）</summary>
    public string TempboxPath { get; set; } = "";

    /// <summary>voboX 根目录（空 = 默认 Box\voboX）</summary>
    public string VoboxPath { get; set; } = "";

    /// <summary>录音目录（空 = 默认 Box\recordBox）</summary>
    public string RecordboxPath { get; set; } = "";

    /// <summary>窗口始终置顶（钉子按钮，持久化）</summary>
    public bool AlwaysOnTop { get; set; }
}
