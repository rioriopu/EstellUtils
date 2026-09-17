using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;

namespace EstellUtils.UI.Widgets;

/// <summary>
/// 既定のウィジェット描画。テーマのトークンだけを使って描く。
/// </summary>
/// <remarks>
/// すべてのメソッドが <c>virtual</c> なので、継承して一部だけ差し替えられる。
/// <code>
/// public sealed class MyPainter : DefaultWidgetPainter
/// {
///     public override void DrawButton(in WidgetVisual v, ReadOnlySpan&lt;char&gt; label, ButtonStyle style)
///     {
///         // 独自の見た目
///     }
/// }
/// theme.Painter = new MyPainter();
/// </code>
/// </remarks>
public class DefaultWidgetPainter : IWidgetPainter
{
    /// <summary>現在のテーマ。</summary>
    protected static Theme Theme => ThemeManager.Current;

    /// <summary>現在のテーマの色。</summary>
    protected static ThemeColors Colors => ThemeManager.Current.Colors;

    /// <summary>現在のテーマの寸法。</summary>
    protected static ThemeMetrics Metrics => ThemeManager.Current.Metrics;

    /// <inheritdoc/>
    public virtual void DrawButton(in WidgetVisual visual, ReadOnlySpan<char> label, ButtonStyle style)
    {
        var rect = visual.Rect;
        var rounding = Metrics.WidgetRounding;

        // 押し込んだときに 1px 沈ませて、触った手応えを出す
        var sink = visual.Press * 1f;
        var body = rect.Offset(0f, sink);

        switch (style)
        {
            case ButtonStyle.Ghost:
            {
                var bg = EuColor.WithAlpha(Colors.SurfaceHover, 0.0f + (visual.Hover * 0.85f));
                Painter.Rect(body, bg, rounding);
                break;
            }

            case ButtonStyle.Link:
                break;

            default:
            {
                var (top, bottom, border) = this.ResolveButtonColors(visual, style);
                Painter.RectGradientV(body, top, bottom, rounding);
                Painter.RectOutline(body, border, Metrics.WidgetBorderWidth, rounding);
                break;
            }
        }

        var textColor = this.ResolveButtonTextColor(visual, style);
        var textArea = body.Shrink(Metrics.WidgetPadding);

        if (style == ButtonStyle.Link)
        {
            var size = TextPainter.Measure(label);
            var placed = textArea.Place(size, Align.Center, Align.Center);
            TextPainter.TextIn(textArea, textColor, label, Align.Center, Align.Center);

            // ホバー中だけ下線を引く
            if (visual.Hover > 0.01f)
            {
                Painter.HLine(
                    placed.Min.X, placed.Max.X, placed.Max.Y,
                    EuColor.ScaleAlpha(textColor, visual.Hover));
            }

            return;
        }

        TextPainter.TextIn(textArea, textColor, label, Align.Center, Align.Center);
    }

    /// <summary>ボタンの地と枠の色を決める。</summary>
    protected virtual (uint Top, uint Bottom, uint Border) ResolveButtonColors(
        in WidgetVisual visual, ButtonStyle style)
    {
        uint top;
        uint bottom;

        switch (style)
        {
            case ButtonStyle.Primary:
                top = EuColor.Lerp(Colors.Accent, Colors.AccentHover, visual.Hover);
                top = EuColor.Lerp(top, Colors.AccentActive, visual.Press);
                bottom = EuColor.Darken(top, 0.22f);
                break;

            case ButtonStyle.Danger:
                top = EuColor.Lerp(Colors.Danger, EuColor.Lighten(Colors.Danger, 0.15f), visual.Hover);
                top = EuColor.Lerp(top, EuColor.Darken(Colors.Danger, 0.15f), visual.Press);
                bottom = EuColor.Darken(top, 0.25f);
                break;

            default:
                top = EuColor.Lerp(Colors.WidgetTop, Colors.WidgetHoverTop, visual.Hover);
                bottom = EuColor.Lerp(Colors.WidgetBottom, Colors.WidgetHoverBottom, visual.Hover);

                // 押下時は上下を入れ替えて凹んで見せる
                var pressedTop = Colors.WidgetActiveTop;
                var pressedBottom = Colors.WidgetActiveBottom;
                top = EuColor.Lerp(top, pressedTop, visual.Press);
                bottom = EuColor.Lerp(bottom, pressedBottom, visual.Press);
                break;
        }

        var border = EuColor.Lerp(Colors.WidgetBorder, Colors.WidgetBorderHover, visual.Hover);

        if (visual.Disabled > 0f)
        {
            top = EuColor.Lerp(top, Colors.WidgetDisabled, visual.Disabled);
            bottom = EuColor.Lerp(bottom, Colors.WidgetDisabled, visual.Disabled);
            border = EuColor.ScaleAlpha(border, 1f - (visual.Disabled * 0.6f));
        }

        return (top, bottom, border);
    }

