using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

using EstellUtils.UI.Core;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;

namespace EstellUtils.UI.Widgets;

/// <summary>
/// 画面の隅に一定時間だけ出る通知。
/// </summary>
/// <remarks>
/// <para>
/// ウィンドウを持たず、最前面の描画リストへ直接描く。ゲーム画面の上に重なるため、
/// プラグインのウィンドウが閉じていても通知だけは見える。
/// </para>
/// <para>
/// マウスを乗せている間は時間が止まるので、読んでいる最中に消えることがない。
/// クリックするとその場で閉じる。
/// </para>
/// </remarks>
public static class ToastManager
{
    /// <summary>同時に表示する上限。これを超えると古いものから消える。</summary>
    private const int MaxVisible = 6;

    /// <summary>通知の幅。</summary>
    private const float ToastWidth = 340f;

    /// <summary>画面端からの余白。</summary>
    private const float ScreenMargin = 16f;

    /// <summary>内側の余白。</summary>
    private const float Padding = 11f;

    /// <summary>左端に引く種類の色帯の幅。</summary>
    private const float AccentBarWidth = 4f;

    /// <summary>アイコン欄の幅。</summary>
    private const float IconColumnWidth = 26f;

    /// <summary>下端に出す残り時間バーの高さ。</summary>
    private const float ProgressHeight = 2f;

    /// <summary>出現にかける秒数。</summary>
    private const float AppearDuration = 0.22f;

    /// <summary>消えるまでのフェードに使う秒数。</summary>
    private const float FadeDuration = 0.35f;

    private static readonly List<ToastItem> Items = new(8);

    /// <summary>表示中の通知の数。</summary>
    public static int Count => Items.Count;

    /// <summary>通知を出す。</summary>
    /// <param name="message">本文。</param>
    /// <param name="kind">種類。色とアイコンが変わる。</param>
    /// <param name="duration">表示し続ける秒数。</param>
    public static void Show(string message, NoteKind kind = NoteKind.Info, float duration = 4f)
        => Show(null, message, kind, duration);

