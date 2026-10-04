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
/// <param name="Wrap">
/// この列の文字を折り返すか。true にすると、行の高さが中身に合わせて伸びる。
/// </param>
public readonly record struct TableColumn(
    string Header, SizeSpec Width, Align Align = Align.Start, bool Wrap = false);

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
    /// <param name="columns">列定義。行と同じものを渡すこと。</param>
    /// <param name="height">見出し行の高さ。</param>
    /// <param name="reserveScrollbar">
    /// 送りのつまみの分だけ、右端を空けておくか。
    /// </param>
    /// <remarks>
    /// 見出しを送り領域の外に置いて固定する場合、<paramref name="reserveScrollbar"/> を
    /// true にする。送り領域はつまみが出ているとき内容の右端を削るため、
    /// 何もしないと見出しと行で列がずれる。しかもつまみは行数で出たり消えたりするので、
    /// 行が増えた瞬間に見出しだけズレる、という気づきにくい壊れ方をする。
    /// </remarks>
    public static void TableHeader(
        ReadOnlySpan<TableColumn> columns, float? height = null, bool reserveScrollbar = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (columns.Length == 0)
            return;

        var rowHeight = height ?? Metrics.WidgetHeight;
        var available = ctx.Layout.AvailableRect;
        var rowRect = Rect.FromSize(available.Min, new Vector2(available.Width, rowHeight));

        Painter.Rect(rowRect, EuColor.WithAlpha(Colors.Surface, 0.9f), Metrics.WidgetRounding, Corners.Top);

        if (reserveScrollbar)
        {
            // 送り領域が内容の右端を削る分と、同じだけ空けておく
            rowRect = rowRect.WithWidth(MathF.Max(0f, rowRect.Width - ScrollbarInset()));
        }

        Span<SizeSpec> widths = stackalloc SizeSpec[columns.Length];
        for (var i = 0; i < columns.Length; i++)
            widths[i] = columns[i].Width;

        ctx.Layout.Push(
            LayoutKind.Horizontal, rowRect, new Vector2(Metrics.ItemSpacing.X, 0f), widths,
            false, default, Align.Center, rowHeight);

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

        // セルが寄せ方を引けるよう、この行の列定義を控えておく
        RememberTableColumns(columns);

        // 折り返す列があると、中身を描くまで必要な高さが分からない。
        // 前のフレームで測った高さを使い、描き終えてから次回ぶんを覚える
        var rowId = ctx.GetId("##euTableRow").Child(index);
        var measured = ctx.Store.GetRef(rowId).MeasuredHeight;

        var rowHeight = height
            ?? (measured > 0f ? measured : Metrics.WidgetHeight);
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
            widths[..columns.Length], false, default, Align.Center, rowHeight);

        // 高さを明示していなければ、折り返す列とセルの中身の両方を拾って次フレームへ渡す
        return new TableRowHandle(rowRect, rowId, height is null);
    }

    /// <summary>
    /// 送り領域が、つまみのために内容の右端から削る幅。
    /// </summary>
    /// <remarks>
    /// 送り領域の外に置いたものと、中に置いたものの幅を揃えたいときに使う。
    /// </remarks>
    public static float ScrollbarInset()
        => Metrics.ScrollbarWidth + Metrics.SpacingSm;

    /// <summary>この行の列定義。セルが寄せ方を引くために使う。</summary>
    private static TableColumn[] tableColumns = [];

    /// <summary>控えている列の数。</summary>
    private static int tableColumnCount;

    /// <summary>行の列定義を控える。毎フレームの確保を避けて配列を使い回す。</summary>
    private static void RememberTableColumns(ReadOnlySpan<TableColumn> columns)
    {
        if (tableColumns.Length < columns.Length)
            tableColumns = new TableColumn[Math.Max(8, columns.Length * 2)];

        columns.CopyTo(tableColumns);
        tableColumnCount = columns.Length;
    }

    /// <summary>控えた列定義から、寄せ方を引く。</summary>
    private static Align ColumnAlignAt(int index)
        => index >= 0 && index < tableColumnCount ? tableColumns[index].Align : Align.Start;

    /// <summary>
    /// 現在の列を 1 つ消費し、その中へ横並びのレイアウトを開く。
    /// </summary>
    /// <param name="spacing">中身の要素間の空き。</param>
    /// <param name="align">中身を縦方向のどこへ置くか。</param>
    /// <param name="padding">セルの内側の余白。省略すると文字のセルと同じ左右余白。</param>
    /// <remarks>
    /// <para>
    /// 列を宣言した行では、ウィジェットを 1 つ置くごとに次の列へ進みます。
    /// 「文字 + ボタン」のように 1 つのセルへ複数を置きたい場合は、これで囲んでください。
    /// </para>
    /// <code>
    /// using (EUi.TableRow(columns, i))
    /// {
    ///     EUi.TableCell(item.Name);
    ///
    ///     using (EUi.Cell())
    ///     {
    ///         EUi.Label(item.State);
    ///
    ///         if (EUi.Button("再開"))
    ///             Resume(item);
    ///     }
    /// }
    /// </code>
    /// <para>
    /// 中で置くものの数が行ごとに変わっても、消費する列は 1 つのままです。
    /// 表の外でも使えますが、その場合は残り幅を 1 つ分として取ります。
    /// </para>
    /// </remarks>
    public static CellHandle Cell(
        float? spacing = null, Align align = Align.Center, EdgeInsets? padding = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var scope = ctx.Layout.Current;
        var height = scope?.Bounds.Height ?? Metrics.WidgetHeight;

        // 列が宣言されていれば、ここで列幅が使われる
        var rect = ctx.Allocate(SizeSpec.Fill, height);

        var gap = new Vector2(spacing ?? Metrics.ItemSpacing.X, 0f);
        var inset = padding ?? EdgeInsets.Horizontal(Metrics.SpacingSm);

        ctx.Layout.Push(
            LayoutKind.Horizontal, rect, gap, default, false, inset, align, height);

        // 領域は上で確保済み。閉じるときは、はみ出した高さだけを行へ伝える
        return new CellHandle(scope, rect);
    }

    /// <summary>
    /// 現在の列を 1 つ消費し、その中へ縦積みのレイアウトを開く。
    /// </summary>
    /// <param name="spacing">中身の要素間の空き。</param>
    /// <param name="padding">セルの内側の余白。</param>
    /// <remarks>
    /// 名前の下に補足を添える、といった 2 段のセルに使う。
    /// 行の高さは <c>TableRow</c> へ渡した高さのままなので、
    /// 2 段ぶんの高さを指定しておくこと。
    /// </remarks>
    public static CellHandle CellStack(float? spacing = null, EdgeInsets? padding = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var scope = ctx.Layout.Current;
        var height = scope?.Bounds.Height ?? Metrics.WidgetHeight;
        var rect = ctx.Allocate(SizeSpec.Fill, height);

        var gap = new Vector2(0f, spacing ?? Metrics.SpacingXs);
        var inset = padding ?? EdgeInsets.Horizontal(Metrics.SpacingSm);

        ctx.Layout.Push(LayoutKind.Vertical, rect, gap, default, false, inset);

        return new CellHandle(scope, rect);
    }

    /// <summary>表のセルへ文字列を表示する。</summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="align">寄せ方。</param>
    /// <param name="color">文字色。</param>
    /// <param name="tipWhenTruncated">省略したときに、全文をツールチップで見せるか。</param>
    public static WidgetResult TableCell(
        ReadOnlySpan<char> text, Align? align = null, uint? color = null,
        bool tipWhenTruncated = true)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var scope = ctx.Layout.Current;
        var height = scope?.Bounds.Height ?? Metrics.WidgetHeight;

        // 寄せ方を省略したら、列定義の指定に従う
        var resolvedAlign = align ?? ColumnAlignAt(scope?.ColumnIndex ?? 0);

        var rect = ctx.Allocate(SizeSpec.Fill, height);

        var textRect = rect.Shrink(EdgeInsets.Horizontal(Metrics.SpacingSm));
        var truncated = TextPainter.Measure(text).X > textRect.Width + 1f;

        TextPainter.TextIn(textRect, color ?? Colors.Text, text, resolvedAlign, Align.Center);

        var result = MakeTextResult(ctx, rect) with { Truncated = truncated };

        if (truncated && tipWhenTruncated)
            result.Tip(text);

        return result;
    }
}