    /// <summary>ボタンの文字色を決める。</summary>
    protected virtual uint ResolveButtonTextColor(in WidgetVisual visual, ButtonStyle style)
    {
        var color = style switch
        {
            ButtonStyle.Primary => Colors.TextOnAccent,
            ButtonStyle.Danger => Colors.TextOnAccent,
            ButtonStyle.Link => EuColor.Lerp(Colors.TextLink, EuColor.Lighten(Colors.TextLink, 0.3f), visual.Hover),
            _ => Colors.Text,
        };

        return visual.Disabled > 0f ? EuColor.Lerp(color, Colors.TextDisabled, visual.Disabled) : color;
    }

    /// <inheritdoc/>
    public virtual void DrawCheckbox(in WidgetVisual visual)
    {
        var rect = visual.Rect;
        var rounding = MathF.Min(Metrics.WidgetRounding, 3f);

        var top = EuColor.Lerp(Colors.Track, Colors.SurfaceHover, visual.Hover * 0.6f);
        var bottom = EuColor.Darken(top, 0.15f);

        // ON のときはアクセント色で塗り、チェックを描き込む
        if (visual.OnAmount > 0f)
        {
            top = EuColor.Lerp(top, Colors.Accent, visual.OnAmount);
            bottom = EuColor.Lerp(bottom, EuColor.Darken(Colors.Accent, 0.2f), visual.OnAmount);
        }

        if (visual.Disabled > 0f)
        {
            top = EuColor.Lerp(top, Colors.WidgetDisabled, visual.Disabled);
            bottom = EuColor.Lerp(bottom, Colors.WidgetDisabled, visual.Disabled);
        }

        Painter.RectGradientV(rect, top, bottom, rounding);

        var border = EuColor.Lerp(Colors.WidgetBorder, Colors.WidgetBorderHover, visual.Hover);
        Painter.RectOutline(rect, border, Metrics.WidgetBorderWidth, rounding);

        if (visual.OnAmount > 0.01f)
        {
            var mark = visual.Disabled > 0f
                ? EuColor.Lerp(Colors.Checkmark, Colors.TextDisabled, visual.Disabled)
                : Colors.Checkmark;

            Painter.Check(rect, mark, MathF.Max(1.6f, rect.Width * 0.13f), visual.OnAmount);
        }
    }

    /// <inheritdoc/>
    public virtual void DrawRadio(in WidgetVisual visual)
    {
        var rect = visual.Rect;
        var center = rect.Center;
        var radius = MathF.Min(rect.Width, rect.Height) * 0.5f;

        var fill = EuColor.Lerp(Colors.Track, Colors.SurfaceHover, visual.Hover * 0.6f);
        if (visual.Disabled > 0f)
            fill = EuColor.Lerp(fill, Colors.WidgetDisabled, visual.Disabled);

        Painter.Circle(center, radius, fill);

        var border = EuColor.Lerp(Colors.WidgetBorder, Colors.WidgetBorderHover, visual.Hover);
        Painter.CircleOutline(center, radius, border, Metrics.WidgetBorderWidth);

        if (visual.OnAmount > 0.01f)
        {
            var dot = visual.Disabled > 0f
                ? EuColor.Lerp(Colors.Accent, Colors.TextDisabled, visual.Disabled)
                : Colors.Accent;

            Painter.Circle(center, radius * 0.5f * visual.OnAmount, dot);
        }
    }

