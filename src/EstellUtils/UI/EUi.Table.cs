using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>表の列定義。</summary>
/// <param name="Header">見出しに表示する文字列。</param>
/// <param name="Width">列幅。<see cref="SizeSpec.Fill"/> で残り幅を分け合う。</param>
/// <param name="Align">セルの中身の寄せ方。</param>
public readonly record struct TableColumn(string Header, SizeSpec Width, Align Align = Align.Start);

/// <summary>
/// 表。
/// </summary>
/// <remarks>
/// 列定義を呼び出し側で保持して、見出しと各行へ同じものを渡す形にしている。
/// 列幅の解決はレイアウトの <see cref="LayoutKind.Horizontal"/> スコープがそのまま担うので、
/// 表のためだけの特別な仕組みは持たない。
/// <code>
/// private static readonly TableColumn[] Columns =
/// [
///     new("名前", SizeSpec.Fill),
///     new("数", 60f, Align.End),
/// ];
///
/// EUi.TableHeader(Columns);
/// for (var i = 0; i &lt; items.Count; i++)
/// {
///     using (EUi.TableRow(Columns, i))
///     {
///         EUi.TableCell(items[i].Name);
///         EUi.TableCell(items[i].Count.ToString());
///     }
/// }
/// </code>
/// </remarks>
public static partial class EUi
{
    /// <summary>表の見出し行を描く。</summary>
    /// <param name="columns">列定義。</param>
    /// <param name="height">見出し行の高さ。</param>
    public static void TableHeader(ReadOnlySpan<TableColumn> columns, float? height = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (columns.Length == 0)
            return;

        var rowHeight = height ?? Metrics.WidgetHeight;
        var available = ctx.Layout.AvailableRect;
        var rowRect = Rect.FromSize(available.Min, new Vector2(available.Width, rowHeight));

        Painter.Rect(rowRect, EuColor.WithAlpha(Colors.Surface, 0.9f), Metrics.WidgetRounding, Corners.Top);

        Span<SizeSpec> widths = stackalloc SizeSpec[columns.Length];
        for (var i = 0; i < columns.Length; i++)
            widths[i] = columns[i].Width;

        ctx.Layout.Push(
            LayoutKind.Horizontal, rowRect, new Vector2(Metrics.ItemSpacing.X, 0f), widths);

        for (var i = 0; i < columns.Length; i++)
        {
            var cellRect = ctx.Allocate(columns[i].Width, rowHeight);
            TextPainter.TextIn(
                cellRect.Shrink(EdgeInsets.Horizontal(Metrics.SpacingSm)),
                Colors.TextHeading, columns[i].Header, columns[i].Align, Align.Center);
        }

        ctx.Layout.Pop(commitToParent: false);
        ctx.Allocate(rowRect.Size);

        Painter.HLine(rowRect.Min.X, rowRect.Max.X, rowRect.Max.Y, Colors.Separator);
    }

    /// <summary>
    /// 表の 1 行を開く。中で <see cref="TableCell"/> か任意のウィジェットを列の数だけ並べる。
    /// </summary>
    /// <param name="columns">列定義。見出しと同じものを渡すこと。</param>
    /// <param name="index">行番号。交互に背景色を変えるのに使う。</param>
    /// <param name="height">行の高さ。</param>
    /// <param name="selected">選択中の行として強調するか。</param>
    public static TableRowHandle TableRow(
        ReadOnlySpan<TableColumn> columns, int index, float? height = null, bool selected = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var rowHeight = height ?? Metrics.WidgetHeight;
        var available = ctx.Layout.AvailableRect;
        var rowRect = Rect.FromSize(available.Min, new Vector2(available.Width, rowHeight));

        if (selected)
            Painter.Rect(rowRect, Colors.Selection);
        else if (index % 2 == 1)
            Painter.Rect(rowRect, EuColor.WithAlpha(Colors.Surface, 0.45f));

        Span<SizeSpec> widths = stackalloc SizeSpec[Math.Max(1, columns.Length)];
        for (var i = 0; i < columns.Length; i++)
            widths[i] = columns[i].Width;

        ctx.Layout.Push(
            LayoutKind.Horizontal, rowRect, new Vector2(Metrics.ItemSpacing.X, 0f),
            widths[..columns.Length]);

        return new TableRowHandle(rowRect);
    }

    /// <summary>表のセルへ文字列を表示する。</summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="align">寄せ方。</param>
    /// <param name="color">文字色。</param>
    /// <param name="tipWhenTruncated">省略したときに、全文をツールチップで見せるか。</param>
    public static WidgetResult TableCell(
        ReadOnlySpan<char> text, Align align = Align.Start, uint? color = null,
        bool tipWhenTruncated = true)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var scope = ctx.Layout.Current;
        var height = scope?.Bounds.Height ?? Metrics.WidgetHeight;
        var rect = ctx.Allocate(SizeSpec.Fill, height);

        var textRect = rect.Shrink(EdgeInsets.Horizontal(Metrics.SpacingSm));
        var truncated = TextPainter.Measure(text).X > textRect.Width + 1f;

        TextPainter.TextIn(textRect, color ?? Colors.Text, text, align, Align.Center);

        var result = MakeTextResult(ctx, rect) with { Truncated = truncated };

        if (truncated && tipWhenTruncated)
            result.Tip(text);

        return result;
    }
}

/// <summary><c>using</c> で表の行を閉じるハンドル。</summary>
public readonly struct TableRowHandle : IDisposable
{
    private readonly Rect rowRect;

    internal TableRowHandle(Rect rowRect) => this.rowRect = rowRect;

    /// <summary>行の矩形。行全体のクリック判定などに使う。</summary>
    public Rect Rect => this.rowRect;

    /// <inheritdoc/>
    public void Dispose()
    {
        var ctx = UiContext.Current;

        // 行の高さは固定なので、レイアウトの実測ではなく行矩形の分を消費させる
        ctx.Layout.Pop(commitToParent: false);
        ctx.Allocate(this.rowRect.Size);
    }
}
