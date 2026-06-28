namespace PLCPak.Core.Models;

public sealed class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ImportedFields { get; set; } = [];
}