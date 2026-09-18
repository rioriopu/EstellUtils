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
                    // 内容サイズはこの時点では不明。ラベル列は LabelColumn で別途揃える
                    widths[i] = 0f;
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
                if (columns[i].Mode is SizeMode.Fixed or SizeMode.Ratio)
                    widths[i] *= scale;
            }

            used = available;
        }

        if (totalWeight <= 0f)
            return;

        var remaining = MathF.Max(0f, available - used);

        for (var i = 0; i < count; i++)
        {
            if (columns[i].Mode == SizeMode.Fill)
                widths[i] = remaining * (columns[i].Value / totalWeight);
        }
    }

    /// <summary>列を並べたときに、隙間が占める合計幅。</summary>
    public static float SpacingTotal(int columnCount, float spacing)
        => spacing * Math.Max(0, columnCount - 1);
}
