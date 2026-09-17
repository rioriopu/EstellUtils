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
    /// 選択できる 1 行。一覧を自前で組み立てるときに使う。
    /// </summary>
    /// <param name="label">表示する文字列。</param>
    /// <param name="selected">選択中か。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="height">高さ。省略すると標準のウィジェット高さ。</param>
    /// <param name="disabled">無効にするか。</param>
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
        SizeSpec? width = null, float? height = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, height ?? Metrics.WidgetHeight);

        var interaction = Interaction.Behavior(
            rect, id, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        if (selected)
        {
            Painter.Rect(rect, Colors.Selection, Metrics.WidgetRounding);
        }
        else if (interaction.HoverAmount > 0.01f)
        {
            Painter.Rect(
                rect,
                EuColor.WithAlpha(Colors.SurfaceHover, interaction.HoverAmount * 0.9f),
                Metrics.WidgetRounding);
        }

        var color = disabled
            ? Colors.TextDisabled
            : selected ? Colors.TextHeading : Colors.Text;

        var textRect = rect.Shrink(EdgeInsets.Horizontal(Metrics.SpacingSm));
        var truncated = TextPainter.Measure(display).X > textRect.Width + 1f;

        TextPainter.TextIn(textRect, color, display, Align.Start, Align.Center);

        var result = WidgetResult.From(interaction) with { Truncated = truncated };

        if (truncated)
            result.Tip(display);

        return result;
    }
}
