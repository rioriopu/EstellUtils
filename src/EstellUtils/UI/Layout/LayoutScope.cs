using System;
using System.Runtime.CompilerServices;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Layout;

/// <summary>レイアウトの並べ方。</summary>
public enum LayoutKind
{
    /// <summary>縦に積む。</summary>
    Vertical,

    /// <summary>横に並べる。</summary>
    Horizontal,
}

/// <summary>
/// 1 段分のレイアウト状態。要素を並べる位置を管理する。
/// </summary>
/// <remarks>
/// <para>
/// immediate mode では「全要素を見てから配置を決める」ことができないため、
/// 残り領域の配分が要る場合 (<see cref="SizeMode.Fill"/>) は
/// <see cref="Columns"/> で列を先に宣言してもらう方式を採る。
/// これなら 1 パスで正確に配分できる。
/// </para>
/// <para>
/// 入れ子にすると、内側のスコープは閉じるときに実際に使った大きさを外側へ申告する。
/// </para>
/// </remarks>
public sealed class LayoutScope
{
    private SizeSpec[] columnBuffer = Array.Empty<SizeSpec>();
    private float[] columnWidths = Array.Empty<float>();
    private float[] autoWidths = Array.Empty<float>();
    private float[] measuredWidths = Array.Empty<float>();
    private int columnCount;
    private bool hasAutoColumn;

    /// <summary>並べ方。</summary>
    public LayoutKind Kind { get; private set; }

    /// <summary>配置できる領域。</summary>
    public Rect Bounds { get; private set; }

    /// <summary>次に要素を置く位置 (画面座標)。</summary>
    public Vector2 Cursor { get; private set; }

    /// <summary>要素同士の空き。</summary>
    public Vector2 Spacing { get; private set; }

    /// <summary>横並びのとき、行の右端に達したら折り返すか。</summary>
    public bool Wrap { get; private set; }

    /// <summary>横並びのときの現在行の高さ。</summary>
    public float LineHeight { get; private set; }

    /// <summary>
    /// 横並びのとき、高さの違う要素を縦方向のどこへ置くか。
    /// </summary>
    public Align CrossAlign { get; private set; }

    /// <summary>
    /// 行の基準となる高さ。0 なら要素の高さをそのまま使う (揃えは効かない)。
    /// </summary>
    public float RowHeight { get; private set; }

    /// <summary>列定義があるときの現在の列番号。</summary>
    public int ColumnIndex { get; private set; }

    /// <summary>このスコープで実際に使われた範囲。</summary>
    public Rect ContentBounds { get; private set; }

    /// <summary>配置した要素の数。</summary>
    public int ItemCount { get; private set; }

    /// <summary>列定義。宣言されていなければ空。</summary>
    public ReadOnlySpan<SizeSpec> Columns => this.columnBuffer.AsSpan(0, this.columnCount);

    /// <summary>次の要素に使える残り幅。</summary>
    public float RemainingWidth => MathF.Max(0f, this.Bounds.Max.X - this.Cursor.X);

    /// <summary>
    /// 次の要素が実際に使える残り幅。要素間の空きを差し引く。
    /// </summary>
    /// <remarks>
    /// <see cref="RemainingWidth"/> は空きを含んだままなので、2 つ目以降の要素へ
    /// そのまま配ると空きの分だけ右へはみ出す。
    /// </remarks>
    private float RemainingWidthForNext
        => this.ItemCount > 0
            ? MathF.Max(0f, this.RemainingWidth - this.Spacing.X)
            : this.RemainingWidth;

