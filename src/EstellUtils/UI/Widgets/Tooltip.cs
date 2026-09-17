using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;

namespace EstellUtils.UI.Widgets;

/// <summary>
/// ツールチップ。ImGui の <c>BeginTooltip</c> は使わず、最前面の描画リストへ直接描く。
/// </summary>
/// <remarks>
/// ImGui のツールチップウィンドウを使うとテーマの見た目が適用できないため、
/// 位置決めと描画をすべて自前で行う。画面端でははみ出さないように折り返す。
/// </remarks>
public static class Tooltip
{
    /// <summary>マウスカーソルからのずらし量。</summary>
    private static readonly Vector2 CursorOffset = new(16f, 20f);

    /// <summary>直前にツールチップを出したフレーム。続けて出すときの待ち時間を省くのに使う。</summary>
    private static uint lastShownFrame;

    /// <summary>
    /// ホバーが一定時間続いていればツールチップを表示する。
    /// </summary>
    /// <param name="text">表示する説明文。</param>
    /// <param name="hoveredDuration">ホバーが続いている秒数。</param>
    /// <remarks>
    /// 直前のフレームまでツールチップが出ていた場合は待ち時間を省く。
    /// 説明の付いた項目を続けてなぞるときに、いちいち待たされないようにするため。
    /// </remarks>
    public static void Show(ReadOnlySpan<char> text, float hoveredDuration)
    {
        if (text.IsEmpty)
            return;

        var ctx = UiContext.Current;
        var motion = ThemeManager.Current.Motion;

        // 2 フレーム以内に出ていたなら、続けて見ているとみなす
        var continuing = lastShownFrame != 0 && ctx.FrameCount - lastShownFrame <= 2;
        var delay = continuing ? 0f : motion.TooltipDelay;

        if (hoveredDuration < delay)
            return;

        Draw(text);
        lastShownFrame = ctx.FrameCount;
    }

    /// <summary>遅延なしでツールチップを表示する。</summary>
    public static void Draw(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return;

        var ctx = UiContext.Current;
        if (!ctx.Input.HasMousePos)
            return;

        var theme = ThemeManager.Current;
        var metrics = theme.Metrics;
        var colors = theme.Colors;

        // 最前面レイヤーへ描く。ウィンドウの重なりに関係なく常に見える
        using var _ = Painter.UseForeground();

        var wrapWidth = metrics.TooltipMaxWidth - metrics.TooltipPadding.TotalHorizontal;
        var textSize = TextPainter.Measure(text, wrapWidth);
        var boxSize = textSize + metrics.TooltipPadding.Total;

        var position = ClampToViewport(ctx.Input.MousePos + CursorOffset, boxSize);
        var rect = Rect.FromSize(position, boxSize);

        Painter.Shadow(rect, colors.Shadow, 6f, metrics.TooltipRounding, new Vector2(0f, 2f));
        Painter.Rect(rect, colors.TooltipBackground, metrics.TooltipRounding);
        Painter.RectOutline(rect, colors.TooltipBorder, 1f, metrics.TooltipRounding);

        TextPainter.TextWrapped(rect.Min + metrics.TooltipPadding.TopLeft, colors.Text, text, wrapWidth);
    }

    /// <summary>画面からはみ出さない位置へ補正する。</summary>
    private static Vector2 ClampToViewport(Vector2 position, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();
        var min = viewport.WorkPos;
        var max = viewport.WorkPos + viewport.WorkSize;

        // 右や下へはみ出す場合はカーソルの反対側へ寄せる
        if (position.X + size.X > max.X)
            position.X = MathF.Max(min.X, max.X - size.X);

        if (position.Y + size.Y > max.Y)
            position.Y = MathF.Max(min.Y, position.Y - size.Y - CursorOffset.Y - 4f);

        return new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
    }
}
