using System.Collections.Concurrent;
using System.IO;
using TinyPinyin;

namespace voboX.Services;

/// <summary>
/// 拼音检索服务：把文件名转成全拼（带内存缓存）。
/// 命中规则（对文件名去扩展名后的名字）：
///  1. 原始文件名包含关键字（保留原有逻辑，恒生效）
///  2. 全拼包含关键字（如搜 "dingdongji" 命中「叮咚鸡大狗叫」）—— 由「拼」按钮开关控制是否启用
///
/// 说明：使用 TinyPinyin 默认读音，不做多音字语境修正（多音字功能已按需求移除）。
/// </summary>
public static class PinyinService
{
    /// <summary>缓存：文件名 → 全拼。搜索时会反复重建 pool，转换有成本，必须缓存</summary>
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    /// <summary>缓存上限，超过则整体清空（音频文件名规模小，简单策略足够）</summary>
    private const int MaxCacheSize = 5000;

    /// <summary>判断文件名是否命中关键字：原文包含（恒生效）或全拼包含</summary>
    public static bool Matches(string fileName, string keyword)
    {
        // 1. 原文匹配：用去扩展名后的名字（排除 .wav/.mp3 后缀干扰，搜 "wav" 不应命中）
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (baseName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. 全拼匹配（小写比较）
        var full = GetOrBuild(fileName);
        if (full.Length == 0)
            return false;
        return full.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>文件列表变化（导入/删除/改名）后调用，避免旧拼音残留</summary>
    public static void ClearCache() => Cache.Clear();

    private static string GetOrBuild(string fileName)
    {
        if (Cache.TryGetValue(fileName, out var full))
            return full;

        full = Build(fileName);
        if (Cache.Count >= MaxCacheSize)
            Cache.Clear(); // 超限整体清空重建，避免复杂淘汰逻辑
        Cache[fileName] = full;
        return full;
    }

    private static string Build(string fileName)
    {
        // 去掉扩展名再转，避免 ".mp3"/".wav" 等混入拼音干扰匹配
        // GetPinyin(整串, 分隔符)：汉字出全拼，非汉字（英文/数字/符号）原样保留
        var name = Path.GetFileNameWithoutExtension(fileName);
        try
        {
            return PinyinHelper.GetPinyin(name, "").ToLowerInvariant();
        }
        catch
        {
            return "";
        }
    }
}

