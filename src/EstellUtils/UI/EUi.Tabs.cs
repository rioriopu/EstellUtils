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
    /// タブバーを描き、選択中のタブを返す。
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
        ref var state = ref ctx.Store.GetRef(euId);

        var selected = Math.Clamp(state.SelectedIndex, 0, labels.Length - 1);
        var rowRect = ctx.Allocate(SizeSpec.Fill, Metrics.TabHeight);

        var x = rowRect.Min.X;

        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            var width = TextPainter.Measure(label).X + Metrics.TabPadding.TotalHorizontal;

            // 右端を超える分は描かない (折り返しはしない)
            if (x + width > rowRect.Max.X && i > 0)
                break;

            var tabRect = Rect.FromSize(new Vector2(x, rowRect.Min.Y), new Vector2(width, rowRect.Height));
            var interaction = Interaction.Behavior(tabRect, euId.Child(i));

            if (interaction.Clicked)
                selected = i;

            var visual = WidgetVisual.From(interaction, i == selected) with { Rect = tabRect };
            WidgetPainter.DrawTab(visual, label);

            x += width + TabGap;
        }

        state.SelectedIndex = selected;

        // タブ行の下に区切り線を引いて、中身との境界を示す
        Painter.HLine(rowRect.Min.X, rowRect.Max.X, rowRect.Max.Y - 1f, Colors.Separator);

        return new TabBarResult(selected, labels.Length == 0 ? null : labels[selected]);
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
