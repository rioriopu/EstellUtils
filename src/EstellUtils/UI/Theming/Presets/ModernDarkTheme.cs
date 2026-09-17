using EstellUtils.UI.Render;

namespace EstellUtils.UI.Theming.Presets;

/// <summary>
/// 装飾を抑えたモダンダークテーマ。情報量の多い画面向け。
/// </summary>
/// <remarks>
/// グラデーションをほぼ使わず、角丸を大きめに取る。ゲームの世界観よりも
/// 可読性と情報密度を優先したいプラグイン向けの代替テーマ。
/// </remarks>
public static class ModernDarkTheme
{
    /// <summary>テーマを生成する。</summary>
    public static Theme Create()
    {
        var colors = new ThemeColors
        {
            WindowTop = EuColor.Rgb(0x1A1C20, 0.96f),
            WindowBottom = EuColor.Rgb(0x16181C, 0.96f),
            WindowBorder = EuColor.Rgb(0x32363D, 1f),
            WindowBorderInner = EuColor.Rgb(0x23262B, 0f),

            TitleTop = EuColor.Rgb(0x22252A, 1f),
            TitleBottom = EuColor.Rgb(0x1C1F23, 1f),
            TitleInactive = EuColor.Rgb(0x1A1C20, 1f),
            TitleText = EuColor.Rgb(0xE8EAED, 1f),
            TitleUnderline = EuColor.Rgb(0x32363D, 1f),

            Surface = EuColor.Rgb(0x212429, 1f),
            SurfaceHover = EuColor.Rgb(0x282C32, 1f),
            SurfaceActive = EuColor.Rgb(0x2F343B, 1f),
            SurfaceBorder = EuColor.Rgb(0x32363D, 1f),

            WidgetTop = EuColor.Rgb(0x2C3037, 1f),
            WidgetBottom = EuColor.Rgb(0x2C3037, 1f),
            WidgetHoverTop = EuColor.Rgb(0x373C44, 1f),
            WidgetHoverBottom = EuColor.Rgb(0x373C44, 1f),
            WidgetActiveTop = EuColor.Rgb(0x23262B, 1f),
            WidgetActiveBottom = EuColor.Rgb(0x23262B, 1f),
            WidgetBorder = EuColor.Rgb(0x3C4149, 1f),
            WidgetBorderHover = EuColor.Rgb(0x4E545E, 1f),
            WidgetDisabled = EuColor.Rgb(0x1F2125, 1f),

            Text = EuColor.Rgb(0xE3E5E8, 1f),
            TextMuted = EuColor.Rgb(0x8B9198, 1f),
            TextDisabled = EuColor.Rgb(0x565B62, 1f),
            TextHeading = EuColor.Rgb(0xF2F4F6, 1f),
            TextOnAccent = EuColor.Rgb(0x0E1116, 1f),
            TextLink = EuColor.Rgb(0x6BAEF5, 1f),

            Accent = EuColor.Rgb(0x5B9BD5, 1f),
            AccentHover = EuColor.Rgb(0x74AEE3, 1f),
            AccentActive = EuColor.Rgb(0x4A84B8, 1f),
            AccentMuted = EuColor.Rgb(0x5B9BD5, 0.22f),

            Success = EuColor.Rgb(0x5BB974, 1f),
            Warning = EuColor.Rgb(0xE0A94A, 1f),
            Danger = EuColor.Rgb(0xE05B52, 1f),
            Info = EuColor.Rgb(0x5B9BD5, 1f),

            Track = EuColor.Rgb(0x16181C, 1f),
            TrackFill = EuColor.Rgb(0x5B9BD5, 1f),
            Knob = EuColor.Rgb(0xE3E5E8, 1f),
            KnobBorder = EuColor.Rgb(0x3C4149, 1f),
            Checkmark = EuColor.Rgb(0xFFFFFF, 1f),
            Separator = EuColor.Rgb(0x2C3037, 1f),

            ScrollbarTrack = EuColor.Rgb(0x16181C, 0.5f),
            ScrollbarGrab = EuColor.Rgb(0x3C4149, 1f),
            ScrollbarGrabHover = EuColor.Rgb(0x4E545E, 1f),

            Selection = EuColor.Rgb(0x5B9BD5, 0.35f),
            FocusRing = EuColor.Rgb(0x5B9BD5, 0.9f),
            Shadow = EuColor.Rgb(0x000000, 0.45f),
            Overlay = EuColor.Rgb(0x000000, 0.55f),

            TooltipBackground = EuColor.Rgb(0x2A2E34, 0.98f),
            TooltipBorder = EuColor.Rgb(0x3C4149, 1f),
        };

        var metrics = new ThemeMetrics
        {
            WindowRounding = 6f,
            WidgetRounding = 4f,
            CardRounding = 6f,
            TooltipRounding = 5f,
            WidgetHeight = 26f,
        };

        return new Theme("モダンダーク", colors, metrics, new ThemeMotion());
    }
}
