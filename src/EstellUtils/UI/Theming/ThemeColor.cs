using System;

namespace EstellUtils.UI.Theming;

/// <summary>
/// テーマの色の役割。
/// </summary>
/// <remarks>
/// 1 色だけ一時的に差し替えるとき (<c>EUi.PushColor</c>) に、どの色かを指すのに使う。
/// </remarks>
public enum ThemeColor
{
    /// <summary>WindowTop。</summary>
    WindowTop,
    /// <summary>WindowBottom。</summary>
    WindowBottom,
    /// <summary>WindowBorder。</summary>
    WindowBorder,
    /// <summary>WindowBorderInner。</summary>
    WindowBorderInner,
    /// <summary>WindowGloss。</summary>
    WindowGloss,
    /// <summary>TitleTop。</summary>
    TitleTop,
    /// <summary>TitleBottom。</summary>
    TitleBottom,
    /// <summary>TitleInactive。</summary>
    TitleInactive,
    /// <summary>TitleText。</summary>
    TitleText,
    /// <summary>TitleUnderline。</summary>
    TitleUnderline,
    /// <summary>Surface。</summary>
    Surface,
    /// <summary>SurfaceHover。</summary>
    SurfaceHover,
    /// <summary>SurfaceActive。</summary>
    SurfaceActive,
    /// <summary>SurfaceBorder。</summary>
    SurfaceBorder,
    /// <summary>WidgetTop。</summary>
    WidgetTop,
    /// <summary>WidgetBottom。</summary>
    WidgetBottom,
    /// <summary>WidgetHoverTop。</summary>
    WidgetHoverTop,
    /// <summary>WidgetHoverBottom。</summary>
    WidgetHoverBottom,
    /// <summary>WidgetActiveTop。</summary>
    WidgetActiveTop,
    /// <summary>WidgetActiveBottom。</summary>
    WidgetActiveBottom,
    /// <summary>WidgetBorder。</summary>
    WidgetBorder,
    /// <summary>WidgetBorderHover。</summary>
    WidgetBorderHover,
    /// <summary>WidgetDisabled。</summary>
    WidgetDisabled,
    /// <summary>Text。</summary>
    Text,
    /// <summary>TextMuted。</summary>
    TextMuted,
    /// <summary>TextDisabled。</summary>
    TextDisabled,
    /// <summary>TextHeading。</summary>
    TextHeading,
    /// <summary>TextOnAccent。</summary>
    TextOnAccent,
    /// <summary>TextLink。</summary>
    TextLink,
    /// <summary>Accent。</summary>
    Accent,
    /// <summary>AccentHover。</summary>
    AccentHover,
    /// <summary>AccentActive。</summary>
    AccentActive,
    /// <summary>AccentMuted。</summary>
    AccentMuted,
    /// <summary>Success。</summary>
    Success,
    /// <summary>Warning。</summary>
    Warning,
    /// <summary>Danger。</summary>
    Danger,
    /// <summary>Info。</summary>
    Info,
    /// <summary>Track。</summary>
    Track,
    /// <summary>TrackFill。</summary>
    TrackFill,
    /// <summary>Knob。</summary>
    Knob,
    /// <summary>KnobBorder。</summary>
    KnobBorder,
    /// <summary>Checkmark。</summary>
    Checkmark,
    /// <summary>Separator。</summary>
    Separator,
    /// <summary>ScrollbarTrack。</summary>
    ScrollbarTrack,
    /// <summary>ScrollbarGrab。</summary>
    ScrollbarGrab,
    /// <summary>ScrollbarGrabHover。</summary>
    ScrollbarGrabHover,
    /// <summary>Selection。</summary>
    Selection,
    /// <summary>FocusRing。</summary>
    FocusRing,
    /// <summary>Shadow。</summary>
    Shadow,
    /// <summary>Overlay。</summary>
    Overlay,
    /// <summary>TooltipBackground。</summary>
    TooltipBackground,
    /// <summary>TooltipBorder。</summary>
    TooltipBorder,
}

