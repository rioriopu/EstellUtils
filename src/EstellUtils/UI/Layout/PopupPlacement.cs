using System;
using System.Numerics;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Layout;

/// <summary>
/// ポップアップを画面の中へ収める位置を求める。
/// </summary>
/// <remarks>
/// <para>
/// 描画にも ImGui にも触れない純粋な計算だけを置く。作業領域を引数で受け取るので、
/// ゲームを起動せずに検証できる。
/// </para>
/// <para>
/// ここが抜けると「画面の下のほうで一覧を開くと、中身が画面の外へ出て読めない」
/// という形で必ず表に出る。
/// </para>
/// </remarks>
public static class PopupPlacement
{
    /// <summary>
    /// 欄の下に開く一覧の位置を求める。下に入らなければ欄の上へ返す。
    /// </summary>
    /// <param name="anchor">基準にする欄の矩形。</param>
    /// <param name="size">
    /// 一覧の大きさ。上下どちらにも入りきらない場合は、はみ出す代わりに高さを縮めて返す。
    /// </param>
    /// <param name="workMin">作業領域の左上。</param>
    /// <param name="workMax">作業領域の右下。</param>
    /// <param name="gap">欄と一覧の間に空ける隙間。</param>
    /// <param name="minHeight">縮めるときに、これより低くはしない高さ。</param>
    /// <returns>一覧の左上に置く座標。</returns>
    public static Vector2 Dropdown(
        Rect anchor, ref Vector2 size, Vector2 workMin, Vector2 workMax,
        float gap = 2f, float minHeight = 0f)
    {
        var roomBelow = MathF.Max(0f, workMax.Y - (anchor.Max.Y + gap));
        var roomAbove = MathF.Max(0f, anchor.Min.Y - gap - workMin.Y);

        // 下に入るならそのまま下。入らないなら、空いているほうへ開く
        var below = size.Y <= roomBelow || roomBelow >= roomAbove;
        var room = MathF.Max(below ? roomBelow : roomAbove, minHeight);

        // どちらにも入りきらない場合は、画面の外へ出す代わりに縮める
        size.Y = MathF.Min(size.Y, room);

        var y = below
            ? anchor.Max.Y + gap
            : anchor.Min.Y - gap - size.Y;

        return new Vector2(
            Clamp1D(anchor.Min.X, size.X, workMin.X, workMax.X),
            Clamp1D(y, size.Y, workMin.Y, workMax.Y));
    }

    /// <summary>
    /// 置きたい座標を、作業領域の中へ収める。
    /// </summary>
    /// <param name="position">置きたい左上の座標。</param>
    /// <param name="size">ポップアップの大きさ。</param>
    /// <param name="workMin">作業領域の左上。</param>
    /// <param name="workMax">作業領域の右下。</param>
    public static Vector2 Clamp(Vector2 position, Vector2 size, Vector2 workMin, Vector2 workMax)
        => new(
            Clamp1D(position.X, size.X, workMin.X, workMax.X),
            Clamp1D(position.Y, size.Y, workMin.Y, workMax.Y));

    /// <summary>1 軸ぶんの収め込み。領域より大きい場合は、手前の端に合わせる。</summary>
    private static float Clamp1D(float position, float size, float min, float max)
        => Math.Clamp(position, min, MathF.Max(min, max - size));
}
