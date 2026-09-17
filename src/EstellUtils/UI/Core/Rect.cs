using System;
using System.Numerics;

namespace EstellUtils.UI.Core;

/// <summary>
/// 矩形の内側/外側に付ける余白 (上下左右)。padding にも margin にも使う。
/// </summary>
public readonly record struct EdgeInsets(float Left, float Top, float Right, float Bottom)
{
    /// <summary>余白なし。</summary>
    public static readonly EdgeInsets Zero = default;

    /// <summary>四辺すべて同じ余白。</summary>
    public static EdgeInsets All(float value) => new(value, value, value, value);

    /// <summary>左右 / 上下で分けた余白。</summary>
    public static EdgeInsets Symmetric(float horizontal, float vertical)
        => new(horizontal, vertical, horizontal, vertical);

    /// <summary>左右のみ。</summary>
    public static EdgeInsets Horizontal(float value) => new(value, 0f, value, 0f);

    /// <summary>上下のみ。</summary>
    public static EdgeInsets Vertical(float value) => new(0f, value, 0f, value);

    /// <summary>左右の合計。</summary>
    public float TotalHorizontal => this.Left + this.Right;

    /// <summary>上下の合計。</summary>
    public float TotalVertical => this.Top + this.Bottom;

    /// <summary>左上方向のオフセット量。</summary>
    public Vector2 TopLeft => new(this.Left, this.Top);

    /// <summary>右下方向のオフセット量。</summary>
    public Vector2 BottomRight => new(this.Right, this.Bottom);

    /// <summary>四辺の合計を (横, 縦) で返す。要素サイズの算出に使う。</summary>
    public Vector2 Total => new(this.TotalHorizontal, this.TotalVertical);

    public static EdgeInsets operator +(EdgeInsets a, EdgeInsets b)
        => new(a.Left + b.Left, a.Top + b.Top, a.Right + b.Right, a.Bottom + b.Bottom);

    public static EdgeInsets operator *(EdgeInsets a, float scale)
        => new(a.Left * scale, a.Top * scale, a.Right * scale, a.Bottom * scale);
}

/// <summary>
/// 軸方向の寄せ方。レイアウトとテキスト配置の両方で使う。
/// </summary>
public enum Align
{
    /// <summary>始端寄せ (左 / 上)。</summary>
    Start,

    /// <summary>中央寄せ。</summary>
    Center,

    /// <summary>終端寄せ (右 / 下)。</summary>
    End,

    /// <summary>利用可能な領域いっぱいに引き伸ばす。</summary>
    Stretch,
}

