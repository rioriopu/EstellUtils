using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 数値の並びを扱うウィジェット。
/// </summary>
/// <remarks>
/// 座標や余白のように、色ではないベクトルを編集する。
/// 色として扱いたい場合は <c>ColorEdit</c> を使う。
/// </remarks>
public static partial class EUi
{
    /// <summary>成分に添える既定の文字。</summary>
    private static readonly string[] DefaultVectorLabels = ["X", "Y", "Z", "W"];

    /// <summary>
    /// 数値を横に並べて編集する。
    /// <code>
    /// // 画面端からの余白 (左 / 下 / 右 / 上)
    /// if (EUi.InputVector("##margin", ref margin, ["L", "D", "R", "U"]))
    ///     config.Save();
    /// </code>
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="values">対象の値。要素数だけ欄が並ぶ。</param>
    /// <param name="labels">各成分に添える文字。省略すると X / Y / Z / W。</param>
    /// <param name="step">増減ボタンの刻み。0 にするとボタンを出さない。</param>
    /// <param name="min">下限。</param>
    /// <param name="max">上限。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <returns>どれかの値が変わったら <c>Changed</c> が立つ。</returns>
    public static WidgetResult InputVector(
        string id, Span<float> values, ReadOnlySpan<string> labels = default,
        float step = 0f, float? min = null, float? max = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (values.IsEmpty)
            return default;

        using var scope = ctx.ScopedId(id);

        var count = values.Length;
        var changed = false;

        // 成分ごとに「ラベル + 入力欄」を縦に積み、それを横へ並べる。
        // 幅は均等に配り、残り幅の計算はレイアウトへ任せる
        Span<SizeSpec> columns = stackalloc SizeSpec[count];
        columns.Fill(SizeSpec.Fill);

        var lineHeight = TextPainter.LineHeight;
        var hasLabels = !labels.IsEmpty;

        using (Row(Align.Start, columns))
        {
            for (var i = 0; i < count; i++)
            {
                var cellRect = ctx.Allocate(
                    SizeSpec.Fill,
                    hasLabels ? lineHeight + Metrics.SpacingXs + Metrics.WidgetHeight : Metrics.WidgetHeight);

                using (Region(cellRect, default, Metrics.SpacingXs))
                {
                    if (hasLabels)
                    {
                        var label = i < labels.Length ? labels[i] : DefaultVectorLabels[Math.Min(i, 3)];
                        Muted(label, Align.Center);
                    }

                    var value = values[i];

                    if (InputFloat(
                            "##v" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ref value, step, min, max, SizeSpec.Fill, disabled))
                    {
                        values[i] = value;
                        changed = true;
                    }
                }
            }
        }

        return new WidgetResult { Changed = changed, Disabled = disabled };
    }

    /// <summary><see cref="Vector2"/> を編集する。</summary>
    public static WidgetResult InputVector2(
        string id, ref Vector2 value, ReadOnlySpan<string> labels = default,
        float step = 0f, float? min = null, float? max = null, bool disabled = false)
    {
        Span<float> values = [value.X, value.Y];
        var result = InputVector(id, values, labels, step, min, max, disabled);

        if (result.Changed)
            value = new Vector2(values[0], values[1]);

        return result;
    }

    /// <summary><see cref="Vector3"/> を編集する。</summary>
    public static WidgetResult InputVector3(
        string id, ref Vector3 value, ReadOnlySpan<string> labels = default,
        float step = 0f, float? min = null, float? max = null, bool disabled = false)
    {
        Span<float> values = [value.X, value.Y, value.Z];
        var result = InputVector(id, values, labels, step, min, max, disabled);

        if (result.Changed)
            value = new Vector3(values[0], values[1], values[2]);

        return result;
    }

    /// <summary>
    /// <see cref="Vector4"/> を編集する。色ではない 4 つ組に使う。
    /// </summary>
    public static WidgetResult InputVector4(
        string id, ref Vector4 value, ReadOnlySpan<string> labels = default,
        float step = 0f, float? min = null, float? max = null, bool disabled = false)
    {
        Span<float> values = [value.X, value.Y, value.Z, value.W];
        var result = InputVector(id, values, labels, step, min, max, disabled);

        if (result.Changed)
            value = new Vector4(values[0], values[1], values[2], values[3]);

        return result;
    }

