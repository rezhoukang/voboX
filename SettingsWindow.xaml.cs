using System.IO;
using System.Windows;
using System.Windows.Controls;
using voboX.Services;

namespace voboX;

/// <summary>设置窗口：录制来源 / 排序规则 / 开机自启（Box 路径全部固定不可改）</summary>
public partial class SettingsWindow : Window
{
    public record SortOption(string Key, string Label);

    private readonly SettingsService _settings;
    private readonly AudioRepository _repo;

    private static readonly SortOption[] SortOptions =
    {
        new("none", "无排序（原始顺序）"),
        new("time", "加入时间（新→旧）"),
        new("timeAsc", "加入时间（旧→新）"),
        new("name", "文件名（A→Z）"),
        new("nameDesc", "文件名（Z→A）"),
        new("duration", "时长（长→短）"),
        new("durationAsc", "时长（短→长）"),
    };

    public SettingsWindow(SettingsService settings, AudioRepository repo)
    {
        InitializeComponent();
        _settings = settings;
        _repo = repo;

        // 录制设备
        var devices = AudioRecorderService.GetDeviceNames();
        DeviceCombo.ItemsSource = devices;
        DeviceCombo.IsEnabled = devices.Length > 0;
        if (devices.Length > 0)
            DeviceCombo.SelectedIndex = Math.Clamp(_settings.Settings.RecordDeviceIndex, 0, devices.Length - 1);

        // 排序规则
        SortCombo.ItemsSource = SortOptions;
        var idx = Array.FindIndex(SortOptions, s => s.Key == _settings.Settings.SortRule);
        SortCombo.SelectedIndex = Math.Max(0, idx);

        // Box 下路径全部固定（voboX / recordBox / cutBox / tempBox 不可修改）
        AutoStartCheck.IsChecked = _settings.Settings.AutoStart;
        SaveBoxPathText.Text = _settings.ResolveVoboxDir();
    }

    // ================= 拷贝真实文件到 voboX =================

    private void CopyToVobox_Click(object sender, RoutedEventArgs e)
    {
        int n = FolderService.CopyIndexedToVobox();
        MessageBox.Show(this,
            $"已拷贝 {n} 个文件到：\n{AppPaths.DefaultSaveBoxPath}\n\n" +
            $"（原 log 索引已移除，文件已真实拷入）",
            "voboX");
    }

    private void SaveBoxOpen_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(AppPaths.DefaultSaveBoxPath);

    /// <summary>打开 Box 根目录（voboX / recordBox / cutBox / tempBox 都在里面）</summary>
    private void OpenBoxFolder_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(FolderService.BoxRoot);

    /// <summary>清空 tempBox：删除其下所有临时副本文件与子目录（含确认）</summary>
    private void ClearTempbox_Click(object sender, RoutedEventArgs e)
    {
        var dir = AppPaths.DefaultTempboxPath;
        if (!Directory.Exists(dir))
        {
            MessageBox.Show(this, "tempBox 目录不存在，无需清理。", "voboX");
            return;
        }
        var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).ToList();
        if (files.Count == 0)
        {
            MessageBox.Show(this, "tempBox 已经是空的。", "voboX");
            return;
        }
        if (MessageBox.Show(this,
                $"确定清空 tempBox 吗？\n将删除 {files.Count} 个临时副本文件。\n\n外部软件中正在使用的副本可能失效。",
                "voboX", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        int deleted = 0;
        foreach (var f in files)
        {
            try { File.Delete(f); deleted++; } catch { }
        }
        // 删除空子目录（从最深开始）
        foreach (var d in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)
                     .OrderByDescending(x => x.Length))
        {
            try { Directory.Delete(d, recursive: false); } catch { }
        }
        MessageBox.Show(this, $"已清空 tempBox（删除 {deleted} 个文件）。", "voboX");
    }

    // ================= 打开文件夹（在文件管理器中直接打开，目录不存在则先创建） =================

    private static void OpenInExplorer(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start("explorer.exe", path);
        }
        catch
        {
            // 打开失败忽略
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Settings.RecordDeviceIndex = Math.Max(0, DeviceCombo.SelectedIndex);
        if (SortCombo.SelectedItem is SortOption opt)
            _settings.Settings.SortRule = opt.Key;
        _settings.Settings.AutoStart = AutoStartCheck.IsChecked == true;
        _settings.Save();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
