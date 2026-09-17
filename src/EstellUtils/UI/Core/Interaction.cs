using System;

namespace EstellUtils.UI.Core;

/// <summary>ウィジェットの入力挙動を調整するフラグ。</summary>
[Flags]
public enum InteractionFlags
{
    /// <summary>既定。離したときにクリック成立。</summary>
    None = 0,

    /// <summary>無効状態。入力を一切受け付けず、ホバーもしない。</summary>
    Disabled = 1 << 0,

    /// <summary>押した瞬間にクリックを成立させる。ドラッグ開始を伴う操作に使う。</summary>
    ClickOnPress = 1 << 1,

    /// <summary>押しっぱなしでクリックを繰り返す。スピナーの増減ボタンなどに使う。</summary>
    Repeat = 1 << 2,

    /// <summary>右クリックも検知する。コンテキストメニュー用。</summary>
    AllowRightClick = 1 << 3,

    /// <summary>
    /// 押下中に矩形の外へ出てもクリックを成立させる。スライダーのつまみのように
    /// 掴んだまま動かす操作で使う。
    /// </summary>
    AllowDragOutside = 1 << 4,
}

/// <summary>
/// <see cref="Interaction.Behavior"/> の結果。ウィジェットはこれを見て見た目を決める。
/// </summary>
public readonly record struct InteractionResult
{
    /// <summary>対象のウィジェット ID。</summary>
    public EuId Id { get; init; }

    /// <summary>判定に使った矩形。</summary>
    public Rect Rect { get; init; }

    /// <summary>マウスが乗っているか。</summary>
    public bool Hovered { get; init; }

    /// <summary>押下中か (このウィジェットが操作中で、かつボタンが押されている)。</summary>
    public bool Held { get; init; }

    /// <summary>このフレームで押し込まれたか。</summary>
    public bool Pressed { get; init; }

    /// <summary>このフレームで離されたか。</summary>
    public bool Released { get; init; }

    /// <summary>クリックが成立したか。ボタンの発火判定はこれを使う。</summary>
    public bool Clicked { get; init; }

    /// <summary>ダブルクリックが成立したか。</summary>
    public bool DoubleClicked { get; init; }

    /// <summary>右クリックされたか (<see cref="InteractionFlags.AllowRightClick"/> 指定時のみ)。</summary>
    public bool RightClicked { get; init; }

    /// <summary>無効状態か。</summary>
    public bool Disabled { get; init; }

    /// <summary>ホバー遷移量 (0〜1)。色の補間に使う。</summary>
    public float HoverAmount { get; init; }

    /// <summary>押下遷移量 (0〜1)。</summary>
    public float PressAmount { get; init; }

    /// <summary>無効遷移量 (0〜1)。1 なら完全に無効。</summary>
    public float DisabledAmount { get; init; }

    /// <summary>押し始めてからのマウス移動量。ドラッグ操作で使う。</summary>
    public System.Numerics.Vector2 DragDelta { get; init; }
}

/// <summary>
/// ウィジェットの入力挙動をまとめた中核処理。
/// </summary>
/// <remarks>
/// <para>
/// ImGui の <c>ImGui.IsItemHovered</c> / <c>ImGui.Button</c> には一切依存せず、
/// 矩形と ID だけでホバー・押下・クリックを判定する。当たり判定の形を変えたい場合
/// (円形ボタンなど) は、呼び出し側で判定してからこのメソッドへ矩形を渡すか、
/// 本メソッドを参考に独自の挙動を書けばよい。
/// </para>
/// <para>
/// ホバー/押下の遷移量 (<see cref="InteractionResult.HoverAmount"/> 等) もここで更新されるため、
/// ウィジェット側はアニメーションの管理を意識しなくてよい。
/// </para>
/// </remarks>
public static class Interaction
{
    /// <summary>オートリピートの初回待ち時間 (秒)。</summary>
    private const float RepeatDelay = 0.4f;

    /// <summary>オートリピートの発火間隔 (秒)。</summary>
    private const float RepeatRate = 0.05f;

    /// <summary>ホバー遷移の速度。</summary>
    private const float HoverSpeed = 16f;

    /// <summary>押下遷移の速度。押下は即応性が要るのでホバーより速くする。</summary>
    private const float PressSpeed = 30f;

    /// <summary>
    /// 矩形に対するマウス操作を判定し、ホバー/押下のアニメーション値も更新する。
    /// </summary>
    /// <param name="rect">当たり判定の矩形 (画面座標)。</param>
    /// <param name="id">ウィジェット ID。</param>
    /// <param name="flags">挙動の調整。</param>
    public static InteractionResult Behavior(Rect rect, EuId id, InteractionFlags flags = InteractionFlags.None)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var input = ctx.Input;
        var disabled = (flags & InteractionFlags.Disabled) != 0;
        var isActive = ctx.ActiveId == id;

