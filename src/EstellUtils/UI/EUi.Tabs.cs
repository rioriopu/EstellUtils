using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// タブ。
/// </summary>
public static partial class EUi
{
    /// <summary>タブ同士の間隔。</summary>
    private const float TabGap = 2f;

    /// <summary>
    /// タブバーを描き、選択中のタブを返す。選択状態はライブラリ側が覚える。
    /// <code>
    /// var tabs = EUi.TabBar("main", "基本", "設定", "ご支援");
    /// if (tabs.IsSelected("基本"))
    ///     DrawBasic();
    /// </code>
    /// </summary>
    /// <param name="id">選択状態を記憶するための識別子。</param>
    /// <param name="labels">タブのラベル。</param>
    /// <remarks>
    /// ラベルを先に宣言する形にしてあるのは、タブの見出し行を 1 回で描き切るため。
    /// 「タブを描く → 中身を描く → 次のタブを描く」という順序にならないので、
    /// レイアウトが素直になる。
    /// </remarks>
    public static TabBarResult TabBar(ReadOnlySpan<char> id, params ReadOnlySpan<string> labels)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (labels.Length == 0)
            return default;

        var euId = ctx.GetId(id);

        // 状態の参照は描画の前後で分ける。描画の途中で Store が育つと ref が無効になる
        var selected = Math.Clamp(ctx.Store.GetRef(euId).SelectedIndex, 0, labels.Length - 1);
        var result = DrawTabBar(ctx, euId, ref selected, labels);

        ctx.Store.GetRef(euId).SelectedIndex = selected;
        return result;
    }

    /// <summary>
    /// 選択状態を呼び出し側で持つタブバー。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="selected">選択中のタブの添字。クリックで書き換わる。</param>
    /// <param name="labels">タブのラベル。</param>
    /// <remarks>
    /// コードから別のタブへ飛ばしたい場合に使う。
    /// 「設定のこの項目はあちらのタブ」といった案内を作れる。
    /// <code>
    /// if (EUi.Button("プリセットへ"))
    ///     this.tabIndex = 2;
    ///
    /// var tabs = EUi.TabBar("main", ref this.tabIndex, "基本", "詳細", "プリセット");
    /// </code>
    /// </remarks>
    public static TabBarResult TabBar(
        ReadOnlySpan<char> id, ref int selected, params ReadOnlySpan<string> labels)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (labels.Length == 0)
            return default;

        var euId = ctx.GetId(id);
        selected = Math.Clamp(selected, 0, labels.Length - 1);

        var result = DrawTabBar(ctx, euId, ref selected, labels);

        // 内部の記憶も合わせておく。SelectTab と混ぜて使っても食い違わない
        ctx.Store.GetRef(euId).SelectedIndex = selected;
        return result;
    }

    /// <summary>
    /// タブバーの選択をコードから変える。
    /// </summary>
    /// <param name="id">タブバーの識別子。</param>
    /// <param name="index">選ぶタブの添字。</param>
    /// <remarks>
    /// 選択状態をライブラリ側に持たせたまま、別の場所から飛ばしたいときに使う。
    /// <code>
    /// if (EUi.Button("プリセットへ"))
    ///     EUi.SelectTab("main", 2);
    /// </code>
    /// 呼び出しの順序は問わない。タブバーを描く前でも後でもよい。
    /// </remarks>
    public static void SelectTab(ReadOnlySpan<char> id, int index)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        ctx.Store.GetRef(ctx.GetId(id)).SelectedIndex = Math.Max(0, index);
    }

    /// <summary>タブの見出し行を描く。</summary>
    /// <remarks>
    /// 行に入り切らないタブは次の行へ折り返す。捨ててしまうと、
    /// タブがそもそも無いように見えて原因に辿り着けない。
    /// </remarks>
    private static TabBarResult DrawTabBar(
        UiContext ctx, EuId euId, ref int selected, ReadOnlySpan<string> labels)
    {
        var tabHeight = Metrics.TabHeight;

        // 何行になるかを先に数えて、必要な高さを確保しておく
        var rows = CountTabRows(labels, AvailableWidth);
        var rowRect = ctx.Allocate(SizeSpec.Fill, (tabHeight * rows) + (TabGap * (rows - 1)));

        var x = rowRect.Min.X;
        var y = rowRect.Min.Y;

        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            var width = TabWidth(label);

            // 行の頭でなければ折り返す。1 枚で行を超える場合は、そのまま行幅へ収める
            if (x > rowRect.Min.X && x + width > rowRect.Max.X)
            {
                x = rowRect.Min.X;
                y += tabHeight + TabGap;
            }

            var tabRect = Rect.FromSize(
                new Vector2(x, y),
                new Vector2(MathF.Min(width, rowRect.Width), tabHeight));

            var interaction = Interaction.Behavior(tabRect, euId.Child(i));

            if (interaction.Clicked)
                selected = i;

            var visual = WidgetVisual.From(interaction, i == selected) with { Rect = tabRect };
            WidgetPainter.DrawTab(visual, label);

            x += width + TabGap;
        }

        // タブ行の下に区切り線を引いて、中身との境界を示す
        Painter.HLine(rowRect.Min.X, rowRect.Max.X, rowRect.Max.Y - 1f, Colors.Separator);

        return new TabBarResult(selected, labels[selected]);
    }

    /// <summary>タブ 1 枚分の幅。</summary>
    private static float TabWidth(ReadOnlySpan<char> label)
        => MathF.Ceiling(TextPainter.Measure(label).X) + Metrics.TabPadding.TotalHorizontal;

    /// <summary>与えられた幅で、タブが何行になるかを数える。</summary>
    private static int CountTabRows(ReadOnlySpan<string> labels, float available)
    {
        var rows = 1;
        var x = 0f;

        for (var i = 0; i < labels.Length; i++)
        {
            var width = TabWidth(labels[i]);

            if (x > 0f && x + width > available)
            {
                rows++;
                x = 0f;
            }

            x += width + TabGap;
        }

        return rows;
    }
}

/// <summary>タブバーの結果。</summary>
public readonly record struct TabBarResult
{
    internal TabBarResult(int selected, string? selectedLabel)
    {
        this.Selected = selected;
        this.SelectedLabel = selectedLabel;
    }

    /// <summary>選択中のタブの添字。</summary>
    public int Selected { get; }

    /// <summary>選択中のタブのラベル。</summary>
    public string? SelectedLabel { get; }

    /// <summary>指定したラベルのタブが選択中か。</summary>
    public bool IsSelected(ReadOnlySpan<char> label)
        => this.SelectedLabel is not null && label.SequenceEqual(this.SelectedLabel);

    /// <summary>指定した添字のタブが選択中か。</summary>
    public bool IsSelected(int index) => this.Selected == index;
}