/// <summary>
/// 画面座標系の矩形。左上が <see cref="Min"/>、右下が <see cref="Max"/>。
/// ImGui の座標系と同じく Y 軸は下向き。
/// </summary>
/// <remarks>
/// レイアウトとヒットテストの基本単位。immediate mode で毎フレーム大量に作られるため
/// 値型 (record struct) とし、メソッドはすべて非破壊 (新しい Rect を返す) にしてある。
/// </remarks>
public readonly record struct Rect(Vector2 Min, Vector2 Max)
{
    /// <summary>面積ゼロの矩形。</summary>
    public static readonly Rect Zero = default;

    /// <summary>左上座標とサイズから作る。</summary>
    public static Rect FromSize(Vector2 position, Vector2 size) => new(position, position + size);

    /// <summary>左上座標とサイズから作る。</summary>
    public static Rect FromSize(float x, float y, float width, float height)
        => new(new Vector2(x, y), new Vector2(x + width, y + height));

    /// <summary>中心座標とサイズから作る。</summary>
    public static Rect FromCenter(Vector2 center, Vector2 size)
    {
        var half = size * 0.5f;
        return new Rect(center - half, center + half);
    }

    public float Left => this.Min.X;
    public float Top => this.Min.Y;
    public float Right => this.Max.X;
    public float Bottom => this.Max.Y;

    public float Width => this.Max.X - this.Min.X;
    public float Height => this.Max.Y - this.Min.Y;

    /// <summary>幅と高さ。</summary>
    public Vector2 Size => this.Max - this.Min;

    /// <summary>中心座標。</summary>
    public Vector2 Center => (this.Min + this.Max) * 0.5f;

    public Vector2 TopLeft => this.Min;
    public Vector2 TopRight => new(this.Max.X, this.Min.Y);
    public Vector2 BottomLeft => new(this.Min.X, this.Max.Y);
    public Vector2 BottomRight => this.Max;

    /// <summary>幅か高さが 0 以下なら true。描画をスキップする判定に使う。</summary>
    public bool IsEmpty => this.Max.X <= this.Min.X || this.Max.Y <= this.Min.Y;

    /// <summary>点を含むか (右端・下端は含まない)。ヒットテストの基本。</summary>
    public bool Contains(Vector2 point)
        => point.X >= this.Min.X && point.Y >= this.Min.Y &&
           point.X < this.Max.X && point.Y < this.Max.Y;

    /// <summary>矩形全体を含むか。</summary>
    public bool Contains(Rect other)
        => other.Min.X >= this.Min.X && other.Min.Y >= this.Min.Y &&
           other.Max.X <= this.Max.X && other.Max.Y <= this.Max.Y;

    /// <summary>重なりがあるか。カリング判定に使う。</summary>
    public bool Overlaps(Rect other)
        => other.Min.X < this.Max.X && other.Max.X > this.Min.X &&
           other.Min.Y < this.Max.Y && other.Max.Y > this.Min.Y;

    /// <summary>四辺を外側へ広げる。</summary>
    public Rect Expand(float amount)
        => new(new Vector2(this.Min.X - amount, this.Min.Y - amount),
               new Vector2(this.Max.X + amount, this.Max.Y + amount));

    /// <summary>四辺を外側へ広げる (辺ごとに指定)。</summary>
    public Rect Expand(EdgeInsets insets)
        => new(new Vector2(this.Min.X - insets.Left, this.Min.Y - insets.Top),
               new Vector2(this.Max.X + insets.Right, this.Max.Y + insets.Bottom));

    /// <summary>四辺を内側へ縮める。padding の適用に使う。</summary>
    public Rect Shrink(float amount) => this.Expand(-amount);

    /// <summary>四辺を内側へ縮める (辺ごとに指定)。</summary>
    public Rect Shrink(EdgeInsets insets)
        => new(new Vector2(this.Min.X + insets.Left, this.Min.Y + insets.Top),
               new Vector2(this.Max.X - insets.Right, this.Max.Y - insets.Bottom));

    /// <summary>平行移動する。</summary>
    public Rect Offset(Vector2 delta) => new(this.Min + delta, this.Max + delta);

    /// <summary>平行移動する。</summary>
    public Rect Offset(float dx, float dy) => this.Offset(new Vector2(dx, dy));

    /// <summary>左上を保ったままサイズを変える。</summary>
    public Rect WithSize(Vector2 size) => FromSize(this.Min, size);

    /// <summary>左上を保ったまま幅を変える。</summary>
    public Rect WithWidth(float width) => new(this.Min, new Vector2(this.Min.X + width, this.Max.Y));

    /// <summary>左上を保ったまま高さを変える。</summary>
    public Rect WithHeight(float height) => new(this.Min, new Vector2(this.Max.X, this.Min.Y + height));

    /// <summary>共通部分。重なりがなければ面積ゼロの矩形を返す。</summary>
    public Rect Intersect(Rect other)
    {
        var min = Vector2.Max(this.Min, other.Min);
        var max = Vector2.Min(this.Max, other.Max);
        return new Rect(min, Vector2.Max(min, max));
    }

    /// <summary>両方を包含する最小の矩形。内容サイズの集計に使う。</summary>
    public Rect Union(Rect other)
        => new(Vector2.Min(this.Min, other.Min), Vector2.Max(this.Max, other.Max));

    /// <summary>点を含むように広げる。</summary>
    public Rect Union(Vector2 point)
        => new(Vector2.Min(this.Min, point), Vector2.Max(this.Max, point));

    // ── 切り出し (レイアウト用) ────────────────────────────────
    // 「左から 80px 切り出して、残りにさらに配置する」といった書き方を支える。

    // 以下の切り出しメソッドは、呼び出し側が
    //     area = area.CutRight(w, out area);
    // のようにレシーバと out 引数へ同じ変数を渡せるようにしてある。
    // readonly struct の this は参照で渡されるため、remainder を先に代入してしまうと
    // その後に読む this の中身まで書き換わってしまう。
    // そのため、どのメソッドも「戻り値を先に組み立ててから remainder へ代入する」。

    /// <summary>左から指定幅を切り出し、残りを <paramref name="remainder"/> で返す。</summary>
    public Rect CutLeft(float width, out Rect remainder)
    {
        width = Math.Clamp(width, 0f, this.Width);

        var min = this.Min;
        var max = this.Max;
        var split = min.X + width;

        var result = new Rect(min, new Vector2(split, max.Y));
        remainder = new Rect(new Vector2(split, min.Y), max);
        return result;
    }

    /// <summary>右から指定幅を切り出し、残りを <paramref name="remainder"/> で返す。</summary>
    public Rect CutRight(float width, out Rect remainder)
    {
        width = Math.Clamp(width, 0f, this.Width);

        var min = this.Min;
        var max = this.Max;
        var split = max.X - width;

        var result = new Rect(new Vector2(split, min.Y), max);
        remainder = new Rect(min, new Vector2(split, max.Y));
        return result;
    }

    /// <summary>上から指定高さを切り出し、残りを <paramref name="remainder"/> で返す。</summary>
    public Rect CutTop(float height, out Rect remainder)
    {
        height = Math.Clamp(height, 0f, this.Height);

        var min = this.Min;
        var max = this.Max;
        var split = min.Y + height;

        var result = new Rect(min, new Vector2(max.X, split));
        remainder = new Rect(new Vector2(min.X, split), max);
        return result;
    }

    /// <summary>下から指定高さを切り出し、残りを <paramref name="remainder"/> で返す。</summary>
    public Rect CutBottom(float height, out Rect remainder)
    {
        height = Math.Clamp(height, 0f, this.Height);

        var min = this.Min;
        var max = this.Max;
        var split = max.Y - height;

        var result = new Rect(new Vector2(min.X, split), max);
        remainder = new Rect(min, new Vector2(max.X, split));
        return result;
    }

    /// <summary>
    /// この矩形の中に指定サイズの要素を配置したときの矩形を返す。
    /// <paramref name="anchor"/> は (0,0) で左上、(0.5,0.5) で中央、(1,1) で右下。
    /// </summary>
    public Rect Place(Vector2 size, Vector2 anchor)
    {
        var free = this.Size - size;
        var pos = this.Min + new Vector2(free.X * anchor.X, free.Y * anchor.Y);
        return FromSize(pos, size);
    }

    /// <summary>軸ごとの寄せ方で要素を配置する。<see cref="Align.Stretch"/> はその軸を引き伸ばす。</summary>
    public Rect Place(Vector2 size, Align horizontal, Align vertical)
    {
        var w = horizontal == Align.Stretch ? this.Width : size.X;
        var h = vertical == Align.Stretch ? this.Height : size.Y;
        var x = horizontal switch
        {
            Align.Center => this.Min.X + (this.Width - w) * 0.5f,
            Align.End => this.Max.X - w,
            _ => this.Min.X,
        };
        var y = vertical switch
        {
            Align.Center => this.Min.Y + (this.Height - h) * 0.5f,
            Align.End => this.Max.Y - h,
            _ => this.Min.Y,
        };
        return FromSize(new Vector2(x, y), new Vector2(w, h));
    }

    /// <summary>座標をこの矩形の内側へ丸め込む。</summary>
    public Vector2 ClampPoint(Vector2 point)
        => Vector2.Clamp(point, this.Min, this.Max);

    /// <summary>
    /// 座標を小数点以下で丸める。テキストや 1px 線がぼやけるのを防ぐ。
    /// </summary>
    public Rect Rounded()
        => new(new Vector2(MathF.Round(this.Min.X), MathF.Round(this.Min.Y)),
               new Vector2(MathF.Round(this.Max.X), MathF.Round(this.Max.Y)));

    public override string ToString()
        => $"Rect({this.Min.X:F1}, {this.Min.Y:F1} → {this.Max.X:F1}, {this.Max.Y:F1})";
}
