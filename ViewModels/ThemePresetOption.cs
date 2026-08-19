using XNote.Utils;

namespace XNote.ViewModels;

public sealed class ThemePresetOption
{
    public ThemePresetOption(ThemePresetInfo info)
    {
        Id = info.Id;
        DisplayName = info.DisplayName;
        PreviewColor = info.Palette.Panel.ToString();
        IsCustom = info.IsCustom;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string PreviewColor { get; }
    public bool IsCustom { get; }
}