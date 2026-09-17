using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;

namespace EstellUtils.UI.Layout;

/// <summary>
/// スクロール領域。ImGui の子ウィンドウやスクロールバーは使わず、
/// クリップ・内容のオフセット・つまみの描画をすべて自前で行う。
/// </summary>
/// <remarks>
/// 内容の高さは前フレームの実測値を使う。初回フレームだけスクロールバーの長さが
/// 確定しないが、2 フレーム目以降は正確になる。
/// </remarks>
public static class ScrollArea
{
    /// <summary>ホイール 1 目盛りのスクロール量 (ピクセル)。</summary>
    public const float WheelStep = 48f;

    /// <summary>
    /// スクロール領域を開く。<c>using</c> で閉じること。
    /// </summary>
    /// <param name="id">状態を保持するための識別子。</param>
    /// <param name="height">領域の高さ。</param>
    /// <param name="spacing">内容の要素間の空き。</param>
    public static ScrollHandle Begin(ReadOnlySpan<char> id, float height, float? spacing = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var theme = ThemeManager.Current;
        var euId = ctx.GetId(id);
        var rect = ctx.Allocate(SizeSpec.Fill, height);

        ref var state = ref ctx.Store.GetRef(euId);

        var contentHeight = state.MeasuredHeight;
        var maxScroll = MathF.Max(0f, contentHeight - rect.Height);
        var showBar = maxScroll > 0.5f;

        state.ScrollTarget = Math.Clamp(state.ScrollTarget, 0f, maxScroll);

        state.Scroll = theme.Motion.Enabled
            ? Anim.Approach(state.Scroll, state.ScrollTarget, theme.Motion.ScrollSpeed, ctx.DeltaTime)
            : state.ScrollTarget;

        var barWidth = showBar ? theme.Metrics.ScrollbarWidth : 0f;

        // 内容はスクロールバーより手前で止める。ぴったりまで使うと、
        // 行末の文字やスライダーの値がつまみに触れて読みにくい
        var contentInset = showBar ? barWidth + theme.Metrics.SpacingSm : 0f;

        // 内容はクリップ矩形の中へ、スクロール量だけ上へずらして描く
        var clip = Painter.Clip(rect);

        var contentBounds = new Rect(
            new Vector2(rect.Min.X, rect.Min.Y - state.Scroll),
            new Vector2(rect.Max.X - contentInset, rect.Min.Y - state.Scroll + 1_000_000f));

        var gap = new Vector2(0f, spacing ?? theme.Metrics.ItemSpacing.Y);
        ctx.Layout.Push(LayoutKind.Vertical, contentBounds, gap);

        return new ScrollHandle(euId, rect, barWidth, maxScroll, clip);
    }
}