    /// <summary>
    /// 次に確保される要素の幅。
    /// </summary>
    /// <remarks>
    /// 列を宣言した行では列幅、それ以外は使える幅。
    /// 折り返しの行数を先に数えるウィジェットは、この値を基準にする。
    /// <c>AvailableWidth</c> を使うと、列の中で幅が食い違う。
    /// </remarks>
    public float NextWidth
    {
        get
        {
            if (this.Kind == LayoutKind.Horizontal && this.columnCount > 0)
                return this.columnWidths[Math.Min(this.ColumnIndex, this.columnCount - 1)];

            return this.Kind == LayoutKind.Vertical
                ? this.Bounds.Width
                : this.RemainingWidthForNext;
        }
    }

    /// <summary>
    /// 次の要素に使える残り高さ。
    /// </summary>
    /// <remarks>
    /// 送り領域の中身は、下端を遠くへ置いて「いくらでも積める」状態にしてある。
    /// その値をそのまま返すと、残り高さが 100 万 px になって
    /// <c>SizeSpec.Fill</c> の高さが現実離れする。見えている範囲を上限にする。
    /// </remarks>
    public float RemainingHeight
    {
        get
        {
            var remaining = MathF.Max(0f, this.Bounds.Max.Y - this.Cursor.Y);

            if (this.FillHeight <= 0f)
                return remaining;

            var used = MathF.Max(0f, this.Cursor.Y - this.Bounds.Min.Y);
            return MathF.Min(remaining, MathF.Max(0f, this.FillHeight - used));
        }
    }

    /// <summary>
    /// 高さを配分するときの上限。0 なら <see cref="Bounds"/> の下端まで使う。
    /// </summary>
    /// <remarks>
    /// 送り領域が「見えている高さ」を入れる。中身を積める範囲とは別に持つ。
    /// </remarks>
    public float FillHeight { get; private set; }

    /// <summary>外側から見たときの余白。<see cref="Bounds"/> はこれを差し引いた領域になる。</summary>
    public EdgeInsets Padding { get; private set; }

    /// <summary>
    /// 余白を差し引く前の左上。<see cref="ConsumedSize"/> の起点。
    /// </summary>
    /// <remarks>
    /// 外側へ大きさを申告するとき、起点がここでないと二重に数えられる。
    /// 中身を置くたびに ImGui のカーソルは終端へ動いているので、戻す先として使う。
    /// </remarks>
    public Vector2 Origin => new(
        this.Bounds.Min.X - this.Padding.Left,
        this.Bounds.Min.Y - this.Padding.Top);

    /// <summary>
    /// このスコープが消費した大きさ。余白を含む (外側のレイアウトへ申告する値)。
    /// </summary>
    public Vector2 ConsumedSize
    {
        get
        {
            if (this.ItemCount == 0)
                return this.Padding.Total;

            return this.ContentSize + this.Padding.Total;
        }
    }

    /// <summary>
    /// 中身そのものの大きさ。余白を含まない。
    /// </summary>
    /// <remarks>
    /// 外へ余白を足し直す側 (中身に合わせる窓など) が使う。
    /// <see cref="ConsumedSize"/> を渡すと余白が二重に入る。
    /// </remarks>
    public Vector2 ContentSize
        => this.ItemCount == 0
            ? Vector2.Zero
            : new Vector2(
                this.ContentBounds.Max.X - this.Bounds.Min.X,
                this.ContentBounds.Max.Y - this.Bounds.Min.Y);

    /// <summary>
    /// 中身が希望した幅。余白を含まない。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 頭打ちの前の希望幅なので、窓の幅で切られた部品でも本来の幅が分かる。
    /// 中身に合わせる窓が、長い文字に合わせて広がるために使う。
    /// </para>
    /// <para>
    /// <see cref="SizeMode.Fill"/> と <see cref="SizeMode.Ratio"/> の指定は数えない。
    /// あれは窓の幅から決まる値なので、数えると「窓が広がる → 希望も広がる」の
    /// 繰り返しになって止まらない。
    /// </para>
    /// </remarks>
    public float RequestedContentWidth
        => this.ItemCount == 0 ? 0f : MathF.Max(0f, this.requestedMaxX - this.Bounds.Min.X);

