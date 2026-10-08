using System;
using System.Numerics;

using EstellUtils.Diagnostics;
using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;

namespace EstellUtils.UI;

/// <summary>
/// レイアウト系 API。
/// </summary>
public static partial class EUi
{
    /// <summary>列を宣言できる上限。これを超える列は切り詰められる。</summary>
    private const int MaxColumns = 64;

    /// <summary>Spacer の誤用をすでに知らせたか。毎フレーム出しても仕方がないので一度だけ。</summary>
    private static bool spacerMisuseReported;

    /// <summary>次の要素を配置できる領域。</summary>
    public static Rect AvailableRect => UiContext.Current.Layout.AvailableRect;

    /// <summary>次の要素に使える幅。</summary>
    public static float AvailableWidth => AvailableRect.Width;

    /// <summary>
    /// 次に確保される要素の幅。列を宣言した行では列幅になる。
    /// </summary>
    /// <remarks>
    /// 折り返しの行数を先に数えてから領域を確保するウィジェットは、この値を基準にする。
    /// <see cref="AvailableWidth"/> は行全体の残りなので、列の中では食い違う。
    /// </remarks>
    public static float NextItemWidth
        => UiContext.Current.Layout.Current?.NextWidth ?? AvailableWidth;

    /// <summary>
    /// 列を並べたときに、列の間の隙間が占める合計幅。
    /// </summary>
    /// <param name="columnCount">列の数。</param>
    /// <param name="spacing">隙間。省略するとテーマの既定値。</param>
    /// <remarks>
    /// 固定幅の列を自分で計算するときは、この分を引いてから配る。
    /// 引き忘れると、右端の要素が行からはみ出して枠を突き抜ける。
    /// <code>
    /// // 入力欄 + ボタン 2 つを、与えられた幅へ収める
    /// var fieldWidth = totalWidth - (buttonWidth * 2f) - EUi.ColumnSpacing(3);
    ///
    /// using (EUi.Row(SizeSpec.Px(fieldWidth), SizeSpec.Px(buttonWidth), SizeSpec.Px(buttonWidth)))
    /// </code>
    /// そもそも 1 列を <see cref="SizeSpec.Fill"/> にすれば、残り幅の計算は
    /// レイアウト側が行うので、この関数は要らなくなる。
    /// </remarks>
    public static float ColumnSpacing(int columnCount, float? spacing = null)
        => (spacing ?? Metrics.ItemSpacing.X) * Math.Max(0, columnCount - 1);

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
    /// <param name="align">
    /// 高さの違う要素を縦方向のどこへ置くか。既定は中央。
    /// <see cref="Align.Stretch"/> にすると、行の高さいっぱいに引き伸ばす。
    /// </param>
    /// <param name="rowHeight">
    /// 行の基準となる高さ。省略するとテーマの標準ウィジェット高さ。
    /// 0 を渡すと揃えを行わず、要素の高さをそのまま使う。
    /// </param>
    /// <remarks>
    /// ボタン (24px) と文字 (16px) のように高さの違う要素を並べると、
    /// 上端で揃えた場合に文字だけ浮いて見える。既定で縦中央に揃える。
    /// </remarks>
    public static LayoutHandle HStack(
        float? spacing = null, bool wrap = false,
        Align align = Align.Center, float? rowHeight = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var gap = new Vector2(spacing ?? Metrics.ItemSpacing.X, Metrics.ItemSpacing.Y);

        ctx.Layout.Push(
            LayoutKind.Horizontal, ctx.Layout.AvailableRect, gap, default, wrap,
            default, align, rowHeight ?? Metrics.WidgetHeight);

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
        => Row(Align.Center, columns);

    /// <summary>
    /// 縦方向の揃え方を指定して、列幅を宣言した横並びを開く。
    /// </summary>
    /// <param name="align">高さの違う要素を縦方向のどこへ置くか。</param>
    /// <param name="columns">左から順の列幅指定。</param>
    public static LayoutHandle Row(Align align, params ReadOnlySpan<SizeSpec> columns)
        => Row(default, align, columns);

    /// <summary>
    /// 識別子を与えて、列幅を宣言した横並びを開く。
    /// </summary>
    /// <param name="id">
    /// 内容に合わせる列 (<see cref="SizeSpec.Auto"/>) の幅を覚えるための識別子。
    /// </param>
    /// <param name="align">高さの違う要素を縦方向のどこへ置くか。</param>
    /// <param name="columns">左から順の列幅指定。</param>
    /// <remarks>
    /// <see cref="SizeSpec.Auto"/> の列は、前のフレームに測った内容幅になる。
    /// 初回だけ 0 幅で、次のフレームから揃う。
    /// 識別子を省略すると覚える先が無いので、Auto の列は 0 幅のままになる。
    /// </remarks>
    public static LayoutHandle Row(
        ReadOnlySpan<char> id, Align align, params ReadOnlySpan<SizeSpec> columns)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var gap = new Vector2(Metrics.ItemSpacing.X, Metrics.ItemSpacing.Y);

        var scope = ctx.Layout.Push(
            LayoutKind.Horizontal, ctx.Layout.AvailableRect, gap, columns, false,
            default, align, Metrics.WidgetHeight);

        if (!scope.HasAutoColumn || id.IsEmpty)
            return new LayoutHandle(ctx.Layout);

        // 内容に合わせる列は、前のフレームに測った幅を使う
        var rowId = ctx.GetId(id);
        var stored = ctx.Store.GetOrCreate(rowId, static () => new float[MaxColumns]);

        scope.SetAutoWidths(stored);

        return new LayoutHandle(ctx.Layout, true, rowId);
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

        ctx.Layout.Push(
            LayoutKind.Horizontal, ctx.Layout.AvailableRect, gap, columns, wrap: true,
            default, Align.Center, Metrics.WidgetHeight);

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

        // 矩形を明示して開いたスコープなので、外側へは領域を申告しない
        return new LayoutHandle(ctx.Layout, commitToParent: false);
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

        // 領域は上で確保済みなので、閉じるときに二重で消費しないようにする
        return new LayoutHandle(ctx.Layout, commitToParent: false);
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

    /// <summary>
    /// 高さの指定方法を選んでスクロール領域を開く。
    /// </summary>
    /// <param name="id">スクロール位置を保持するための識別子。</param>
    /// <param name="height">
    /// 領域の高さ。<see cref="SizeSpec.Fill"/> で残り高さいっぱい。
    /// </param>
    /// <param name="reserveBelow">
    /// 下に空けておく高さ。送り領域のあとに何かを置く場合に、その分を渡す。
    /// </param>
    /// <param name="spacing">内容の要素間の空き。</param>
    /// <remarks>
    /// <para>
    /// <b><see cref="SizeSpec.Fill"/> は残り高さを全部使います。</b>
    /// そのあとに置いたものは場所が無くなって出ません。
    /// 即時モードでは「後ろに何が来るか」を先に知れないため、
    /// 下に置くものがある場合は <paramref name="reserveBelow"/> でその分を伝えてください。
    /// </para>
    /// <code>
    /// // 一覧の下に 1 行の注記を置く
    /// using (EUi.Scroll("list", SizeSpec.Fill, reserveBelow: EUi.LineHeight + EUi.Metrics.ItemSpacing.Y))
    /// {
    ///     foreach (var item in items)
    ///         EUi.Selectable(item.Name, item == selected);
    /// }
    ///
    /// EUi.Muted($"{hidden} 件は表示していません");
    /// </code>
    /// </remarks>
    public static ScrollHandle Scroll(
        ReadOnlySpan<char> id, SizeSpec height, float? reserveBelow = null, float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var available = MathF.Max(0f, AvailableHeight - (reserveBelow ?? 0f));
        var resolved = MathF.Max(0f, height.Resolve(available));

        return ScrollArea.Begin(id, resolved, spacing);
    }

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

    /// <summary>
    /// 列を宣言した行で、余った幅を埋めて以降の要素を右へ寄せる。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>列を宣言した行 (<see cref="Row(ReadOnlySpan{SizeSpec})"/>) の中でだけ使えます。</b>
    /// 埋めたい位置の列を <see cref="SizeSpec.Fill"/> にしておき、その列でこれを呼ぶ。
    /// </para>
    /// <code>
    /// // ボタンを行の右端へ寄せる
    /// using (EUi.Row(SizeSpec.Fill, 80f, 80f))
    /// {
    ///     EUi.Spacer();
    ///     EUi.Button("キャンセル", ButtonStyle.Normal, SizeSpec.Fill);
    ///     EUi.Button("OK", ButtonStyle.Primary, SizeSpec.Fill);
    /// }
    /// </code>
    /// <para>
    /// 列を宣言していない横並び (<see cref="HStack"/>) では何もしません。
    /// そこで残り幅を確保すると、後ろの要素へ配る幅が無くなって描かれなくなるためです。
    /// 即時モードでは「後ろに何が来るか」を先に知れないので、
    /// 右へ寄せたい場合は列を宣言してください。
    /// </para>
    /// <para>
    /// <see cref="Spacing"/> とは別物です。あちらは隙間を空けるだけで
    /// 列を消費しないため、列を宣言した行で使うと以降の要素が 1 つずつ前の列へずれます。
    /// </para>
    /// </remarks>
    public static void Spacer()
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var scope = ctx.Layout.Current;

        if (scope is not null && scope.Kind == LayoutKind.Horizontal && !scope.Columns.IsEmpty)
        {
            ctx.Allocate(SizeSpec.Fill, 0f);
            return;
        }

        // ここで残り幅を取ってしまうと、後ろの要素が幅 0 になって消える。
        // 黙って消えるのが一番たちが悪いので、何もせずに一度だけ知らせる
        if (spacerMisuseReported)
            return;

        spacerMisuseReported = true;

        UiLog.Warning(
            "EUi.Spacer() は列を宣言した行 (EUi.Row) の中でだけ使えます。" +
            "列のない横並びでは何もしません。右へ寄せたい場合は " +
            "EUi.Row(SizeSpec.Fill, ...) で列を宣言してください。");
    }

