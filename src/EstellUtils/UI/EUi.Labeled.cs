using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;

namespace EstellUtils.UI;

/// <summary>
/// 「本体 + ラベル」の行を組み立てる共通部分。
/// </summary>
/// <remarks>
/// <para>
/// 入力系のウィジェットは、第 1 引数をラベル兼識別子として受け取ります。
/// <c>##</c> より前が画面に出て、全体が識別子になります。ImGui と同じ扱いです。
/// </para>
/// <para>
/// ラベルは本体の右へ添えます。スライダーと同じ並びで、
/// <c>width</c> が指すのはどのウィジェットでも「本体の幅」です。
/// </para>
/// </remarks>
public static partial class EUi
{
    /// <summary>
    /// ラベル付きの行を確保し、本体を置く矩形を返す。
    /// </summary>
    /// <param name="display">表示するラベル。空ならラベル欄は作らない。</param>
    /// <param name="width">本体の幅。省略すると、ラベルの分を残した残り幅。</param>
    /// <param name="height">行の高さ。</param>
    /// <param name="labelRect">ラベルを描く矩形。</param>
    private static Rect AllocateLabeledRow(
        ReadOnlySpan<char> display, SizeSpec? width, float height, out Rect labelRect)
    {
        var ctx = UiContext.Current;

        // 確保時と配置時で同じ余白を使う。ここがずれるとラベルの末尾が省略される。
        // 計測値は切り上げて、丸めの差で 1px 不足するのも防ぐ
        var labelSpace = display.IsEmpty
            ? 0f
            : MathF.Ceiling(TextPainter.Measure(display).X) + Metrics.LabelSpacing;

        var available = NextItemWidth;

        // 幅を省略した場合だけ、ラベルの分を残す。
        // 明示された幅は本体そのものの幅として扱う (スライダーと同じ)
        var bodyWidth = width is { } spec
            ? spec.Resolve(MathF.Max(0f, available - labelSpace))
            : MathF.Max(Metrics.WidgetMinWidth, available - labelSpace);

        var rowRect = ctx.Allocate(SizeSpec.Px(bodyWidth + labelSpace), height);
        var bodyRect = rowRect.CutLeft(bodyWidth, out labelRect);

        return bodyRect;
    }

    /// <summary>本体の右に添えるラベルを描く。</summary>
    private static void DrawTrailingLabel(Rect labelRect, ReadOnlySpan<char> display, bool disabled)
    {
        if (display.IsEmpty || labelRect.Width <= 1f)
            return;

        var textRect = labelRect.Shrink(new EdgeInsets(Metrics.LabelSpacing, 0f, 0f, 0f));

        TextPainter.TextIn(
            textRect,
            disabled ? Colors.TextDisabled : Colors.Text,
            display,
            Align.Start,
            Align.Center);
    }
}
