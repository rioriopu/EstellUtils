using EstellUtils.UI.Render;

namespace EstellUtils.UI.Theming.Presets;

/// <summary>
/// FFXIV のネイティブ UI に寄せた既定テーマ。
/// </summary>
/// <remarks>
/// ゲーム本体のウィンドウが持つ「黒に近い紺の半透明地」「金ベージュの細枠」
/// 「金属質の縦グラデーション」を、外部テクスチャに依存せず手描きで再現する。
/// より忠実にしたい場合は、ゲームの uld テクスチャを読み込んで
/// <see cref="Painter.NineSlice"/> で描く経路へ差し替えられる。
/// </remarks>
public static class XivNativeTheme
{
    /// <summary>既定テーマを生成する。</summary>
    public static Theme Create()
    {
        var colors = new ThemeColors
        {
            // ウィンドウ: ごくわずかに上が明るい紺。半透明でゲーム画面を透かす
            WindowTop = EuColor.Rgb(0x121820, 0.94f),
            WindowBottom = EuColor.Rgb(0x0B0F15, 0.94f),
            WindowBorder = EuColor.Rgb(0x9A8B63, 1f),
            WindowBorderInner = EuColor.Rgb(0x2A3340, 0.9f),

            TitleTop = EuColor.Rgb(0x2B3648, 1f),
            TitleBottom = EuColor.Rgb(0x141C28, 1f),
            TitleInactive = EuColor.Rgb(0x171D26, 1f),
            TitleText = EuColor.Rgb(0xF2ECDF, 1f),
            TitleUnderline = EuColor.Rgb(0x9A8B63, 0.7f),

            Surface = EuColor.Rgb(0x18202B, 0.85f),
            SurfaceHover = EuColor.Rgb(0x1F2936, 0.9f),
            SurfaceActive = EuColor.Rgb(0x25313F, 0.95f),
            SurfaceBorder = EuColor.Rgb(0x2E3A49, 0.9f),

            // ウィジェット: 上が明るい金属質のグラデ。押下時は上下を反転させて凹ませる
            WidgetTop = EuColor.Rgb(0x39445A, 1f),
            WidgetBottom = EuColor.Rgb(0x222B3B, 1f),
            WidgetHoverTop = EuColor.Rgb(0x4A5670, 1f),
            WidgetHoverBottom = EuColor.Rgb(0x2D3849, 1f),
            WidgetActiveTop = EuColor.Rgb(0x1E2734, 1f),
            WidgetActiveBottom = EuColor.Rgb(0x2B3547, 1f),
            WidgetBorder = EuColor.Rgb(0x55607A, 0.85f),
            WidgetBorderHover = EuColor.Rgb(0xC0A972, 1f),
            WidgetDisabled = EuColor.Rgb(0x1A1F28, 0.7f),

            Text = EuColor.Rgb(0xEFE9DC, 1f),
            TextMuted = EuColor.Rgb(0x9AA0AB, 1f),
            TextDisabled = EuColor.Rgb(0x5C636E, 1f),
            TextHeading = EuColor.Rgb(0xE6CF9B, 1f),
            TextOnAccent = EuColor.Rgb(0x1A1408, 1f),
            TextLink = EuColor.Rgb(0x7FB6E8, 1f),

            Accent = EuColor.Rgb(0xD8B368, 1f),
            AccentHover = EuColor.Rgb(0xEBC97F, 1f),
            AccentActive = EuColor.Rgb(0xB9954F, 1f),
            AccentMuted = EuColor.Rgb(0xD8B368, 0.25f),

            Success = EuColor.Rgb(0x6FC177, 1f),
            Warning = EuColor.Rgb(0xE3B14C, 1f),
            Danger = EuColor.Rgb(0xD96A5E, 1f),
            Info = EuColor.Rgb(0x6FA8DC, 1f),

            Track = EuColor.Rgb(0x12181F, 0.9f),
            TrackFill = EuColor.Rgb(0xC0A972, 1f),
            Knob = EuColor.Rgb(0xE8E2D4, 1f),
            KnobBorder = EuColor.Rgb(0x8A7B55, 1f),
            Checkmark = EuColor.Rgb(0xE6CF9B, 1f),
            Separator = EuColor.Rgb(0x36404F, 0.8f),

            ScrollbarTrack = EuColor.Rgb(0x0E131A, 0.6f),
            ScrollbarGrab = EuColor.Rgb(0x49546A, 0.9f),
            ScrollbarGrabHover = EuColor.Rgb(0x6C7996, 1f),

            Selection = EuColor.Rgb(0x3E6FA8, 0.45f),
            FocusRing = EuColor.Rgb(0xD8B368, 0.9f),
            Shadow = EuColor.Rgb(0x000000, 0.55f),
            Overlay = EuColor.Rgb(0x05080C, 0.6f),

            TooltipBackground = EuColor.Rgb(0x10161E, 0.97f),
            TooltipBorder = EuColor.Rgb(0x9A8B63, 0.85f),
        };

        // 寸法はゲーム UI に合わせてやや詰める。
        // 角丸は 1〜2px だと「丸めたつもりが角が立つ」中途半端な見え方になるため、
        // 丸みが分かる程度まで取る
        var metrics = new ThemeMetrics
        {
            WindowRounding = 5f,
            WidgetRounding = 4f,
            CardRounding = 5f,
            TooltipRounding = 4f,
        };

        var motion = new ThemeMotion();

        return new Theme("FFXIV ネイティブ風", colors, metrics, motion);
    }
}
