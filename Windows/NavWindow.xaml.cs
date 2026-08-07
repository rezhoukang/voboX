using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

    public NavWindow()
    {
        InitializeComponent();
    }

    /// <summary>重新加载树，默认选中「未分类」</summary>
    public void LoadFolderTree()
    {
        FolderTree.Items.Clear();
        foreach (var node in FolderService.GetFolderTree())
            FolderTree.Items.Add(BuildTreeItem(node));
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
        var item = new TreeViewItem { Header = header, Tag = node.Path };
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
            FolderSelected?.Invoke(Path.GetRelativePath(FolderService.Root, path));
    }

    // ================= 右键：新建子 / 同级 / 删除 =================

    private void FolderTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var selPath = (FolderTree.SelectedItem as TreeViewItem)?.Tag as string;
        var menu = new ContextMenu();

        var addChild = new MenuItem { Header = "新建子文件夹…" };
        addChild.Click += (s, _) => CreateFolderUnder(selPath ?? FolderService.Root);
        menu.Items.Add(addChild);

        var addSibling = new MenuItem { Header = "新建同级文件夹…" };
        addSibling.Click += (s, _) =>
            CreateFolderUnder(selPath is null ? FolderService.Root
                : Path.GetDirectoryName(selPath) ?? FolderService.Root);
        menu.Items.Add(addSibling);

        var selName = selPath is null ? "" : Path.GetFileName(selPath.TrimEnd(Path.DirectorySeparatorChar));
        if (selPath is not null && selName != FolderService.Uncategorized)
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
                    if (FolderTree.SelectedItem is null)
                        SelectFolder(FolderService.Uncategorized);
                    FolderChanged?.Invoke();
                }
                catch { }
            };
            menu.Items.Add(del);
        }

        menu.IsOpen = true;
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
                FolderChanged?.Invoke();
            }
            catch { }
        }
    }
}