    /// <summary>
    /// ドラッグで値を変える欄。範囲の広い値を、上限を決めずに動かしたいときに使う。
    /// <code>
    /// if (EUi.DragFloat("拡大率", ref scale, 0.01f, 0.1f, 10f))
    ///     config.Save();
    /// </code>
    /// </summary>
    /// <param name="label">ラベル。<c>##</c> 以降は ID にのみ使われる。</param>
    /// <param name="value">対象の値。</param>
    /// <param name="speed">1px 動かしたときの変化量。</param>
    /// <param name="min">下限。</param>
    /// <param name="max">上限。</param>
    /// <param name="decimals">小数の表示桁数。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// スライダーと違って端が無いので、上限のはっきりしない値に向く。
    /// ドラッグ中に Shift を押すと細かく動く。値の上でクリックすると直接打ち込める。
    /// </remarks>
    public static WidgetResult DragFloat(
        ReadOnlySpan<char> label, ref float value, float speed = 0.01f,
        float? min = null, float? max = null, int decimals = 2,
        SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, Metrics.WidgetHeight);

        var interaction = Interaction.Behavior(
            rect, id,
            disabled ? InteractionFlags.Disabled : InteractionFlags.AllowDragOutside);

        var visual = WidgetVisual.From(interaction) with { Rect = rect };
        WidgetPainter.DrawInputFrame(visual);

        var changed = false;

        if (!disabled && interaction.Held)
        {
            var delta = ctx.Input.MouseDelta.X;

            if (delta != 0f)
            {
                // Shift を押している間は細かく動かす
                var scale = ctx.Input.Shift ? 0.1f : 1f;
                var next = value + (delta * speed * scale);

                if (min.HasValue)
                    next = MathF.Max(min.Value, next);

                if (max.HasValue)
                    next = MathF.Min(max.Value, next);

                if (next != value)
                {
                    value = next;
                    changed = true;
                }
            }
        }

        var inner = rect.Shrink(Metrics.WidgetPadding);

        // 値は中央、ラベルは左。ラベルが無ければ値だけを中央に置く
        Span<char> buffer = stackalloc char[32];
        var format = decimals <= 0 ? "0" : "0." + new string('#', decimals);

        if (value.TryFormat(buffer, out var written, format, System.Globalization.CultureInfo.InvariantCulture))
        {
            var color = disabled ? Colors.TextDisabled : Colors.Text;

            if (display.IsEmpty)
            {
                TextPainter.TextIn(inner, color, buffer[..written], Align.Center, Align.Center);
            }
            else
            {
                TextPainter.TextIn(inner, Colors.TextMuted, display, Align.Start, Align.Center);
                TextPainter.TextIn(inner, color, buffer[..written], Align.End, Align.Center);
            }
        }

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// ビットの立ち下がりを切り替えるチェックボックス。
    /// <code>
    /// // 32 個の島の解放状態を、1 つの uint で持っている場合
    /// for (var i = 0; i &lt; 32; i++)
    /// {
    ///     if (EUi.CheckboxFlags($"島 {i + 1}", ref mask, 1u &lt;&lt; i))
    ///         config.Save();
    /// }
    /// </code>
    /// </summary>
    /// <param name="label">ラベル。</param>
    /// <param name="value">対象のビット列。</param>
    /// <param name="mask">切り替えるビット。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// <paramref name="mask"/> に複数のビットを渡した場合、すべて立っていれば ON と見なす。
    /// 押すと、すべて立てるか、すべて落とすかのどちらかになる。
    /// </remarks>
    public static WidgetResult CheckboxFlags(
        ReadOnlySpan<char> label, ref uint value, uint mask, bool disabled = false)
    {
        var on = mask != 0u && (value & mask) == mask;
        var before = on;

        var result = Checkbox(label, ref on, disabled);

        if (on == before)
            return result;

        value = on ? value | mask : value & ~mask;
        return result with { Changed = true };
    }

    /// <summary><c>int</c> のビット列を切り替える版。</summary>
    public static WidgetResult CheckboxFlags(
        ReadOnlySpan<char> label, ref int value, int mask, bool disabled = false)
    {
        var unsigned = unchecked((uint)value);
        var result = CheckboxFlags(label, ref unsigned, unchecked((uint)mask), disabled);

        if (result.Changed)
            value = unchecked((int)unsigned);

        return result;
    }
}
