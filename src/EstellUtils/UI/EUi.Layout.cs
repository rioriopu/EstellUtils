using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;

namespace EstellUtils.UI;

/// <summary>
/// レイアウト系 API。
/// </summary>
public static partial class EUi
{
    /// <summary>列を宣言できる上限。これを超える列は切り詰められる。</summary>
    private const int MaxColumns = 64;

    /// <summary>次の要素を配置できる領域。</summary>
    public static Rect AvailableRect => UiContext.Current.Layout.AvailableRect;

    /// <summary>次の要素に使える幅。</summary>
    public static float AvailableWidth => AvailableRect.Width;

    /// <summary>次の要素に使える高さ。</summary>
    public static float AvailableHeight => AvailableRect.Height;

    /// <summary>
    /// 縦に積むレイアウトを開く。
    /// <code>
    /// using (EUi.VStack())
    /// {
    ///     EUi.Label("上");
    ///     EUi.Label("下");
    /// }
    /// </code>
    /// </summary>
    /// <param name="spacing">要素間の空き。省略するとテーマの既定値。</param>
    public static LayoutHandle VStack(float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var gap = new Vector2(0f, spacing ?? Metrics.ItemSpacing.Y);
        ctx.Layout.Push(LayoutKind.Vertical, ctx.Layout.AvailableRect, gap);
        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// 横に並べるレイアウトを開く。
    /// </summary>
    /// <param name="spacing">要素間の空き。省略するとテーマの既定値。</param>
    /// <param name="wrap">右端に達したら折り返すか。</param>
    public static LayoutHandle HStack(float? spacing = null, bool wrap = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var gap = new Vector2(spacing ?? Metrics.ItemSpacing.X, Metrics.ItemSpacing.Y);
        ctx.Layout.Push(LayoutKind.Horizontal, ctx.Layout.AvailableRect, gap, default, wrap);
        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// 列幅を先に宣言した横並びを開く。残り幅の配分が必要な行はこれを使う。
    /// <code>
    /// using (EUi.Row(120f, SizeSpec.Fill, 60f))
    /// {
    ///     EUi.Label("名前");
    ///     EUi.TextInput("##name", ref name);
    ///     EUi.Button("削除");
    /// }
    /// </code>
    /// </summary>
    /// <param name="columns">左から順の列幅指定。</param>
    public static LayoutHandle Row(params ReadOnlySpan<SizeSpec> columns)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var gap = new Vector2(Metrics.ItemSpacing.X, Metrics.ItemSpacing.Y);
        ctx.Layout.Push(LayoutKind.Horizontal, ctx.Layout.AvailableRect, gap, columns);
        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// 均等幅の列を並べ、右端で折り返すグリッドを開く。
    /// </summary>
    /// <param name="columnCount">列数。</param>
    /// <param name="spacing">要素間の空き。</param>
    public static LayoutHandle Grid(int columnCount, float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var count = Math.Clamp(columnCount, 1, MaxColumns);
        Span<SizeSpec> columns = stackalloc SizeSpec[count];
        columns.Fill(SizeSpec.Fill);

        var gap = new Vector2(spacing ?? Metrics.ItemSpacing.X, spacing ?? Metrics.ItemSpacing.Y);
        ctx.Layout.Push(LayoutKind.Horizontal, ctx.Layout.AvailableRect, gap, columns, wrap: true);
        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// 内側に余白を取った縦積みを開く。カードの中身などに使う。
    /// </summary>
    /// <param name="padding">内側の余白。省略するとテーマのカード余白。</param>
    /// <param name="spacing">要素間の空き。</param>
    public static LayoutHandle Inset(EdgeInsets? padding = null, float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var gap = new Vector2(0f, spacing ?? Metrics.ItemSpacing.Y);
        ctx.Layout.Push(
            LayoutKind.Vertical, ctx.Layout.AvailableRect, gap,
            default, false, padding ?? Metrics.CardPadding);

        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// 明示した矩形の中へ縦積みでレイアウトする。ウィンドウ本体など、
    /// 領域が先に決まっている場所で使う。
    /// </summary>
    public static LayoutHandle Region(Rect bounds, EdgeInsets? padding = null, float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var gap = new Vector2(0f, spacing ?? Metrics.ItemSpacing.Y);
        ctx.Layout.Push(LayoutKind.Vertical, bounds, gap, default, false, padding ?? default);
        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// 大きさを固定した領域を確保し、その中へ縦積みでレイアウトする。
    /// </summary>
    public static LayoutHandle Sized(SizeSpec width, float height, EdgeInsets? padding = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var rect = ctx.Allocate(width, height);
        var gap = new Vector2(0f, Metrics.ItemSpacing.Y);
        ctx.Layout.Push(LayoutKind.Vertical, rect, gap, default, false, padding ?? default);
        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// スクロールする領域を開く。内容がはみ出すと自前のスクロールバーが出る。
    /// <code>
    /// using (EUi.Scroll("log", 200f))
    /// {
    ///     foreach (var line in lines)
    ///         EUi.Label(line);
    /// }
    /// </code>
    /// </summary>
    /// <param name="id">スクロール位置を保持するための識別子。</param>
    /// <param name="height">領域の高さ。</param>
    /// <param name="spacing">内容の要素間の空き。</param>
    public static ScrollHandle Scroll(ReadOnlySpan<char> id, float height, float? spacing = null)
        => ScrollArea.Begin(id, height, spacing);

    /// <summary>空きを入れる。省略するとテーマの標準の空き。</summary>
    public static void Spacing(float? amount = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var value = amount ?? Metrics.SpacingMd;
        var scope = ctx.Layout.Current;

        if (scope is not null)
            scope.AddSpacing(value);
        else
            ctx.Allocate(new Vector2(0f, value));
    }

    /// <summary>横並びのとき、次の行へ移る。</summary>
    public static void NewLine() => UiContext.Current.Layout.Current?.NewLine();

    /// <summary>領域だけを確保して矩形を得る。独自描画を差し込みたいときに使う。</summary>
    public static Rect Reserve(Vector2 size)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();
        return ctx.Allocate(size);
    }

    /// <summary>領域だけを確保して矩形を得る。</summary>
    public static Rect Reserve(SizeSpec width, float height)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();
        return ctx.Allocate(width, height);
    }
}
