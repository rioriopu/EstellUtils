using System;
using System.Globalization;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 短い語をひと続きに並べる表示。
/// </summary>
public static partial class EUi
{
    /// <summary>並べた文字の置き場。毎フレームの確保を避けて使い回す。</summary>
    private static char[] inlineBuffer = new char[256];

    /// <summary>ツールチップの文字の置き場。</summary>
    private static char[] inlineTipBuffer = new char[256];

    /// <summary>並べた文字の長さ。</summary>
    private static int inlineLength;

    /// <summary>
    /// 短い語を区切り記号で並べ、入りきらない分を「他 N」にまとめる。
    /// </summary>
    /// <param name="items">並べる語。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい（列の中では列幅）。</param>
    /// <param name="color">文字色。省略するとテーマの標準色。</param>
    /// <param name="separator">語の区切り。省略すると「 ・ 」。</param>
    /// <param name="countRemaining">
    /// 入りきらない分を「他 N」で示すか。false にすると省略記号だけになる。
    /// </param>
    /// <param name="prefixWidth">
    /// 各語の前に空ける幅。絵と名前の間の空きも含めた幅を渡す。
    /// <paramref name="drawPrefix"/> と一緒に指定する。
    /// </param>
    /// <param name="drawPrefix">
    /// 各語の前を描く処理。語の添字と、空けた矩形を受け取る。
    /// 絵の出所はライブラリでは決めないので、描くのは呼び出し側。
    /// </param>
    /// <remarks>
    /// <para>
    /// 「ドロップ品を ・ で並べ、入る分だけ出して残りは数で示す」といった、
    /// 表のセルでよく要る形をひとつにまとめたもの。入りきらなかった場合は
    /// 全部の語をツールチップで出す。
    /// </para>
    /// <code>
    /// using (EUi.TableRow(Columns, i))
    /// {
    ///     EUi.TableCell(mob.Name);
    ///
    ///     using (EUi.Cell())
    ///         EUi.InlineList(mob.Drops);
    /// }
    /// </code>
    /// <para>
    /// 語を 1 つずつ足しながら測るので、何語出せるかは実際の幅で決まる。
    /// 1 語目だけで入りきらない場合は、その語が省略記号で切られる。
    /// </para>
    /// <para>
    /// 各語の前に絵を添えたい場合は <paramref name="prefixWidth"/> と
    /// <paramref name="drawPrefix"/> を渡す。入る語の数は絵の幅も含めて決まる。
    /// </para>
    /// <code>
    /// using (EUi.Cell())
    /// {
    ///     EUi.InlineList(
    ///         row.DropNames, prefixWidth: 20f,
    ///         drawPrefix: (i, area) =&gt; EUi.ImageAt(row.DropIcons[i], area.Shrink(2f)));
    /// }
    /// </code>
    /// <para>
    /// <paramref name="drawPrefix"/> にラムダを渡すと毎フレーム確保が起きる。
    /// 行数の多い表では、閉じ込める変数を 1 つにまとめるなどして抑えること。
    /// </para>
    /// </remarks>
    public static WidgetResult InlineList(
        ReadOnlySpan<string> items, SizeSpec? width = null, uint? color = null,
        ReadOnlySpan<char> separator = default, bool countRemaining = true,
        float prefixWidth = 0f, Action<int, Rect>? drawPrefix = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var lineHeight = TextPainter.LineHeight;
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, lineHeight);

        if (items.IsEmpty || !Painter.IsVisible(rect))
            return MakeTextResult(ctx, rect);

        var gap = separator.IsEmpty ? " ・ " : separator;

        if (drawPrefix is not null && prefixWidth > 0f)
        {
            return InlineListWithPrefix(
                ctx, rect, items, color ?? Colors.Text, gap, countRemaining,
                prefixWidth, drawPrefix);
        }

        inlineLength = 0;

        var shown = 0;

        for (var i = 0; i < items.Length; i++)
        {
            var mark = inlineLength;

            if (i > 0)
                InlineAppend(gap);

            InlineAppend(items[i]);

            // 残りがあるなら「他 N」の分も要る。それを含めた幅で判断する
            var measured = MeasureInline(items.Length - (i + 1), countRemaining, gap);

            // 1 語目は必ず出す。全部消えると何の列なのか分からなくなる
            if (measured > rect.Width + 1f && i > 0)
            {
                inlineLength = mark;
                break;
            }

            shown = i + 1;
        }

        var hidden = items.Length - shown;

        if (hidden > 0)
            AppendRemaining(hidden, countRemaining, gap);

        var text = inlineBuffer.AsSpan(0, inlineLength);
        var truncated = hidden > 0 || TextPainter.Measure(text).X > rect.Width + 1f;

        TextPainter.TextIn(rect, color ?? Colors.Text, text, Align.Start, Align.Center);

        var result = MakeTextResult(ctx, rect) with { Truncated = truncated };

        // 全文はホバー中だけ組み立てる。毎フレーム作ると、行の数だけ無駄になる
        if (truncated && result.HoveredDuration > 0f)
            result.Tip(BuildInlineTip(items));