    /// <summary>
    /// 大きさを決めて場所を取り、その中へ並べるスコープを開く。
    /// </summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。省略すると標準のウィジェット高さ。</param>
    /// <param name="horizontal">横に並べるか。false なら縦積み。</param>
    /// <param name="spacing">要素間の空き。</param>
    /// <param name="padding">内側の余白。</param>
    /// <remarks>
    /// <para>
    /// 先に場所を取るので、行の中では<b>親の縦揃えが効きます</b>。
    /// また列を宣言した行では、ここで列を 1 つ消費します。
    /// </para>
    /// <para>
    /// <c>Reserve</c> で場所を取ってから <c>Region</c> を開く、という組み合わせを
    /// 1 つにまとめたものです。
    /// </para>
    /// <code>
    /// using (EUi.Row(SizeSpec.Fill, 120f))
    /// {
    ///     EUi.Label("目標");
    ///
    ///     using (EUi.Place(SizeSpec.Fill))       // 行の縦中央に置かれる
    ///         EUi.InputInt("##target", ref target);
    /// }
    /// </code>
    /// </remarks>
    public static LayoutHandle Place(
        SizeSpec width, float? height = null, bool horizontal = false,
        float? spacing = null, EdgeInsets? padding = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        // ここで場所を取る。行の中なら、このときに縦の位置が決まる
        var rect = ctx.Allocate(width, height ?? Metrics.WidgetHeight);

        var gap = horizontal
            ? new Vector2(spacing ?? Metrics.ItemSpacing.X, 0f)
            : new Vector2(0f, spacing ?? Metrics.ItemSpacing.Y);

        ctx.Layout.Push(
            horizontal ? LayoutKind.Horizontal : LayoutKind.Vertical,
            rect, gap, default, false, padding ?? default,
            horizontal ? Align.Center : Align.Start,
            horizontal ? rect.Height : 0f);

        // 場所は上で取ってあるので、閉じるときに二重で消費しない
        return new LayoutHandle(ctx.Layout, commitToParent: false);
    }

