using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed class GlobalShortcutConflict
{
    public string BindingText { get; set; } = string.Empty;
    public List<string> ShortcutIds { get; set; } = [];
    public List<string> Labels { get; set; } = [];
}

public static class GlobalShortcutConflictService
{
    public static IReadOnlyList<GlobalShortcutConflict> FindConflicts(UiPreferences prefs)
    {
        var bindingMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in GlobalShortcutRegistry.Definitions)
        {
            if (!GlobalShortcutRegistry.IsEnabled(prefs, definition.Id))
                continue;

            var bindingText = GlobalShortcutBindingService.Format(
                GlobalShortcutBindingService.GetEffectiveBinding(prefs, definition.Id));
            if (string.IsNullOrWhiteSpace(bindingText))
                continue;

            if (!bindingMap.TryGetValue(bindingText, out var ids))
            {
                ids = [];
                bindingMap[bindingText] = ids;
            }

            ids.Add(definition.Id);
        }

        return bindingMap
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => new GlobalShortcutConflict
            {
                BindingText = pair.Key,
                ShortcutIds = pair.Value.ToList(),
                Labels = pair.Value
                    .Select(id => GlobalShortcutRegistry.FindDefinition(id)?.Label ?? id)
                    .ToList()
            })
            .OrderBy(conflict => conflict.BindingText, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildConflictMessage(IEnumerable<GlobalShortcutConflict> conflicts)
    {
        var lines = conflicts
            .Select(conflict =>
                $"{conflict.BindingText}：{string.Join("、", conflict.Labels)}")
            .ToList();
        return lines.Count == 0
            ? string.Empty
            : "检测到快捷键冲突：" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}