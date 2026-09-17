using EstellUtils.UI.Render;

namespace EstellUtils.UI.Theming.Presets;

/// <summary>
/// すりガラス風テーマ。
/// </summary>
/// <remarks>
/// <para>
/// <b>背景を実際にぼかしているわけではありません。</b>
/// ImGui はゲーム画面の上へ重ねて描くだけで、背後のピクセルを読み取れないため、
/// 本物のガウスぼかしは描画の仕組み上できません
/// (やるなら D3D のレンダーターゲットを取得してシェーダを掛ける必要があり、
/// UI ライブラリの範囲を超えます)。
/// </para>
/// <para>
/// 代わりに、透過を強めた地・上端の光沢・明るい細枠を組み合わせて、
/// 「厚みのある曇りガラスの板」に見えるように作ってあります。
/// ゲーム画面が透けるので、動きのある背景の上ではガラス越しのように見えます。
/// </para>
/// </remarks>
public static class FrostedGlassTheme
{
    /// <summary>テーマを生成する。</summary>
    public static Theme Create()
    {
        var colors = new ThemeColors
        {
            // 地は大きく透かす。背後が透けることでガラス感が出る
            WindowTop = EuColor.Rgb(0x1E2732, 0.62f),
            WindowBottom = EuColor.Rgb(0x0E141C, 0.70f),
            WindowBorder = EuColor.Rgb(0xC8D4E4, 0.55f),
            WindowBorderInner = EuColor.Rgb(0xFFFFFF, 0.10f),
            WindowGloss = EuColor.Rgb(0xFFFFFF, 0.10f),

            TitleTop = EuColor.Rgb(0x35424F, 0.75f),
            TitleBottom = EuColor.Rgb(0x1C2530, 0.75f),
            TitleInactive = EuColor.Rgb(0x1A222C, 0.7f),
            TitleText = EuColor.Rgb(0xF4F7FA, 1f),
            TitleUnderline = EuColor.Rgb(0xFFFFFF, 0.18f),

            Surface = EuColor.Rgb(0xFFFFFF, 0.06f),
            SurfaceHover = EuColor.Rgb(0xFFFFFF, 0.10f),
            SurfaceActive = EuColor.Rgb(0xFFFFFF, 0.14f),
            SurfaceBorder = EuColor.Rgb(0xFFFFFF, 0.12f),

            WidgetTop = EuColor.Rgb(0xFFFFFF, 0.16f),
            WidgetBottom = EuColor.Rgb(0xFFFFFF, 0.07f),
            WidgetHoverTop = EuColor.Rgb(0xFFFFFF, 0.26f),
            WidgetHoverBottom = EuColor.Rgb(0xFFFFFF, 0.12f),
            WidgetActiveTop = EuColor.Rgb(0x000000, 0.18f),
            WidgetActiveBottom = EuColor.Rgb(0xFFFFFF, 0.10f),
            WidgetBorder = EuColor.Rgb(0xFFFFFF, 0.20f),
            WidgetBorderHover = EuColor.Rgb(0xFFFFFF, 0.45f),
            WidgetDisabled = EuColor.Rgb(0x000000, 0.20f),

            Text = EuColor.Rgb(0xF2F5F8, 1f),
            TextMuted = EuColor.Rgb(0xB6C0CC, 1f),
            TextDisabled = EuColor.Rgb(0x7A8593, 1f),
            TextHeading = EuColor.Rgb(0xFFFFFF, 1f),
            TextOnAccent = EuColor.Rgb(0x10161E, 1f),
            TextLink = EuColor.Rgb(0x9CCBF5, 1f),

            Accent = EuColor.Rgb(0x8FC4F0, 1f),
            AccentHover = EuColor.Rgb(0xAFD8FA, 1f),
            AccentActive = EuColor.Rgb(0x6FA6D4, 1f),
            AccentMuted = EuColor.Rgb(0x8FC4F0, 0.22f),

            Success = EuColor.Rgb(0x86D79A, 1f),
            Warning = EuColor.Rgb(0xEFC97A, 1f),
            Danger = EuColor.Rgb(0xEC8B84, 1f),
            Info = EuColor.Rgb(0x8FC4F0, 1f),

            Track = EuColor.Rgb(0x000000, 0.28f),
            TrackFill = EuColor.Rgb(0x8FC4F0, 0.9f),
            Knob = EuColor.Rgb(0xFFFFFF, 0.95f),
            KnobBorder = EuColor.Rgb(0x000000, 0.25f),
            Checkmark = EuColor.Rgb(0x10161E, 1f),
            Separator = EuColor.Rgb(0xFFFFFF, 0.14f),

            ScrollbarTrack = EuColor.Rgb(0x000000, 0.20f),
            ScrollbarGrab = EuColor.Rgb(0xFFFFFF, 0.24f),
            ScrollbarGrabHover = EuColor.Rgb(0xFFFFFF, 0.40f),

            Selection = EuColor.Rgb(0x8FC4F0, 0.30f),
            FocusRing = EuColor.Rgb(0x8FC4F0, 0.9f),
            Shadow = EuColor.Rgb(0x000000, 0.45f),
            Overlay = EuColor.Rgb(0x05080C, 0.55f),

            TooltipBackground = EuColor.Rgb(0x141C26, 0.95f),
            TooltipBorder = EuColor.Rgb(0xFFFFFF, 0.25f),
        };

        var metrics = new ThemeMetrics
        {
            WindowRounding = 8f,
            WidgetRounding = 5f,
            CardRounding = 7f,
            TooltipRounding = 6f,
            WindowShadowSize = 14f,
        };

        return new Theme("すりガラス", colors, metrics, new ThemeMotion());
    }
}