/// <summary><c>using</c> でスクロール領域を閉じるハンドル。</summary>
public readonly struct ScrollHandle : IDisposable
{
    private readonly EuId id;
    private readonly Rect rect;
    private readonly float barWidth;
    private readonly float maxScroll;
    private readonly ClipScope clip;

    internal ScrollHandle(EuId id, Rect rect, float barWidth, float maxScroll, ClipScope clip)
    {
        this.id = id;
        this.rect = rect;
        this.barWidth = barWidth;
        this.maxScroll = maxScroll;
        this.clip = clip;
    }

    /// <summary>領域を閉じ、内容の高さを記録してスクロールバーを描く。</summary>
    public void Dispose()
    {
        var ctx = UiContext.Current;
        var scope = ctx.Layout.Current;

        var contentHeight = scope?.ConsumedSize.Y ?? 0f;

        // 領域は Begin で確保済みなので、外側へは申告しない
        ctx.Layout.Pop(commitToParent: false);

        // スクロールバーはクリップの外側へ描くので、先に解除する
        this.clip.Dispose();

        ref var state = ref ctx.Store.GetRef(this.id);
        state.MeasuredHeight = contentHeight;

        // ホイールは領域を閉じるときに拾う。入れ子になっている場合、内側の領域から
        // 先に Dispose されるので、マウスが乗っている一番内側だけが反応する
        this.HandleWheel(ctx, ref state, contentHeight);

        if (this.barWidth > 0f)
            this.DrawScrollbar(ctx, ref state, contentHeight);
    }

    /// <summary>ホイール入力を拾ってスクロール位置を動かす。</summary>
    private void HandleWheel(UiContext ctx, ref WidgetState state, float contentHeight)
    {
        var wheel = ctx.Input.WheelY;

        if (wheel == 0f || ctx.WheelConsumed)
            return;

        var maxScroll = MathF.Max(0f, contentHeight - this.rect.Height);
        if (maxScroll <= 0.5f)
            return;

        if (!Interaction.IsHovered(this.rect))
            return;

        state.ScrollTarget = Math.Clamp(state.ScrollTarget - (wheel * ScrollArea.WheelStep), 0f, maxScroll);
        ctx.WheelConsumed = true;
    }

    /// <summary>スクロールバーを描き、つまみのドラッグを処理する。</summary>
    private void DrawScrollbar(UiContext ctx, ref WidgetState state, float contentHeight)
    {
        var theme = ThemeManager.Current;
        var metrics = theme.Metrics;
        var colors = theme.Colors;

        var trackRect = new Rect(
            new Vector2(this.rect.Max.X - this.barWidth, this.rect.Min.Y),
            this.rect.Max);

        Painter.Rect(trackRect, colors.ScrollbarTrack, metrics.ScrollbarRounding);

        // つまみの長さは「表示領域 / 内容全体」の比率で決める
        var viewRatio = Math.Clamp(this.rect.Height / MathF.Max(contentHeight, 1f), 0.08f, 1f);
        var grabHeight = MathF.Max(24f, trackRect.Height * viewRatio);
        var scrollRatio = this.maxScroll > 0f ? Math.Clamp(state.Scroll / this.maxScroll, 0f, 1f) : 0f;
        var grabTop = trackRect.Min.Y + ((trackRect.Height - grabHeight) * scrollRatio);

        var grabRect = Rect.FromSize(
            new Vector2(trackRect.Min.X + 1f, grabTop),
            new Vector2(trackRect.Width - 2f, grabHeight));

        var grabId = this.id.Child("scrollGrab");
        var result = Interaction.Behavior(grabRect, grabId, InteractionFlags.AllowDragOutside);

        // つまみの外側 (溝) を押したときは、その位置へ飛ばす
        if (!result.Hovered && this.maxScroll > 0f)
        {
            var trackResult = Interaction.Behavior(
                trackRect, this.id.Child("scrollTrack"), InteractionFlags.ClickOnPress);

            if (trackResult.Clicked)
            {
                var localY = ctx.Input.MousePos.Y - trackRect.Min.Y - (grabHeight * 0.5f);
                var ratio = Math.Clamp(localY / MathF.Max(1f, trackRect.Height - grabHeight), 0f, 1f);

                state.ScrollTarget = ratio * this.maxScroll;
            }
        }

        if (result.Held && this.maxScroll > 0f)
        {
            // つまみを掴んだままの移動量を、スクロール量へ換算する
            var travel = MathF.Max(1f, trackRect.Height - grabHeight);
            var delta = ctx.Input.MouseDelta.Y / travel * this.maxScroll;

            state.ScrollTarget = Math.Clamp(state.ScrollTarget + delta, 0f, this.maxScroll);
            state.Scroll = state.ScrollTarget;
        }
        else if (result.Pressed)
        {
            state.ScrollTarget = state.Scroll;
        }

        var grabColor = EuColor.Lerp(colors.ScrollbarGrab, colors.ScrollbarGrabHover, result.HoverAmount);
        Painter.Rect(grabRect, grabColor, metrics.ScrollbarRounding);
    }
}