    /// <summary>
    /// 次の要素が置かれる領域。入れ子のスコープを開くときの基準になる。
    /// </summary>
    public Rect PeekAvailable()
    {
        var next = this.ItemCount == 0
            ? this.Cursor
            : this.Kind == LayoutKind.Vertical
                ? new Vector2(this.Bounds.Min.X, this.Cursor.Y + this.Spacing.Y)
                : new Vector2(this.Cursor.X + this.Spacing.X, this.Cursor.Y);

        var max = this.Bounds.Max;

        // 送り領域の中では、下端は「いくらでも積める」位置にある。
        // 使える高さとしては、見えている範囲を返す
        if (this.FillHeight > 0f)
            max = new Vector2(max.X, this.Bounds.Min.Y + this.FillHeight);

        return new Rect(next, Vector2.Max(next, max));
    }

    /// <summary>スコープを初期化する (プールから再利用するため公開している)。</summary>
    public void Reset(
        LayoutKind kind, Rect bounds, Vector2 spacing, ReadOnlySpan<SizeSpec> columns, bool wrap,
        EdgeInsets padding = default, Align crossAlign = Align.Start, float rowHeight = 0f,
        float fillHeight = 0f)
    {
        this.CrossAlign = crossAlign;
        this.RowHeight = rowHeight;
        this.FillHeight = fillHeight;

        this.Padding = padding;
        bounds = bounds.Shrink(padding);

        this.Kind = kind;
        this.Bounds = bounds;
        this.Cursor = bounds.Min;
        this.Spacing = spacing;
        this.Wrap = wrap;
        this.LineHeight = 0f;
        this.ColumnIndex = 0;
        this.ItemCount = 0;
        this.ContentBounds = Rect.FromSize(bounds.Min, Vector2.Zero);
        this.requestedMaxX = bounds.Min.X;
        this.pendingRequestedWidth = -1f;

        // 呼び出し側の Span は借り物なので、内部バッファへ複製して保持する
        this.columnCount = columns.Length;
        if (this.columnCount > 0)
        {
            if (this.columnBuffer.Length < this.columnCount)
                this.columnBuffer = new SizeSpec[Math.Max(8, this.columnCount * 2)];

            columns.CopyTo(this.columnBuffer);
        }

        this.hasAutoColumn = false;

        for (var i = 0; i < this.columnCount; i++)
        {
            if (this.columnBuffer[i].Mode == SizeMode.Auto)
            {
                this.hasAutoColumn = true;
                break;
            }
        }

        // 前にこのスコープを使った行の実測を引き継がない。プールから取り回すので、
        // 消さないと識別子を渡していない行が別の行の幅を拾う
        Array.Clear(this.autoWidths);

        this.ResolveColumns();
    }

    /// <summary>列定義から実際の列幅を計算する。</summary>
    /// <remarks>
    /// 配分そのものは <see cref="ColumnLayout"/> が行う。
    /// 描画にも ImGui にも依らない純粋な計算に切り出してあり、機械的に検証している。
    /// </remarks>
    private void ResolveColumns()
    {
        var count = this.columnCount;

        if (count == 0)
            return;

        if (this.columnWidths.Length < count)
            this.columnWidths = new float[count];

        if (this.autoWidths.Length < count)
            this.autoWidths = new float[Math.Max(8, count * 2)];

        if (this.measuredWidths.Length < count)
            this.measuredWidths = new float[Math.Max(8, count * 2)];

        this.measuredWidths.AsSpan(0, count).Clear();

        ColumnLayout.Resolve(
            this.columnBuffer.AsSpan(0, count),
            this.Bounds.Width,
            this.Spacing.X,
            this.columnWidths.AsSpan(0, count),
            this.autoWidths.AsSpan(0, count));
    }