    /// <summary>
    /// 中身をひとまとまりとして扱うスコープを開く。
    /// </summary>
    /// <param name="id">ホバーの継続時間を覚えるための識別子。</param>
    /// <param name="horizontal">横に並べるか。false なら縦積み。</param>
    /// <param name="spacing">要素間の空き。</param>
    /// <remarks>
    /// <para>
    /// 閉じるときに、使った範囲を「直前のウィジェット」として記録します。
    /// そのあとの <see cref="Tip"/> は、塊のどこへマウスを乗せても出ます。
    /// </para>
    /// <para>
    /// これが無いと、<c>EUi.Tip</c> は最後に置いた要素 (文字を含む) にだけ付きます。
    /// <c>ImGui.BeginGroup</c> / <c>EndGroup</c> に当たるものです。
    /// </para>
    /// <code>
    /// using (EUi.Group("state", horizontal: true))
    /// {
    ///     EUi.Button("ON にする");
    ///     EUi.Label("停止中");
    /// }
    ///
    /// EUi.Tip("押すと有効になります");   // 塊全体に付く
    /// </code>
    /// </remarks>
    public static GroupHandle Group(
        ReadOnlySpan<char> id, bool horizontal = false, float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var euId = ctx.GetId(id);

        var gap = horizontal
            ? new Vector2(spacing ?? Metrics.ItemSpacing.X, Metrics.ItemSpacing.Y)
            : new Vector2(0f, spacing ?? Metrics.ItemSpacing.Y);

        ctx.Layout.Push(
            horizontal ? LayoutKind.Horizontal : LayoutKind.Vertical,
            ctx.Layout.AvailableRect, gap, default, false, default,
            horizontal ? Align.Center : Align.Start,
            horizontal ? Metrics.WidgetHeight : 0f);

        return new GroupHandle(euId);
    }