    /// <inheritdoc/>
    public virtual void DrawToggle(in WidgetVisual visual)
    {
        var rect = visual.Rect;
        var radius = rect.Height * 0.5f;

        var offColor = EuColor.Lerp(Colors.Track, Colors.SurfaceHover, visual.Hover * 0.5f);
        var onColor = EuColor.Lerp(Colors.Accent, Colors.AccentHover, visual.Hover);
        var track = EuColor.Lerp(offColor, onColor, visual.OnAmount);

        if (visual.Disabled > 0f)
            track = EuColor.Lerp(track, Colors.WidgetDisabled, visual.Disabled);

        Painter.Rect(rect, track, radius);
        Painter.RectOutline(rect, Colors.WidgetBorder, Metrics.WidgetBorderWidth, radius);

        // つまみは左端から右端へスライドする
        var knobRadius = radius - 2.5f;
        var travel = rect.Width - (knobRadius * 2f) - 5f;
        var knobCenter = new Vector2(
            rect.Min.X + 2.5f + knobRadius + (travel * visual.OnAmount),
            rect.Center.Y);

        var knobColor = visual.Disabled > 0f
            ? EuColor.Lerp(Colors.Knob, Colors.TextDisabled, visual.Disabled)
            : Colors.Knob;

        Painter.Circle(knobCenter, knobRadius, knobColor);
        Painter.CircleOutline(knobCenter, knobRadius, Colors.KnobBorder, 1f);
    }

    /// <inheritdoc/>
    public virtual void DrawSlider(in WidgetVisual visual, Rect knob)
    {
        var rect = visual.Rect;
        var trackHeight = Metrics.SliderTrackHeight;
        var trackRect = Rect.FromCenter(
            new Vector2(rect.Center.X, rect.Center.Y),
            new Vector2(rect.Width, trackHeight));

        var rounding = trackHeight * 0.5f;

        Painter.Rect(trackRect, Colors.Track, rounding);

        // 埋まっている部分はつまみの中心まで
        var fillRect = new Rect(trackRect.Min, new Vector2(knob.Center.X, trackRect.Max.Y));
        var fill = visual.Disabled > 0f
            ? EuColor.Lerp(Colors.TrackFill, Colors.WidgetDisabled, visual.Disabled)
            : Colors.TrackFill;

        if (!fillRect.IsEmpty)
            Painter.Rect(fillRect, fill, rounding);

        Painter.RectOutline(trackRect, Colors.WidgetBorder, 1f, rounding);

        var knobColor = EuColor.Lerp(Colors.Knob, EuColor.Lighten(Colors.Knob, 0.25f), visual.Hover);
        if (visual.Disabled > 0f)
            knobColor = EuColor.Lerp(knobColor, Colors.WidgetDisabled, visual.Disabled);

        var knobRadius = knob.Width * 0.5f;

        // 掴んでいる間は軽く光らせる
        if (visual.Press > 0.01f)
            Painter.Circle(knob.Center, knobRadius + (3f * visual.Press), EuColor.WithAlpha(Colors.Accent, 0.25f * visual.Press));

        Painter.Circle(knob.Center, knobRadius, knobColor);
        Painter.CircleOutline(knob.Center, knobRadius, Colors.KnobBorder, 1f);
    }

    /// <inheritdoc/>
    public virtual void DrawProgressBar(in WidgetVisual visual, ReadOnlySpan<char> overlay)
    {
        var rect = visual.Rect;
        var rounding = Metrics.WidgetRounding;

        Painter.Rect(rect, Colors.Track, rounding);

        var value = Math.Clamp(visual.Value, 0f, 1f);
        if (value > 0f)
        {
            var fillRect = rect.WithWidth(rect.Width * value);
            Painter.RectGradientV(
                fillRect,
                EuColor.Lighten(Colors.TrackFill, 0.15f),
                Colors.TrackFill,
                rounding);
        }

        Painter.RectOutline(rect, Colors.WidgetBorder, Metrics.WidgetBorderWidth, rounding);

        if (!overlay.IsEmpty)
            TextPainter.TextIn(rect, Colors.Text, overlay, Align.Center, Align.Center);
    }

