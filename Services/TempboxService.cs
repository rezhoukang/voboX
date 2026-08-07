using System.IO;

namespace voboX.Services;

/// <summary>
/// tempBox 临时副本：文件拖出到外部软件前，先复制一份副本到 tempBox，
/// 保证各软件对片段的修改互不影响。
/// 命名规则：原文件名 + temp + 时间戳。不自动清理。
/// </summary>
public class TempboxService
{
    private readonly Func<string> _dirProvider;

    public TempboxService(Func<string> dirProvider) => _dirProvider = dirProvider;

    /// <summary>tempBox 目录（跟随设置实时变化）</summary>
    public string TempboxDir => _dirProvider();

    /// <summary>把源文件复制到 tempBox（原文件名 + temp + 时间戳），返回副本路径</summary>
    public string CreateCopy(string sourcePath)
    {
        var dir = _dirProvider();
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        var name = $"{Path.GetFileNameWithoutExtension(sourcePath)}_temp_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
        var dest = Path.Combine(dir, name);
        File.Copy(sourcePath, dest, overwrite: false);
        return dest;
    }
}
