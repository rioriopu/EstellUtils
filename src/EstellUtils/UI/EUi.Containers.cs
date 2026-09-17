using System;
using System.Collections.Generic;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 中身をまとめるウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>ラベル幅を揃えるスコープのスタック。</summary>
    private static readonly List<LabelColumnState> LabelColumns = new(4);

    /// <summary>
    /// 枠と地を持つカードを開く。
    /// <code>
    /// using (EUi.Card("status"))
    /// {
    ///     EUi.Label("状態: 動作中");
    /// }
    /// </code>
    /// </summary>
    /// <param name="id">高さを記憶するための識別子。</param>
    /// <param name="padding">内側の余白。省略するとテーマのカード余白。</param>
    /// <remarks>
    /// 背景は前フレームに測った高さで先に描く。そのため中身の高さが変わった直後の
    /// 1 フレームだけ背景がずれるが、次のフレームで揃う。
    /// </remarks>
    public static CardHandle Card(ReadOnlySpan<char> id, EdgeInsets? padding = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var euId = ctx.GetId(id);
        var pad = padding ?? Metrics.CardPadding;

        ref var state = ref ctx.Store.GetRef(euId);

        var available = ctx.Layout.AvailableRect;
        var height = state.MeasuredHeight > 0f ? state.MeasuredHeight : pad.TotalVertical;
        var rect = Rect.FromSize(available.Min, new Vector2(available.Width, height));

        // カードは器なので、ホバーでは反応させない (中身のウィジェットが反応する)
        WidgetPainter.DrawCard(new WidgetVisual { Rect = rect });

        ctx.Layout.Push(
            LayoutKind.Vertical, available, new Vector2(0f, Metrics.ItemSpacing.Y),
            default, false, pad);

        return new CardHandle(euId);
    }

    /// <summary>
    /// 見出し付きのセクションを開く。折りたたみに対応する。
    /// <code>
    /// using (var s = EUi.Section("共通設定"))
    /// {
    ///     if (s.IsOpen)
    ///     {
    ///         EUi.Checkbox("デバッグ", ref debug);
    ///     }
    /// }
    /// </code>
    /// </summary>
    /// <param name="label">見出し。</param>
    /// <param name="collapsible">クリックで折りたためるか。</param>
    /// <param name="defaultOpen">初期状態で開いているか。</param>
    public static SectionHandle Section(
        ReadOnlySpan<char> label, bool collapsible = true, bool defaultOpen = true)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var id = ctx.GetId(label, out var display);
        ref var state = ref ctx.Store.GetRef(id);

        // 初回だけ既定の開閉状態を入れる
        if (!state.Initialized)
        {
            state.Initialized = true;
            state.Open = defaultOpen;
            state.OpenAmount = defaultOpen ? 1f : 0f;
        }

        var headerHeight = MathF.Max(Metrics.WidgetHeight, TextPainter.LineHeight + Metrics.SpacingSm);
        var headerRect = ctx.Allocate(SizeSpec.Fill, headerHeight);

        var interaction = collapsible
            ? Interaction.Behavior(headerRect, id)
            : default;

        if (collapsible && interaction.Clicked)
            state.Open = !state.Open;

        // 開閉は一定時間で進める。指数的に近づける方式だと畳まれ切る瞬間がぼやけてしまう
        var duration = Motion.Enabled ? Motion.CollapseDuration : 0f;

        if (duration > 0f)
        {
            var step = ctx.DeltaTime / duration;
            state.OpenAmount = state.Open
                ? MathF.Min(1f, state.OpenAmount + step)
                : MathF.Max(0f, state.OpenAmount - step);
        }
        else
        {
            state.OpenAmount = state.Open ? 1f : 0f;
        }

        var eased = Easing.Apply(EaseKind.OutCubic, state.OpenAmount);

        var visual = WidgetVisual.From(interaction, state.Open, eased) with { Rect = headerRect };
        WidgetPainter.DrawSectionHeader(visual, display, collapsible);

        var isOpen = state.Open || state.OpenAmount > 0.001f;

        if (!isOpen)
            return new SectionHandle(id, false, false, default, 0f);

        var clip = default(ClipScope);
        var bounds = ctx.Layout.AvailableRect;
        var contentHeight = state.MeasuredHeight;

        if (eased < 0.999f && contentHeight > 0f)
        {
            // 見えている高さを削りつつ、中身をわずかに上へ寄せる。
            // 下端が切り取られるだけの動きより、畳まれて吸い込まれるように見える
            var visibleHeight = contentHeight * eased;
            clip = Painter.Clip(Rect.FromSize(bounds.Min, new Vector2(bounds.Width, visibleHeight)));

            bounds = bounds.Offset(0f, -(1f - eased) * contentHeight * 0.3f);
        }

        ctx.Layout.Push(
            LayoutKind.Vertical, bounds, new Vector2(0f, Metrics.ItemSpacing.Y),
            default, false, new EdgeInsets(Metrics.SpacingMd, Metrics.SpacingSm, 0f, Metrics.SpacingMd));

        return new SectionHandle(id, state.Open, true, clip, eased);
    }

    /// <summary>
    /// コールバックで中身を書くセクション。
    /// 閉じているとき (畳むアニメーションも終わっているとき) は中身が呼ばれない。
    /// </summary>
    public static void Section(ReadOnlySpan<char> label, Action body, bool collapsible = true, bool defaultOpen = true)
    {
        using var section = Section(label, collapsible, defaultOpen);

        if (section.IsVisible)
            body();
    }

    /// <summary>
    /// ラベル幅を揃えるスコープを開く。この中の <see cref="Field"/> は同じ幅のラベル列を持つ。
    /// </summary>
    /// <param name="id">幅を記憶するための識別子。</param>
    /// <param name="minWidth">ラベル列の最小幅。</param>
    /// <param name="maxWidth">ラベル列の最大幅。長すぎるラベルで本体が潰れるのを防ぐ。</param>
    /// <remarks>
    /// 幅は前フレームに測った「最も長いラベル」に合わせる。項目が増減した直後の
    /// 1 フレームだけ列幅がずれるが、次のフレームで揃う。
    /// </remarks>
    public static LabelColumnHandle LabelColumn(
        ReadOnlySpan<char> id, float minWidth = 80f, float maxWidth = 260f)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var euId = ctx.GetId(id);
        ref var state = ref ctx.Store.GetRef(euId);

        // 前フレームの実測値を今フレームの列幅として採用し、集計用に測り直す
        var resolved = Math.Clamp(state.MeasuredWidth, minWidth, maxWidth);
        state.MeasuredWidth = 0f;

        LabelColumns.Add(new LabelColumnState(euId, resolved, maxWidth));
        return new LabelColumnHandle();
    }

    /// <summary>
    /// 「ラベル + ウィジェット」の 1 行を開く。
    /// <code>
    /// using (EUi.Field("更新間隔"))
    ///     EUi.SliderInt("##interval", ref interval, 1, 6);
    /// </code>
    /// </summary>
    /// <param name="label">左側に表示するラベル。</param>
    /// <param name="labelWidth">ラベル列の幅。省略すると <see cref="LabelColumn"/> の幅、それも無ければ内容幅。</param>
    public static LayoutHandle Field(ReadOnlySpan<char> label, SizeSpec? labelWidth = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var textSize = TextPainter.Measure(label);
        float width;

        if (labelWidth.HasValue)
        {
            width = labelWidth.Value.Resolve(AvailableWidth, textSize.X);
        }
        else if (LabelColumns.Count > 0)
        {
            var column = LabelColumns[^1];
            width = column.Width;

            // 今フレームの最長ラベルを記録して、次フレームの列幅に使う
            ref var state = ref ctx.Store.GetRef(column.Id);
            var measured = MathF.Min(MathF.Ceiling(textSize.X) + Metrics.LabelSpacing, column.MaxWidth);
            if (measured > state.MeasuredWidth)
                state.MeasuredWidth = measured;
        }
        else
        {
            width = MathF.Ceiling(textSize.X) + Metrics.LabelSpacing;
        }

        var handle = Row(SizeSpec.Px(width), SizeSpec.Fill);

        var labelRect = ctx.Allocate(
            SizeSpec.Px(width),
            MathF.Max(Metrics.WidgetHeight, textSize.Y));

        TextPainter.TextIn(labelRect, Colors.Text, label, Align.Start, Align.Center);

        return handle;
    }

    /// <summary>ラベル幅を揃えるスコープの状態。</summary>
    private readonly record struct LabelColumnState(EuId Id, float Width, float MaxWidth);

    /// <summary>ラベル列スコープを 1 段閉じる。</summary>
    internal static void PopLabelColumn()
    {
        if (LabelColumns.Count > 0)
            LabelColumns.RemoveAt(LabelColumns.Count - 1);
    }

    /// <summary>ラベル列スコープをすべて捨てる。フレーム境界での保険。</summary>
    internal static void ResetLabelColumns() => LabelColumns.Clear();
}