    /// <inheritdoc/>
    public virtual void DrawCard(in WidgetVisual visual)
    {
        var rect = visual.Rect;
        var rounding = Metrics.CardRounding;

        var fill = EuColor.Lerp(Colors.Surface, Colors.SurfaceHover, visual.Hover);
        Painter.Rect(rect, fill, rounding);
        Painter.RectOutline(rect, Colors.SurfaceBorder, 1f, rounding);
    }

    /// <inheritdoc/>
    public virtual void DrawSectionHeader(in WidgetVisual visual, ReadOnlySpan<char> label, bool collapsible)
    {
        var rect = visual.Rect;
        var textRect = rect;

        if (collapsible)
        {
            // 左端にシェブロン。開閉に合わせて 90 度回す代わりに向きを切り替える
            var arrowRect = rect.CutLeft(rect.Height, out textRect);
            var arrowColor = EuColor.Lerp(Colors.TextMuted, Colors.Accent, visual.Hover);

            Painter.Chevron(
                arrowRect,
                visual.OnAmount > 0.5f ? Direction.Down : Direction.Right,
                arrowColor,
                1.8f);
        }

        var color = EuColor.Lerp(Colors.TextHeading, Colors.Accent, visual.Hover * 0.5f);
        TextPainter.TextIn(textRect, color, label, Align.Start, Align.Center);

        // 見出しの右側へ余った幅いっぱいに細い線を引く
        var labelWidth = TextPainter.Measure(label).X;
        var lineStart = textRect.Min.X + labelWidth + Metrics.SpacingMd;

        if (lineStart < rect.Max.X - 4f)
            Painter.HLine(lineStart, rect.Max.X, rect.Center.Y, Colors.Separator);
    }

    /// <inheritdoc/>
    public virtual void DrawSeparator(Rect rect, ReadOnlySpan<char> label)
    {
        var y = rect.Center.Y;

        if (label.IsEmpty)
        {
            Painter.HLine(rect.Min.X, rect.Max.X, y, Colors.Separator, Metrics.SeparatorThickness);
            return;
        }

        var size = TextPainter.Measure(label);
        var gap = Metrics.SpacingMd;
        var textX = rect.Min.X + Metrics.SpacingLg;

        Painter.HLine(rect.Min.X, textX - gap, y, Colors.Separator, Metrics.SeparatorThickness);
        TextPainter.Text(new Vector2(textX, y - (size.Y * 0.5f)), Colors.TextMuted, label);
        Painter.HLine(textX + size.X + gap, rect.Max.X, y, Colors.Separator, Metrics.SeparatorThickness);
    }

    /// <inheritdoc/>
    public virtual void DrawTab(in WidgetVisual visual, ReadOnlySpan<char> label)
    {
        var rect = visual.Rect;
        var rounding = Metrics.WidgetRounding;

        if (visual.On)
        {
            Painter.RectGradientV(rect, Colors.WidgetHoverTop, Colors.WidgetTop, rounding, Corners.Top);
            Painter.RectOutline(rect, Colors.WidgetBorderHover, 1f, rounding, Corners.Top);

            // 選択中のタブは下端にアクセントの線を引く
            Painter.Rect(
                Rect.FromSize(new Vector2(rect.Min.X, rect.Max.Y - 2f), new Vector2(rect.Width, 2f)),
                Colors.Accent);
        }
        else
        {
            var bg = EuColor.WithAlpha(Colors.SurfaceHover, visual.Hover * 0.7f);
            Painter.Rect(rect, bg, rounding, Corners.Top);
        }

        var color = visual.On
            ? Colors.TextHeading
            : EuColor.Lerp(Colors.TextMuted, Colors.Text, visual.Hover);

        TextPainter.TextIn(rect, color, label, Align.Center, Align.Center);
    }

