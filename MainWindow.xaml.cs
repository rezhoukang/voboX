using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using voboX.Models;
using voboX.Services;
using voboX.Windows;

namespace voboX;

/// <summary>
/// 主窗口：暗夜模式 + 顶部菜单栏 + 搜索/分组/全选/文件列表
/// </summary>
public partial class MainWindow : Window
{
    private readonly AudioRepository _repo;
    private readonly SettingsService _settings;
    private readonly AudioPlayerService _player;
    private readonly AudioRecorderService _recorder;
    private readonly TempboxService _tempbox;

    private readonly List<AudioItem> _allItems = new();
    private AudioItem? _current;
    private AudioItem? _playingItem;
    private string _currentFolder = Path.Combine(FolderService.Root, FolderService.Uncategorized); // 当前文件夹（完整路径）
    private bool _navExpanded; // 左侧导航悬浮窗是否展开（默认收起）
    private NavWindow? _navWindow; // 左侧导航悬浮窗（铺在主窗口左侧，随主窗口移动）
    private NavButtonWindow? _navButton; // 展开/收起按钮悬浮窗（随状态换位换图标）
    private NavButtonWindow? _navRefresh; // 刷新按钮悬浮窗（导航栏底部）
    private Point _mouseDownPos;
    private ListBoxItem? _dragItem;
    private bool _suppressDragClick;   // 右键菜单弹出后，忽略下一次左键拖拽（防误复制）
    private readonly ContextMenu _fileMenu = new(); // 复用同一菜单实例，避免右键菜单闪烁

    public MainWindow()
    {
        InitializeComponent();
        _repo = new AudioRepository(AppPaths.DbPath);
        _settings = new SettingsService(Path.Combine(AppPaths.DataDir, "settings.json"));
        _player = new AudioPlayerService();
        _recorder = new AudioRecorderService();
        _tempbox = new TempboxService(() => _settings.ResolveTempboxDir());

        _player.PositionChanged += OnPositionChanged;
        _player.PlaybackFinished += OnPlaybackFinished;
        Waveform.SelectionChanged += OnSelectionChanged;

        FileList.ContextMenu = _fileMenu; // 复用同一个菜单实例，避免右键菜单闪烁
        Topmost = _settings.Settings.AlwaysOnTop;
        UpdatePinVisual();
        ApplyAutoStart(_settings.Settings.AutoStart);
        FolderService.Root = _settings.ResolveVoboxDir();
        FolderService.EnsureStructure();
        ReloadSamples();
        // Owner 要求窗口已显示，故按钮悬浮窗等窗口 Loaded 后再创建
        Loaded += (s, e) =>
        {
            EnsureNavButton();
            UpdateNavPositions();
        };
    }

    // ================= 左侧导航悬浮窗（导航窗 + 按钮悬浮窗，随主窗口移动） =================

    /// <summary>展开/收起导航：展开时显示导航悬浮窗，收起时隐藏；按钮悬浮窗自动换位换图标</summary>
    private void ToggleNav()
    {
        _navExpanded = !_navExpanded;
        if (_navExpanded)
        {
            EnsureNavWindow();
            _navWindow!.LoadFolderTree();
            _navWindow.Show();
        }
        else
        {
            _navWindow?.Hide();
        }
        UpdateNavPositions();
    }

    /// <summary>创建按钮悬浮窗（收起态在主窗口左侧外；位置跟随主窗口）</summary>
    private void EnsureNavButton()
    {
        if (_navButton is not null) return;
        _navButton = new NavButtonWindow { Owner = this, ShowActivated = false };
        _navButton.Clicked += ToggleNav;
        _navButton.Show();

        // 刷新按钮：导航栏底部、左右居中；高宽与收起按钮反转（横长条）
        _navRefresh = new NavButtonWindow
        {
            Owner = this,
            ShowActivated = false,
            Width = 48,   // 收起按钮 22x48 → 反转 48x22
            Height = 22,
        };
        _navRefresh.Icon = "\uE72C"; // Refresh 图标
        _navRefresh.Clicked += RefreshNav;
        _navRefresh.Hide();

        // 主窗口移动 / 缩放时，导航窗与按钮窗一起贴紧
        LocationChanged += (s, e) => UpdateNavPositions();
        SizeChanged += (s, e) => UpdateNavPositions();
    }

