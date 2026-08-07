using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using voboX.Services;

namespace voboX.Windows;

/// <summary>
/// 左侧导航悬浮窗：voboX 文件夹树。
/// 作为独立窗口铺在主窗口左侧，随主窗口移动/缩放（由 MainWindow 定位）。
/// 所有层级统一样式；「未分类」置顶、不可删除；同目录不允许重名。
/// </summary>
public partial class NavWindow : Window
{
    /// <summary>选中了某文件夹（参数为相对 voboX 根的路径）</summary>
    public event Action<string>? FolderSelected;

    /// <summary>树结构变化（新建 / 删除文件夹）</summary>
    public event Action? FolderChanged;

    /// <summary>文件/文件夹拖放到某节点（参数：目标文件夹完整路径, 拖入路径）</summary>
    public event Action<string, string[]>? FolderDropped;

    public NavWindow()
    {
        InitializeComponent();
        // 支持把文件/文件夹拖到树中指定节点导入
        FolderTree.AllowDrop = true;
        FolderTree.DragOver += FolderTree_DragOver;
        FolderTree.Drop += FolderTree_Drop;
        // 滚动条：滚动时显示，停止后自动隐藏
        _barTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _barTimer.Tick += (s, e) =>
        {
            _barTimer.Stop();
            if (_vBar is not null) _vBar.Opacity = 0;
        };
        Loaded += (s, e) => FindTreeScrollParts();
    }

    private ScrollViewer? _treeScroller;
    private ScrollBar? _vBar;
    private readonly DispatcherTimer _barTimer;

