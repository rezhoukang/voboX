namespace voboX.Models;

/// <summary>分组（标签）</summary>
public class SampleGroup
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2563EB";
    public int SortOrder { get; set; }
}
