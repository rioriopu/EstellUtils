using System;
using System.Numerics;

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
    private int columnCount;

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

    /// <summary>次の要素に使える残り高さ。</summary>
    public float RemainingHeight => MathF.Max(0f, this.Bounds.Max.Y - this.Cursor.Y);

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
        if (this.ItemCount == 0)
            return new Rect(this.Cursor, this.Bounds.Max);

        var next = this.Kind == LayoutKind.Vertical
            ? new Vector2(this.Bounds.Min.X, this.Cursor.Y + this.Spacing.Y)
            : new Vector2(this.Cursor.X + this.Spacing.X, this.Cursor.Y);

        return new Rect(next, Vector2.Max(next, this.Bounds.Max));
    }

    /// <summary>スコープを初期化する (プールから再利用するため公開している)。</summary>
    public void Reset(
        LayoutKind kind, Rect bounds, Vector2 spacing, ReadOnlySpan<SizeSpec> columns, bool wrap,
        EdgeInsets padding = default)
    {
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

        this.ResolveColumns();
    }

    /// <summary>列定義から実際の列幅を計算する。</summary>
    private void ResolveColumns()
    {
        var count = this.columnCount;
        if (count == 0)
            return;
        if (this.columnWidths.Length < count)
            this.columnWidths = new float[count];

        var totalSpacing = this.Spacing.X * (count - 1);
        var available = MathF.Max(0f, this.Bounds.Width - totalSpacing);

        // 固定幅・比率を先に確定し、残りを Fill の重みで分配する
        var used = 0f;
        var totalWeight = 0f;

        for (var i = 0; i < count; i++)
        {
            var spec = this.columnBuffer[i];
            switch (spec.Mode)
            {
                case SizeMode.Fixed:
                    this.columnWidths[i] = spec.Value;
                    used += spec.Value;
                    break;

                case SizeMode.Ratio:
                    this.columnWidths[i] = available * spec.Value;
                    used += this.columnWidths[i];
                    break;

                case SizeMode.Auto:
                    // 内容サイズはこの時点では不明。ラベル列は LabelColumn で別途揃える
                    this.columnWidths[i] = 0f;
                    break;

                default:
                    this.columnWidths[i] = 0f;
                    totalWeight += spec.Value;
                    break;
            }
        }

        if (totalWeight <= 0f)
            return;

        var remaining = MathF.Max(0f, available - used);
        for (var i = 0; i < count; i++)
        {
            if (this.columnBuffer[i].Mode == SizeMode.Fill)
                this.columnWidths[i] = remaining * (this.columnBuffer[i].Value / totalWeight);
        }
    }

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
            var available = this.Kind == LayoutKind.Vertical ? this.Bounds.Width : this.RemainingWidth;
            resolved = width.Resolve(available);
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
        return rect;
    }

    private Rect AllocateHorizontal(Vector2 size)
    {
        if (this.ItemCount > 0)
        {
            var needsWrap = this.Wrap && this.Cursor.X + this.Spacing.X + size.X > this.Bounds.Max.X;

            if (needsWrap)
                this.NewLine();
            else
                this.Cursor = new Vector2(this.Cursor.X + this.Spacing.X, this.Cursor.Y);
        }

        var rect = Rect.FromSize(this.Cursor, size);
        this.Cursor = new Vector2(rect.Max.X, this.Cursor.Y);
        this.LineHeight = MathF.Max(this.LineHeight, size.Y);
        this.ColumnIndex++;

        this.Track(rect);
        return rect;
    }

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
    public void AddSpacing(float amount)
    {
        if (this.Kind == LayoutKind.Vertical)
            this.Cursor = new Vector2(this.Cursor.X, this.Cursor.Y + amount);
        else
            this.Cursor = new Vector2(this.Cursor.X + amount, this.Cursor.Y);
    }

    /// <summary>カーソルを直接動かす。独自配置を行うウィジェット向け。</summary>
    public void SetCursor(Vector2 position) => this.Cursor = position;

    /// <summary>使用済み範囲を更新する。</summary>
    private void Track(Rect rect)
    {
        this.ContentBounds = this.ItemCount == 0 ? rect : this.ContentBounds.Union(rect);
        this.ItemCount++;
    }
}