    /// <summary>刷新：重新加载导航树 + 当前文件夹列表</summary>
    private void RefreshNav()
    {
        if (_navWindow is not null && _navExpanded)
            _navWindow.LoadFolderTree();
        ReloadSamples();
    }

    /// <summary>创建导航悬浮窗（矮高度：到搜索框高度）并挂钩事件</summary>
    private void EnsureNavWindow()
    {
        if (_navWindow is not null) return;
        _navWindow = new NavWindow { Owner = this, RecordDir = _settings.ResolveRecordboxDir() };
        _navWindow.FolderSelected += path =>
        {
            _currentFolder = path;
            ReloadSamples();
        };
        _navWindow.FolderChanged += () =>
        {
            // 若当前文件夹被删，回退到「未分类」并同步悬浮窗选中
            if (!Directory.Exists(_currentFolder))
            {
                _currentFolder = Path.Combine(FolderService.Root, FolderService.Uncategorized);
                _navWindow.SelectFolder(FolderService.Uncategorized);
            }
            ReloadSamples();
        };
    }

    /// <summary>
    /// 让导航窗铺在主窗口左侧、按钮窗贴在对应位置：
    /// 导航窗顶部对齐窗口内搜索框、底部与窗口齐平（高 = 搜索框到窗口底部）；
    /// 收起态按钮在主窗口左侧外（左箭头=展开）；展开态按钮移到导航栏左侧（右箭头=收起）。
    /// </summary>
    private const double NavTopOffset = 92; // 顶栏 42 + 内容区边距 12 + 搜索框高度

    private void UpdateNavPositions()
    {
        if (_navWindow is not null && _navExpanded)
        {
            _navWindow.Left = Left - _navWindow.Width;
            _navWindow.Top = Top + NavTopOffset;          // 顶部到搜索框高度
            _navWindow.Height = Height - NavTopOffset - 32; // 底部再上移，留白更多
        }
        if (_navButton is not null)
        {
            _navButton.Icon = _navExpanded ? "\uE76C" : "\uE76B";
            _navButton.Left = _navExpanded
                ? Left - 200 - _navButton.Width   // 导航栏左边（收起按钮）
                : Left - _navButton.Width;         // 主窗口左侧外（展开按钮）
            _navButton.Top = Top + (Height - _navButton.Height) / 2; // 垂直居中
        }
        // 刷新按钮：仅展开时显示，位于导航栏底部、左右居中
        if (_navRefresh is not null)
        {
            if (_navExpanded && _navWindow is not null)
            {
                var navBottom = Top + NavTopOffset + (Height - NavTopOffset - 32);
                var navLeft = Left - 200;
                _navRefresh.Left = navLeft + (200 - _navRefresh.Width) / 2; // 导航栏内左右居中
                _navRefresh.Top = navBottom + 8;                           // 导航栏底部下方
                _navRefresh.Show();
            }
            else
            {
                _navRefresh.Hide();
            }
        }
    }

