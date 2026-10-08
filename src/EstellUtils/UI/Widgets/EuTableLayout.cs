using System;
using System.Collections.Generic;

using EstellUtils.UI.Layout;

namespace EstellUtils.UI.Widgets;

/// <summary>
/// 利用者が変えた列幅。設定クラスへ持たせて保存する。
/// </summary>
/// <remarks>
/// <para>
/// 見出しの境をつまんで列幅を変えられるようにするための入れ物。
/// 変えていない列は入っていないので、宣言した幅のまま働く。
/// </para>
/// <para>
/// 単純なプロパティだけで構成してあるので、Newtonsoft.Json でも
/// System.Text.Json でもそのまま読み書きできる。
/// </para>
/// <code>
/// // 設定クラス
/// public EuTableLayout MobColumns { get; set; } = new();
///
/// // 描画
/// var columns = this.config.MobColumns.Apply(BaseColumns);
///
/// EUi.TableHeader("mobs", columns, this.config.MobColumns, onResized: this.config.Save);
///
/// foreach (…)
///     using (EUi.TableRow(columns, i)) { … }
/// </code>
/// </remarks>
public sealed class EuTableLayout
{
    /// <summary>反映後の列定義。保存の対象ではないので、毎回同じ配列を使い回す。</summary>
    private TableColumn[] applied = [];

    /// <summary>
    /// 利用者が決めた列の幅 (px)。鍵は列番号。
    /// </summary>
    /// <remarks>
    /// 変えていない列は入れない。入っていない列は宣言した幅で配分される。
    /// </remarks>
    public Dictionary<int, float> Widths { get; set; } = new();

    /// <summary>変えた列が 1 つでもあるか。</summary>
    public bool HasChanges => this.Widths.Count > 0;

    /// <summary>
    /// 宣言した列定義へ、利用者が変えた幅を反映して返す。
    /// </summary>
    /// <param name="columns">宣言した列定義。</param>
    /// <remarks>
    /// 返した範囲はこの入れ物が持つ配列を指す。見出しと行へ同じものを渡すこと。
    /// </remarks>
    public ReadOnlySpan<TableColumn> Apply(ReadOnlySpan<TableColumn> columns)
    {
        if (this.applied.Length < columns.Length)
            this.applied = new TableColumn[columns.Length];

        for (var i = 0; i < columns.Length; i++)
        {
            this.applied[i] = this.Widths.TryGetValue(i, out var width)
                ? columns[i] with { Width = SizeSpec.Px(width) }
                : columns[i];
        }

        return this.applied.AsSpan(0, columns.Length);
    }

    /// <summary>その列の幅を利用者が変えているか。</summary>
    public bool IsCustom(int index) => this.Widths.ContainsKey(index);

    /// <summary>すべての列を宣言した幅へ戻す。</summary>
    public void Reset() => this.Widths.Clear();

    /// <summary>その列だけを宣言した幅へ戻す。変わったら true。</summary>
    public bool Reset(int index) => this.Widths.Remove(index);

    /// <summary>列の幅を決める。変わったら true。</summary>
    internal bool Set(int index, float width)
    {
        if (this.Widths.TryGetValue(index, out var current)
            && MathF.Abs(current - width) < 0.5f)
        {
            return false;
        }

        this.Widths[index] = width;
        return true;
    }
}
