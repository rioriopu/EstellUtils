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

    /// <summary>ラベルで要求された選択。描かれるときに添字へ直す。</summary>
    private static readonly System.Collections.Generic.Dictionary<EuId, string> PendingTabLabels = new();

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
        // 選んだタブは利用者の意図なので、別のタブを見ている間も覚えておく
        ref var before = ref ctx.Store.GetPersistentRef(euId);

        // SelectTab で外から番号の要求があればそちらを採る。
        // 覚えている「どのタブか」を先に見てしまうと、前のフレームに書いた鍵が
        // 必ず一致するので、要求が黙って捨てられる
        var selected = before.SelectionRequested
            ? Math.Clamp(before.SelectedIndex, 0, labels.Length - 1)
            : ResolveSelected(before, labels);

        ApplyPendingLabel(euId, ref selected, labels);

        var result = DrawTabBar(ctx, euId, ref selected, labels);

        ref var after = ref ctx.Store.GetPersistentRef(euId);
        after.SelectedIndex = selected;
        after.SelectedKey = TabKey(euId, labels, selected);
        after.SelectionRequested = false;

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

        // SelectTab で外から要求があればそちらを採る。
        // 書き戻すだけだと、要求が次のフレームで上書きされて消えてしまう
        ref var state = ref ctx.Store.GetPersistentRef(euId);

        if (state.SelectionRequested)
        {
            selected = state.SelectedIndex;
            state.SelectionRequested = false;
        }

        selected = Math.Clamp(selected, 0, labels.Length - 1);
        ApplyPendingLabel(euId, ref selected, labels);

        var result = DrawTabBar(ctx, euId, ref selected, labels);

        ref var after = ref ctx.Store.GetPersistentRef(euId);
        after.SelectedIndex = selected;
        after.SelectedKey = TabKey(euId, labels, selected);

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

        // タブバーと同じ入れ物を使う。別のタブを見ている間も要求を落とさない
        ref var state = ref ctx.Store.GetPersistentRef(ctx.GetId(id));
        state.SelectedIndex = Math.Max(0, index);
        state.SelectionRequested = true;
    }

    /// <summary>
    /// タブバーの選択を、ラベルを指定してコードから変える。
    /// </summary>
    /// <param name="id">タブバーの識別子。</param>
    /// <param name="label">選ぶタブのラベル。宣言したとおりの文字列を渡す。</param>
    /// <remarks>
    /// 添字で指すと、条件によってタブが増減する画面では飛び先がずれる。
    /// ラベルで指せば並びが変わっても壊れない。
    /// <code>
    /// if (EUi.Button("プリセットへ"))
    ///     EUi.SelectTab("##tabs", "プリセット");
    /// </code>
    /// 一致するラベルが無ければ何も起きない。
    /// </remarks>
    public static void SelectTab(ReadOnlySpan<char> id, ReadOnlySpan<char> label)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        PendingTabLabels[ctx.GetId(id)] = label.ToString();
    }

    /// <summary>
    /// 覚えている選択から、今の並びでの添字を決める。
    /// </summary>
    /// <remarks>
    /// まず「どのタブか」で探す。見つからなければ番号へ落とす。
    /// 条件でタブが増減しても、選んでいたタブに居続けられる。
    /// </remarks>
    private static int ResolveSelected(in WidgetState state, ReadOnlySpan<string> labels)
    {
        if (state.SelectedKey != 0UL)
        {
            for (var i = 0; i < labels.Length; i++)
            {
                if (EuId.FromLabel(labels[i], 0UL, out _).Value == state.SelectedKey)
                    return i;
            }
        }

        return Math.Clamp(state.SelectedIndex, 0, labels.Length - 1);
    }

    /// <summary>タブを見分けるための鍵。ラベルから作る。</summary>
    private static ulong TabKey(EuId euId, ReadOnlySpan<string> labels, int index)
    {
        if (index < 0 || index >= labels.Length)
            return 0UL;

        return EuId.FromLabel(labels[index], 0UL, out _).Value;
    }

    /// <summary>ラベルでの選択要求があれば、添字へ直して取り込む。</summary>
    private static void ApplyPendingLabel(EuId euId, ref int selected, ReadOnlySpan<string> labels)
    {
        if (PendingTabLabels.Count == 0 || !PendingTabLabels.Remove(euId, out var wanted))
            return;

        for (var i = 0; i < labels.Length; i++)
        {
            if (string.Equals(labels[i], wanted, StringComparison.Ordinal))
            {
                selected = i;
                return;
            }
        }
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

        // 何行になるかを先に数えて、必要な高さを確保しておく。
        // 基準は「次に確保される幅」。列の中では行全体の残り幅と食い違う
        var rows = CountTabRows(labels, NextItemWidth);
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

            // ID はラベル全体から作る。添字で作ると、条件でタブが増減したときに
            // 別のタブの状態を引き継いでしまう
            var tabId = EuId.FromLabel(label, euId.Value, out var display);
            var interaction = Interaction.Behavior(tabRect, tabId);

            if (interaction.Clicked)
                selected = i;

            var visual = WidgetVisual.From(interaction, i == selected) with { Rect = tabRect };
            WidgetPainter.DrawTab(visual, display);

            x += width + TabGap;
        }

        // タブ行の下に区切り線を引いて、中身との境界を示す
        Painter.HLine(rowRect.Min.X, rowRect.Max.X, rowRect.Max.Y - 1f, Colors.Separator);

        return new TabBarResult(selected, labels[selected]);
    }

    /// <summary>タブ 1 枚分の幅。<c>##</c> 以降は表示されないので幅にも入れない。</summary>
    private static float TabWidth(ReadOnlySpan<char> label)
    {
        EuId.FromLabel(label, 0UL, out var display);
        return MathF.Ceiling(TextPainter.Measure(display).X) + Metrics.TabPadding.TotalHorizontal;
    }

    /// <summary>与えられた幅で、タブが何行になるかを数える。</summary>
    private static int CountTabRows(ReadOnlySpan<string> labels, float available)
    {
        if (labels.Length <= 8)
        {
            Span<float> widths = stackalloc float[labels.Length];

            for (var i = 0; i < labels.Length; i++)
                widths[i] = TabWidth(labels[i]);

            return CountRows(widths, available, TabGap);
        }

        var buffer = new float[labels.Length];

        for (var i = 0; i < labels.Length; i++)
            buffer[i] = TabWidth(labels[i]);

        return CountRows(buffer, available, TabGap);
    }

    /// <summary>
    /// 幅の並びから、折り返した行数を数える。
    /// </summary>
    /// <remarks>
    /// 描画にも文字の計測にも依らない純粋な計算に切り出してある。
    /// 高さの確保と実際の折り返しが同じ規則で動くことを、機械的に検証するため。
    /// </remarks>
    internal static int CountRows(ReadOnlySpan<float> widths, float available, float gap)
    {
        var rows = 1;
        var x = 0f;

        for (var i = 0; i < widths.Length; i++)
        {
            if (x > 0f && x + widths[i] > available)
            {
                rows++;
                x = 0f;
            }

            x += widths[i] + gap;
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
