using PLCPak.Core.Models;

namespace PLCPak.Core.Services;

public sealed record GlobalShortcutBinding
{
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool Alt { get; init; }
    public string Key { get; init; } = string.Empty;
}

public static class GlobalShortcutBindingService
{
    public static bool TryParse(string? text, out GlobalShortcutBinding binding)
    {
        binding = new GlobalShortcutBinding();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        string? key = null;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                binding = binding with { Ctrl = true };
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                binding = binding with { Shift = true };
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                binding = binding with { Alt = true };
                continue;
            }

            key = NormalizeKeyToken(part);
        }

        if (string.IsNullOrWhiteSpace(key))
            return false;

        binding = binding with { Key = key };
        return true;
    }

    public static GlobalShortcutBinding GetEffectiveBinding(UiPreferences prefs, string shortcutId)
    {
        if (prefs.ShortcutOverrides.TryGetValue(shortcutId, out var overrideText)
            && TryParse(overrideText, out var overrideBinding))
            return overrideBinding;

        var definition = GlobalShortcutRegistry.FindDefinition(shortcutId);
        if (definition is not null && TryParse(definition.Keys, out var defaultBinding))
            return defaultBinding;

        return new GlobalShortcutBinding();
    }

    public static bool Matches(
        GlobalShortcutBinding binding,
        bool ctrl,
        bool shift,
        bool alt,
        string keyToken)
        => binding.Ctrl == ctrl
            && binding.Shift == shift
            && binding.Alt == alt
            && string.Equals(binding.Key, NormalizeKeyToken(keyToken), StringComparison.OrdinalIgnoreCase);

    public static string Format(GlobalShortcutBinding binding)
    {
        var parts = new List<string>();
        if (binding.Ctrl)
            parts.Add("Ctrl");
        if (binding.Shift)
            parts.Add("Shift");
        if (binding.Alt)
            parts.Add("Alt");
        if (!string.IsNullOrWhiteSpace(binding.Key))
            parts.Add(binding.Key);
        return parts.Count == 0 ? string.Empty : string.Join('+', parts);
    }

    public static string GetDisplayKeys(UiPreferences prefs, string shortcutId)
    {
        var binding = GetEffectiveBinding(prefs, shortcutId);
        var formatted = Format(binding);
        return string.IsNullOrWhiteSpace(formatted)
            ? GlobalShortcutRegistry.FindDefinition(shortcutId)?.Keys ?? string.Empty
            : formatted;
    }

    private static string NormalizeKeyToken(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 1)
            return trimmed.ToUpperInvariant();

        if (trimmed.StartsWith("Number", StringComparison.OrdinalIgnoreCase) && trimmed.Length == 7)
            return trimmed[^1..];

        if (trimmed.StartsWith('F')
            && trimmed.Length >= 2
            && int.TryParse(trimmed[1..], out var fn)
            && fn is >= 1 and <= 24)
            return $"F{fn}";

        return trimmed switch
        {
            "Enter" or "Return" => "Enter",
            "Esc" or "Escape" => "Escape",
            "Space" => "Space",
            _ => trimmed
        };
    }
}