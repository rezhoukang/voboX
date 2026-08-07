using System.IO;
using Microsoft.Data.Sqlite;
using voboX.Models;

namespace voboX.Services;

/// <summary>
/// 音频仓库：SQLite 索引。
/// 只保存元数据（路径/时长/大小/标签），不复制原文件。
/// </summary>
public class AudioRepository
{
    private readonly string _dbPath;

    public AudioRepository(string dbPath)
    {
        _dbPath = dbPath;
        Initialize();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_path TEXT NOT NULL UNIQUE,
                duration_ms INTEGER NOT NULL DEFAULT 0,
                tags TEXT NOT NULL DEFAULT '',
                added_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS groups (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                color TEXT NOT NULL DEFAULT '#2563EB',
                sort_order INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS sample_groups (
                sample_id INTEGER NOT NULL,
                group_id INTEGER NOT NULL,
                PRIMARY KEY (sample_id, group_id)
            );
            """;
        cmd.ExecuteNonQuery();

        // 迁移：老数据库可能残留 file_size 列，已不再使用，直接删掉（新库无此列时忽略）
        try
        {
            cmd.CommandText = "ALTER TABLE samples DROP COLUMN file_size";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // 列不存在，忽略
        }
    }

    /// <summary>添加音频到仓库（已存在则忽略），返回样本 id</summary>
    public long AddSample(string filePath, long durationMs)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO samples (file_path, duration_ms, added_at)
            VALUES ($p, $d, $t)
            """;
        cmd.Parameters.AddWithValue("$p", filePath);
        cmd.Parameters.AddWithValue("$d", durationMs);
        cmd.Parameters.AddWithValue("$t", DateTime.Now.ToString("o"));
        cmd.ExecuteNonQuery();

        cmd.CommandText = "SELECT id FROM samples WHERE file_path = $p";
        return (long)cmd.ExecuteScalar()!;
    }

    /// <summary>从仓库移除（只移除索引，不删除原文件）</summary>
    public void RemoveSample(long id)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM sample_groups WHERE sample_id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM samples WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>查询所有音频，支持排序</summary>
    public List<AudioItem> GetSamples(string sortRule = "time")
    {
        var orderBy = sortRule switch
        {
            "name" => "s.file_path COLLATE NOCASE ASC",
            "duration" => "s.duration_ms DESC",
            _ => "s.added_at DESC",
        };

        using var conn = Open();
        var items = new List<AudioItem>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT s.id, s.file_path, s.duration_ms, s.tags, s.added_at,
                   (SELECT group_concat(g.name, ',') FROM sample_groups sg
                     JOIN groups g ON g.id = sg.group_id WHERE sg.sample_id = s.id) AS group_names
            FROM samples s
            ORDER BY {orderBy}
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new AudioItem
            {
                Id = reader.GetInt64(0),
                FilePath = reader.GetString(1),
                DurationMs = reader.GetInt64(2),
                Tags = reader.IsDBNull(3) ? "" : reader.GetString(3),
                AddedAt = DateTime.TryParse(reader.GetString(4), out var dt) ? dt : DateTime.MinValue,
                GroupNames = reader.IsDBNull(5) ? "" : reader.GetString(5),
            });
        }
        return items;
    }

    /// <summary>按关键字搜索（文件名 / 标签 / 分组名）</summary>
    public List<AudioItem> Search(string keyword, string sortRule)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return GetSamples(sortRule);
        var kw = keyword.Trim();
        return GetSamples(sortRule)
            .Where(a => a.FileName.Contains(kw, StringComparison.OrdinalIgnoreCase)
                     || a.Tags.Contains(kw, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
