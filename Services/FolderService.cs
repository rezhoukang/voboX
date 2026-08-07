using System.IO;
using voboX.Models;

namespace voboX.Services;

/// <summary>
/// voboX 树状文件夹管理：
/// - Root = Box\voboX（全部按文件夹管理，不复制，只索引）
/// - 默认「未分类」文件夹（开屏进入）
/// 每个文件夹下带一个文件（不是文件夹）：
///     log   外部文件索引：每行一个源文件绝对路径（未拷贝）
/// </summary>
public static class FolderService
{
    public const string Uncategorized = "未分类";

    /// <summary>树根目录（Box\voboX，可经设置修改）</summary>
    public static string Root { get; set; } = AppPaths.DefaultSaveBoxPath;

    /// <summary>文件夹下的外部索引 log 文件</summary>
    public static string LogFile(string folderPath) => Path.Combine(folderPath, "log.txt");

    /// <summary>确保目录结构：根 / 未分类，并为每个子文件夹创建 log 文件</summary>
    public static void EnsureStructure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, Uncategorized));
        // 只为子文件夹创建 log 文件（根本身不创建）
        foreach (var d in Directory.GetDirectories(Root))
            EnsureFolderFiles(d);
    }

    /// <summary>为 dir 及其全部子文件夹补建 log 文件（不存在才建）</summary>
    public static void EnsureFolderFiles(string dir)
    {
        foreach (var d in Directory.GetDirectories(dir))
            EnsureFolderFiles(d);
        EnsureLogFile(dir);
    }

    private static void EnsureLogFile(string dir)
    {
        if (!File.Exists(LogFile(dir)))
            File.WriteAllText(LogFile(dir), "", new System.Text.UTF8Encoding(false));
    }

    /// <summary>确保「未分类」文件夹存在（含 log），被删后自动恢复</summary>
    public static void EnsureUncategorized()
    {
        var dir = Path.Combine(Root, Uncategorized);
        Directory.CreateDirectory(dir);
        EnsureLogFile(dir);
    }

    /// <summary>文件夹树节点</summary>
    public class FolderNode
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public List<FolderNode> Children { get; set; } = new();
    }

    /// <summary>递归构建文件夹树（log 是文件，不会进树）</summary>
    public static List<FolderNode> GetFolderTree()
    {
        var list = new List<FolderNode>();
        BuildTree(Root, list);
        return list;
    }

    private static void BuildTree(string dir, List<FolderNode> nodes)
    {
        // 「未分类」始终置顶，其余按名字排序
        var dirs = Directory.GetDirectories(dir)
            .OrderBy(d => Path.GetFileName(d) == Uncategorized ? 0 : 1)
            .ThenBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var d in dirs)
        {
            var node = new FolderNode { Name = Path.GetFileName(d), Path = d };
            BuildTree(d, node.Children);
            nodes.Add(node);
        }
    }

    // ================= log 索引（每个文件夹一个 log 文件） =================

    /// <summary>把外部文件登记到指定文件夹的 log 文件（每行一个路径）</summary>
    public static void AddIndexLog(string folderDir, string sourcePath)
    {
        Directory.CreateDirectory(folderDir);
        var log = LogFile(folderDir);
        var lines = File.Exists(log) ? File.ReadAllLines(log).ToList() : new List<string>();
        if (!lines.Contains(sourcePath, StringComparer.OrdinalIgnoreCase))
            lines.Add(sourcePath);
        File.WriteAllLines(log, lines, new System.Text.UTF8Encoding(false));
    }

    /// <summary>移除条目：目录内物理文件直接删文件；外部索引从 log 文件移除该行</summary>
    public static void RemoveEntry(string folderDir, AudioItem item)
    {
        if (IsInside(item.FilePath, folderDir))
        {
            if (File.Exists(item.FilePath)) File.Delete(item.FilePath);
            return;
        }
        var log = LogFile(folderDir);
        if (!File.Exists(log)) return;
        var lines = File.ReadAllLines(log)
            .Where(l => !l.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        File.WriteAllLines(log, lines, new System.Text.UTF8Encoding(false));
    }

    // ================= 检索当前文件夹（打开即检索：搜到=有） =================

    /// <summary>当前文件夹内容 = 物理音频文件 + log 索引（源文件真实存在才显示）</summary>
    public static List<AudioItem> GetFolderItems(string folderPath)
    {
        var items = new List<AudioItem>();
        if (!Directory.Exists(folderPath)) return items; // 目录不存在（如被删）则空

        // 物理音频
        foreach (var f in Directory.EnumerateFiles(folderPath, "*.*"))
            if (IsAudio(f)) items.Add(BuildItem(f));

        // log 外部索引
        var log = LogFile(folderPath);
        if (File.Exists(log))
        {
            foreach (var line in File.ReadAllLines(log))
            {
                var src = line.Trim();
                if (src.Length == 0) continue;
                if (File.Exists(src)) items.Add(BuildItem(src));
            }
        }

        return items
            .GroupBy(a => a.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ================= 拷贝真实文件到 voboX =================

    /// <summary>
    /// 遍历每个文件夹的 log 文件，把外部文件真实拷贝进该文件夹，
    /// 并从 log 移除该行。返回拷贝数量。
    /// </summary>
    public static int CopyIndexedToVobox()
    {
        int copied = 0;
        foreach (var dir in Directory.GetDirectories(Root, "*", SearchOption.AllDirectories))
        {
            var log = LogFile(dir);
            if (!File.Exists(log)) continue;

            var remain = new List<string>();
            foreach (var line in File.ReadAllLines(log))
            {
                var src = line.Trim();
                if (src.Length == 0) continue;
                if (!File.Exists(src)) { remain.Add(src); continue; }

                var dest = UniquePath(dir, Path.GetFileName(src));
                try
                {
                    File.Copy(src, dest, overwrite: false);
                    copied++;
                    continue; // 已拷贝，从 log 移除
                }
                catch { }
                remain.Add(src);
            }
            File.WriteAllLines(log, remain, new System.Text.UTF8Encoding(false));
        }
        return copied;
    }

    // ================= 工具 =================

    private static AudioItem BuildItem(string filePath)
    {
        long ms = 0;
        try
        {
            using var r = new NAudio.Wave.AudioFileReader(filePath);
            ms = (long)r.TotalTime.TotalMilliseconds;
        }
        catch
        {
            // 读取失败按 0 处理
        }
        DateTime added;
        try { added = File.GetCreationTime(filePath); } catch { added = DateTime.Now; }
        return new AudioItem { FilePath = filePath, DurationMs = ms, AddedAt = added };
    }

    private static bool IsAudio(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".wav"; // 仅支持 WAV
    }

    /// <summary>判断文件是否位于某目录（含子目录）内</summary>
    public static bool IsInside(string filePath, string dir)
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

    private static string UniquePath(string dir, string fileName)
    {
        var dest = Path.Combine(dir, fileName);
        if (!File.Exists(dest)) return dest;
        var name = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (int i = 2; ; i++)
        {
            dest = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(dest)) return dest;
        }
    }
}
