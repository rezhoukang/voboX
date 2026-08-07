using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace voboX.Models;

/// <summary>
/// 音频条目（仓库索引中的一条记录，指向磁盘上的原始文件，不复制原文件）
/// </summary>
public class AudioItem : INotifyPropertyChanged
{
    private bool _isPlaying;
    private bool _isSelected;

    /// <summary>SQLite 主键</summary>
    public long Id { get; set; }

    /// <summary>原始文件完整路径（索引指向，非副本）</summary>
    public string FilePath { get; set; } = "";

    /// <summary>时长（毫秒）</summary>
    public long DurationMs { get; set; }

    /// <summary>标签（逗号分隔）</summary>
    public string Tags { get; set; } = "";

    /// <summary>所属分组名（逗号分隔，查询时填充）</summary>
    public string GroupNames { get; set; } = "";

    /// <summary>加入仓库时间</summary>
    public DateTime AddedAt { get; set; } = DateTime.Now;

    /// <summary>文件名（含扩展名）</summary>
    public string FileName => System.IO.Path.GetFileName(FilePath);

    /// <summary>时长文本：小于 1s 用毫秒（如 850ms），大于等于 1s 用秒（如 23s）</summary>
    public string DurationText => DurationMs switch
    {
        <= 0 => "--:--",
        < 1000 => $"{DurationMs}ms",
        _ => $"{DurationMs / 1000.0:0.#}s",
    };

    /// <summary>是否正在播放（驱动列表播放按钮状态）</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    /// <summary>是否被选中（驱动列表复选框）</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}