    // ================= 列表加载 / 搜索 =================
    private void ReloadSamples()
    {
        // 未分类被删后自动恢复
        if (_currentFolder.Equals(Path.Combine(FolderService.Root, FolderService.Uncategorized),
                StringComparison.OrdinalIgnoreCase) && !Directory.Exists(_currentFolder))
            FolderService.EnsureUncategorized();

        var keyword = SearchBox.Text.Trim();
        var items = FolderService.GetFolderItems(_currentFolder);
        _allItems.Clear();
        _allItems.AddRange(items);

        var visible = string.IsNullOrEmpty(keyword)
            ? _allItems
            : _allItems.Where(a => a.FileName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        // 必须赋新实例：ItemsSource 引用相同会被 WPF 视为无变化，列表不会刷新
        FileList.ItemsSource = visible.ToList();
        FileCountText.Text = $"{Path.GetFileName(_currentFolder.TrimEnd(Path.DirectorySeparatorChar))}（{visible.Count}）";
        SelectedCountText.Text = "已选择 0 个文件";
        UpdateSelectAllState();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ReloadSamples();

    private void SelectAllCheck_Click(object sender, RoutedEventArgs e)
    {
        // 依据真实选择状态切换，不依赖三态复选框的循环（true→null 不是想要的）
        ToggleSelectAll();
    }

    /// <summary>Ctrl+A：全选状态下再次按则全不选，否则全选（切换）</summary>
    private void FileList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            ToggleSelectAll();
        }
    }

    /// <summary>全选 ↔ 全不选（依据当前真实选择状态）</summary>
    private void ToggleSelectAll()
    {
        var all = FileList.Items.Count > 0 && FileList.SelectedItems.Count == FileList.Items.Count;
        if (all) FileList.UnselectAll();
        else FileList.SelectAll();
    }