    /// <summary>
    /// 横並びのとき、次の行へ移る。
    /// </summary>
    /// <remarks>
    /// <b><c>ImGui.NewLine</c> とは別物です。</b> あちらは空行を 1 つ入れるもので、
    /// これは <see cref="HStack"/> の中で折り返す位置を指定するもの。
    /// 空行を入れたい場合は <see cref="BlankLine"/> か <see cref="Spacing"/> を使う。
    /// </remarks>
    public static void LineBreak() => UiContext.Current.Layout.Current?.NewLine();

    /// <summary>
    /// 1 行分の空きを入れる。<c>ImGui.NewLine</c> の置き換え。
    /// </summary>
    /// <param name="lines">空ける行数。</param>
    public static void BlankLine(int lines = 1)
        => Spacing(TextPainter.LineHeight * Math.Max(1, lines));

    /// <summary>
    /// 左に字下げした縦積みを開く。項目のぶら下がりを表すときに使う。
    /// <code>
    /// EUi.Checkbox("詳細を表示", ref showDetail);
    ///
    /// if (showDetail)
    /// {
    ///     using (EUi.Indent())
    ///     {
    ///         EUi.Checkbox("座標も出す", ref showCoords);
    ///     }
    /// }
    /// </code>
    /// </summary>
    /// <param name="amount">字下げの量。省略するとテーマの既定値。</param>
    public static LayoutHandle Indent(float? amount = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var inset = amount ?? Metrics.IndentWidth;
        var gap = new Vector2(0f, Metrics.ItemSpacing.Y);

        ctx.Layout.Push(
            LayoutKind.Vertical, ctx.Layout.AvailableRect, gap,
            default, false, new EdgeInsets(inset, 0f, 0f, 0f));

        return new LayoutHandle(ctx.Layout);
    }

    /// <summary>
    /// 生の <c>ImGui.*</c> が進めたカーソル位置を、レイアウトへ取り込む。
    /// </summary>
    /// <remarks>
    /// EstellUtils のウィジェットは ImGui 側のカーソルも一緒に動かすので、
    /// 「EstellUtils → 生 ImGui」の順に呼ぶ分には何もしなくても位置が揃う。
    /// 逆に「生 ImGui → EstellUtils」と続けるときは、間でこれを呼ぶ。
    /// <code>
    /// EUi.Label("ここまで EstellUtils");
    /// ImGui.TextColored(color, "生の ImGui");
    /// EUi.SyncFromImGui();               // ImGui が進めた分を取り込む
    /// EUi.Label("続きも正しい位置に出る");
    /// </code>
    /// </remarks>
    public static void SyncFromImGui()
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        ctx.Layout.Current?.SetCursor(Dalamud.Bindings.ImGui.ImGui.GetCursorScreenPos());
    }

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

/// <summary>
/// <c>using</c> で塊のスコープを閉じるハンドル。
/// </summary>
/// <remarks>
/// 閉じるときに、使った範囲を「直前のウィジェット」として記録する。
/// </remarks>
public readonly struct GroupHandle : IDisposable
{
    private readonly EuId id;

    internal GroupHandle(EuId id) => this.id = id;

    /// <inheritdoc/>
    public void Dispose()
    {
        var ctx = UiContext.Current;
        var scope = ctx.Layout.Current;

        if (scope is null)
            return;

        var bounds = scope.ItemCount > 0
            ? scope.ContentBounds
            : Rect.FromSize(scope.Bounds.Min, Vector2.Zero);

        ctx.Layout.Pop();

        // 塊全体を直前のウィジェットとして記録する。
        // 継続時間は専用の ID で覚えるので、中の要素と取り合いにならない
        var duration = ctx.TrackHover(bounds, this.id);
        ctx.SetLastItem(bounds, duration);
    }
}
