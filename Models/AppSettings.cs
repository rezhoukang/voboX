namespace voboX.Models;

/// <summary>应用设置（持久化到 JSON）</summary>
public class AppSettings
{
    /// <summary>录制设备索引</summary>
    public int RecordDeviceIndex { get; set; }

    /// <summary>文件排序规则：time | name | size | duration</summary>
    public string SortRule { get; set; } = "time";

    /// <summary>开机自启动</summary>
    public bool AutoStart { get; set; }

    /// <summary>裁剪保存目录（空 = 默认 Box\Cutbox）</summary>
    public string CropSavePath { get; set; } = "";

    /// <summary>Tempbox 目录（空 = 默认）</summary>
    public string TempboxPath { get; set; } = "";

    /// <summary>窗口始终置顶（钉子按钮，持久化）</summary>
    public bool AlwaysOnTop { get; set; }
}
