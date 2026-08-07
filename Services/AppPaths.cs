using System.IO;

namespace voboX.Services;

/// <summary>应用路径管理</summary>
public static class AppPaths
{
    /// <summary>应用数据目录（SQLite 仓库 / 设置 / 录音）</summary>
    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "voboX");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>SQLite 仓库文件</summary>
    public static string DbPath => Path.Combine(DataDir, "voboX.db");

    /// <summary>录音输出目录</summary>
    public static string RecordingsDir
    {
        get
        {
            var dir = Path.Combine(DataDir, "Recordings");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string? _boxDir;

    /// <summary>
    /// 统一的 Box 文件夹（Tempbox / Cutbox 都放在这里）。
    /// 开发运行时定位到项目根目录（向上找到 .csproj）；发布后回退到 exe 同级。
    /// </summary>
    private static string BoxDir
    {
        get
        {
            if (_boxDir is not null) return _boxDir;

            // 优先：从 exe 目录向上找项目根（含 .csproj），Box 放项目根目录
            try
            {
                for (var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                     d is not null; d = d.Parent)
                {
                    if (d.GetFiles("*.csproj").Length > 0)
                    {
                        var root = Path.Combine(d.FullName, "Box");
                        Directory.CreateDirectory(root);
                        return _boxDir = root;
                    }
                }
            }
            catch { }

            // 回退：exe 同级
            try
            {
                var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Box");
                Directory.CreateDirectory(dir);
                return _boxDir = dir;
            }
            catch
            {
                var dir = Path.Combine(DataDir, "Box");
                Directory.CreateDirectory(dir);
                return _boxDir = dir;
            }
        }
    }

    /// <summary>默认 Tempbox 目录（根目录 Box\Tempbox，不可写时回退应用数据目录）</summary>
    public static string DefaultTempboxPath
    {
        get
        {
            var dir = Path.Combine(BoxDir, "Tempbox");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>默认裁剪保存目录（根目录 Box\Cutbox，不可写时回退应用数据目录）</summary>
    public static string DefaultCutboxPath
    {
        get
        {
            var dir = Path.Combine(BoxDir, "Cutbox");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>启动时确保 Box 目录结构存在（Tempbox / Cutbox）</summary>
    public static void EnsureBoxFolders()
    {
        _ = DefaultTempboxPath;
        _ = DefaultCutboxPath;
    }
}