/// <summary>
/// 役割と <see cref="ThemeColors"/> のプロパティを対応づける。
/// </summary>
/// <remarks>
/// 反射を使うと毎フレームの負荷になるので、switch で引く。
/// </remarks>
internal static class ThemeColorRoles
{
    /// <summary>すべての役割。</summary>
    internal static readonly ThemeColor[] All =
    [
        ThemeColor.WindowTop,
        ThemeColor.WindowBottom,
        ThemeColor.WindowBorder,
        ThemeColor.WindowBorderInner,
        ThemeColor.WindowGloss,
        ThemeColor.TitleTop,
        ThemeColor.TitleBottom,
        ThemeColor.TitleInactive,
        ThemeColor.TitleText,
        ThemeColor.TitleUnderline,
        ThemeColor.Surface,
        ThemeColor.SurfaceHover,
        ThemeColor.SurfaceActive,
        ThemeColor.SurfaceBorder,
        ThemeColor.WidgetTop,
        ThemeColor.WidgetBottom,
        ThemeColor.WidgetHoverTop,
        ThemeColor.WidgetHoverBottom,
        ThemeColor.WidgetActiveTop,
        ThemeColor.WidgetActiveBottom,
        ThemeColor.WidgetBorder,
        ThemeColor.WidgetBorderHover,
        ThemeColor.WidgetDisabled,
        ThemeColor.Text,
        ThemeColor.TextMuted,
        ThemeColor.TextDisabled,
        ThemeColor.TextHeading,
        ThemeColor.TextOnAccent,
        ThemeColor.TextLink,
        ThemeColor.Accent,
        ThemeColor.AccentHover,
        ThemeColor.AccentActive,
        ThemeColor.AccentMuted,
        ThemeColor.Success,
        ThemeColor.Warning,
        ThemeColor.Danger,
        ThemeColor.Info,
        ThemeColor.Track,
        ThemeColor.TrackFill,
        ThemeColor.Knob,
        ThemeColor.KnobBorder,
        ThemeColor.Checkmark,
        ThemeColor.Separator,
        ThemeColor.ScrollbarTrack,
        ThemeColor.ScrollbarGrab,
        ThemeColor.ScrollbarGrabHover,
        ThemeColor.Selection,
        ThemeColor.FocusRing,
        ThemeColor.Shadow,
        ThemeColor.Overlay,
        ThemeColor.TooltipBackground,
        ThemeColor.TooltipBorder
    ];

    /// <summary>役割に対応する色を読む。</summary>
    internal static uint Get(ThemeColors colors, ThemeColor role)
        => role switch
        {
            ThemeColor.WindowTop => colors.WindowTop,
            ThemeColor.WindowBottom => colors.WindowBottom,
            ThemeColor.WindowBorder => colors.WindowBorder,
            ThemeColor.WindowBorderInner => colors.WindowBorderInner,
            ThemeColor.WindowGloss => colors.WindowGloss,
            ThemeColor.TitleTop => colors.TitleTop,
            ThemeColor.TitleBottom => colors.TitleBottom,
            ThemeColor.TitleInactive => colors.TitleInactive,
            ThemeColor.TitleText => colors.TitleText,
            ThemeColor.TitleUnderline => colors.TitleUnderline,
            ThemeColor.Surface => colors.Surface,
            ThemeColor.SurfaceHover => colors.SurfaceHover,
            ThemeColor.SurfaceActive => colors.SurfaceActive,
            ThemeColor.SurfaceBorder => colors.SurfaceBorder,
            ThemeColor.WidgetTop => colors.WidgetTop,
            ThemeColor.WidgetBottom => colors.WidgetBottom,
            ThemeColor.WidgetHoverTop => colors.WidgetHoverTop,
            ThemeColor.WidgetHoverBottom => colors.WidgetHoverBottom,
            ThemeColor.WidgetActiveTop => colors.WidgetActiveTop,
            ThemeColor.WidgetActiveBottom => colors.WidgetActiveBottom,
            ThemeColor.WidgetBorder => colors.WidgetBorder,
            ThemeColor.WidgetBorderHover => colors.WidgetBorderHover,
            ThemeColor.WidgetDisabled => colors.WidgetDisabled,
            ThemeColor.Text => colors.Text,
            ThemeColor.TextMuted => colors.TextMuted,
            ThemeColor.TextDisabled => colors.TextDisabled,
            ThemeColor.TextHeading => colors.TextHeading,
            ThemeColor.TextOnAccent => colors.TextOnAccent,
            ThemeColor.TextLink => colors.TextLink,
            ThemeColor.Accent => colors.Accent,
            ThemeColor.AccentHover => colors.AccentHover,
            ThemeColor.AccentActive => colors.AccentActive,
            ThemeColor.AccentMuted => colors.AccentMuted,
            ThemeColor.Success => colors.Success,
            ThemeColor.Warning => colors.Warning,
            ThemeColor.Danger => colors.Danger,
            ThemeColor.Info => colors.Info,
            ThemeColor.Track => colors.Track,
            ThemeColor.TrackFill => colors.TrackFill,
            ThemeColor.Knob => colors.Knob,
            ThemeColor.KnobBorder => colors.KnobBorder,
            ThemeColor.Checkmark => colors.Checkmark,
            ThemeColor.Separator => colors.Separator,
            ThemeColor.ScrollbarTrack => colors.ScrollbarTrack,
            ThemeColor.ScrollbarGrab => colors.ScrollbarGrab,
            ThemeColor.ScrollbarGrabHover => colors.ScrollbarGrabHover,
            ThemeColor.Selection => colors.Selection,
            ThemeColor.FocusRing => colors.FocusRing,
            ThemeColor.Shadow => colors.Shadow,
            ThemeColor.Overlay => colors.Overlay,
            ThemeColor.TooltipBackground => colors.TooltipBackground,
            ThemeColor.TooltipBorder => colors.TooltipBorder,
            _ => colors.Text,
        };

