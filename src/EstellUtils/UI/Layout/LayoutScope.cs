using System;
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
    /// このスコープが消費した大きさ。余白を含む (外側のレイアウトへ申告する値)。
    /// </summary>
    public Vector2 ConsumedSize
    {
        get
        {
            if (this.ItemCount == 0)
                return this.Padding.Total;

            var inner = new Vector2(
                this.ContentBounds.Max.X - this.Bounds.Min.X,
                this.ContentBounds.Max.Y - this.Bounds.Min.Y);

            return inner + this.Padding.Total;
        }
    }

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

    /// <summary>幅の指定方法と高さを与えて領域を確保する。</summary>
    public Rect Allocate(SizeSpec width, float height)
    {
        float resolved;

        if (this.Kind == LayoutKind.Horizontal && this.columnCount > 0)
        {
            // 列が宣言されている行では、要素の希望幅より列幅を優先する。
            // そうしないと中身の短い要素で列がずれてしまう
            var index = Math.Min(this.ColumnIndex, this.columnCount - 1);
            resolved = this.columnWidths[index];
        }
        else
        {
            var available = this.Kind == LayoutKind.Vertical
                ? this.Bounds.Width
                : this.RemainingWidthForNext;

            resolved = width.Resolve(available);

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

        this.Track(rect);
        this.SyncImGuiCursor();
        return rect;
    }

    private Rect AllocateHorizontal(Vector2 size)
    {
        var columnWidth = 0f;

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

        // 内容に合わせる列のために、実際に使った幅を覚えておく
        if (this.columnCount > 0)
        {
            var index = Math.Min(this.ColumnIndex, this.columnCount - 1);

            if (index < this.measuredWidths.Length)
                this.measuredWidths[index] = MathF.Max(this.measuredWidths[index], size.X);
        }

        // カーソルは列ぶん進める。中身が列より狭くても、次の要素は次の列から始まる
        var advance = columnWidth > 0f ? MathF.Max(columnWidth, size.X) : size.X;

        // カーソルの縦位置は行の上端のままにしておく (次の要素も同じ行へ並ぶ)
        this.Cursor = new Vector2(rect.Min.X + advance, this.Cursor.Y);
        this.LineHeight = MathF.Max(this.LineHeight, baseline);
        this.ColumnIndex++;

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
    private void SyncImGuiCursor() => ImGui.SetCursorScreenPos(this.Cursor);

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
