using Battlegrounds.Content;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _combatThemeUiApplied;
    private bool _combatScreenThemeActive;

    private void RefreshCombatThemeState()
    {
        if (_combatOverlay is null)
            return;

        if (!_combatThemeUiApplied)
        {
            ApplyCombatSemanticTheme();
            _combatThemeUiApplied = true;
        }

        var combatVisible = _combatOverlay.Visible;
        if (combatVisible == _combatScreenThemeActive)
            return;

        _combatScreenThemeActive = combatVisible;
        ApplyScreenTheme(combatVisible ? ModThemeScreenRoles.Combat : ModThemeScreenRoles.Preparation);
    }

    private void ApplyCombatSemanticTheme()
    {
        if (_modTheme is null || _modTheme.IsEmpty)
            return;

        if (_combatTitle is not null)
        {
            _combatTitle.ThemeTypeVariation = "TitleLabel";
            RemoveFontSizeOverrideWhenThemed(_combatTitle, "title");
        }

        if (_combatProgress is not null)
            _combatProgress.ThemeTypeVariation = "CaptionLabel";

        if (_combatLeftHeader is not null)
        {
            _combatLeftHeader.ThemeTypeVariation = "HeadingLabel";
            RemoveFontSizeOverrideWhenThemed(_combatLeftHeader, "heading");
        }

        if (_combatRightHeader is not null)
        {
            _combatRightHeader.ThemeTypeVariation = "HeadingLabel";
            RemoveFontSizeOverrideWhenThemed(_combatRightHeader, "heading");
        }

        if (_combatEvent is not null)
        {
            _combatEvent.ThemeTypeVariation = "BodyLabel";
            RemoveFontSizeOverrideWhenThemed(_combatEvent, "body");
        }

        if (_combatNextButton is not null)
            _combatNextButton.ThemeTypeVariation = "PrimaryButton";
    }

    private void RemoveFontSizeOverrideWhenThemed(Godot.Label label, string sizeToken)
    {
        if (_modTheme?.FontSizes.ContainsKey(sizeToken) == true)
            label.RemoveThemeFontSizeOverride("font_size");
    }
}