/// <summary><c>using</c> でカードを閉じるハンドル。</summary>
public readonly struct CardHandle : IDisposable
{
    private readonly EuId id;

    internal CardHandle(EuId id) => this.id = id;

    /// <summary>カードを閉じ、次フレーム用に高さを記録する。</summary>
    public void Dispose()
    {
        var ctx = UiContext.Current;
        var consumed = ctx.Layout.Current?.ConsumedSize ?? Vector2.Zero;

        ctx.Layout.Pop();

        ref var state = ref ctx.Store.GetRef(this.id);
        state.MeasuredHeight = consumed.Y;
    }
}

/// <summary><c>using</c> でセクションを閉じるハンドル。</summary>
public readonly struct SectionHandle : IDisposable
{
    private readonly bool pushedLayout;
    private readonly ClipScope clip;
    private readonly float openAmount;

    internal SectionHandle(EuId id, bool open, bool pushedLayout, ClipScope clip, float openAmount)
    {
        this.Id = id;
        this.IsOpen = open;
        this.pushedLayout = pushedLayout;
        this.clip = clip;
        this.openAmount = openAmount;
    }

    /// <summary>セクションの ID。</summary>
    public EuId Id { get; }

    /// <summary>見出しが開いた状態か。設定の表示条件などに使う。</summary>
    public bool IsOpen { get; }

    /// <summary>
    /// 中身を描く必要があるか。畳むアニメーションの最中も true になる。
    /// 中身の描画はこちらで判定する。
    /// </summary>
    public bool IsVisible => this.pushedLayout;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.pushedLayout)
            return;

        var ctx = UiContext.Current;
        var consumed = ctx.Layout.Current?.ConsumedSize ?? Vector2.Zero;

        // 実際に消費する高さは開閉の進み具合を掛けたもの。外側への申告は自分で行う
        ctx.Layout.Pop(commitToParent: false);
        this.clip.Dispose();

        ref var state = ref ctx.Store.GetRef(this.Id);
        state.MeasuredHeight = consumed.Y;

        var height = consumed.Y * this.openAmount;
        if (height > 0.5f)
            ctx.Allocate(new Vector2(consumed.X, height));
    }
}

/// <summary><c>using</c> でラベル列スコープを閉じるハンドル。</summary>
public readonly struct LabelColumnHandle : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => EUi.PopLabelColumn();
}
