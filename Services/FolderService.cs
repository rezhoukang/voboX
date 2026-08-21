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
    // 行格式：`路径`（纯索引）或 `路径|别名`（登记显示别名）。别名空 = 显示物理文件名。

    /// <summary>解析 log 行 → (路径, 别名)</summary>
    public static (string Path, string Alias) ParseLogLine(string line)
    {
        var i = line.IndexOf('|');
        if (i < 0) return (line.Trim(), "");
        return (line[..i].Trim(), line[(i + 1)..].Trim());
    }

    /// <summary>把外部文件登记到指定文件夹的 log 文件（每行一个路径，按解析后的路径去重）</summary>
    public static void AddIndexLog(string folderDir, string sourcePath)
    {
        Directory.CreateDirectory(folderDir);
        var log = LogFile(folderDir);
        var lines = File.Exists(log) ? File.ReadAllLines(log).ToList() : new List<string>();
        if (!lines.Any(l => ParseLogLine(l).Path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase)))
            lines.Add(sourcePath);
        File.WriteAllLines(log, lines, new System.Text.UTF8Encoding(false));
    }

    /// <summary>
    /// 设置某条目的显示别名（写入文件夹 log.txt）。
    /// alias 为空 = 清除别名：物理文件直接移除登记行；外部索引保留纯路径行（索引不丢）。
    /// </summary>
    public static void SetAlias(string folderDir, string filePath, string alias)
    {
        Directory.CreateDirectory(folderDir);
        var log = LogFile(folderDir);
        var lines = File.Exists(log) ? File.ReadAllLines(log).ToList() : new List<string>();
        var remain = new List<string>();
        bool found = false;
        foreach (var line in lines)
        {
            var (p, _) = ParseLogLine(line);
            if (!p.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            {
                remain.Add(line);
                continue;
            }
            found = true;
            if (string.IsNullOrEmpty(alias))
            {
                // 清别名：物理文件移除登记行；外部索引保留纯路径行
                if (!IsInside(filePath, folderDir)) remain.Add(filePath);
            }
            else
            {
                remain.Add($"{filePath}|{alias}");
            }
        }
        if (!found && !string.IsNullOrEmpty(alias))
            remain.Add($"{filePath}|{alias}");
        File.WriteAllLines(log, remain, new System.Text.UTF8Encoding(false));
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
            .Where(l => !ParseLogLine(l).Path.Equals(item.FilePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        File.WriteAllLines(log, lines, new System.Text.UTF8Encoding(false));
    }

    /// <summary>
    /// 查找文件夹内与 sourcePath 同名的音频（物理 wav + log 索引中仍存在的源文件）。
    /// 返回已存在的那个路径；无重名返回 null。供导入前重名检查。
    /// </summary>
    public static string? FindDuplicateName(string folderDir, string sourcePath)
    {
        var name = Path.GetFileName(sourcePath);
        if (string.IsNullOrEmpty(name)) return null;

        // 物理文件
        if (Directory.Exists(folderDir))
        {
            foreach (var f in Directory.EnumerateFiles(folderDir, "*.*"))
                if (IsAudio(f) && Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return f;
        }

        // log 索引（源文件仍存在才视为同名的现存条目）
        var log = LogFile(folderDir);
        if (File.Exists(log))
        {
            foreach (var line in File.ReadAllLines(log))
            {
                var (src, _) = ParseLogLine(line);
                if (src.Length == 0) continue;
                if (File.Exists(src) && Path.GetFileName(src).Equals(name, StringComparison.OrdinalIgnoreCase))
                    return src;
            }
        }
        return null;
    }

    // ================= 检索（物理 wav + log 索引，源文件真实存在才显示） =================

    /// <summary>当前文件夹内容 = 物理音频文件 + log 索引（源文件真实存在才显示）</summary>
    public static List<AudioItem> GetFolderItems(string folderPath)
    {
        var paths = new List<(string Path, string Alias)>();
        if (Directory.Exists(folderPath)) CollectFolderPaths(folderPath, paths);
        return paths.Select(BuildItem).ToList(); // 保持原始顺序，排序交给 MainWindow
    }

    /// <summary>轻量收集当前文件夹（不读时长），供「先搜出文件、再加载时长」</summary>
    public static List<AudioItem> GetFolderPaths(string folderPath)
    {
        var paths = new List<(string Path, string Alias)>();
        if (Directory.Exists(folderPath)) CollectFolderPaths(folderPath, paths);
        return paths.Select(BuildSkeleton).ToList();
    }

    // ================= 树 / 全局搜索 =================

    /// <summary>Box 根目录（voboX 索引目录的上一级；含 recordBox / cutBox / tempBox / voboX）</summary>
    public static string BoxRoot =>
        Path.GetDirectoryName(Root.TrimEnd(Path.DirectorySeparatorChar)) ?? Root;

    /// <summary>
    /// 递归收集目录树下的音频项：物理 wav + 各层 log 索引（源文件真实存在才显示）。
    /// 供「全局搜索（整个 Box，排除 tempBox）」与「voboX搜索（索引目录树）」范围使用，按路径去重。
    /// </summary>
    public static List<AudioItem> GetTreeItems(string rootDir, params string[] skipSubDirNames)
    {
        var paths = new List<(string Path, string Alias)>();
        if (Directory.Exists(rootDir)) CollectTreePaths(rootDir, paths, skipSubDirNames);
        return paths.Select(BuildItem).ToList();
    }

    /// <summary>轻量收集目录树（不读时长），供「先搜出文件、再加载时长」</summary>
    public static List<AudioItem> GetTreePaths(string rootDir, params string[] skipSubDirNames)
    {
        var paths = new List<(string Path, string Alias)>();
        if (Directory.Exists(rootDir)) CollectTreePaths(rootDir, paths, skipSubDirNames);
        return paths.Select(BuildSkeleton).ToList();
    }

    /// <summary>收集单文件夹的音频路径：物理 wav + log 索引（源文件仍存在）</summary>
    /// <summary>
    /// 收集单文件夹的音频：物理 wav + log 登记的路径（物理与外部都带可选别名）。
    /// log 里登记的物理文件只用于补别名（不重复加入）；外部文件作为索引加入。
    /// </summary>
    private static void CollectFolderPaths(string folderPath, List<(string Path, string Alias)> paths)
    {
        // 先读 log 建立 路径→别名 映射（物理文件别名与外部索引都在这里）
        var log = LogFile(folderPath);
        var aliasByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(log))
        {
            foreach (var line in File.ReadAllLines(log))
            {
                var (p, a) = ParseLogLine(line);
                if (p.Length == 0 || !File.Exists(p)) continue;
                aliasByPath[p] = a;
            }
        }
        // 物理文件：直接枚举，查别名
        foreach (var f in Directory.EnumerateFiles(folderPath, "*.*"))
            if (IsAudio(f))
                paths.Add((f, aliasByPath.TryGetValue(f, out var a) ? a : ""));
        // log 登记的外部文件（不在本目录内）
        foreach (var kv in aliasByPath)
            if (!IsInside(kv.Key, folderPath))
                paths.Add((kv.Key, kv.Value));
    }

    /// <summary>递归收集目录树（跳过指定子目录，如 tempBox）</summary>
    private static void CollectTreePaths(string dir, List<(string Path, string Alias)> paths, string[] skipSubDirNames)
    {
        CollectFolderPaths(dir, paths);
        foreach (var sub in Directory.GetDirectories(dir))
        {
            var name = Path.GetFileName(sub.TrimEnd(Path.DirectorySeparatorChar));
            if (skipSubDirNames.Any(s => s.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            CollectTreePaths(sub, paths, skipSubDirNames);
        }
    }

    // ================= 拷贝 log 索引的外部文件到对应目录 =================

    /// <summary>
    /// 把指定根目录下各文件夹 log 文件索引的外部文件真实拷贝进该文件夹，
    /// 并从 log 移除已拷贝的行。
    /// recursive=true（voboX）时递归其下所有子文件夹；false（recordBox/cutBox）只处理根目录自身。
    /// 返回 (处理的目录数, 拷贝文件数)。
    /// </summary>
    public static (int dirs, int copied) CopyIndexedLogs(string rootDir, bool recursive)
    {
        int copied = 0, dirs = 0;
        var dirsToProcess = recursive
            ? Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories).ToList()
            : new List<string> { rootDir };

        foreach (var dir in dirsToProcess)
        {
            var log = LogFile(dir);
            if (!File.Exists(log)) continue;

            var remain = new List<string>();
            foreach (var line in File.ReadAllLines(log))
            {
                var (src, alias) = ParseLogLine(line);
                if (src.Length == 0) continue;
                if (!File.Exists(src)) { remain.Add(line); continue; }
                // 物理文件（本目录内）的别名登记行：不拷，保留
                if (IsInside(src, dir)) { remain.Add(line); continue; }

                // 目标文件名：有别名用别名，否则用原文件名
                var dest = UniquePath(dir, !string.IsNullOrEmpty(alias) ? alias : Path.GetFileName(src));
                try
                {
                    File.Copy(src, dest, overwrite: false);
                    copied++;
                    continue; // 已拷贝，从 log 移除
                }
                catch { }
                remain.Add(line);
            }
            File.WriteAllLines(log, remain, new System.Text.UTF8Encoding(false));
            dirs++;
        }
        return (dirs, copied);
    }

    /// <summary>统计根目录下各文件夹 log 索引的有效条目数（供拷贝前的确认提示）</summary>
    public static int CountLogEntries(string rootDir, bool recursive)
    {
        int count = 0;
        var dirs = recursive
            ? Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories).ToList()
            : new List<string> { rootDir };
        foreach (var dir in dirs)
        {
            var log = LogFile(dir);
            if (!File.Exists(log)) continue;
            count += File.ReadAllLines(log).Count(l => !string.IsNullOrWhiteSpace(ParseLogLine(l).Path));
        }
        return count;
    }

    // ================= 工具 =================

    /// <summary>读取音频时长（毫秒）；失败返回 0</summary>
    public static long GetDuration(string filePath)
    {
        try
        {
            using var r = new NAudio.Wave.AudioFileReader(filePath);
            return (long)r.TotalTime.TotalMilliseconds;
        }
        catch { return 0; }
    }

    /// <summary>轻量条目：只带路径与加入时间（时长 0，搜索命中后再补读）</summary>
    private static AudioItem BuildSkeleton((string Path, string Alias) p)
    {
        DateTime added;
        try { added = File.GetCreationTime(p.Path); } catch { added = DateTime.Now; }
        return new AudioItem { FilePath = p.Path, Alias = p.Alias, DurationMs = 0, AddedAt = added };
    }

    private static AudioItem BuildItem((string Path, string Alias) p)
    {
        var item = BuildSkeleton(p);
        item.DurationMs = GetDuration(p.Path);
        return item;
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
