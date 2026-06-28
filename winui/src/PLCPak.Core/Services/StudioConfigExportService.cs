using System.Text.Json;
using System.Text.Json.Nodes;
using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class StudioConfigExportService
{
    private readonly StudioConfigService _studioConfig;

    public StudioConfigExportService(StudioConfigService studioConfig) => _studioConfig = studioConfig;

    public string ExportStudioConfig(string path)
    {
        var source = _studioConfig.ConfigPath;
        if (!File.Exists(source))
            throw new FileNotFoundException($"工作室配置不存在: {source}");

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.Copy(source, fullPath, overwrite: true);
        return fullPath;
    }

    public ImportResult ImportStudioConfig(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return new ImportResult
            {
                Success = false,
                Message = $"导入文件不存在: {fullPath}"
            };
        }

        JsonObject importObject;
        try
        {
            var importNode = JsonNode.Parse(File.ReadAllText(fullPath).TrimStart('\uFEFF'));
            if (importNode is not JsonObject parsed)
            {
                return new ImportResult
                {
                    Success = false,
                    Message = "配置 JSON 必须是对象"
                };
            }

            importObject = parsed;
        }
        catch (JsonException ex)
        {
            return new ImportResult
            {
                Success = false,
                Message = $"配置 JSON 无效: {ex.Message}"
            };
        }

        JsonObject mergedObject;
        if (File.Exists(_studioConfig.ConfigPath))
        {
            var existingNode = JsonNode.Parse(File.ReadAllText(_studioConfig.ConfigPath).TrimStart('\uFEFF'));
            mergedObject = existingNode as JsonObject ?? new JsonObject();
        }
        else
        {
            mergedObject = new JsonObject();
        }

        var importedFields = new List<string>();
        foreach (var property in importObject)
        {
            mergedObject[property.Key] = property.Value?.DeepClone();
            importedFields.Add(property.Key);
        }

        try
        {
            var mergedJson = mergedObject.ToJsonString(JsonHelper.Options);
            _ = JsonSerializer.Deserialize<StudioConfig>(mergedJson, JsonHelper.Options)
                ?? throw new InvalidOperationException("无法解析合并后的配置");
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                Message = $"配置校验失败: {ex.Message}"
            };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_studioConfig.ConfigPath)!);
        File.WriteAllText(_studioConfig.ConfigPath, mergedObject.ToJsonString(JsonHelper.Options));

        return new ImportResult
        {
            Success = true,
            Message = $"已合并 {importedFields.Count} 个配置字段",
            ImportedFields = importedFields
        };
    }
}