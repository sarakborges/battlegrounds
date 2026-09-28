using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _combatThemeUiApplied;

    private void RefreshCombatThemeState()
    {
        if (_combatOverlay is null || _combatThemeUiApplied)
            return;

        ApplyCombatSemanticTheme();
        _combatThemeUiApplied = true;
    }

    private void ApplyCombatSemanticTheme()
    {
        if (_modTheme is null || _modTheme.IsEmpty)
            return;

        if (_combatOverlay is not null)
        {
            _combatOverlay.ThemeTypeVariation = "CombatOverlay";
            ApplyCombatLayoutTheme(_combatOverlay);
        }

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

    private void ApplyCombatLayoutTheme(PanelContainer overlay)
    {
        var margin = overlay.FindChild("CombatMargin", recursive: true, owned: false) as MarginContainer;
        if (margin is not null)
        {
            var horizontal = Mathf.RoundToInt(ResolvePresentationMetric(
                ModThemeMetricKeys.Layout.Combat.MarginHorizontal, 0.0f, 512.0f));
            var vertical = Mathf.RoundToInt(ResolvePresentationMetric(
                ModThemeMetricKeys.Layout.Combat.MarginVertical, 0.0f, 512.0f));

            margin.AddThemeConstantOverride("margin_left", horizontal);
            margin.AddThemeConstantOverride("margin_right", horizontal);
            margin.AddThemeConstantOverride("margin_top", vertical);
            margin.AddThemeConstantOverride("margin_bottom", vertical);
        }

        var root = overlay.FindChild("CombatRoot", recursive: true, owned: false) as VBoxContainer;
        if (root is not null)
        {
            root.AddThemeConstantOverride(
                "separation",
                Mathf.RoundToInt(ResolvePresentationMetric(
                    ModThemeMetricKeys.Layout.Combat.ContentGap, 0.0f, 512.0f)));
        }

        var battlefield = overlay.FindChild("CombatBattlefield", recursive: true, owned: false) as VBoxContainer;
        if (battlefield is not null)
        {
            battlefield.AddThemeConstantOverride(
                "separation",
                Mathf.RoundToInt(ResolvePresentationMetric(
                    ModThemeMetricKeys.Layout.Combat.BoardsGap, 0.0f, 512.0f)));
        }

        var controls = overlay.FindChild("CombatControls", recursive: true, owned: false) as HBoxContainer;
        if (controls is not null)
        {
            controls.AddThemeConstantOverride(
                "separation",
                Mathf.RoundToInt(ResolvePresentationMetric(
                    ModThemeMetricKeys.Layout.Combat.ControlsGap, 0.0f, 512.0f)));
        }

        var unitGap = Mathf.RoundToInt(ResolvePresentationMetric(
            ModThemeMetricKeys.Layout.Combat.UnitGap, 0.0f, 512.0f));
        _combatLeftUnits?.AddThemeConstantOverride("separation", unitGap);
        _combatRightUnits?.AddThemeConstantOverride("separation", unitGap);
    }

    private void RemoveFontSizeOverrideWhenThemed(Label label, string sizeToken)
    {
        if (_modTheme?.FontSizes.ContainsKey(sizeToken) == true)
            label.RemoveThemeFontSizeOverride("font_size");
    }
}
