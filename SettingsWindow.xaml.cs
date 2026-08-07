using System.IO;
using System.Windows;
using System.Windows.Controls;
using voboX.Services;

namespace voboX;

/// <summary>设置窗口：录制来源 / 排序规则 / 裁剪保存路径 / tempBox / 开机自启</summary>
public partial class SettingsWindow : Window
{
    public record SortOption(string Key, string Label);

    private readonly SettingsService _settings;
    private readonly AudioRepository _repo;

    private static readonly SortOption[] SortOptions =
    {
        new("time", "加入时间（新→旧）"),
        new("name", "文件名（A→Z）"),
        new("duration", "时长（长→短）"),
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

        // 裁剪默认路径就是 Box\cutBox：留空时也把默认路径显示在输入框里
        // 路径框留空 = 跟随默认（相对应用位置，换电脑可移植）；悬停显示默认路径
        CropPathBox.Text = _settings.Settings.CropSavePath;
        CropPathBox.ToolTip = "默认：" + AppPaths.DefaultCutboxPath;
        TempboxBox.Text = _settings.Settings.TempboxPath;
        TempboxBox.ToolTip = "默认：" + AppPaths.DefaultTempboxPath;
        VoboxBox.Text = _settings.Settings.VoboxPath;
        VoboxBox.ToolTip = "默认：" + AppPaths.DefaultSaveBoxPath;
        RecordboxBox.Text = _settings.Settings.RecordboxPath;
        RecordboxBox.ToolTip = "默认：" + AppPaths.RecordingsDir;
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

    private void CropBrowse_Click(object sender, RoutedEventArgs e) => BrowseFolder(CropPathBox);

    private void TempboxBrowse_Click(object sender, RoutedEventArgs e) => BrowseFolder(TempboxBox);

    private void VoboxBrowse_Click(object sender, RoutedEventArgs e) => BrowseFolder(VoboxBox);

    private void RecordboxBrowse_Click(object sender, RoutedEventArgs e) => BrowseFolder(RecordboxBox);

    private void BrowseFolder(TextBox box)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择文件夹" };
        if (dlg.ShowDialog(this) == true)
            box.Text = dlg.FolderName;
    }

    // ================= 打开文件夹（在文件管理器中直接打开，目录不存在则先创建） =================

    private void CropOpen_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(string.IsNullOrWhiteSpace(CropPathBox.Text)
            ? AppPaths.DefaultCutboxPath : CropPathBox.Text.Trim());

    private void TempboxOpen_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(string.IsNullOrWhiteSpace(TempboxBox.Text)
            ? AppPaths.DefaultTempboxPath : TempboxBox.Text.Trim());

    private void VoboxOpen_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(string.IsNullOrWhiteSpace(VoboxBox.Text)
            ? _settings.ResolveVoboxDir() : VoboxBox.Text.Trim());

    private void RecordboxOpen_Click(object sender, RoutedEventArgs e) =>
        OpenInExplorer(string.IsNullOrWhiteSpace(RecordboxBox.Text)
            ? _settings.ResolveRecordboxDir() : RecordboxBox.Text.Trim());

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
        _settings.Settings.CropSavePath = CropPathBox.Text.Trim();
        _settings.Settings.TempboxPath = TempboxBox.Text.Trim();
        _settings.Settings.VoboxPath = VoboxBox.Text.Trim();
        _settings.Settings.RecordboxPath = RecordboxBox.Text.Trim();
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
