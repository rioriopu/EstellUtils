using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;

namespace EstellUtils.UI;

/// <summary>
/// 件数の多い一覧を、見えている分だけ描くための API。
/// </summary>
public static partial class EUi
{
    /// <summary>
    /// 次に置かれる行が、クリップ範囲に入っているか。
    /// </summary>
    /// <param name="height">これから置く行の高さ。</param>
    /// <remarks>
    /// <para>
    /// 送り領域の中で長い一覧を回すとき、見えていない行の中身を組み立てるのは無駄になる。
    /// これが false なら、領域だけ確保して次へ進めばよい。
    /// </para>
    /// <code>
    /// foreach (var item in items)
    /// {
    ///     if (!EUi.IsRowVisible(24f))
    ///     {
    ///         EUi.Reserve(SizeSpec.Fill, 24f);
    ///         continue;
    ///     }
    ///
    ///     DrawRow(item);
    /// }
    /// </code>
    /// <para>
    /// 行の高さが一定なら <see cref="VirtualList"/> のほうが簡単で、
    /// 件数に関係なく一定のコストになる。
    /// </para>
    /// </remarks>
    public static bool IsRowVisible(float height)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        return IsRowVisible(ctx, height);
    }

    /// <summary>
    /// 高さの揃った一覧を、見えている範囲だけ描く。
    /// </summary>
    /// <param name="id">送り位置を覚えるための識別子。</param>
    /// <param name="count">項目の総数。</param>
    /// <param name="itemHeight">1 項目の高さ。</param>
    /// <param name="height">一覧全体の高さ。</param>
    /// <param name="draw">項目を描く処理。添字が渡される。</param>
    /// <param name="spacing">項目同士の空き。</param>
    /// <remarks>
    /// <para>
    /// 見えている範囲の前後だけを描き、残りは高さの確保だけで済ませる。
    /// 件数が数千になっても、1 フレームで描く数は画面に入る分しか増えない。
    /// </para>
    /// <code>
    /// EUi.VirtualList("items", this.items.Count, 24f, 300f, i =>
    /// {
    ///     using (EUi.Row(SizeSpec.Fill, 80f))
    ///     {
    ///         EUi.Label(this.items[i].Name);
    ///         EUi.Label(this.items[i].Count.ToString(), align: Align.End);
    ///     }
    /// });
    /// </code>
    /// <para>
    /// 高さが項目ごとに違う場合はこれを使えない。
    /// <c>EUi.IsRowVisible</c> で 1 行ずつ間引くこと。
    /// </para>
    /// </remarks>
    public static void VirtualList(
        ReadOnlySpan<char> id, int count, float itemHeight, float height,
        Action<int> draw, float? spacing = null)
    {
        ArgumentNullException.ThrowIfNull(draw);

        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (count <= 0 || itemHeight <= 0f)
        {
            using (Scroll(id, height, spacing))
            {
            }

            return;
        }

        var gap = spacing ?? Metrics.ItemSpacing.Y;
        var step = itemHeight + gap;

        using var scroll = Scroll(id, height, spacing);

        var clip = Painter.CurrentClip;
        var top = ctx.Layout.AvailableRect.Min.Y;

        // 画面に入る範囲を、送り量から直接求める。
        // 1 件ずつ判定するより、端から端まで飛ばせるぶん速い
        var firstVisible = Math.Clamp((int)MathF.Floor((clip.Min.Y - top) / step), 0, count - 1);
        var lastVisible = Math.Clamp((int)MathF.Ceiling((clip.Max.Y - top) / step), 0, count - 1);

        // 上側のまとめて飛ばす分
        if (firstVisible > 0)
            Reserve(SizeSpec.Fill, (firstVisible * step) - gap);

        for (var i = firstVisible; i <= lastVisible; i++)
            draw(i);

        // 下側も同じく、高さだけ確保して送りの長さを保つ
        var remaining = count - 1 - lastVisible;

        if (remaining > 0)
            Reserve(SizeSpec.Fill, (remaining * step) - gap);
    }
}
