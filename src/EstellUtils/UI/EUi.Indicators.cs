using System;
using System.Globalization;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 状態を見せるための小さなウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>状態の点の半径。</summary>
    private const float StatusDotRadius = 4f;

    /// <summary>推移グラフの左右に空ける余白。</summary>
    private const float SparklinePadding = 1f;

    /// <summary>
    /// 小さな見出し札。状態や種別を 1 語で示す。
    /// <code>
    /// using (EUi.HStack())
    /// {
    ///     EUi.Label("オーバーレイ方式");
    ///     EUi.Badge("試験", NoteKind.Warning);
    /// }
    /// </code>
    /// </summary>
    /// <param name="text">中の文字。</param>
    /// <param name="kind">種類。色が変わる。</param>
    /// <param name="filled">地を塗るか。false にすると枠と文字だけになる。</param>
    public static WidgetResult Badge(
        ReadOnlySpan<char> text, NoteKind kind = NoteKind.Info, bool filled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var accent = NoteColor(kind);
        var padding = Metrics.SpacingSm;

        var textSize = TextPainter.Measure(text);
        var size = new Vector2(
            MathF.Ceiling(textSize.X) + (padding * 2f),
            MathF.Ceiling(TextPainter.LineHeight) + (Metrics.SpacingXs * 2f));

        var rect = ctx.Allocate(size);

        if (!Painter.IsVisible(rect))
            return MakeTextResult(ctx, rect);

        // 角丸は高さの半分。薬のような形にする
        var rounding = rect.Height * 0.5f;

        if (filled)
        {
            Painter.Rect(rect, accent, rounding);
        }
        else
        {
            Painter.Rect(rect, EuColor.WithAlpha(accent, 0.16f), rounding);
            Painter.RectOutline(rect, EuColor.WithAlpha(accent, 0.5f), 1f, rounding);
        }

        TextPainter.TextIn(
            rect,
            filled ? EuColor.ReadableOn(accent) : accent,
            text,
            Align.Center,
            Align.Center,
            ellipsize: false);

        return MakeTextResult(ctx, rect);
    }

    /// <summary>
    /// 状態を示す点とラベル。動いているかどうかを一目で伝える。
    /// <code>
    /// EUi.StatusDot(this.running, this.running ? "動作中" : "停止中");
    /// </code>
    /// </summary>
    /// <param name="on">点いているか。</param>
    /// <param name="label">右に添える文字。</param>
    /// <param name="onColor">点いているときの色。省略すると成功色。</param>
    /// <param name="pulse">点いている間、点をゆっくり明滅させるか。</param>
    public static WidgetResult StatusDot(
        bool on, ReadOnlySpan<char> label = default,
        uint? onColor = null, bool pulse = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var lineHeight = TextPainter.LineHeight;
        var dotColumn = StatusDotRadius * 2f + Metrics.SpacingSm;

        var textSize = label.IsEmpty ? Vector2.Zero : TextPainter.Measure(label);
        var rect = ctx.Allocate(
            new Vector2(dotColumn + MathF.Ceiling(textSize.X), lineHeight));

        if (!Painter.IsVisible(rect))
            return MakeTextResult(ctx, rect);

        var color = on ? onColor ?? Colors.Success : Colors.TextDisabled;

        // 明滅は薄くする方向だけ。明るくすると視線を奪いすぎる
        if (on && pulse && Motion.Enabled)
        {
            var wave = Anim.PingPong(ctx.Input.Time, 2f);
            color = EuColor.ScaleAlpha(color, 0.55f + (wave * 0.45f));
        }

        var center = new Vector2(
            rect.Min.X + StatusDotRadius,
            rect.Min.Y + (lineHeight * 0.5f));

        // 点いているときは細いリングを添えて、消えている状態と見分けやすくする。
        // 薄い面を敷く方法も試したが、点の背後に四角い染みがあるように見えた
        Painter.Circle(center, StatusDotRadius, color);

        if (on)
            Painter.CircleOutline(center, StatusDotRadius + 2f, EuColor.WithAlpha(color, 0.45f), 1f);

        if (!label.IsEmpty)
        {
            var textArea = rect;
            textArea.CutLeft(dotColumn, out textArea);

            TextPainter.TextIn(
                textArea, on ? Colors.Text : Colors.TextMuted, label, Align.Start, Align.Center);
        }

        return MakeTextResult(ctx, rect);
    }

    /// <summary>
    /// 値の推移を折れ線で見せる。負荷や FPS の移り変わりの確認に使う。
    /// <code>
    /// EUi.Sparkline("fps", this.fpsHistory, 40f, label: $"{fps:0} fps");
    /// </code>
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="values">古いものから順に並んだ値。</param>
    /// <param name="height">高さ。</param>
    /// <param name="min">下端にあたる値。省略すると <paramref name="values"/> の最小値。</param>
    /// <param name="max">上端にあたる値。省略すると <paramref name="values"/> の最大値。</param>
    /// <param name="color">線の色。省略するとアクセント色。</param>
    /// <param name="label">左上へ重ねて出す文字。</param>
    /// <param name="format">値の書式。省略すると小数 2 桁まで。</param>
    /// <remarks>
    /// マウスを乗せると、その位置に印と値が出る。値はグラフの中へ直接描く。
    /// ツールチップにすると、呼び出し側が <c>Tip</c> で付けた説明と取り合いになるため。
    /// </remarks>
    public static WidgetResult Sparkline(
        ReadOnlySpan<char> id, ReadOnlySpan<float> values,
        float height = 36f, float? min = null, float? max = null,
        uint? color = null, ReadOnlySpan<char> label = default,
        ReadOnlySpan<char> format = default)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var euId = ctx.GetId(id);
        var rect = ctx.Allocate(SizeSpec.Fill, height);

        var interaction = Interaction.Behavior(rect, euId);

        var rounding = Metrics.WidgetRounding;
        Painter.Rect(rect, Colors.Track, rounding);

        if (!Painter.IsVisible(rect) || values.Length == 0)
        {
            Painter.RectOutline(rect, Colors.WidgetBorder, 1f, rounding);
            return WidgetResult.From(interaction);
        }

        // 値の範囲。指定がなければ実際の幅に合わせる
        var lower = min ?? float.MaxValue;
        var upper = max ?? float.MinValue;

        if (min is null || max is null)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (min is null)
                    lower = MathF.Min(lower, values[i]);

                if (max is null)
                    upper = MathF.Max(upper, values[i]);
            }
        }

        // 値が一定のときは平らな線になるよう、幅を持たせる
        if (upper - lower < 0.0001f)
        {
            lower -= 0.5f;
            upper += 0.5f;
        }

        var plot = rect.Shrink(SparklinePadding);
        var lineColor = color ?? Colors.Accent;

        var step = values.Length > 1 ? plot.Width / (values.Length - 1) : 0f;
        var previous = PointAt(plot, 0, step, values[0], lower, upper);

        for (var i = 1; i < values.Length; i++)
        {
            var current = PointAt(plot, i, step, values[i], lower, upper);

            // 線の下を薄く塗って、量の多い少ないを分かりやすくする
            Painter.Triangle(
                previous, current, new Vector2(current.X, plot.Max.Y),
                EuColor.WithAlpha(lineColor, 0.14f));

            Painter.Triangle(
                previous, new Vector2(current.X, plot.Max.Y), new Vector2(previous.X, plot.Max.Y),
                EuColor.WithAlpha(lineColor, 0.14f));

            Painter.Line(previous, current, lineColor, 1.5f);
            previous = current;
        }

        Painter.RectOutline(rect, Colors.WidgetBorder, 1f, rounding);

        if (!label.IsEmpty)
        {
            TextPainter.TextIn(
                rect.Shrink(Metrics.SpacingSm), Colors.TextMuted, label, Align.Start, Align.Start);
        }

        // 乗せた位置の値を出す。どの時点の値かを読み取れるようにする
        if (interaction.Hovered && step > 0f)
        {
            var offset = Math.Clamp(
                (int)MathF.Round((ctx.Input.MousePos.X - plot.Min.X) / step),
                0,
                values.Length - 1);

            var marker = PointAt(plot, offset, step, values[offset], lower, upper);

            Painter.Line(
                new Vector2(marker.X, plot.Min.Y),
                new Vector2(marker.X, plot.Max.Y),
                EuColor.WithAlpha(lineColor, 0.4f));

            Painter.Circle(marker, 2.5f, lineColor);

            DrawSparklineValue(rect, marker, values[offset], format);
        }

        return WidgetResult.From(interaction);

        static Vector2 PointAt(Rect plot, int index, float step, float value, float lower, float upper)
        {
            var t = Math.Clamp((value - lower) / (upper - lower), 0f, 1f);

            return new Vector2(
                plot.Min.X + (step * index),
                plot.Max.Y - (plot.Height * t));
        }
    }

    /// <summary>推移グラフの、印を付けた位置の値を吹き出しで描く。</summary>
    private static void DrawSparklineValue(
        Rect bounds, Vector2 marker, float value, ReadOnlySpan<char> format)
    {
        Span<char> buffer = stackalloc char[32];

        if (!value.TryFormat(
                buffer, out var written,
                format.IsEmpty ? "0.##" : format,
                CultureInfo.InvariantCulture))
        {
            return;
        }

        var text = buffer[..written];
        var textSize = TextPainter.Measure(text);

        var box = Rect.FromSize(
            Vector2.Zero,
            new Vector2(
                MathF.Ceiling(textSize.X) + (Metrics.SpacingSm * 2f),
                MathF.Ceiling(TextPainter.LineHeight) + (Metrics.SpacingXs * 2f)));

        // 印の上へ。上端からはみ出すなら下へ回す
        var x = Math.Clamp(
            marker.X - (box.Width * 0.5f),
            bounds.Min.X,
            MathF.Max(bounds.Min.X, bounds.Max.X - box.Width));

        var y = marker.Y - box.Height - 4f;

        if (y < bounds.Min.Y)
            y = MathF.Min(marker.Y + 4f, bounds.Max.Y - box.Height);

        box = Rect.FromSize(new Vector2(MathF.Round(x), MathF.Round(y)), box.Size);

        var rounding = Metrics.WidgetRounding;
        Painter.Rect(box, Colors.TooltipBackground, rounding);
        Painter.RectOutline(box, Colors.TooltipBorder, 1f, rounding);

        TextPainter.TextIn(box, Colors.Text, text, Align.Center, Align.Center, ellipsize: false);
    }
}
