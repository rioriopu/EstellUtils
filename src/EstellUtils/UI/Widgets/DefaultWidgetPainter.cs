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

        // 押し込んだときは沈ませたうえで、ほんの少し縮める。
        // 位置と大きさの両方が動くと、指で押し込んだような手応えになる
        var sink = visual.Press * 1.5f;
        var body = rect.Offset(0f, sink).Shrink(visual.Press * 0.5f);

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

                // 押している間は内側に影を落として、へこんで見えるようにする
                if (visual.Press > 0.01f)
                {
                    Painter.InnerShadow(
                        body, EuColor.WithAlpha(EuColor.Black, 0.40f * visual.Press), 3f, rounding);
                }

                Painter.RectOutline(body, border, Metrics.WidgetBorderWidth, rounding);
                break;
            }
        }

        var textColor = this.ResolveButtonTextColor(visual, style);

        // 文字は沈み込みだけを追い、縮小は追わない。
        // ボタンの幅は文字にちょうど合わせて作られるので、地に合わせて縮めると
        // 押した瞬間にラベルが省略記号へ化けてしまう
        var textArea = rect.Offset(0f, sink).Shrink(Metrics.WidgetPadding);

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
        // 押している間はわずかに縮めて、反応していることを見せる
        var rect = visual.Rect.Shrink(visual.Press * 1.2f);
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
        var radius = (MathF.Min(rect.Width, rect.Height) * 0.5f) - (visual.Press * 1.2f);

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
        var rect = visual.Rect.Shrink(visual.Press * 1f);
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

        // 溝は細く、つまみは溝より高く。
        // 高さいっぱいを塗ると埋まり具合が主張しすぎて、画面の中で浮いてしまう
        var trackHeight = Metrics.SliderTrackHeight > 0f
            ? MathF.Min(Metrics.SliderTrackHeight, rect.Height)
            : rect.Height;

        var trackRect = Rect.FromCenter(rect.Center, new Vector2(rect.Width, trackHeight));
        var trackRounding = trackHeight * 0.5f;

        Painter.Rect(trackRect, Colors.Track, trackRounding);

        // 埋まっている部分はつまみの中心まで
        var fillWidth = knob.Center.X - trackRect.Min.X;

        if (fillWidth > 0.5f)
        {
            var fillRect = trackRect.WithWidth(fillWidth);

            var fillTop = EuColor.Lighten(Colors.TrackFill, 0.15f);
            var fillBottom = Colors.TrackFill;

            if (visual.Disabled > 0f)
            {
                fillTop = EuColor.Lerp(fillTop, Colors.WidgetDisabled, visual.Disabled);
                fillBottom = EuColor.Lerp(fillBottom, Colors.WidgetDisabled, visual.Disabled);
            }

            Painter.RectGradientV(fillRect, fillTop, fillBottom, trackRounding, Corners.Left);
        }

        Painter.RectOutline(trackRect, Colors.WidgetBorder, Metrics.WidgetBorderWidth, trackRounding);

        // つまみ。溝より高くして、掴む場所をはっきりさせる
        var knobHeight = MathF.Min(
            Metrics.SliderKnobHeight > 0f ? Metrics.SliderKnobHeight : rect.Height,
            rect.Height);

        var grow = visual.Hover * 1.5f;
        var knobRect = Rect.FromCenter(
            new Vector2(knob.Center.X, trackRect.Center.Y),
            new Vector2(knob.Width + grow, knobHeight + grow));

        var knobRounding = MathF.Min(Metrics.WidgetRounding, knobRect.Width * 0.5f);

        var knobTop = EuColor.Lighten(Colors.Knob, 0.12f + (visual.Hover * 0.15f));
        var knobBottom = EuColor.Darken(Colors.Knob, 0.10f);

        if (visual.Disabled > 0f)
        {
            knobTop = EuColor.Lerp(knobTop, Colors.WidgetDisabled, visual.Disabled);
            knobBottom = EuColor.Lerp(knobBottom, Colors.WidgetDisabled, visual.Disabled);
        }

        // 掴んでいる間は下へわずかに沈ませ、押している手応えを出す
        if (visual.Press > 0.01f)
            knobRect = knobRect.Offset(0f, visual.Press * 0.5f);

        Painter.Shadow(knobRect, EuColor.WithAlpha(Colors.Shadow, 0.5f), 3f, knobRounding, new Vector2(0f, 1f));
        Painter.RectGradientV(knobRect, knobTop, knobBottom, knobRounding);

        var knobBorder = EuColor.Lerp(Colors.KnobBorder, Colors.WidgetBorderHover, visual.Hover);
        Painter.RectOutline(knobRect, knobBorder, 1f, knobRounding);

        // つまみの中央に細い線を入れて、掴む部分だと分かるようにする
        var gripColor = EuColor.WithAlpha(Colors.KnobBorder, 0.7f);
        Painter.VLine(
            knobRect.Center.X, knobRect.Min.Y + 4f, knobRect.Max.Y - 4f, gripColor);
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
            // 左端のシェブロンは、開閉の進み具合に合わせて右向きから下向きへ回す
            var arrowRect = rect.CutLeft(rect.Height, out textRect);
            var arrowColor = EuColor.Lerp(Colors.TextMuted, Colors.Accent, visual.Hover);
            var angle = visual.OnAmount * MathF.PI * 0.5f;

            Painter.Chevron(arrowRect, angle, arrowColor, 1.8f);
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
        var rect = visual.Rect.Offset(0f, visual.Press * 1f);
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
    public virtual void DrawWindowChrome(
        Rect window, Rect titleBar, Rect titleTextArea, ReadOnlySpan<char> title, bool focused)
    {
        var rounding = Metrics.WindowRounding;

        // 影 → 地 → 光沢 → タイトルバー → 枠 の順に重ねる
        Painter.Shadow(window, Colors.Shadow, Metrics.WindowShadowSize, rounding, new Vector2(0f, 3f));
        Painter.RectGradientV(window, Colors.WindowTop, Colors.WindowBottom, rounding);

        // 上端の光沢。厚みのある板に光が当たっているように見せる
        if ((Colors.WindowGloss >> 24) != 0)
        {
            var glossHeight = MathF.Min(window.Height * 0.45f, 90f);
            var glossRect = window.WithHeight(glossHeight);

            Painter.RectGradientV(
                glossRect,
                Colors.WindowGloss,
                EuColor.WithAlpha(Colors.WindowGloss, 0f),
                rounding,
                Corners.Top);
        }

        if (!titleBar.IsEmpty)
        {
            if (focused)
                Painter.RectGradientV(titleBar, Colors.TitleTop, Colors.TitleBottom, rounding, Corners.Top);
            else
                Painter.Rect(titleBar, Colors.TitleInactive, rounding, Corners.Top);

            Painter.HLine(titleBar.Min.X, titleBar.Max.X, titleBar.Max.Y, Colors.TitleUnderline);

            // 文字はボタンを除いた範囲へ。長いタイトルは末尾が省略される
            var titleColor = focused ? Colors.TitleText : EuColor.ScaleAlpha(Colors.TitleText, 0.6f);
            TextPainter.TextIn(titleTextArea, titleColor, title, Align.Start, Align.Center);
        }

        // 内側に一本細い線を入れると、ゲーム UI に近い二重枠になる
        Painter.RectOutline(window.Shrink(1f), Colors.WindowBorderInner, 1f, MathF.Max(0f, rounding - 1f));
        Painter.RectOutline(window, Colors.WindowBorder, Metrics.WindowBorderWidth, rounding);
    }

    /// <inheritdoc/>
    public virtual void DrawWindowButton(in WidgetVisual visual, WindowButtonKind kind)
    {
        var rect = visual.Rect;
        var rounding = MathF.Min(Metrics.WidgetRounding, 3f);

        // 暗い地の上では、暗い色を薄く重ねても見えない。
        // ホバー時は明るい色ではっきり反応させ、押せる場所が分かるようにする
        var hoverBg = kind == WindowButtonKind.Close
            ? EuColor.WithAlpha(Colors.Danger, visual.Hover * 0.9f)
            : EuColor.WithAlpha(Colors.WidgetHoverTop, visual.Hover * 0.9f);

        Painter.Rect(rect, hoverBg, rounding);

        if (visual.Press > 0.01f)
            Painter.Rect(rect, EuColor.WithAlpha(EuColor.Black, visual.Press * 0.3f), rounding);

        var color = EuColor.Lerp(Colors.TextMuted, Colors.TitleText, MathF.Max(visual.Hover, 0.45f));
        var center = rect.Center;

        // アイコンはボタンの 6 割強の大きさにする。小さすぎると何のボタンか分からない。
        // ただし線の太さと角丸の分は必ず内側へ収める。
        // 収めないと、角丸のカーブに線が乗って端が欠けて見える
        var half = MathF.Min(rect.Width, rect.Height) * 0.5f;
        var thickness = MathF.Max(1.8f, half * 0.16f);
        // 角丸が効くのは「中心から half-rounding より外」なので、そこまでに収める
        var safeExtent = half - rounding - (thickness * 0.5f);
        var extent = MathF.Min(half * 0.62f, MathF.Max(2f, safeExtent));

        switch (kind)
        {
            case WindowButtonKind.Close:
            {
                // × は対角線なので、同じ広がりでも外接する四角が他のアイコンより大きくなる。
                // 並べたときに一つだけ大ぶりに見えないよう、少し詰める
                var arm = extent * 0.82f;

                Painter.Line(
                    center - new Vector2(arm, arm), center + new Vector2(arm, arm), color, thickness);
                Painter.Line(
                    center + new Vector2(arm, -arm), center + new Vector2(-arm, arm), color, thickness);
                break;
            }

            case WindowButtonKind.Collapse:
                // 展開中は上向き (畳む)、畳まれているときは下向き (開く)
                Painter.Chevron(rect, visual.On ? Direction.Up : Direction.Down, color, thickness);
                break;

            case WindowButtonKind.Compact:
            {
                // 小窓を表す枠。タイトルバー付きのウィンドウに見えるよう上辺を厚くする
                var box = Rect.FromCenter(center, new Vector2(extent * 1.9f, extent * 1.6f));

                Painter.RectOutline(box, color, thickness, 1f);
                Painter.Rect(box.WithHeight(thickness * 1.6f), color, 1f, Corners.Top);

                if (visual.On)
                    Painter.Rect(box.Shrink(thickness * 1.8f), EuColor.ScaleAlpha(color, 0.7f));

                break;
            }

            case WindowButtonKind.Restore:
            {
                // 元の大きさへ戻す。小さい枠から大きい枠へ広がる形で表す
                var big = Rect.FromCenter(center, new Vector2(extent * 2f, extent * 2f));
                var small = Rect.FromSize(
                    new Vector2(big.Min.X, big.Min.Y + (extent * 0.6f)),
                    new Vector2(extent * 1.4f, extent * 1.4f));

                Painter.RectOutline(big, EuColor.ScaleAlpha(color, 0.5f), thickness * 0.8f, 1f);
                Painter.RectOutline(small, color, thickness, 1f);
                break;
            }

            case WindowButtonKind.Lock:
            {
                // 錠前。掛かっているときは弦を閉じ、外れているときは片側を開く
                var body = Rect.FromCenter(
                    center + new Vector2(0f, extent * 0.45f),
                    new Vector2(extent * 1.7f, extent * 1.3f));

                Painter.Rect(body, color, 1f);

                var arcCenter = new Vector2(center.X + (visual.On ? 0f : extent * 0.35f), body.Min.Y);
                Painter.Arc(arcCenter, extent * 0.6f, MathF.PI, MathF.PI * 2f, color, thickness);
                break;
            }

            default:
                Painter.CircleOutline(center, extent, color, thickness);
                Painter.Circle(center, extent * 0.35f, color);
                break;
        }
    }

    /// <inheritdoc/>
    public virtual void DrawTitleBarIconButton(in WidgetVisual visual, ReadOnlySpan<char> icon)
    {
        var rect = visual.Rect;
        var rounding = MathF.Min(Metrics.WidgetRounding, 3f);

        var bg = visual.On
            ? EuColor.WithAlpha(Colors.Accent, 0.30f + (visual.Hover * 0.35f))
            : EuColor.WithAlpha(Colors.SurfaceHover, 0.10f + (visual.Hover * 0.75f));

        Painter.Rect(rect, bg, rounding);

        if (visual.Press > 0.01f)
            Painter.Rect(rect, EuColor.WithAlpha(EuColor.Black, visual.Press * 0.25f), rounding);

        var color = visual.On
            ? Colors.TextHeading
            : EuColor.Lerp(Colors.TextMuted, Colors.Text, MathF.Max(visual.Hover, 0.35f));

        TextPainter.TextIn(rect, color, icon, Align.Center, Align.Center, ellipsize: false);
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