    /// <summary>
    /// 見出し付きの通知を出す。何が起きたのかを一目で伝えたいときに使う。
    /// </summary>
    /// <param name="title">見出し。省略すると本文だけになる。</param>
    /// <param name="message">本文。</param>
    /// <param name="kind">種類。色とアイコンが変わる。</param>
    /// <param name="duration">表示し続ける秒数。</param>
    public static void Show(string? title, string message, NoteKind kind = NoteKind.Info, float duration = 4f)
    {
        if (string.IsNullOrEmpty(message) && string.IsNullOrEmpty(title))
            return;

        Items.Add(new ToastItem
        {
            Title = string.IsNullOrEmpty(title) ? null : title,
            Message = message ?? string.Empty,
            Kind = kind,
            Duration = MathF.Max(0.5f, duration),
        });

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
        var viewport = ImGui.GetMainViewport();
        var anchor = viewport.WorkPos + viewport.WorkSize - new Vector2(ScreenMargin, ScreenMargin);

        using var layer = Painter.UseForeground();

        var dt = ctx.DeltaTime;
        var offsetY = 0f;

        // 新しいものを下に積む
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var item = Items[i];
            var size = Measure(theme, item);

            offsetY += size.Y;

            var appear = Easing.Apply(EaseKind.OutCubic, Math.Clamp(item.Appear / AppearDuration, 0f, 1f));
            var slide = (1f - appear) * 48f;

            var rect = Rect.FromSize(
                new Vector2(
                    MathF.Round(anchor.X - size.X + slide),
                    MathF.Round(anchor.Y - offsetY)),
                size);

            var hovered = ctx.Input.HasMousePos && rect.Contains(ctx.Input.MousePos);

            // 読んでいる間は時間を止める
            item.Appear += dt;

            if (!hovered)
                item.Elapsed += dt;

            var remaining = item.Duration - item.Elapsed;

            if (remaining <= 0f)
            {
                Items.RemoveAt(i);
                offsetY -= size.Y;
                continue;
            }

            // クリックで閉じる
            if (hovered && ctx.Input.IsPressed(MouseButton.Left))
            {
                Items.RemoveAt(i);
                offsetY -= size.Y;
                continue;
            }

            var fade = Math.Clamp(remaining / FadeDuration, 0f, 1f);
            var alpha = appear * fade;

            Draw(theme, item, rect, alpha, hovered, remaining);

            offsetY += theme.Metrics.SpacingSm;
        }
    }

    /// <summary>通知 1 件の大きさを求める。</summary>
    private static Vector2 Measure(Theme theme, ToastItem item)
    {
        var textWidth = ToastWidth - (Padding * 2f) - AccentBarWidth - IconColumnWidth;
        var lineHeight = TextPainter.LineHeight;

        var height = 0f;

        if (item.Title is not null)
            height += lineHeight + (theme.Metrics.SpacingXs * 2f);

        if (item.Message.Length > 0)
            height += TextPainter.Measure(item.Message, textWidth).Y;

        // アイコンの高さ分は最低限確保する
        height = MathF.Max(height, lineHeight * 1.4f);

        return new Vector2(ToastWidth, height + (Padding * 2f) + ProgressHeight);
    }

    /// <summary>通知 1 件を描く。</summary>
    private static void Draw(
        Theme theme, ToastItem item, Rect rect, float alpha, bool hovered, float remaining)
    {
        var metrics = theme.Metrics;
        var colors = theme.Colors;
        var rounding = metrics.CardRounding;

        var accent = item.Kind switch
        {
            NoteKind.Success => colors.Success,
            NoteKind.Warning => colors.Warning,
            NoteKind.Danger => colors.Danger,
            _ => colors.Info,
        };

        // 地と枠。マウスが乗っている間は少し明るくして、止まっていることを見せる
        var background = hovered
            ? EuColor.Lerp(colors.TooltipBackground, colors.SurfaceHover, 0.5f)
            : colors.TooltipBackground;

        Painter.Shadow(rect, EuColor.ScaleAlpha(colors.Shadow, alpha), 8f, rounding, new Vector2(0f, 3f));
        Painter.Rect(rect, EuColor.ScaleAlpha(background, alpha), rounding);

        // 種類を示す帯を左端へ
        Painter.Rect(
            rect.WithWidth(AccentBarWidth), EuColor.ScaleAlpha(accent, alpha), rounding, Corners.Left);

        Painter.RectOutline(
            rect, EuColor.ScaleAlpha(hovered ? accent : colors.TooltipBorder, alpha), 1f, rounding);

        var inner = rect.Shrink(new EdgeInsets(AccentBarWidth + Padding, Padding, Padding, Padding + ProgressHeight));

        // アイコン欄
        var iconArea = inner.CutLeft(IconColumnWidth, out var textArea);

        using (EUi.PushFont(FontRole.Icon))
        {
            var icon = item.Kind switch
            {
                NoteKind.Success => FontAwesomeIcon.CheckCircle,
                NoteKind.Warning => FontAwesomeIcon.ExclamationTriangle,
                NoteKind.Danger => FontAwesomeIcon.TimesCircle,
                _ => FontAwesomeIcon.InfoCircle,
            };

            TextPainter.TextIn(
                iconArea.WithHeight(TextPainter.LineHeight * 1.4f),
                EuColor.ScaleAlpha(accent, alpha),
                icon.ToIconString(),
                Align.Start,
                Align.Center,
                ellipsize: false);
        }

        // 見出しと本文
        var cursor = textArea.Min;

        if (item.Title is not null)
        {
            TextPainter.Text(cursor, EuColor.ScaleAlpha(colors.TextHeading, alpha), item.Title);
            cursor.Y += TextPainter.LineHeight + (metrics.SpacingXs * 2f);
        }

        if (item.Message.Length > 0)
        {
            TextPainter.TextWrapped(
                cursor, EuColor.ScaleAlpha(colors.Text, alpha), item.Message, textArea.Width);
        }

        // 残り時間。マウスを乗せている間は止まるので、待ってくれていることが分かる
        var ratio = Math.Clamp(remaining / item.Duration, 0f, 1f);
        var barRect = Rect.FromSize(
            new Vector2(rect.Min.X + AccentBarWidth, rect.Max.Y - ProgressHeight),
            new Vector2((rect.Width - AccentBarWidth) * ratio, ProgressHeight));

        Painter.Rect(barRect, EuColor.ScaleAlpha(accent, alpha * (hovered ? 0.45f : 0.75f)), 0f);
    }

    /// <summary>1 件の通知。</summary>
    private sealed class ToastItem
    {
        public string? Title { get; init; }

        public string Message { get; init; } = string.Empty;

        public NoteKind Kind { get; init; }

        public float Duration { get; init; }

        /// <summary>表示されてからの経過。マウスが乗っている間は進まない。</summary>
        public float Elapsed { get; set; }

        /// <summary>出現アニメーションの進み。</summary>
        public float Appear { get; set; }
    }
}
