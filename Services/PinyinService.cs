using System.Collections.Concurrent;
using System.IO;
using System.Text;
using TinyPinyin;

namespace voboX.Services;

/// <summary>
/// 拼音检索服务：把文件名转成全拼（带内存缓存 + 多音字词表）。
/// 命中规则（对文件名去扩展名后的名字）：
///  1. 原始文件名包含关键字（保留原有逻辑，恒生效）
///  2. 全拼包含关键字（如搜 "yinyue" 命中「音乐」）—— 由「拼」按钮开关控制是否启用
///
/// 多音字处理：TinyPinyin 默认词典把「乐」一律读 le，这里内置一张多音字词表，
/// 转换时先做词表最长匹配（精确修正语境读音），其余字符逐字转换。
/// </summary>
public static class PinyinService
{
    /// <summary>缓存：文件名 → 全拼。搜索时会反复重建 pool，转换有成本，必须缓存</summary>
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    /// <summary>缓存上限，超过则整体清空（音频文件名规模小，简单策略足够）</summary>
    private const int MaxCacheSize = 5000;

    /// <summary>
    /// 多音字词表：词 → 正确全拼。只收录 TinyPinyin 默认转错、且常见的词。
    /// 「乐」读 yue 的词必收；地名/常用词按需补充。转换时按最长词优先匹配。
    /// </summary>
    private static readonly Dictionary<string, string> WordDict = new()
    {
        // —— 乐：音乐语境读 yuè ——
        { "音乐", "yinyue" },
        { "乐队", "yuedui" },
        { "乐曲", "yuequ" },
        { "乐器", "yueqi" },
        { "乐谱", "yuepu" },
        { "乐章", "yuezhang" },
        { "乐坛", "yuetang" },
        { "乐迷", "yuemi" },
        { "乐声", "yuesheng" },
        { "乐音", "yueyin" },
        // —— 其他常见多音字 ——
        { "重庆", "chongqing" },
        { "长城", "changcheng" },
        { "长度", "changdu" },
        { "重新", "chongxin" },
        { "重来", "chonglai" },
        { "行长", "hangzhang" },
        { "银行", "yinhang" },
        { "行走", "xingzou" },
        { "行业", "hangye" },
        { "歌曲", "gequ" },
        { "曲子", "quzi" },
        { "旋律", "xuanlv" },
        { "感觉", "ganjue" },
        { "觉得", "juede" },
    };

    /// <summary>词表按词长降序排列，保证最长匹配优先</summary>
    private static readonly KeyValuePair<string, string>[] WordDictSorted =
        WordDict.OrderByDescending(kv => kv.Key.Length).ToArray();

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
        var name = Path.GetFileNameWithoutExtension(fileName);
        var sb = new StringBuilder(name.Length * 2);

        var i = 0;
        while (i < name.Length)
        {
            // 1. 多音字词表最长匹配（精确修正语境读音）
            var word = TryMatchWord(name, i);
            if (word.HasValue)
            {
                sb.Append(word.Value.Value);
                i += word.Value.Key.Length;
                continue;
            }

            // 2. 单字转换：汉字出拼音，非汉字（英文/数字/符号）原样保留
            var ch = name[i];
            string py;
            try
            {
                py = PinyinHelper.GetPinyin(ch).ToLowerInvariant();
            }
            catch
            {
                py = "";
            }
            sb.Append(py.Length > 0 ? py : ch.ToString());
            i++;
        }

        return sb.ToString();
    }

    /// <summary>在 name[i..] 处尝试匹配词表，返回命中的 (词, 拼音) 对；无命中返回 null</summary>
    private static KeyValuePair<string, string>? TryMatchWord(string name, int i)
    {
        foreach (var kv in WordDictSorted)
        {
            if (i + kv.Key.Length <= name.Length &&
                string.Compare(name, i, kv.Key, 0, kv.Key.Length, StringComparison.Ordinal) == 0)
                return kv;
        }
        return null;
    }
}