    /// <summary>役割に対応する色を書く。</summary>
    internal static void Set(ThemeColors colors, ThemeColor role, uint value)
    {
        switch (role)
        {
            case ThemeColor.WindowTop:
                colors.WindowTop = value;
                break;

            case ThemeColor.WindowBottom:
                colors.WindowBottom = value;
                break;

            case ThemeColor.WindowBorder:
                colors.WindowBorder = value;
                break;

            case ThemeColor.WindowBorderInner:
                colors.WindowBorderInner = value;
                break;

            case ThemeColor.WindowGloss:
                colors.WindowGloss = value;
                break;

            case ThemeColor.TitleTop:
                colors.TitleTop = value;
                break;

            case ThemeColor.TitleBottom:
                colors.TitleBottom = value;
                break;

            case ThemeColor.TitleInactive:
                colors.TitleInactive = value;
                break;

            case ThemeColor.TitleText:
                colors.TitleText = value;
                break;

            case ThemeColor.TitleUnderline:
                colors.TitleUnderline = value;
                break;

            case ThemeColor.Surface:
                colors.Surface = value;
                break;

            case ThemeColor.SurfaceHover:
                colors.SurfaceHover = value;
                break;

            case ThemeColor.SurfaceActive:
                colors.SurfaceActive = value;
                break;

            case ThemeColor.SurfaceBorder:
                colors.SurfaceBorder = value;
                break;

            case ThemeColor.WidgetTop:
                colors.WidgetTop = value;
                break;

            case ThemeColor.WidgetBottom:
                colors.WidgetBottom = value;
                break;

            case ThemeColor.WidgetHoverTop:
                colors.WidgetHoverTop = value;
                break;

            case ThemeColor.WidgetHoverBottom:
                colors.WidgetHoverBottom = value;
                break;

            case ThemeColor.WidgetActiveTop:
                colors.WidgetActiveTop = value;
                break;

            case ThemeColor.WidgetActiveBottom:
                colors.WidgetActiveBottom = value;
                break;

            case ThemeColor.WidgetBorder:
                colors.WidgetBorder = value;
                break;

            case ThemeColor.WidgetBorderHover:
                colors.WidgetBorderHover = value;
                break;

            case ThemeColor.WidgetDisabled:
                colors.WidgetDisabled = value;
                break;

            case ThemeColor.Text:
                colors.Text = value;
                break;

            case ThemeColor.TextMuted:
                colors.TextMuted = value;
                break;

            case ThemeColor.TextDisabled:
                colors.TextDisabled = value;
                break;

            case ThemeColor.TextHeading:
                colors.TextHeading = value;
                break;

            case ThemeColor.TextOnAccent:
                colors.TextOnAccent = value;
                break;

            case ThemeColor.TextLink:
                colors.TextLink = value;
                break;

            case ThemeColor.Accent:
                colors.Accent = value;
                break;

            case ThemeColor.AccentHover:
                colors.AccentHover = value;
                break;

            case ThemeColor.AccentActive:
                colors.AccentActive = value;
                break;

            case ThemeColor.AccentMuted:
                colors.AccentMuted = value;
                break;

            case ThemeColor.Success:
                colors.Success = value;
                break;

            case ThemeColor.Warning:
                colors.Warning = value;
                break;

            case ThemeColor.Danger:
                colors.Danger = value;
                break;

            case ThemeColor.Info:
                colors.Info = value;
                break;

            case ThemeColor.Track:
                colors.Track = value;
                break;

            case ThemeColor.TrackFill:
                colors.TrackFill = value;
                break;

            case ThemeColor.Knob:
                colors.Knob = value;
                break;

            case ThemeColor.KnobBorder:
                colors.KnobBorder = value;
                break;

            case ThemeColor.Checkmark:
                colors.Checkmark = value;
                break;

            case ThemeColor.Separator:
                colors.Separator = value;
                break;

            case ThemeColor.ScrollbarTrack:
                colors.ScrollbarTrack = value;
                break;

            case ThemeColor.ScrollbarGrab:
                colors.ScrollbarGrab = value;
                break;

            case ThemeColor.ScrollbarGrabHover:
                colors.ScrollbarGrabHover = value;
                break;

            case ThemeColor.Selection:
                colors.Selection = value;
                break;

            case ThemeColor.FocusRing:
                colors.FocusRing = value;
                break;

            case ThemeColor.Shadow:
                colors.Shadow = value;
                break;

            case ThemeColor.Overlay:
                colors.Overlay = value;
                break;

            case ThemeColor.TooltipBackground:
                colors.TooltipBackground = value;
                break;

            case ThemeColor.TooltipBorder:
                colors.TooltipBorder = value;
                break;
        }
    }
}