    /// <summary>
    /// 内容に合わせる列のために、前のフレームに測った幅を受け取る。
    /// </summary>
    internal void SetAutoWidths(ReadOnlySpan<float> widths)
    {
        if (this.columnCount == 0 || !this.hasAutoColumn)
            return;

        if (this.autoWidths.Length < this.columnCount)
            this.autoWidths = new float[Math.Max(8, this.columnCount * 2)];

        widths[..Math.Min(widths.Length, this.columnCount)].CopyTo(this.autoWidths);
        this.ResolveColumns();
    }

    /// <summary>このフレームに各列が使った幅。内容に合わせる列の次フレーム用。</summary>
    internal ReadOnlySpan<float> MeasuredColumnWidths
        => this.measuredWidths.AsSpan(0, this.columnCount);

    /// <summary>内容に合わせる列を含むか。</summary>
    internal bool HasAutoColumn => this.hasAutoColumn;

    /// <summary>指定サイズの領域を確保する。</summary>
    public Rect Allocate(Vector2 size)
    {
        if (this.Kind == LayoutKind.Vertical)
            return this.AllocateVertical(size);

        return this.AllocateHorizontal(size);
    }

    /// <summary>
    /// 入れ子のスコープが消費した大きさを取り込む。
    /// </summary>
    /// <param name="size">消費した大きさ。</param>
    /// <param name="requestedWidth">
    /// 中身が希望した幅 (余白を含む)。中身に合わせる窓のために、
    /// 入れ子の中の希望を外側まで持ち上げる。
    /// </param>
    internal Rect AllocateChild(Vector2 size, float requestedWidth)
    {
        this.pendingRequestedWidth = requestedWidth;
        return this.Allocate(size);
    }

    /// <summary>
    /// この確保で希望幅として数える値。負なら実際の大きさを使う。
    /// </summary>
    private float pendingRequestedWidth = -1f;

    /// <summary>中身が希望した範囲の右端 (絶対座標)。</summary>
    private float requestedMaxX;

    /// <summary>希望した幅を覚える。</summary>
    private void NoteRequestedWidth(Rect rect, float actualWidth)
    {
        var wanted = this.pendingRequestedWidth >= 0f ? this.pendingRequestedWidth : actualWidth;
        this.pendingRequestedWidth = -1f;

        this.requestedMaxX = MathF.Max(this.requestedMaxX, rect.Min.X + wanted);
    }

    /// <summary>幅の指定方法と高さを与えて領域を確保する。</summary>
    public Rect Allocate(SizeSpec width, float height)
    {
        float resolved;

        if (this.Kind == LayoutKind.Horizontal && this.columnCount > 0)
        {
            // 列が宣言されている行では、要素の希望幅より列幅を優先する。
            // そうしないと中身の短い要素で列がずれてしまう
            var index = Math.Min(this.ColumnIndex, this.columnCount - 1);

            // 内容に合わせる列だけは例外。ピクセル指定の希望幅を通し、
            // それを実測として次のフレームへ渡す。列幅 (前の実測) で上書きすると、
            // 実測が常に列幅と等しくなって内容にまったく追従しない。
            // 比率や残り幅の指定は「列の中での割合」なので、列幅のまま扱う
            resolved = this.columnBuffer[index].Mode == SizeMode.Auto && width.Mode == SizeMode.Fixed
                ? width.Resolve(0f)
                : this.columnWidths[index];
        }
        else
        {
            var available = this.Kind == LayoutKind.Vertical
                ? this.Bounds.Width
                : this.RemainingWidthForNext;

            resolved = width.Resolve(available);

            // 中身に合わせる窓のために、頭打ちの前の希望幅を覚える。
            // 残り幅・比率の指定は窓の幅から決まる値なので数えない
            this.pendingRequestedWidth = width.Mode is SizeMode.Fill or SizeMode.Ratio
                ? 0f
                : resolved;

            // 使える幅を超えた固定幅は頭打ちにする。超えたままにすると、
            // 文字が領域の端で黙って切られ、省略記号もツールチップも出ないまま
            // 読めなくなる。さらにセルの中では隣の列へ描き込んでしまう。
            // 折り返す行だけは例外で、次の行で幅を取り直すので縮めない
            if (this.Kind == LayoutKind.Vertical || !this.Wrap)
                resolved = MathF.Min(resolved, available);
        }

        return this.Allocate(new Vector2(resolved, height));
    }