    /// <summary>找到 TreeView 内部 ScrollViewer，滚动时短暂显示滚动条</summary>
    private void FindTreeScrollParts()
    {
        _treeScroller = FindDescendant<ScrollViewer>(FolderTree);
        if (_treeScroller is null) return;
        _treeScroller.ScrollChanged += (s, e) =>
        {
            _vBar ??= FindDescendant<ScrollBar>(_treeScroller);
            if (_vBar is null) return;
            _vBar.Opacity = 1;
            _barTimer.Stop();
            _barTimer.Start();
        };
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            var r = FindDescendant<T>(child);
            if (r is not null) return r;
        }
        return null;
    }

    /// <summary>常驻「recordBox」对应的录音目录（可配置）</summary>
    public string RecordDir { get; set; } = AppPaths.RecordingsDir;

    /// <summary>常驻「cutBox」对应的裁剪保存目录</summary>
    public string CutDir { get; set; } = AppPaths.DefaultCutboxPath;

    /// <summary>重新加载树：recordBox 最顶，voboX 其次（未分类等都在 voboX 底下）</summary>
    public void LoadFolderTree()
    {
        FolderService.EnsureUncategorized(); // 未分类被删后自动恢复
        FolderTree.Items.Clear();

        // 最顶：recordBox（录音目录；右键不允许，什么都不允许）
        var recordHeader = new TextBlock
        {
            Text = "recordBox",
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "录音",
        };
        FolderTree.Items.Add(new TreeViewItem
        {
            Header = recordHeader,
            Tag = RecordDir,
            Style = (Style)FindResource("NavTreeItemStyle"),
        });

        // cutBox（裁剪保存；与 recordBox 一样什么都不允许）
        var cutHeader = new TextBlock
        {
            Text = "cutBox",
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "裁剪保存",
        };
        FolderTree.Items.Add(new TreeViewItem
        {
            Header = cutHeader,
            Tag = CutDir,
            Style = (Style)FindResource("NavTreeItemStyle"),
        });

        // 第二：voboX 根；未分类等所有文件夹都是它的子项
        var rootHeader = new TextBlock
        {
            Text = "voboX",
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "根目录",
        };
        var voboxItem = new TreeViewItem
        {
            Header = rootHeader,
            Tag = FolderService.Root,
            IsExpanded = true,
            Style = (Style)FindResource("NavTreeItemStyle"),
        };
        foreach (var node in FolderService.GetFolderTree())
            voboxItem.Items.Add(BuildTreeItem(node));
        FolderTree.Items.Add(voboxItem);

        SelectFolder(FolderService.Uncategorized);
    }

    /// <summary>按文件夹名（相对根）选中树节点</summary>
    public void SelectFolder(string folderName)
    {
        var target = Path.Combine(FolderService.Root, folderName);
        foreach (var obj in FolderTree.Items)
            if (obj is TreeViewItem item && TrySelectRecursive(item, target))
                return;
    }

    private TreeViewItem BuildTreeItem(FolderService.FolderNode node)
    {
        var header = new TextBlock
        {
            Text = node.Name,
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 120,
            ToolTip = node.Name,
        };
        var item = new TreeViewItem
        {
            Header = header,
            Tag = node.Path,
            IsExpanded = true, // 默认展开，新建后不会“缩起来”
            Style = (Style)FindResource("NavTreeItemStyle"), // 手动指定样式，保证子文件夹也有悬浮/选中效果
        };
        foreach (var child in node.Children)
            item.Items.Add(BuildTreeItem(child));
        return item;
    }

    private static bool TrySelectRecursive(TreeViewItem item, string target)
    {
        if ((item.Tag as string)?.Equals(target, StringComparison.OrdinalIgnoreCase) == true)
        {
            item.IsSelected = true;
            return true;
        }
        foreach (var obj in item.Items)
            if (obj is TreeViewItem child && TrySelectRecursive(child, target))
                return true;
        return false;
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (FolderTree.SelectedItem is TreeViewItem item && item.Tag is string path)
            FolderSelected?.Invoke(path); // 完整路径（Record=录音目录 / voboX 内文件夹）
    }

    // ================= 右键：新建子 / 同级 / 删除 =================

    private void FolderTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // 右键针对鼠标悬停的文件夹，而非当前选中项；空白处不弹菜单
        var hitItem = GetTreeItemAtMouse();
        if (hitItem is null)
        {
            e.Handled = true;
            return;
        }
        var selPath = hitItem.Tag as string;

        // recordBox / cutBox 什么都不允许：右键也不弹菜单
        if (selPath is not null &&
            (selPath.Equals(RecordDir, StringComparison.OrdinalIgnoreCase) ||
             selPath.Equals(CutDir, StringComparison.OrdinalIgnoreCase)))
        {
            e.Handled = true;
            return;
        }

        var isRoot = selPath is not null && selPath.Equals(FolderService.Root, StringComparison.OrdinalIgnoreCase);
        var menu = new ContextMenu();

        if (isRoot)
        {
            // voboX 只允许新建子文件夹（未分类等都在它底下）
            var addChild = new MenuItem { Header = "新建子文件夹" };
            addChild.Click += (s, _) => CreateFolderUnder(FolderService.Root);
            menu.Items.Add(addChild);
        }
        else
        {
            var addChild = new MenuItem { Header = "新建子文件夹" };
            addChild.Click += (s, _) => CreateFolderUnder(selPath ?? FolderService.Root);
            menu.Items.Add(addChild);

            var addSibling = new MenuItem { Header = "新建同级文件夹" };
            addSibling.Click += (s, _) =>
                CreateFolderUnder(selPath is null ? FolderService.Root
                    : Path.GetDirectoryName(selPath) ?? FolderService.Root);
            menu.Items.Add(addSibling);

            var selName = Path.GetFileName(selPath!.TrimEnd(Path.DirectorySeparatorChar));
            if (selName != FolderService.Uncategorized)
            {
                var del = new MenuItem { Header = "删除文件夹" };
                del.Click += (s, _) =>
                {
                    if (MessageBox.Show(this, $"确定删除文件夹：{selName} 吗？\n（连同其中已拷贝的文件）", "voboX",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                    try
                    {
                        Directory.Delete(selPath, true);
                        LoadFolderTree();
                        FolderChanged?.Invoke();
                    }
                    catch { }
                };
                menu.Items.Add(del);
            }
        }

        menu.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>按鼠标位置命中对应的树节点（右键针对的是鼠标下的文件夹）</summary>
    private TreeViewItem? GetTreeItemAtMouse()
        => GetTreeItemAtPoint(Mouse.GetPosition(FolderTree));

    /// <summary>按坐标命中树节点</summary>
    private TreeViewItem? GetTreeItemAtPoint(Point pos)
    {
        var hit = VisualTreeHelper.HitTest(FolderTree, pos);
        var dep = hit?.VisualHit;
        while (dep is not null and not TreeViewItem)
            dep = VisualTreeHelper.GetParent(dep);
        return dep as TreeViewItem;
    }

    // ================= 拖放导入（拖到指定文件夹） =================

    private void FolderTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FolderTree_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            // 拖放目标是鼠标下的文件夹；空白处 = voboX 根
            var item = GetTreeItemAtPoint(e.GetPosition(FolderTree));
            var target = (item?.Tag as string) ?? FolderService.Root;
            FolderDropped?.Invoke(target, files);
        }
        e.Handled = true;
    }

    private void CreateFolderUnder(string parentDir)
    {
        var dlg = new InputDialog("新建文件夹", "文件夹名称：", "") { Owner = this };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Value))
        {
            var name = dlg.Value.Trim();
            var dir = Path.Combine(parentDir, name);
            try
            {
                if (Directory.Exists(dir))
                {
                    MessageBox.Show(this, "此文件夹已存在。", "voboX");
                    return;
                }
                Directory.CreateDirectory(dir);
                FolderService.EnsureFolderFiles(dir); // 每个新文件夹自带 log / inLog 文件
                LoadFolderTree();
                SelectFolder(Path.GetRelativePath(FolderService.Root, dir)); // 选中新建的文件夹
                FolderChanged?.Invoke();
            }
            catch { }
        }
    }
}