    /// <inheritdoc/>
    public virtual void DrawInputFrame(in WidgetVisual visual)
    {
        var rect = visual.Rect;
        var rounding = Metrics.WidgetRounding;

        var fill = EuColor.Lerp(Colors.Track, EuColor.Lighten(Colors.Track, 0.06f), visual.Hover);
        if (visual.Disabled > 0f)
            fill = EuColor.Lerp(fill, Colors.WidgetDisabled, visual.Disabled);

        Painter.Rect(rect, fill, rounding);

        var border = visual.Focused
            ? Colors.FocusRing
            : EuColor.Lerp(Colors.WidgetBorder, Colors.WidgetBorderHover, visual.Hover);

        Painter.RectOutline(rect, border, visual.Focused ? Metrics.FocusRingWidth : Metrics.WidgetBorderWidth, rounding);
    }

    /// <inheritdoc/>
    public virtual void DrawWindowChrome(Rect window, Rect titleBar, ReadOnlySpan<char> title, bool focused)
    {
        var rounding = Metrics.WindowRounding;

        // 影 → 地 → タイトルバー → 枠 の順に重ねる
        Painter.Shadow(window, Colors.Shadow, Metrics.WindowShadowSize, rounding, new Vector2(0f, 3f));
        Painter.RectGradientV(window, Colors.WindowTop, Colors.WindowBottom, rounding);

        if (!titleBar.IsEmpty)
        {
            if (focused)
                Painter.RectGradientV(titleBar, Colors.TitleTop, Colors.TitleBottom, rounding, Corners.Top);
            else
                Painter.Rect(titleBar, Colors.TitleInactive, rounding, Corners.Top);

            Painter.HLine(titleBar.Min.X, titleBar.Max.X, titleBar.Max.Y, Colors.TitleUnderline);

            var textRect = titleBar.Shrink(EdgeInsets.Symmetric(Metrics.SpacingMd, 0f));
            var titleColor = focused ? Colors.TitleText : EuColor.ScaleAlpha(Colors.TitleText, 0.6f);
            TextPainter.TextIn(textRect, titleColor, title, Align.Start, Align.Center);
        }

        // 内側に一本細い線を入れると、ゲーム UI に近い二重枠になる
        Painter.RectOutline(window.Shrink(1f), Colors.WindowBorderInner, 1f, MathF.Max(0f, rounding - 1f));
        Painter.RectOutline(window, Colors.WindowBorder, Metrics.WindowBorderWidth, rounding);
    }

    /// <inheritdoc/>
    public virtual void DrawWindowButton(in WidgetVisual visual, WindowButtonKind kind)
    {
        var rect = visual.Rect;

        if (visual.Hover > 0.01f)
        {
            var bg = kind == WindowButtonKind.Close
                ? EuColor.WithAlpha(Colors.Danger, visual.Hover * 0.85f)
                : EuColor.WithAlpha(Colors.SurfaceHover, visual.Hover * 0.85f);

            Painter.Rect(rect, bg, Metrics.WidgetRounding);
        }

        var color = EuColor.Lerp(Colors.TextMuted, Colors.Text, visual.Hover);
        var center = rect.Center;
        var size = MathF.Min(rect.Width, rect.Height) * 0.22f;

        switch (kind)
        {
            case WindowButtonKind.Close:
                Painter.Line(center - new Vector2(size, size), center + new Vector2(size, size), color, 1.6f);
                Painter.Line(center + new Vector2(size, -size), center + new Vector2(-size, size), color, 1.6f);
                break;

            case WindowButtonKind.Collapse:
                Painter.Chevron(rect, visual.On ? Direction.Up : Direction.Down, color, 1.6f);
                break;

            default:
                Painter.CircleOutline(center, size, color, 1.6f);
                Painter.Circle(center, size * 0.35f, color);
                break;
        }
    }

    /// <inheritdoc/>
    public virtual void DrawResizeGrip(in WidgetVisual visual)
    {
        var rect = visual.Rect;
        var color = EuColor.Lerp(Colors.WindowBorderInner, Colors.Accent, visual.Hover);

        // 右下に斜めの線を 3 本引く
        for (var i = 1; i <= 3; i++)
        {
            var offset = i * (rect.Width / 4f);
            Painter.Line(
                new Vector2(rect.Max.X - offset, rect.Max.Y - 2f),
                new Vector2(rect.Max.X - 2f, rect.Max.Y - offset),
                color,
                1.2f);
        }
    }
}
