using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace XNote.Utils;

public sealed class ThemePalette
{
    public required Color Background { get; init; }
    public required Color Panel { get; init; }
    public required Color PanelAlt { get; init; }
    public required Color Border { get; init; }
    public required Color BorderSubtle { get; init; }
    public required Color Text { get; init; }
    public required Color TextDim { get; init; }
    public required Color TextFaint { get; init; }
    public required Color Accent { get; init; }
    public required Color Done { get; init; }

    public string PreviewHex => Panel.ToString();
}

public sealed class ThemePresetInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required ThemePalette Palette { get; init; }
    public bool IsCustom { get; init; }
}

public static class ThemeService
{
    public const string CustomPresetId = "custom";
    public const string DefaultPresetId = "default";

    private const double MinLightness = 0.85;
    private const double CustomMinSaturation = 0.18;

    private static readonly Color FixedText = Color.Parse("#1A1A1A");
    private static readonly Color FixedTextDim = Color.Parse("#6B6B6B");
    private static readonly Color FixedTextFaint = Color.Parse("#9C9C9C");
    private static readonly Color FixedAccent = Color.Parse("#000000");
    private static readonly Color FixedDone = Color.Parse("#A3A3A3");

    private static readonly ThemePalette DefaultPalette = new()
    {
        Background = Color.Parse("#FFFFFF"),
        Panel = Color.Parse("#F5F5F5"),
        PanelAlt = Color.Parse("#EAEAEA"),
        Border = Color.Parse("#D8D8D8"),
        BorderSubtle = Color.Parse("#E4E4E4"),
        Text = FixedText,
        TextDim = FixedTextDim,
        TextFaint = FixedTextFaint,
        Accent = FixedAccent,
        Done = FixedDone,
    };

    private static readonly (string Id, string Name, double Hue, double Sat)[] PresetDefs =
    [
        (DefaultPresetId, "Default", 0, 0),
        ("rose", "Rose", 350, 0.34),
        ("mint", "Mint", 155, 0.30),
        ("sky", "Sky", 205, 0.32),
        ("lavender", "Lavender", 265, 0.28),
        ("peach", "Peach", 28, 0.36),
        ("lemon", "Lemon", 52, 0.34),
        ("lilac", "Lilac", 285, 0.30),
    ];

    public static IReadOnlyList<ThemePresetInfo> Presets { get; } = BuildPresets();

    public static string AppliedPresetId { get; private set; } = DefaultPresetId;
    public static int AppliedCustomHue { get; private set; } = 180;

    private static ThemePalette BuildFromHue(double hue, double saturation)
    {
        var sat = Math.Clamp(saturation, CustomMinSaturation, 0.45);
        return new ThemePalette
        {
            Background = Hsl(hue, sat * 0.18, ClampLightness(0.98)),
            Panel = Hsl(hue, sat * 0.28, ClampLightness(0.95)),
            PanelAlt = Hsl(hue, sat * 0.38, ClampLightness(0.91)),
            Border = Hsl(hue, sat * 0.22, ClampLightness(0.86)),
            BorderSubtle = Hsl(hue, sat * 0.16, ClampLightness(0.89)),
            Text = FixedText,
            TextDim = FixedTextDim,
            TextFaint = FixedTextFaint,
            Accent = FixedAccent,
            Done = FixedDone,
        };
    }

    public static ThemePalette GetPalette(string presetId, int customHue)
    {
        if (string.Equals(presetId, DefaultPresetId, StringComparison.OrdinalIgnoreCase))
            return DefaultPalette;

        if (string.Equals(presetId, CustomPresetId, StringComparison.OrdinalIgnoreCase))
            return BuildFromHue(NormalizeHue(customHue), 0.30);

        foreach (var def in PresetDefs)
        {
            if (!string.Equals(def.Id, presetId, StringComparison.OrdinalIgnoreCase)) continue;
            if (def.Id == DefaultPresetId) return DefaultPalette;
            return BuildFromHue(def.Hue, def.Sat);
        }

        return DefaultPalette;
    }

    public static void ApplyFromSettings()
    {
        var settings = SettingsStore.Load();
        AppliedPresetId = NormalizePresetId(settings.ThemePreset);
        AppliedCustomHue = NormalizeHue(settings.CustomHue);
        ApplyPalette(GetPalette(AppliedPresetId, AppliedCustomHue));
    }

