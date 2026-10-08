using System;

namespace EstellUtils.UI.Layout;

/// <summary>
/// 宣言された列を、実際の幅へ配分する。
/// </summary>
/// <remarks>
/// <para>
/// 描画にも ImGui にも触れない純粋な計算だけを置く。ここが崩れると
/// 「右端の要素が枠を突き抜ける」形で必ず表に出るため、機械的に検証できる形にしてある。
/// </para>
/// <para>
/// 配分の規則:
/// </para>
/// <list type="number">
/// <item>列の間の隙間を先に取り除く。</item>
/// <item>固定幅と比率の列を確定する。</item>
/// <item>それだけで入りきらなければ、固定幅と比率の列を比例で縮める。</item>
/// <item>余りを <see cref="SizeMode.Fill"/> の列へ重みに応じて配る。</item>
/// <item>下限 (<see cref="SizeSpec.AtLeast"/>) を割った列を押し上げ、余裕のある列から取る。</item>
/// </list>
/// </remarks>
public static class ColumnLayout
{
    /// <summary>
    /// 列幅を配分する。
    /// </summary>
    /// <param name="columns">列の指定。</param>
    /// <param name="totalWidth">行全体に使える幅。</param>
    /// <param name="spacing">列と列の間の隙間。</param>
    /// <param name="widths">結果を書き込む先。<paramref name="columns"/> と同じ長さが要る。</param>
    /// <remarks>
    /// 列幅の指定は「入るならこの幅で」という意味であり、はみ出す許可ではない。
    /// 合計が収まらない場合は、はみ出す代わりに縮める。
    /// </remarks>
    public static void Resolve(
        ReadOnlySpan<SizeSpec> columns, float totalWidth, float spacing, Span<float> widths)
        => Resolve(columns, totalWidth, spacing, widths, default);

    /// <summary>
    /// 列幅を配分する。<see cref="SizeMode.Auto"/> の列には実測幅を使う。
    /// </summary>
    /// <param name="columns">列の指定。</param>
    /// <param name="totalWidth">行全体に使える幅。</param>
    /// <param name="spacing">列と列の間の隙間。</param>
    /// <param name="widths">結果を書き込む先。</param>
    /// <param name="autoWidths">
    /// <see cref="SizeMode.Auto"/> の列に使う幅。前のフレームに測った値を渡す。
    /// 空なら 0 幅になる。
    /// </param>
    public static void Resolve(
        ReadOnlySpan<SizeSpec> columns, float totalWidth, float spacing, Span<float> widths,
        ReadOnlySpan<float> autoWidths)
    {
        var count = columns.Length;

        if (count == 0)
            return;

        if (widths.Length < count)
            throw new ArgumentException("列の数だけ書き込み先が要ります。", nameof(widths));

        var available = MathF.Max(0f, totalWidth - SpacingTotal(count, spacing));

        // 固定幅・比率を先に確定し、残りを Fill の重みで分配する
        var used = 0f;
        var totalWeight = 0f;

        for (var i = 0; i < count; i++)
        {
            var spec = columns[i];

            switch (spec.Mode)
            {
                case SizeMode.Fixed:
                    widths[i] = MathF.Max(0f, spec.Value);
                    used += widths[i];
                    break;

                case SizeMode.Ratio:
                    widths[i] = MathF.Max(0f, available * spec.Value);
                    used += widths[i];
                    break;

                case SizeMode.Auto:
                    // 前のフレームに測った内容幅を使う。初回は 0 で、次のフレームから揃う
                    widths[i] = i < autoWidths.Length ? MathF.Max(0f, autoWidths[i]) : 0f;
                    used += widths[i];
                    break;

                default:
                    widths[i] = 0f;
                    totalWeight += spec.Value;
                    break;
            }
        }

        // 固定幅と比率だけで入りきらない場合は、比例で縮めて行の中へ収める。
        // そのまま置くと行の外へ描かれ、右端の要素が枠を突き抜けて見える
        if (used > available && used > 0f)
        {
            var scale = available / used;

            for (var i = 0; i < count; i++)
            {
                if (columns[i].Mode is SizeMode.Fixed or SizeMode.Ratio or SizeMode.Auto)
                    widths[i] *= scale;
            }

            used = available;
        }

        if (totalWeight > 0f)
        {
            var remaining = MathF.Max(0f, available - used);

            for (var i = 0; i < count; i++)
            {
                if (columns[i].Mode == SizeMode.Fill)
                    widths[i] = remaining * (columns[i].Value / totalWeight);
            }
        }

        ApplyMinimums(columns, available, widths);
    }

    /// <summary>
    /// 下限を割った列を押し上げ、足りない分を余裕のある列から取る。
    /// </summary>
    /// <remarks>
    /// 固定幅の列の合計が行の幅を超えると <see cref="SizeMode.Fill"/> の列が
    /// 極端に細くなる。下限を付けた列はここで確保し、代わりに余裕のある列を縮める。
    /// </remarks>
    private static void ApplyMinimums(
        ReadOnlySpan<SizeSpec> columns, float available, Span<float> widths)
    {
        var count = columns.Length;
        var deficit = 0f;
        var slack = 0f;

        for (var i = 0; i < count; i++)
        {
            var min = columns[i].Min;

            // 下限の無い列は、幅のすべてが削れる余裕
            if (min <= 0f)
            {
                slack += widths[i];
                continue;
            }

            if (widths[i] < min)
            {
                deficit += min - widths[i];
                widths[i] = min;
            }
            else
            {
                slack += widths[i] - min;
            }
        }

        if (deficit <= 0f)
            return;

        // 余裕に応じて削る。余裕の総量までしか取らないので、
        // ここで他の列が下限を割ることはない
        var take = MathF.Min(deficit, slack);

        if (take > 0f && slack > 0f)
        {
            var ratio = take / slack;

            for (var i = 0; i < count; i++)
            {
                var min = columns[i].Min;
                var columnSlack = min <= 0f ? widths[i] : widths[i] - min;

                if (columnSlack > 0f)
                    widths[i] -= columnSlack * ratio;
            }
        }

        // 下限の合計そのものが入りきらない場合は、はみ出す代わりに全部を縮める
        var total = 0f;

        for (var i = 0; i < count; i++)
            total += widths[i];

        if (total <= available || total <= 0f)
            return;

        var scale = available / total;

        for (var i = 0; i < count; i++)
            widths[i] *= scale;
    }

    /// <summary>列を並べたときに、隙間が占める合計幅。</summary>
    public static float SpacingTotal(int columnCount, float spacing)
        => spacing * Math.Max(0, columnCount - 1);
}