    private Rect AllocateVertical(Vector2 size)
    {
        if (this.ItemCount > 0)
            this.Cursor = new Vector2(this.Bounds.Min.X, this.Cursor.Y + this.Spacing.Y);

        var rect = Rect.FromSize(this.Cursor, size);
        this.Cursor = new Vector2(this.Bounds.Min.X, rect.Max.Y);

        this.NoteRequestedWidth(rect, size.X);
        this.Track(rect);
        this.SyncImGuiCursor();
        return rect;
    }

    private Rect AllocateHorizontal(Vector2 size)
    {
        var columnWidth = 0f;

        // 内容に合わせる列は、頭打ちの前の希望幅を実測として覚える。
        // 頭打ちの後の幅を覚えると「実測 ≤ 列幅 ≤ 前の実測」になり、
        // 値は減ることしかできない。初回は 0 なので 0 に張り付く
        var requested = size.X;

        // 列を宣言した行では、列ぶんだけカーソルを進める。
        // ただし大きさを直接渡す部品 (札・アイコン・画像) は、内容どおりの形で
        // 描きたいので、返す矩形は希望した幅までに留める。
        // 列幅に合わせて引き伸ばすと、札が横長の帯になってしまう
        if (this.columnCount > 0)
        {
            var index = Math.Min(this.ColumnIndex, this.columnCount - 1);
            columnWidth = this.columnWidths[index];
            size = new Vector2(MathF.Min(size.X, columnWidth), size.Y);
        }

        if (this.ItemCount > 0)
        {
            var needsWrap = this.Wrap && this.Cursor.X + this.Spacing.X + size.X > this.Bounds.Max.X;

            if (needsWrap)
                this.NewLine();
            else
                this.Cursor = new Vector2(this.Cursor.X + this.Spacing.X, this.Cursor.Y);
        }

        // 行の基準の高さ。ボタン (24px) と文字 (16px) のように高さが違う要素を
        // 並べたとき、上端で揃えると文字だけ浮いて見える
        var baseline = this.RowHeight > 0f ? MathF.Max(this.RowHeight, size.Y) : size.Y;

        if (this.CrossAlign == Align.Stretch)
            size = new Vector2(size.X, baseline);

        var offset = this.CrossAlign switch
        {
            Align.Center => (baseline - size.Y) * 0.5f,
            Align.End => baseline - size.Y,
            _ => 0f,
        };

        var rect = Rect.FromSize(new Vector2(this.Cursor.X, this.Cursor.Y + offset), size);

        // 内容に合わせる列のために、使った幅を覚えておく。
        // 内容に合わせる列だけは頭打ちの前の希望幅を使う (上のコメント参照)
        if (this.columnCount > 0)
        {
            var index = Math.Min(this.ColumnIndex, this.columnCount - 1);

            if (index < this.measuredWidths.Length)
            {
                var measured = this.columnBuffer[index].Mode == SizeMode.Auto ? requested : size.X;
                this.measuredWidths[index] = MathF.Max(this.measuredWidths[index], measured);
            }
        }

        // カーソルは列ぶん進める。中身が列より狭くても、次の要素は次の列から始まる
        var advance = columnWidth > 0f ? MathF.Max(columnWidth, size.X) : size.X;

        // カーソルの縦位置は行の上端のままにしておく (次の要素も同じ行へ並ぶ)
        this.Cursor = new Vector2(rect.Min.X + advance, this.Cursor.Y);
        this.LineHeight = MathF.Max(this.LineHeight, baseline);
        this.ColumnIndex++;

        // 列を宣言した行では、列ぶん進めた幅までを希望として数える
        this.NoteRequestedWidth(rect, MathF.Max(advance, requested));
        this.Track(rect);
        this.SyncImGuiCursor();
        return rect;
    }