        if (isActive)
            ctx.KeepActiveIdAlive(id);

        var inRect = input.HasMousePos && rect.Contains(input.MousePos);

        // 他のウィジェットを操作中ならホバーさせない (ドラッグ中に別のボタンが光らないように)
        var hovered = !disabled && inRect && ctx.IsWindowHovered &&
                      (ctx.ActiveId.IsNone || isActive);

        if (hovered)
        {
            ctx.HotId = id;
            ctx.WantCaptureMouse = true;
        }

        var pressed = false;
        var released = false;
        var clicked = false;
        var doubleClicked = false;
        var rightClicked = false;

        if (!disabled)
        {
            if (hovered && input.IsPressed(MouseButton.Left))
            {
                ctx.SetActiveId(id);
                isActive = true;
                pressed = true;

                if ((flags & InteractionFlags.ClickOnPress) != 0)
                    clicked = true;
            }

            if (hovered && input.IsDoubleClicked(MouseButton.Left))
                doubleClicked = true;

            if (hovered && (flags & InteractionFlags.AllowRightClick) != 0 && input.IsPressed(MouseButton.Right))
                rightClicked = true;

            if (isActive)
            {
                if (input.IsReleased(MouseButton.Left))
                {
                    released = true;

                    // 離した位置が矩形内なら成立。押した瞬間に成立済みなら二重に立てない。
                    var acceptOutside = (flags & InteractionFlags.AllowDragOutside) != 0;
                    if ((flags & InteractionFlags.ClickOnPress) == 0 && (inRect || acceptOutside))
                        clicked = true;

                    ctx.ClearActiveId();
                    isActive = false;
                }
                else if ((flags & InteractionFlags.Repeat) != 0 && input.IsDown(MouseButton.Left) && inRect)
                {
                    clicked |= ShouldRepeat(ctx, id);
                }
            }
        }
        else if (isActive)
        {
            // 押下中に無効化された場合は操作を打ち切る
            ctx.ClearActiveId();
            isActive = false;
        }

        var held = isActive && input.IsDown(MouseButton.Left);

        // ── アニメーション値の更新 ──
        var dt = ctx.DeltaTime;
        ref var state = ref ctx.Store.GetRef(id);

        var hoverTarget = hovered ? 1f : 0f;
        var pressTarget = held && (inRect || (flags & InteractionFlags.AllowDragOutside) != 0) ? 1f : 0f;
        var disabledTarget = disabled ? 1f : 0f;

        state.Hover = Anim.Approach(state.Hover, hoverTarget, HoverSpeed, dt);
        state.Press = Anim.Approach(state.Press, pressTarget, PressSpeed, dt);
        state.Toggle = Anim.Approach(state.Toggle, disabledTarget, HoverSpeed, dt);

        return new InteractionResult
        {
            Id = id,
            Rect = rect,
            Hovered = hovered,
            Held = held,
            Pressed = pressed,
            Released = released,
            Clicked = clicked,
            DoubleClicked = doubleClicked,
            RightClicked = rightClicked,
            Disabled = disabled,
            HoverAmount = state.Hover,
            PressAmount = state.Press,
            DisabledAmount = state.Toggle,
            DragDelta = isActive ? input.DragDelta(MouseButton.Left) : System.Numerics.Vector2.Zero,
        };
    }

    /// <summary>
    /// マウスが矩形に乗っているかだけを調べる (状態を変更しない)。
    /// ツールチップの表示判定など、操作を伴わない用途に使う。
    /// </summary>
    public static bool IsHovered(Rect rect)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        return ctx.Input.HasMousePos && rect.Contains(ctx.Input.MousePos) && ctx.IsWindowHovered;
    }

    /// <summary>オートリピートの発火タイミングか判定する。</summary>
    private static bool ShouldRepeat(UiContext ctx, EuId id)
    {
        ref var state = ref ctx.Store.GetRef(id);
        var elapsed = ctx.Time - state.ActivatedAt;

        if (elapsed < RepeatDelay)
            return false;

        // 前回発火からの経過を Custom0 に積んでおく
        var sinceLast = ctx.Time - state.Custom0;
        if (state.Custom0 <= state.ActivatedAt || sinceLast >= RepeatRate)
        {
            state.Custom0 = ctx.Time;
            return true;
        }

        return false;
    }
}