/// <summary>
/// <c>using</c> でセルを閉じるハンドル。
/// </summary>
/// <remarks>
/// 中身が確保しておいた高さを超えた場合、その分を行へ伝える。
/// 行の高さを <c>TableColumn.Wrap</c> に任せているとき、
/// セルの中身も一緒に数えられるようにするため。
/// </remarks>
public readonly struct CellHandle : IDisposable
{
    private readonly LayoutScope? parent;
    private readonly Rect rect;

    internal CellHandle(LayoutScope? parent, Rect rect)
    {
        this.parent = parent;
        this.rect = rect;
    }

    /// <summary>セルの矩形。</summary>
    public Rect Rect => this.rect;

    /// <inheritdoc/>
    public void Dispose()
    {
        var ctx = UiContext.Current;
        var consumed = ctx.Layout.Current?.ConsumedSize ?? Vector2.Zero;

        // 領域は開くときに確保済みなので、外側へは申告しない
        ctx.Layout.Pop(commitToParent: false);

        // 中身がはみ出した分だけ、行の使用範囲を広げる。
        // Allocate で申告すると列まで進んでしまうので、範囲だけを伝える
        if (this.parent is null || consumed.Y <= this.rect.Height + 0.5f)
            return;

        this.parent.ExpandContent(
            Rect.FromSize(this.rect.Min, new Vector2(this.rect.Width, consumed.Y)));
    }
}

/// <summary><c>using</c> で表の行を閉じるハンドル。</summary>
public readonly struct TableRowHandle : IDisposable
{
    private readonly Rect rowRect;
    private readonly EuId id;
    private readonly bool autoHeight;

    internal TableRowHandle(Rect rowRect, EuId id = default, bool autoHeight = false)
    {
        this.rowRect = rowRect;
        this.id = id;
        this.autoHeight = autoHeight;
    }

    /// <summary>行の矩形。行全体のクリック判定などに使う。</summary>
    public Rect Rect => this.rowRect;

    /// <inheritdoc/>
    public void Dispose()
    {
        var ctx = UiContext.Current;
        var consumed = ctx.Layout.Current?.ConsumedSize ?? Vector2.Zero;

        // 行の高さは固定なので、レイアウトの実測ではなく行矩形の分を消費させる
        ctx.Layout.Pop(commitToParent: false);
        ctx.Allocate(this.rowRect.Size);

        if (!this.autoHeight)
            return;

        // 折り返す列があるときは、実際に使った高さを次のフレームへ持ち越す。
        // 内容が変わった直後の 1 フレームだけずれるが、次で揃う
        ref var state = ref ctx.Store.GetRef(this.id);
        state.MeasuredHeight = MathF.Max(EUi.Metrics.WidgetHeight, consumed.Y);
    }
}