    /// <summary>
    /// ImGui 側のカーソルをレイアウトの現在位置へ合わせる。
    /// </summary>
    /// <remarks>
    /// これが無いと、レイアウトスコープの中で生の <c>ImGui.*</c> を呼んだときに
    /// ウィンドウの左上へ描かれてしまう。同じフレーム内で混在できるようにするための同期。
    /// 逆方向 (生 ImGui が進めたカーソルへ合わせる) は
    /// <see cref="EUi.SyncFromImGui"/> を呼ぶ。
    /// </remarks>
    private void SyncImGuiCursor()
    {
        if (SuppressImGuiSync)
            return;

        SetImGuiCursor(this.Cursor);
    }

    /// <summary>ImGui のカーソルを動かす。</summary>
    /// <remarks>
    /// 別のメソッドへ分けて差し込みを禁じている。呼び出し元へ展開されると、
    /// 止めている場合でも JIT が ImGui のアセンブリを読もうとして、
    /// ゲーム無しの自己検証が動かせない。
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SetImGuiCursor(Vector2 position) => ImGui.SetCursorScreenPos(position);

    /// <summary>
    /// ImGui へのカーソル同期を止める。自己検証からレイアウトを直接動かすために使う。
    /// </summary>
    /// <remarks>
    /// 「測った幅を次のフレームへ持ち越す」経路は、以前ここの ImGui 依存のために
    /// 検証できず、内容に合わせる列が 0 幅のまま動かない不具合を見逃した。
    /// ゲーム無しで通せるようにしておく。
    /// </remarks>
    internal static bool SuppressImGuiSync { get; set; }

    /// <summary>横並びのとき、次の行へ移る。</summary>
    public void NewLine()
    {
        if (this.Kind != LayoutKind.Horizontal)
            return;

        this.Cursor = new Vector2(this.Bounds.Min.X, this.Cursor.Y + this.LineHeight + this.Spacing.Y);
        this.LineHeight = 0f;
        this.ColumnIndex = 0;
    }

    /// <summary>空き (スペーサー) を入れる。</summary>
    /// <remarks>
    /// 列を宣言した横並びでは何もしない。その行の位置は列幅で決まるため、
    /// ここで隙間を足すと以降の要素が列からずれてしまう。
    /// 余りを埋めて右へ寄せたい場合は <see cref="EUi.Spacer"/> を使う。
    /// </remarks>
    public void AddSpacing(float amount)
    {
        if (this.Kind == LayoutKind.Vertical)
        {
            this.Cursor = new Vector2(this.Cursor.X, this.Cursor.Y + amount);
            return;
        }

        if (this.columnCount > 0)
            return;

        this.Cursor = new Vector2(this.Cursor.X + amount, this.Cursor.Y);
    }

    /// <summary>カーソルを直接動かす。独自配置を行うウィジェット向け。</summary>
    public void SetCursor(Vector2 position) => this.Cursor = position;

    /// <summary>
    /// 列を進めずに、使った範囲だけを広げる。
    /// </summary>
    /// <remarks>
    /// セルの中身が、確保しておいた高さを超えたときに使う。
    /// <see cref="Allocate(Vector2)"/> で申告すると列まで進んでしまう。
    /// </remarks>
    public void ExpandContent(Rect rect)
    {
        this.ContentBounds = this.ItemCount == 0 ? rect : this.ContentBounds.Union(rect);
    }

    /// <summary>使用済み範囲を更新する。</summary>
    private void Track(Rect rect)
    {
        this.ContentBounds = this.ItemCount == 0 ? rect : this.ContentBounds.Union(rect);
        this.ItemCount++;
    }
}
