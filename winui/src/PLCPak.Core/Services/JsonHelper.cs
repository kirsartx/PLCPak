using System.Text.Json;
using System.Text.Json.Serialization;

namespace PLCPak.Core.Services;

internal static class JsonHelper
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static T? Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json.TrimStart('\uFEFF'), Options);

    public static T? ReadFile<T>(string path)
    {
        if (!File.Exists(path))
            return default;

        var json = File.ReadAllText(path).TrimStart('\uFEFF');
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static void WriteFile<T>(string path, T value, bool utf8Bom = false)
    {
        var json = JsonSerializer.Serialize(value, Options);
        var encoding = utf8Bom
            ? new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            : new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(tempPath, json, encoding);
            if (File.Exists(path))
                File.Replace(tempPath, path, destinationBackupFileName: null);
            else
                File.Move(tempPath, path);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            throw;
        }
    }
}