        return result;
    }

    /// <summary>
    /// 各語の前に絵の場所を空けて並べる。
    /// </summary>
    /// <remarks>
    /// 入る語の数を先に決めてから描く。描きながら決めると、入らない語の絵を
    /// 1 つ余分に描いてしまう。
    /// </remarks>
    private static WidgetResult InlineListWithPrefix(
        UiContext ctx, Rect rect, ReadOnlySpan<string> items, uint color,
        ReadOnlySpan<char> gap, bool countRemaining, float prefixWidth,
        Action<int, Rect> drawPrefix)
    {
        var gapWidth = TextPainter.Measure(gap).X;
        var used = 0f;
        var shown = 0;

        for (var i = 0; i < items.Length; i++)
        {
            var needed = (i > 0 ? gapWidth : 0f) + prefixWidth + TextPainter.Measure(items[i]).X;
            var hiddenAfter = items.Length - (i + 1);
            var suffix = hiddenAfter > 0 ? MeasureRemaining(hiddenAfter, countRemaining, gap) : 0f;

            // 1 語目は必ず出す。全部消えると何の列なのか分からなくなる
            if (i > 0 && used + needed + suffix > rect.Width + 1f)
                break;

            used += needed;
            shown = i + 1;
        }

        var x = rect.Min.X;
        var clipped = false;

        for (var i = 0; i < shown; i++)
        {
            if (i > 0)
            {
                TextPainter.TextIn(
                    SliceAt(rect, x, gapWidth), color, gap, Align.Start, Align.Center);

                x += gapWidth;
            }

            drawPrefix(i, SliceAt(rect, x, prefixWidth));
            x += prefixWidth;

            var wanted = TextPainter.Measure(items[i]).X;
            var room = MathF.Max(0f, rect.Max.X - x);
            var labelWidth = MathF.Min(wanted, room);

            clipped |= wanted > room + 1f;

            TextPainter.TextIn(
                SliceAt(rect, x, labelWidth), color, items[i], Align.Start, Align.Center);

            x += labelWidth;
        }

        var hidden = items.Length - shown;

        if (hidden > 0)
        {
            inlineLength = 0;
            AppendRemaining(hidden, countRemaining, gap);

            TextPainter.TextIn(
                SliceAt(rect, x, MathF.Max(0f, rect.Max.X - x)), color,
                inlineBuffer.AsSpan(0, inlineLength), Align.Start, Align.Center);
        }

        var result = MakeTextResult(ctx, rect) with { Truncated = hidden > 0 || clipped };

        if (result.Truncated && result.HoveredDuration > 0f)
            result.Tip(BuildInlineTip(items));

        return result;
    }

    /// <summary>行の中の、ある横位置から幅ぶんを切り出す。</summary>
    private static Rect SliceAt(Rect row, float x, float width)
        => Rect.FromSize(new System.Numerics.Vector2(x, row.Min.Y), new System.Numerics.Vector2(width, row.Height));

    /// <summary>残りの表示だけの幅を測る。</summary>
    private static float MeasureRemaining(int hidden, bool countRemaining, ReadOnlySpan<char> gap)
    {
        inlineLength = 0;
        AppendRemaining(hidden, countRemaining, gap);

        return TextPainter.Measure(inlineBuffer.AsSpan(0, inlineLength)).X;
    }

    /// <summary>並べた文字の末尾へ、残りの数を足す。</summary>
    private static void AppendRemaining(int hidden, bool countRemaining, ReadOnlySpan<char> gap)
    {
        if (!countRemaining)
        {
            InlineAppend("…");
            return;
        }

        Span<char> digits = stackalloc char[12];

        InlineAppend(gap);
        InlineAppend("他 ");

        if (hidden.TryFormat(digits, out var written, provider: CultureInfo.InvariantCulture))
            InlineAppend(digits[..written]);
    }

    /// <summary>残りの表示を含めた幅を測る。測り終えたら足した分を戻す。</summary>
    private static float MeasureInline(int hidden, bool countRemaining, ReadOnlySpan<char> gap)
    {
        var mark = inlineLength;

        if (hidden > 0)
            AppendRemaining(hidden, countRemaining, gap);

        var measured = TextPainter.Measure(inlineBuffer.AsSpan(0, inlineLength)).X;
        inlineLength = mark;

        return measured;
    }

    /// <summary>ツールチップ用に、全部の語を 1 行ずつ並べる。</summary>
    private static ReadOnlySpan<char> BuildInlineTip(ReadOnlySpan<string> items)
    {
        var needed = items.Length;

        for (var i = 0; i < items.Length; i++)
            needed += items[i].Length;

        if (inlineTipBuffer.Length < needed)
            inlineTipBuffer = new char[Math.Max(inlineTipBuffer.Length * 2, needed)];

        var length = 0;

        for (var i = 0; i < items.Length; i++)
        {
            if (i > 0)
                inlineTipBuffer[length++] = '\n';

            items[i].AsSpan().CopyTo(inlineTipBuffer.AsSpan(length));
            length += items[i].Length;
        }

        return inlineTipBuffer.AsSpan(0, length);
    }

    /// <summary>並べた文字の末尾へ足す。足りなければ置き場を広げる。</summary>
    private static void InlineAppend(ReadOnlySpan<char> text)
    {
        if (inlineLength + text.Length > inlineBuffer.Length)
        {
            Array.Resize(
                ref inlineBuffer,
                Math.Max(inlineBuffer.Length * 2, inlineLength + text.Length));
        }

        text.CopyTo(inlineBuffer.AsSpan(inlineLength));
        inlineLength += text.Length;
    }
}
