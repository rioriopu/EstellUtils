using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 画像と選択行。
/// </summary>
public static partial class EUi
{
    /// <summary>
    /// 画像を表示する。
    /// </summary>
    /// <param name="texture">Dalamud のテクスチャ。</param>
    /// <param name="size">表示する大きさ。</param>
    /// <param name="tint">乗算色。省略すると白 (そのまま)。</param>
    /// <remarks>
    /// アイテムアイコンなどの表示に使う。テクスチャの取得と破棄は呼び出し側の責任。
    /// </remarks>
    public static WidgetResult Image(IDalamudTextureWrap? texture, Vector2 size, uint? tint = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var rect = ctx.Allocate(size);

        if (texture is not null)
            Painter.Image(texture.Handle, rect, tint ?? EuColor.White);

        return MakeTextResult(ctx, rect);
    }

    /// <summary>画像を押せるボタンにする。</summary>
    /// <param name="texture">Dalamud のテクスチャ。</param>
    /// <param name="id">識別子。</param>
    /// <param name="size">表示する大きさ。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult ImageButton(
        IDalamudTextureWrap? texture, string id, Vector2 size, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id);
        var padding = Metrics.SpacingXs;
        var rect = ctx.Allocate(size + new Vector2(padding * 2f));

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        // ホバーと押下が分かるよう、地と枠を添える
        var background = EuColor.WithAlpha(Colors.SurfaceHover, interaction.HoverAmount * 0.8f);
        Painter.Rect(rect, background, Metrics.WidgetRounding);

        if (interaction.PressAmount > 0.01f)
        {
            Painter.Rect(
                rect, EuColor.WithAlpha(EuColor.Black, interaction.PressAmount * 0.25f), Metrics.WidgetRounding);
        }

        if (texture is not null)
        {
            var tint = disabled ? EuColor.WithAlpha(EuColor.White, 0.4f) : EuColor.White;
            Painter.Image(texture.Handle, rect.Shrink(padding), tint);
        }

        if (interaction.HoverAmount > 0.01f)
        {
            Painter.RectOutline(
                rect,
                EuColor.ScaleAlpha(Colors.WidgetBorderHover, interaction.HoverAmount),
                1f,
                Metrics.WidgetRounding);
        }

        return WidgetResult.From(interaction);
    }

    /// <summary>
    /// 中身を自分で描く選択行。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="selected">選択中として強調するか。</param>
    /// <param name="height">行の高さ。省略すると標準の高さ。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <param name="spacing">中身の要素間の空き。</param>
    /// <remarks>
    /// <para>
    /// 行全体が当たり判定になり、その中へ好きなものを並べられる。
    /// 文字だけなら <c>EUi.Selectable</c> で足りる。
    /// </para>
    /// <code>
    /// var row = EUi.SelectableRow("##item" + item.Id, item == current);
    ///
    /// using (row)
    /// {
    ///     EUi.TextColored(item.Name, item.Color);
    ///     EUi.Muted($"所持 {item.Count:N0}");
    /// }
    ///
    /// if (row.Clicked)
    ///     Pick(item);
    /// </code>
    /// <para>
    /// クリックの判定は開いた時点で済んでいるので、<c>using</c> を抜けたあとに読める。
    /// </para>
    /// </remarks>
    public static SelectableRowScope SelectableRow(
        ReadOnlySpan<char> id, bool selected,
        float? height = null, bool disabled = false, float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id);
        var rowHeight = height ?? Metrics.WidgetHeight;
        var rect = ctx.Allocate(SizeSpec.Fill, rowHeight);

        var interaction = Interaction.Behavior(
            rect, euId,
            disabled ? InteractionFlags.Disabled : InteractionFlags.AllowRightClick);

        DrawSelectionSurface(rect, selected, interaction.HoverAmount);

        var gap = new Vector2(spacing ?? Metrics.ItemSpacing.X, 0f);

        ctx.Layout.Push(
            LayoutKind.Horizontal, rect, gap, default, false,
            EdgeInsets.Horizontal(Metrics.SpacingSm), Align.Center, rowHeight);

        // 領域は上で確保済みなので、閉じるときに二重で消費しない
        return new SelectableRowScope(WidgetResult.From(interaction), rect);
    }

    /// <summary>
    /// 選択できる 1 行。一覧を自前で組み立てるときに使う。
    /// </summary>
    /// <param name="label">表示する文字列。</param>
    /// <param name="selected">選択中か。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="height">高さ。省略すると標準のウィジェット高さ。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <param name="color">
    /// 文字色。省略するとテーマの標準色。
    /// 「薄く見せるが押せる」項目を作るのに使う。無効にすると押せなくなるので、
    /// 選べない理由を知らせたい場合はこちらで薄くする。
    /// </param>
    /// <remarks>
    /// <see cref="ListBox"/> は文字列の配列しか扱えないため、行ごとに色を変えたり
    /// アイコンを添えたりしたい場合はこちらを使う。
    /// <code>
    /// foreach (var item in items)
    /// {
    ///     using (EUi.Row(24f, SizeSpec.Fill))
    ///     {
    ///         EUi.Image(item.Icon, new Vector2(20, 20));
    ///
    ///         if (EUi.Selectable(item.Name, item == selected))
    ///             selected = item;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static WidgetResult Selectable(
        ReadOnlySpan<char> label, bool selected,
        SizeSpec? width = null, float? height = null, bool disabled = false,
        uint? color = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, height ?? Metrics.WidgetHeight);

        // 一覧の行は右クリックの対象になるのが自然なので、最初から拾っておく
        var interaction = Interaction.Behavior(
            rect, id,
            disabled ? InteractionFlags.Disabled : InteractionFlags.AllowRightClick);

        DrawSelectionSurface(rect, selected, interaction.HoverAmount);

        // 指定があればそれを使う。無効なら必ず薄い色にする
        var textColor = disabled
            ? Colors.TextDisabled
            : color ?? (selected ? Colors.TextHeading : Colors.Text);

        var textRect = rect.Shrink(EdgeInsets.Horizontal(Metrics.SpacingSm));
        var truncated = TextPainter.Measure(display).X > textRect.Width + 1f;

        TextPainter.TextIn(textRect, textColor, display, Align.Start, Align.Center);

        var result = WidgetResult.From(interaction) with { Truncated = truncated };

        if (truncated)
            result.Tip(display);

        return result;
    }

    /// <summary>
    /// 名前の下に補足を添えた 2 段の選択行。
    /// </summary>
    /// <param name="label">1 段目に出す名前。</param>
    /// <param name="detail">2 段目に出す補足。空なら 1 段だけになる。</param>
    /// <param name="selected">選択中か。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="height">高さ。省略すると 2 段ぶん。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <param name="color">名前の色。省略するとテーマの標準色。</param>
    /// <param name="detailColor">補足の色。省略すると控えめな色。</param>
    /// <remarks>
    /// <para>
    /// 「ダンジョン名の下に、選べない理由を小さく添える」といった一覧のための行。
    /// <c>EUi.SelectableRow</c> の中に <c>VStack</c> を入れて組むこともできるが、
    /// 行の高さを自分で計算することになるので、2 段で足りるならこちらを使う。
    /// </para>
    /// <code>
    /// foreach (var d in duties)
    /// {
    ///     if (EUi.Selectable(d.Name, d.Reason, d == current,
    ///                        color: d.Unlocked ? null : Colors.TextMuted).Clicked)
    ///     {
    ///         Pick(d);
    ///     }
    /// }
    /// </code>
    /// <para>
    /// 入り切らない段には省略記号が付き、ツールチップで全文が出る。
    /// </para>
    /// </remarks>
    public static WidgetResult Selectable(
        ReadOnlySpan<char> label, ReadOnlySpan<char> detail, bool selected,
        SizeSpec? width = null, float? height = null, bool disabled = false,
        uint? color = null, uint? detailColor = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var hasDetail = !detail.IsEmpty;
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, height ?? SelectableHeight(hasDetail));

        var interaction = Interaction.Behavior(
            rect, id,
            disabled ? InteractionFlags.Disabled : InteractionFlags.AllowRightClick);

        DrawSelectionSurface(rect, selected, interaction.HoverAmount);

        var textColor = disabled
            ? Colors.TextDisabled
            : color ?? (selected ? Colors.TextHeading : Colors.Text);

        var inner = rect.Shrink(EdgeInsets.Symmetric(Metrics.SpacingSm, Metrics.SpacingXs));

        if (!hasDetail)
        {
            var truncatedOnly = TextPainter.Measure(display).X > inner.Width + 1f;
            TextPainter.TextIn(inner, textColor, display, Align.Start, Align.Center);

            var single = WidgetResult.From(interaction) with { Truncated = truncatedOnly };

            if (truncatedOnly)
                single.Tip(display);

            return single;
        }

        // 1 段目を上から切り出し、残りを 2 段目に充てる。段の高さを両方とも
        // 固定値で置くと、行の高さを指定されたときに下段が枠から溢れる
        var lineHeight = TextPainter.LineHeight;
        var nameArea = inner.CutTop(MathF.Min(lineHeight, inner.Height), out var detailArea);

        var truncated =
            TextPainter.Measure(display).X > nameArea.Width + 1f
            || TextPainter.Measure(detail).X > detailArea.Width + 1f;

        TextPainter.TextIn(nameArea, textColor, display, Align.Start, Align.Center);

        if (detailArea.Height > 1f)
        {
            TextPainter.TextIn(
                detailArea,
                disabled ? Colors.TextDisabled : detailColor ?? Colors.TextMuted,
                detail, Align.Start, Align.Center);
        }

        var result = WidgetResult.From(interaction) with { Truncated = truncated };

        if (truncated)
            result.Tip(display);

        return result;
    }

    /// <summary>
    /// 選択行の標準の高さ。
    /// </summary>
    /// <param name="withDetail">補足を添えた 2 段の行か。</param>
    /// <remarks>
    /// <c>ComboBody</c> の <c>listHeight</c> や、箱の高さを「○ 行ぶん」で決めるときに使う。
    /// </remarks>
    public static float SelectableHeight(bool withDetail = false)
        => withDetail
            ? MathF.Ceiling((TextPainter.LineHeight * 2f) + (Metrics.SpacingXs * 2f))
            : Metrics.WidgetHeight;

    /// <summary>選択行の地を描く。選択中なら塗り、乗っているだけなら薄く光らせる。</summary>
    private static void DrawSelectionSurface(Rect rect, bool selected, float hoverAmount)
    {
        if (selected)
        {
            Painter.Rect(rect, Colors.Selection, Metrics.WidgetRounding);
        }
        else if (hoverAmount > 0.01f)
        {
            Painter.Rect(
                rect,
                EuColor.WithAlpha(Colors.SurfaceHover, hoverAmount * 0.9f),
                Metrics.WidgetRounding);
        }
    }
}

/// <summary>
/// <c>using</c> で選択行を閉じるハンドル。
/// </summary>
/// <remarks>
/// クリックの判定は行を開いた時点で済んでいる。
/// <c>using</c> を抜けたあとでも結果を読める。
/// </remarks>
public readonly struct SelectableRowScope : IDisposable
{
    internal SelectableRowScope(WidgetResult result, Rect rect)
    {
        this.Result = result;
        this.Rect = rect;
    }

    /// <summary>入力の結果。</summary>
    public WidgetResult Result { get; }

    /// <summary>行の矩形。</summary>
    public Rect Rect { get; }

    /// <summary>クリックされたか。</summary>
    public bool Clicked => this.Result.Clicked;

    /// <summary>右クリックされたか。</summary>
    public bool RightClicked => this.Result.RightClicked;

    /// <summary>ダブルクリックされたか。</summary>
    public bool DoubleClicked => this.Result.DoubleClicked;

    /// <summary>マウスが乗っているか。</summary>
    public bool Hovered => this.Result.Hovered;

    /// <inheritdoc/>
    public void Dispose() => UiContext.Current.Layout.Pop(commitToParent: false);
}
