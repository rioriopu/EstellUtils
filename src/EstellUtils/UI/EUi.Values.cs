using System;
using System.Globalization;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 数値を扱うウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>値の書式化に使う作業バッファの長さ。</summary>
    private const int ValueBufferLength = 64;

    /// <summary>
    /// 整数スライダー。つまみを掴んで動かすか、溝をクリックして値を決める。
    /// </summary>
    /// <param name="label">右側に表示するラベル。<c>##</c> 以降は ID にのみ使われる。</param>
    /// <param name="value">対象の値。</param>
    /// <param name="min">最小値。</param>
    /// <param name="max">最大値。</param>
    /// <param name="width">スライダー部分の幅。省略すると残り幅いっぱい。</param>
    /// <param name="suffix">値の後ろに付ける単位 (「フレーム」など)。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult SliderInt(
        ReadOnlySpan<char> label, ref int value, int min, int max,
        SizeSpec? width = null, ReadOnlySpan<char> suffix = default, bool disabled = false)
    {
        float floatValue = value;
        var result = SliderCore(
            label, ref floatValue, min, max, width, suffix, disabled, isInteger: true, decimals: 0);

        if (result.Changed)
            value = (int)MathF.Round(floatValue);

        return result;
    }

    /// <summary>
    /// 小数スライダー。ドラッグ中に Shift を押すと細かく動かせる。
    /// </summary>
    /// <param name="label">右側に表示するラベル。<c>##</c> 以降は ID にのみ使われる。</param>
    /// <param name="value">対象の値。</param>
    /// <param name="min">最小値。</param>
    /// <param name="max">最大値。</param>
    /// <param name="width">スライダー部分の幅。省略すると残り幅いっぱい。</param>
    /// <param name="suffix">値の後ろに付ける単位。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <param name="decimals">表示する小数桁数。</param>
    public static WidgetResult SliderFloat(
        ReadOnlySpan<char> label, ref float value, float min, float max,
        SizeSpec? width = null, ReadOnlySpan<char> suffix = default, bool disabled = false, int decimals = 2)
        => SliderCore(label, ref value, min, max, width, suffix, disabled, isInteger: false, decimals);

    /// <summary>
    /// 進捗バー。
    /// </summary>
    /// <param name="fraction">0〜1 の進捗。</param>
    /// <param name="overlay">バーの中央に重ねる文字列。省略すると百分率。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    public static WidgetResult ProgressBar(
        float fraction, ReadOnlySpan<char> overlay = default, SizeSpec? width = null, float? height = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var rect = ctx.Allocate(width ?? SizeSpec.Fill, height ?? Metrics.WidgetHeight);
        var value = Math.Clamp(fraction, 0f, 1f);

        Span<char> buffer = stackalloc char[ValueBufferLength];
        scoped ReadOnlySpan<char> text = overlay;

        if (text.IsEmpty)
        {
            var percent = value * 100f;
            if (percent.TryFormat(buffer, out var written, "F0", CultureInfo.InvariantCulture) &&
                written + 1 < buffer.Length)
            {
                buffer[written] = '%';
                text = buffer[..(written + 1)];
            }
        }

        var visual = new WidgetVisual { Rect = rect, Value = value };
        WidgetPainter.DrawProgressBar(visual, text);

        return MakeTextResult(ctx, rect);
    }

    /// <summary>スライダーの共通処理。</summary>
    private static WidgetResult SliderCore(
        ReadOnlySpan<char> label, ref float value, float min, float max,
        SizeSpec? width, ReadOnlySpan<char> suffix, bool disabled, bool isInteger, int decimals)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var id = ctx.GetId(label, out var display);

        // 「バー」「値」「ラベル」の 3 つを横に並べる。
        // 値をバーへ重ねて描くと、つまみが数字にかぶって読めなくなるため欄を分ける
        var labelSize = display.IsEmpty ? Vector2.Zero : TextPainter.Measure(display);
        var labelSpace = display.IsEmpty ? 0f : labelSize.X + Metrics.LabelSpacing;
        var valueSpace = Metrics.SliderValueWidth + Metrics.SpacingSm;

        var available = AvailableWidth;
        var barWidth = width?.Resolve(available)
            ?? MathF.Max(Metrics.WidgetMinWidth, available - labelSpace - valueSpace);

        var height = Metrics.WidgetHeight;
        var rowRect = ctx.Allocate(SizeSpec.Px(barWidth + valueSpace + labelSpace), height);

        var sliderRect = rowRect.WithWidth(barWidth);

        var flags = disabled
            ? InteractionFlags.Disabled
            : InteractionFlags.AllowDragOutside;

        var interaction = Interaction.Behavior(sliderRect, id, flags);

        var range = max - min;
        var normalized = range > 0f ? Math.Clamp((value - min) / range, 0f, 1f) : 0f;

        var knobHalf = Metrics.SliderKnobWidth * 0.5f;
        var trackMin = sliderRect.Min.X + knobHalf;
        var trackMax = sliderRect.Max.X - knobHalf;
        var travel = MathF.Max(1f, trackMax - trackMin);

        var changed = false;

        if (!disabled && (interaction.Pressed || interaction.Held))
        {
            changed = DragSlider(
                ctx, id, ref value, min, max, range, trackMin, travel, knobHalf + 2f,
                normalized, isInteger, interaction.Pressed);

            if (changed)
                normalized = range > 0f ? Math.Clamp((value - min) / range, 0f, 1f) : 0f;
        }

        var knobCenter = new Vector2(trackMin + (travel * normalized), sliderRect.Center.Y);
        var knobRect = Rect.FromCenter(
            knobCenter, new Vector2(Metrics.SliderKnobWidth, sliderRect.Height));

        var visual = WidgetVisual.From(interaction, false, 0f, normalized) with { Rect = sliderRect };
        WidgetPainter.DrawSlider(visual, knobRect);

        // 値はバーの右の欄へ。つまみの位置に関係なく読める
        Span<char> buffer = stackalloc char[ValueBufferLength];
        var text = FormatValue(buffer, value, isInteger, decimals, suffix);

        var valueRect = Rect.FromSize(
            new Vector2(sliderRect.Max.X + Metrics.SpacingSm, rowRect.Min.Y),
            new Vector2(Metrics.SliderValueWidth, height));

        var textColor = disabled ? Colors.TextDisabled : Colors.Text;
        TextPainter.TextIn(valueRect, textColor, text, Align.End, Align.Center, ellipsize: false);

        if (!display.IsEmpty)
        {
            var labelRect = new Rect(
                new Vector2(valueRect.Max.X + Metrics.LabelSpacing, rowRect.Min.Y), rowRect.Max);

            TextPainter.TextIn(
                labelRect,
                disabled ? Colors.TextDisabled : Colors.Text,
                display, Align.Start, Align.Center);
        }

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// スライダーのドラッグ処理。
    /// </summary>
    /// <remarks>
    /// <para>
    /// つまみを掴んだときは「掴んだ時点の値」を基準にした相対移動にしている。
    /// マウス位置をそのまま値に変換すると、つまみの端を掴んだ瞬間に値が飛んでしまうため。
    /// </para>
    /// <para>
    /// 溝を直接クリックしたときはその位置へ跳ばし、そこから掴んだものとして扱う。
    /// Shift を押している間は移動量が 1/5 になり、押し直したところで基準を取り直すので
    /// つまみとマウスがずれていかない。
    /// </para>
    /// </remarks>
    private static bool DragSlider(
        UiContext ctx, EuId id, ref float value, float min, float max, float range,
        float trackMin, float travel, float knobRadius, float normalized, bool isInteger, bool pressed)
    {
        const float FineScale = 0.2f;

        ref var state = ref ctx.Store.GetRef(id);

        var mouseX = ctx.Input.MousePos.X;
        var fine = ctx.Input.Shift;
        var changed = false;

        if (pressed)
        {
            var knobCenterX = trackMin + (travel * normalized);
            var onKnob = MathF.Abs(mouseX - knobCenterX) <= knobRadius + 2f;

            if (!onKnob)
            {
                // 溝をクリック: その位置へ跳ばす
                var t = Math.Clamp((mouseX - trackMin) / travel, 0f, 1f);
                var jumped = min + (t * range);

                if (isInteger)
                    jumped = MathF.Round(jumped);

                jumped = Math.Clamp(jumped, min, max);

                if (jumped != value)
                {
                    value = jumped;
                    changed = true;
                }
            }

            state.DragAnchorValue = value;
            state.DragAnchorPos = mouseX;
            state.DragFine = fine;

            return changed;
        }

        // 修飾キーの状態が変わったら、そこを新しい基準にする (値が飛ばないように)
        if (fine != state.DragFine)
        {
            state.DragAnchorValue = value;
            state.DragAnchorPos = mouseX;
            state.DragFine = fine;
        }

        var scale = fine ? FineScale : 1f;
        var delta = (mouseX - state.DragAnchorPos) * scale / travel * range;
        var next = state.DragAnchorValue + delta;

        if (isInteger)
            next = MathF.Round(next);

        next = Math.Clamp(next, min, max);

        if (next == value)
            return false;

        value = next;
        return true;
    }

    /// <summary>値と単位を作業バッファへ書き出す。文字列のアロケーションを避けるため。</summary>
    private static ReadOnlySpan<char> FormatValue(
        Span<char> buffer, float value, bool isInteger, int decimals, ReadOnlySpan<char> suffix)
    {
        int written;

        if (isInteger)
        {
            if (!((int)MathF.Round(value)).TryFormat(buffer, out written, default, CultureInfo.InvariantCulture))
                return default;
        }
        else
        {
            Span<char> format = stackalloc char[3];
            format[0] = 'F';

            var digits = Math.Clamp(decimals, 0, 9);
            format[1] = (char)('0' + digits);

            if (!value.TryFormat(buffer, out written, format[..2], CultureInfo.InvariantCulture))
                return default;
        }

        if (suffix.IsEmpty || written + suffix.Length + 1 > buffer.Length)
            return buffer[..written];

        buffer[written++] = ' ';
        suffix.CopyTo(buffer[written..]);

        return buffer[..(written + suffix.Length)];
    }
}