    public static void Preview(string presetId, int customHue)
    {
        var id = NormalizePresetId(presetId);
        var hue = NormalizeHue(customHue);
        ApplyPalette(GetPalette(id, hue));
    }

    public static void Commit(string presetId, int customHue)
    {
        AppliedPresetId = NormalizePresetId(presetId);
        AppliedCustomHue = NormalizeHue(customHue);

        var settings = SettingsStore.Load();
        settings.ThemePreset = AppliedPresetId;
        settings.CustomHue = AppliedCustomHue;
        SettingsStore.Save(settings);

        ApplyPalette(GetPalette(AppliedPresetId, AppliedCustomHue));
    }

    public static (string PresetId, int CustomHue) CaptureApplied() =>
        (AppliedPresetId, AppliedCustomHue);

    public static void Restore(string presetId, int customHue)
    {
        AppliedPresetId = NormalizePresetId(presetId);
        AppliedCustomHue = NormalizeHue(customHue);
        ApplyPalette(GetPalette(AppliedPresetId, AppliedCustomHue));
    }

    private static void ApplyPalette(ThemePalette palette)
    {
        if (Application.Current is null) return;

        void Apply()
        {
            if (Application.Current?.Resources is not ResourceDictionary res) return;
            Set(res, "XnBlackColor", palette.Background);
            Set(res, "XnPanelColor", palette.Panel);
            Set(res, "XnPanelAltColor", palette.PanelAlt);
            Set(res, "XnBorderColor", palette.Border);
            Set(res, "XnBorderSubtleColor", palette.BorderSubtle);
            Set(res, "XnTextColor", palette.Text);
            Set(res, "XnTextDimColor", palette.TextDim);
            Set(res, "XnTextFaintColor", palette.TextFaint);
            Set(res, "XnAccentColor", palette.Accent);
            Set(res, "XnDoneColor", palette.Done);

            SetBrush(res, "XnBlackBrush", palette.Background);
            SetBrush(res, "XnPanelBrush", palette.Panel);
            SetBrush(res, "XnPanelAltBrush", palette.PanelAlt);
            SetBrush(res, "XnBorderBrush", palette.Border);
            SetBrush(res, "XnBorderSubtleBrush", palette.BorderSubtle);
            SetBrush(res, "XnTextBrush", palette.Text);
            SetBrush(res, "XnTextDimBrush", palette.TextDim);
            SetBrush(res, "XnTextFaintBrush", palette.TextFaint);
            SetBrush(res, "XnAccentBrush", palette.Accent);
            SetBrush(res, "XnDoneBrush", palette.Done);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Apply();
        else
            Dispatcher.UIThread.Post(Apply);
    }

    private static void Set(ResourceDictionary res, string key, Color color) => res[key] = color;

    private static void SetBrush(ResourceDictionary res, string key, Color color)
    {
        if (res[key] is SolidColorBrush existing)
            existing.Color = color;
        else
            res[key] = new SolidColorBrush(color);
    }

    private static double ClampLightness(double l) => Math.Max(l, MinLightness);

    private static int NormalizeHue(int hue)
    {
        var h = hue % 360;
        return h < 0 ? h + 360 : h;
    }

    private static string NormalizePresetId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return DefaultPresetId;
        foreach (var def in PresetDefs)
            if (string.Equals(def.Id, id, StringComparison.OrdinalIgnoreCase))
                return def.Id;
        if (string.Equals(id, CustomPresetId, StringComparison.OrdinalIgnoreCase))
            return CustomPresetId;
        return DefaultPresetId;
    }

    private static Color Hsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        l = Math.Clamp(l, 0, 1);

        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        var m = l - c / 2;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    private static IReadOnlyList<ThemePresetInfo> BuildPresets()
    {
        var list = new List<ThemePresetInfo>(PresetDefs.Length + 1);
        foreach (var def in PresetDefs)
        {
            var palette = def.Id == DefaultPresetId
                ? DefaultPalette
                : BuildFromHue(def.Hue, def.Sat);
            list.Add(new ThemePresetInfo
            {
                Id = def.Id,
                DisplayName = def.Name,
                Palette = palette,
            });
        }

        list.Add(new ThemePresetInfo
        {
            Id = CustomPresetId,
            DisplayName = "Custom",
            Palette = BuildFromHue(180, 0.30),
            IsCustom = true,
        });

        return list;
    }
}