    // ================= 导入（按钮 / 拖入） =================

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "导入音频文件",
            Filter = "音频文件 (*.wav)|*.wav",
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) == true)
            ImportPaths(dlg.FileNames);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            ImportPaths(paths);
    }

    private void ImportPaths(IEnumerable<string> paths)
    {
        var tempboxDir = _settings.ResolveTempboxDir();
        var files = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                files.AddRange(Directory.EnumerateFiles(p, "*.*", SearchOption.AllDirectories)
                    .Where(IsAudioFile));
            }
            else if (File.Exists(p) && IsAudioFile(p))
            {
                files.Add(p);
            }
        }

        // 跳过 tempBox 内的文件：拖出后误拖回本窗口时，不再把临时副本重新导入列表
        files = files.Where(f => !IsInsideDirectory(f, tempboxDir)).ToList();

        int added = 0;
        foreach (var f in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                // 只生成 log 索引登记到当前文件夹，不复制物理文件
                FolderService.AddIndexLog(_currentFolder, f);
                added++;
            }
            catch
            {
                // 损坏或不支持的音频跳过
            }
        }
        ReloadSamples();
        FileCountText.Text = added > 0 ? $"已登记 {added} 个文件" : "未发现可导入的音频";
    }

    private static bool IsAudioFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".mp3" || ext == ".wav";
    }

    /// <summary>判断文件是否位于某目录（含子目录）内</summary>
    private static bool IsInsideDirectory(string filePath, string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return false;
        try
        {
            var fileDir = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? "";
            var root = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar);
            return fileDir.Equals(root, StringComparison.OrdinalIgnoreCase)
                || fileDir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // ================= 录制 =================

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder.IsRecording)
        {
            _recorder.Stop();
            ResetRecordButton();
            if (_recorder.OutputPath is not null && File.Exists(_recorder.OutputPath))
            {
                try
                {
                    // 录音登记为 log 索引（当前文件夹），不复制
                    FolderService.AddIndexLog(_currentFolder, _recorder.OutputPath);
                    ReloadSamples();
                }
                catch { }
            }
            MessageBox.Show(this, "录音已停止并加入仓库。", "voboX");
            return;
        }

        var path = Path.Combine(_settings.ResolveRecordboxDir(), $"{DateTime.Now:yyyyMMdd_HHmmss}.wav");
        try
        {
            _recorder.Start(path, _settings.Settings.RecordDeviceIndex);
            RecordButton.Content = "停止";
            RecordButton.Tag = "\uE71A";
            RecordButton.Foreground = (SolidColorBrush)FindResource("DangerBrush");
            MessageBox.Show(this,
                "开始录音… 再次点击「停止」保存。\n\n提示：录制“屏幕声音”请在「设置」中选择立体声混音设备。",
                "voboX");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法开始录音：\n" + ex.Message + "\n\n请到「设置」中检查录制设备。", "voboX");
        }
    }

    private void ResetRecordButton()
    {
        RecordButton.Content = "录制";
        RecordButton.Tag = "\uE720";
        RecordButton.Foreground = (SolidColorBrush)FindResource("TextPrimary");
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings, _repo) { Owner = this };
        win.ShowDialog();
        ApplyAutoStart(_settings.Settings.AutoStart);
        Topmost = _settings.Settings.AlwaysOnTop;
        UpdatePinVisual();
        // 应用可能变更的路径（voboX / recordBox）
        FolderService.Root = _settings.ResolveVoboxDir();
        FolderService.EnsureStructure();
        if (_navWindow is not null)
        {
            _navWindow.RecordDir = _settings.ResolveRecordboxDir();
            if (_navExpanded) _navWindow.LoadFolderTree();
        }
        if (!Directory.Exists(_currentFolder))
            _currentFolder = Path.Combine(FolderService.Root, FolderService.Uncategorized);
        ReloadSamples();
    }

    // ================= 窗口控制 =================

    /// <summary>
    /// 标题栏手动拖拽：点在按钮上不拖拽（交给按钮处理）；空白处拖动窗口；双击最大化/还原。
    /// </summary>
    private void TitleBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 按在可交互控件（按钮等）上 → 不拖拽，交给按钮正常处理点击
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
            return;

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        try { DragMove(); } catch { }
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T t) return t;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    /// <summary>置顶（钉子）：点击后窗口始终置于最上方，再次点击取消；状态持久化</summary>
    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        _settings.Settings.AlwaysOnTop = Topmost;
        _settings.Save();
        UpdatePinVisual();
    }

    private void UpdatePinVisual()
    {
        if (Topmost)
        {
            PinButton.Background = (SolidColorBrush)FindResource("AccentBrush");
            PinButton.Foreground = Brushes.White;
            PinButton.Tag = "\uE718";
            PinButton.ToolTip = "取消置顶";
        }
        else
        {
            PinButton.Background = Brushes.Transparent;
            PinButton.Foreground = (SolidColorBrush)FindResource("TextSecondary");
            PinButton.Tag = "\uE77A";
            PinButton.ToolTip = "置顶（始终在最上方）";
        }
    }

    /// <summary>置顶激活时悬停变亮色反馈</summary>
    private void PinButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (Topmost)
            PinButton.Background = (SolidColorBrush)FindResource("AccentLight");
    }

    private void PinButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (Topmost)
            PinButton.Background = (SolidColorBrush)FindResource("AccentBrush");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    // ================= 选中 → 底部波形 =================

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 同步列表项的复选框状态
        foreach (AudioItem it in e.AddedItems) it.IsSelected = true;
        foreach (AudioItem it in e.RemovedItems) it.IsSelected = false;

        // 全选框右侧：实时更新已选择数量
        SelectedCountText.Text = $"已选择 {FileList.SelectedItems.Count} 个文件";
        UpdateSelectAllState();

        _current = FileList.SelectedItem as AudioItem;
        if (_current is null)
        {
            BottomPlayer.Visibility = Visibility.Collapsed;
            return;
        }

        BottomPlayer.Visibility = Visibility.Visible;
        CurrentFileName.Text = _current.FileName;
        UpdateTimeText(0, _current.DurationMs / 1000.0);
        UpdatePlayIcon();
        LoadWaveform(_current);
    }

    private void LoadWaveform(AudioItem item)
    {
        Waveform.Peaks = null;
        Waveform.ClearSelection();
        Waveform.Playhead = 0; // 切文件时清零播放头，避免上一文件的进度/蓝色段残留
        var path = item.FilePath;
        Task.Run(() =>
        {
            try
            {
                var (peaks, duration) = WaveformExtractor.Extract(path, 600);
                Dispatcher.BeginInvoke(() =>
                {
                    if (_current?.FilePath == path)
                    {
                        Waveform.Peaks = peaks;
                        Waveform.Duration = Math.Max(duration, 0.001);
                        if (_player.CurrentPath == path)
                            OnPositionChanged(_player.Position);
                    }
                });
            }
            catch
            {
                // 波形提取失败则保持空
            }
        });
    }

    // ================= 播放 =================

    private void FileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 双击：无论当前状态，都从头播放
        if (FileList.SelectedItem is AudioItem item)
            PlayFromStart(item);
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e) => TogglePlayback();

    /// <summary>
    /// 底部播放按钮（空格键同款）：
    /// 播放中 → 暂停；同一文件暂停中 → 继续；真正播完 → 按策略重播；其他 → 按策略播放。
    /// 策略：有蓝色选区 → 只播选区；否则整段从头播。
    /// </summary>
    private void TogglePlayback()
    {
        if (_current is null) return;

        // 正在播放同一文件 → 暂停
        if (_player.IsPlaying && _player.CurrentPath == _current.FilePath)
        {
            _player.Toggle();
            UpdatePlayIcon();
            return;
        }

        // 同一文件：暂停中（含暂停在末尾）→ 继续；真正自然播完 → 按策略重播
        if (_player.CurrentPath == _current.FilePath)
        {
            if (_player.IsFinished)
                PlayCurrentByStrategy();
            else
            {
                _player.Resume();
                UpdatePlayIcon();
            }
            return;
        }

        // 其他文件 → 按策略播放
        PlayCurrentByStrategy();
    }

    /// <summary>底部播放键策略：有蓝色选区 → 只播选区；否则整段从头播</summary>
    private void PlayCurrentByStrategy()
    {
        if (_current is null) return;
        if (TryGetSelectionRange(out var s, out var e))
            PlayRange(_current, s, e);
        else
            PlayFromStart(_current);
    }

    /// <summary>只播放选区范围（start ~ end 秒）</summary>
    private void PlayRange(AudioItem item, double startSec, double endSec)
    {
        if (item is null || !File.Exists(item.FilePath))
        {
            if (item is not null)
                MessageBox.Show(this, "文件已丢失：\n" + item.FilePath, "voboX");
            return;
        }
        StopPlayingFlag();
        _player.Play(item.FilePath, startSec, endSec);
        _playingItem = item;
        item.IsPlaying = true;
        Waveform.Playhead = _player.PlaybackStart;
        UpdateTimeText(0, _player.Duration);
        UpdatePlayIcon();
        if (!ReferenceEquals(_current, item))
            FileList.SelectedItem = item;
    }

    /// <summary>读取波形上的蓝色选区（秒）；无有效选区返回 false</summary>
    private bool TryGetSelectionRange(out double start, out double end)
    {
        start = Waveform.SelectionStart;
        end = Waveform.SelectionEnd;
        return start >= 0 && end > start;
    }

    /// <summary>从头播放指定文件（双击 / 换文件时用）</summary>
    private void PlayFromStart(AudioItem item)
    {
        if (item is null) return;
        if (!File.Exists(item.FilePath))
        {
            MessageBox.Show(this, "文件已丢失：\n" + item.FilePath, "voboX");
            return;
        }
        StopPlayingFlag();
        _player.Play(item.FilePath); // Play 内部先 Stop，再从 0 开始
        _playingItem = item;
        item.IsPlaying = true;
        // 立即重置播放头与时间显示（不必等 10ms 定时器第一拍）
        Waveform.Playhead = 0;
        UpdateTimeText(0, item.DurationMs / 1000.0);
        UpdatePlayIcon();
        if (!ReferenceEquals(_current, item))
            FileList.SelectedItem = item;
    }

    /// <summary>空格键 = 播放/暂停（与底部播放按钮一致；搜索框里按空格不拦截）</summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || _current is null) return;
        if (Keyboard.FocusedElement is TextBox) return; // 输入框内空格是字符
        e.Handled = true;
        TogglePlayback();
    }

    private void StopPlayingFlag()
    {
        if (_playingItem is not null)
        {
            _playingItem.IsPlaying = false;
            _playingItem = null;
        }
    }

    private void UpdatePlayIcon()
    {
        bool active = _player.IsPlaying && _player.CurrentPath == _current?.FilePath;
        PlayButton.Tag = active ? "\uE769" : "\uE768";
    }

    private void OnPositionChanged(double seconds)
    {
        if (_player.CurrentPath == _current?.FilePath)
        {
            UpdateTimeText(seconds, _player.Duration);
            // 播放头用文件绝对坐标：选区播放时从选区左缘走到右缘
            Waveform.Playhead = _player.PlaybackStart + seconds;
        }
    }

    private void OnPlaybackFinished()
    {
        StopPlayingFlag();
        PlayButton.Tag = "\uE768";
        Waveform.Playhead = 0;
        if (_current is null) return;
        // 播放完：选区仍在 → 保持选区总长度；无选区 → 整段时长
        UpdateTimeText(0, TryGetSelectionRange(out var s, out var e) ? e - s : _current.DurationMs / 1000.0);
    }

    private void UpdateTimeText(double current, double total)
        => TimeText.Text = $"{FormatSec(current)} / {FormatSec(total)}";

    private static string FormatSec(double s)
    {
        s = Math.Max(0, s);
        var sec = (int)s;
        var ms = (int)Math.Round((s - sec) * 1000);
        if (ms >= 1000) { sec++; ms = 0; } // 四舍五入进位
        return $"{sec}:{ms:000}";
    }

    // ================= 裁剪 =================

    private void OnSelectionChanged(double start, double end)
    {
        bool hasSel = start >= 0 && end > start;
        CropButton.IsEnabled = hasSel;
        // 拖选时立即刷新时间：选区 → 0 / 选区时长；清除选区 → 0 / 整段时长
        if (_current is null) return;
        UpdateTimeText(0, hasSel ? end - start : _current.DurationMs / 1000.0);
    }

    private void CropButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        if (Waveform.SelectionStart < 0 || Waveform.SelectionEnd <= Waveform.SelectionStart)
        {
            MessageBox.Show(this, "请先在波形图上拖动选择要裁剪的范围。", "voboX");
            return;
        }

        var dir = _settings.ResolveCropDir();
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "裁剪保存",
            InitialDirectory = Directory.Exists(dir) ? dir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            FileName = Path.GetFileNameWithoutExtension(_current.FileName) + "_crop.wav",
            Filter = "WAV 音频 (*.wav)|*.wav",
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            AudioCropService.Crop(_current.FilePath, Waveform.SelectionStart, Waveform.SelectionEnd, dlg.FileName);
            // 输出在 voboX 内 → 物理文件直接可见；否则登记 log 索引到当前文件夹
            if (!FolderService.IsInside(dlg.FileName, FolderService.Root))
                FolderService.AddIndexLog(_currentFolder, dlg.FileName);
            ReloadSamples();
            MessageBox.Show(this, "裁剪完成：\n" + dlg.FileName, "voboX");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "裁剪失败：\n" + ex.Message, "voboX");
        }
    }

    // ================= 右键菜单（移除） =================

    private void FileList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var item = GetItemUnderMouse();
        if (item is null)
        {
            e.Handled = true;
            return;
        }
        if (!FileList.SelectedItems.Contains(item))
            FileList.SelectedItem = item;

        // 右键后：忽略下一次左键拖拽，避免「右键→左键」误触发复制到 tempBox
        _suppressDragClick = true;
        _dragItem = null;

        // 复用同一个 ContextMenu 实例重建菜单项（避免每次新建导致闪烁）
        _fileMenu.Items.Clear();

        var remove = new MenuItem { Header = "移除" };
        remove.Click += (s, _) =>
        {
            var sel = FileList.SelectedItems.Cast<AudioItem>().ToList();
            if (sel.Count == 0) return;
            if (MessageBox.Show(this,
                    $"确定移除 {sel.Count} 个条目吗？\n（voboX 内文件会被删除；外部索引只删 log，源文件保留）",
                    "voboX", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            foreach (var item in sel)
                FolderService.RemoveEntry(_currentFolder, item);
            ReloadSamples();
        };
        _fileMenu.Items.Add(remove);
    }

    private AudioItem? GetItemUnderMouse()
    {
        var pos = Mouse.GetPosition(FileList);
        var hit = VisualTreeHelper.HitTest(FileList, pos);
        var dep = hit?.VisualHit;
        while (dep is not null and not ListBoxItem)
            dep = VisualTreeHelper.GetParent(dep);
        return (dep as ListBoxItem)?.DataContext as AudioItem;
    }

    private ListBoxItem? GetListBoxItemUnderMouse()
    {
        var pos = Mouse.GetPosition(FileList);
        var hit = VisualTreeHelper.HitTest(FileList, pos);
        var dep = hit?.VisualHit;
        while (dep is not null and not ListBoxItem)
            dep = VisualTreeHelper.GetParent(dep);
        return dep as ListBoxItem;
    }

    // ================= 列表项：复选框 =================

    private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is AudioItem item)
        {
            if (cb.IsChecked == true && !FileList.SelectedItems.Contains(item))
                FileList.SelectedItems.Add(item);
            else if (cb.IsChecked != true && FileList.SelectedItems.Contains(item))
                FileList.SelectedItems.Remove(item);
        }
    }

    /// <summary>
    /// 更新左上角全选复选框状态（三态）：
    /// 全选 = true（勾）；部分选 = null（横杠）；未选 = false（空）。
    /// </summary>
    private void UpdateSelectAllState()
    {
        var total = FileList.Items.Count;
        var sel = FileList.SelectedItems.Count;
        SelectAllCheck.IsChecked = total > 0 && sel == total ? (bool?)true : sel == 0 ? false : null;
    }

    // ================= 拖出到外部（tempBox 副本） =================

    private void FileList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 若上一次是右键弹菜单，则这次左键只用于收起/点选，不记录拖拽起点
        if (_suppressDragClick)
        {
            _suppressDragClick = false;
            _dragItem = null;
            return;
        }
        _mouseDownPos = e.GetPosition(null);
        _dragItem = GetListBoxItemUnderMouse();
    }

    private void FileList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _mouseDownPos.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _mouseDownPos.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // 拖的项在选中集合内 → 拖出全部选中；否则只拖该项
        List<AudioItem> dragItems;
        if (FileList.SelectedItems.Contains(_dragItem.DataContext))
            dragItems = FileList.SelectedItems.Cast<AudioItem>().ToList();
        else
            dragItems = new List<AudioItem> { (AudioItem)_dragItem.DataContext };

        // 先复制到 tempBox，再以副本路径拖出，保证外部修改不影响源文件
        var copyPaths = new List<string>();
        foreach (var it in dragItems)
        {
            if (!File.Exists(it.FilePath)) continue;
            try { copyPaths.Add(_tempbox.CreateCopy(it.FilePath)); } catch { }
        }
        _dragItem = null;
        if (copyPaths.Count == 0) return;

        var data = new DataObject(DataFormats.FileDrop, copyPaths.ToArray());
        DragDrop.DoDragDrop(FileList, data, DragDropEffects.Copy);
    }

    // ================= 设置 / 自启 / 清理 =================

    /// <summary>开机自启：写 / 删 HKCU Run 键（仅 Windows）</summary>
    [SupportedOSPlatform("windows")]
    private void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enable)
                key?.SetValue("voboX", $"\"{Environment.ProcessPath}\"");
            else
                key?.DeleteValue("voboX", throwOnMissingValue: false);
        }
        catch
        {
            // 注册表写入失败时忽略
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _navWindow?.Close();
        _player.Dispose();
        _recorder.Dispose();
        _settings.Save();
        base.OnClosed(e);
    }
}
