namespace PLCPak.Core.Services;

public static class ActivityLogBatchFilterService
{
    public const string BatchSearchKeyword = "批量";

    public static readonly string[] BatchCategories =
    [
        "archive",
        "unarchive",
        "delete",
        "tags"
    ];

    public static string GetBatchCategoryLabel(string category)
        => category.ToLowerInvariant() switch
        {
            "archive" => "批量归档",
            "unarchive" => "批量恢复",
            "delete" => "批量删除",
            "tags" => "批量标签",
            _ => category
        };
}