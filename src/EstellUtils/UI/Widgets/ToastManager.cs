using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;

namespace EstellUtils.UI.Widgets;

/// <summary>
/// 画面の隅に一定時間だけ出る通知。
/// </summary>
/// <remarks>
/// ウィンドウを持たず、最前面の描画リストへ直接描く。ゲーム画面の上に重なるため、
/// プラグインのウィンドウが閉じていても通知だけは見える。
/// </remarks>
public static class ToastManager
{
    /// <summary>同時に表示する上限。これを超えると古いものから消える。</summary>
    private const int MaxVisible = 5;

    /// <summary>通知の幅。</summary>
    private const float ToastWidth = 280f;

    /// <summary>画面端からの余白。</summary>
    private const float ScreenMargin = 16f;

    private static readonly List<ToastItem> Items = new(8);

    /// <summary>表示中の通知の数。</summary>
    public static int Count => Items.Count;

    /// <summary>通知を出す。</summary>
    /// <param name="message">本文。</param>
    /// <param name="kind">種類。色が変わる。</param>
    /// <param name="duration">表示し続ける秒数。</param>
    public static void Show(string message, NoteKind kind = NoteKind.Info, float duration = 3.5f)
    {
        if (string.IsNullOrEmpty(message))
            return;

        Items.Add(new ToastItem(message, kind, UiContext.Current.Time, MathF.Max(0.5f, duration)));

        if (Items.Count > MaxVisible)
            Items.RemoveRange(0, Items.Count - MaxVisible);
    }

    /// <summary>表示中の通知をすべて消す。</summary>
    public static void Clear() => Items.Clear();

    /// <summary>通知を描く。毎フレーム呼ばれる。</summary>
    public static void Draw()
    {
        if (Items.Count == 0)
            return;

        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var theme = ThemeManager.Current;
        var metrics = theme.Metrics;
        var colors = theme.Colors;

        var viewport = ImGui.GetMainViewport();
        var anchor = viewport.WorkPos + viewport.WorkSize - new Vector2(ScreenMargin, ScreenMargin);

        using var layer = Painter.UseForeground();

        var now = ctx.Time;
        var offsetY = 0f;

        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            var elapsed = now - item.CreatedAt;

            if (elapsed >= item.Duration)
            {
                Items.RemoveAt(i);
                continue;
            }

            // 出るときは横から滑り込み、消える前にフェードアウトする
            var appear = Easing.Apply(EaseKind.OutCubic, Math.Clamp(elapsed / 0.25f, 0f, 1f));
            var remaining = item.Duration - elapsed;
            var fade = Math.Clamp(remaining / 0.4f, 0f, 1f);
            var alpha = appear * fade;

            var wrapWidth = ToastWidth - metrics.TooltipPadding.TotalHorizontal;
            var textSize = TextPainter.Measure(item.Message, wrapWidth);
            var boxSize = new Vector2(ToastWidth, textSize.Y + metrics.TooltipPadding.TotalVertical);

            offsetY += boxSize.Y;

            var slide = (1f - appear) * 40f;
            var position = new Vector2(
                anchor.X - boxSize.X + slide,
                anchor.Y - offsetY);

            var rect = Rect.FromSize(position, boxSize);

            Painter.Shadow(rect, EuColor.ScaleAlpha(colors.Shadow, alpha), 6f, metrics.CardRounding, new Vector2(0f, 2f));
            Painter.Rect(rect, EuColor.ScaleAlpha(colors.TooltipBackground, alpha), metrics.CardRounding);
            Painter.RectOutline(rect, EuColor.ScaleAlpha(colors.TooltipBorder, alpha), 1f, metrics.CardRounding);

            // 左端に種類を示す色帯を引く
            var accent = item.Kind switch
            {
                NoteKind.Success => colors.Success,
                NoteKind.Warning => colors.Warning,
                NoteKind.Danger => colors.Danger,
                _ => colors.Info,
            };

            Painter.Rect(
                rect.WithWidth(3f),
                EuColor.ScaleAlpha(accent, alpha),
                metrics.CardRounding,
                Corners.Left);

            TextPainter.TextWrapped(
                rect.Min + metrics.TooltipPadding.TopLeft,
                EuColor.ScaleAlpha(colors.Text, alpha),
                item.Message,
                wrapWidth);

            offsetY += metrics.SpacingSm;
        }
    }

    /// <summary>1 件の通知。</summary>
    private readonly record struct ToastItem(string Message, NoteKind Kind, float CreatedAt, float Duration);
}
