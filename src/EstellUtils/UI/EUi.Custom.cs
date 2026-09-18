using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 独自ウィジェットを書くための入口。
/// </summary>
/// <remarks>
/// <para>
/// ライブラリ内のウィジェットも、ここで公開しているものと同じ部品だけで作られている。
/// 内部だけが使える近道は無いので、同じ土俵で好きなものを作れる。
/// </para>
/// <para>
/// 1 つのウィジェットは「領域を取る → 入力を判定する → 描く」の 3 段で書く。
/// 最初の 2 段を <c>EUi.Custom</c> がまとめて行い、描画だけが残る。
/// </para>
/// </remarks>
public static partial class EUi
{
    /// <summary>
    /// 領域を確保し、入力を判定して返す。描画だけを自分で書けばよい。
    /// <code>
    /// var w = EUi.Custom("myKnob", SizeSpec.Px(80f), 80f);
    ///
    /// // 好きに描く。ホバーや押下の遷移量は w.Visual が持っている
    /// Painter.Circle(w.Rect.Center, 36f,
    ///     EuColor.Lerp(EUi.Colors.Surface, EUi.Colors.Accent, w.Visual.Hover));
    ///
    /// if (w.Result.Clicked)
    ///     this.value++;
    /// </code>
    /// </summary>
    /// <param name="id">識別子。同じ階層で重複しなければよい。</param>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="flags">入力の扱い方。ドラッグを見たい場合などに指定する。</param>
    /// <param name="disabled">無効にするか。外側の <see cref="Disabled"/> とも合成される。</param>
    public static CustomWidget Custom(
        ReadOnlySpan<char> id, SizeSpec width, float height,
        InteractionFlags flags = InteractionFlags.None, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (disabled || IsDisabled)
            flags |= InteractionFlags.Disabled;

        var euId = ctx.GetId(id);
        var rect = ctx.Allocate(width, height);
        var interaction = Interaction.Behavior(rect, euId, flags);

        return new CustomWidget
        {
            Id = euId,
            Rect = rect,
            Visual = WidgetVisual.From(interaction) with { Rect = rect },
            Result = WidgetResult.From(interaction),
        };
    }

    /// <summary>大きさを <see cref="Vector2"/> で指定する版。</summary>
    public static CustomWidget Custom(
        ReadOnlySpan<char> id, Vector2 size,
        InteractionFlags flags = InteractionFlags.None, bool disabled = false)
        => Custom(id, SizeSpec.Px(size.X), size.Y, flags, disabled);

    /// <summary>
    /// 場所を決めずに、矩形だけを指定して入力を判定する。
    /// すでに確保した領域の一部を当たり判定にしたいときに使う。
    /// </summary>
    /// <remarks>
    /// レイアウトは進めない。スライダーのつまみのように、
    /// 1 つのウィジェットの中で複数の当たり判定を持たせる場合に使う。
    /// </remarks>
    public static CustomWidget CustomAt(
        ReadOnlySpan<char> id, Rect rect,
        InteractionFlags flags = InteractionFlags.None, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (disabled || IsDisabled)
            flags |= InteractionFlags.Disabled;

        var euId = ctx.GetId(id);
        var interaction = Interaction.Behavior(rect, euId, flags);

        return new CustomWidget
        {
            Id = euId,
            Rect = rect,
            Visual = WidgetVisual.From(interaction) with { Rect = rect },
            Result = WidgetResult.From(interaction),
        };
    }

    /// <summary>
    /// ウィジェットがフレームをまたいで覚えておく値。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 独自ウィジェットで開閉状態やアニメーションの進み具合を持たせたいときに使う。
    /// <c>Custom0</c> / <c>Custom1</c> が自由に使える枠。
    /// </para>
    /// <para>
    /// しばらく使われなかった状態は自動で捨てられるので、後始末は要らない。
    /// </para>
    /// <code>
    /// var w = EUi.Custom("spinner", SizeSpec.Px(24f), 24f);
    /// ref var state = ref EUi.State(w.Id);
    ///
    /// state.Custom0 += EUi.DeltaTime;   // 回転角として使う
    /// </code>
    /// </remarks>
    public static ref WidgetState State(EuId id) => ref UiContext.Current.Store.GetRef(id);
}

/// <summary>
/// <c>EUi.Custom</c> が返す、独自ウィジェットの土台。
/// </summary>
/// <remarks>
/// <c>bool</c> と <see cref="WidgetResult"/> への暗黙変換があるので、
/// <c>if (EUi.Custom(...)) { ... }</c> や、そのまま <c>return</c> して
/// 呼び出し側へ返すことができる。
/// </remarks>
public readonly record struct CustomWidget
{
    /// <summary>このウィジェットの ID。状態の保持や子 ID の生成に使う。</summary>
    public EuId Id { get; init; }

    /// <summary>確保した矩形。</summary>
    public Rect Rect { get; init; }

    /// <summary>
    /// 描画に必要な状態。ホバー・押下・無効の遷移量が入っている。
    /// <c>EUi.WidgetPainter.DrawButton(w.Visual, ...)</c> のように、
    /// 既存の見た目をそのまま借りることもできる。
    /// </summary>
    public WidgetVisual Visual { get; init; }

    /// <summary>入力の結果。そのまま呼び出し側へ返せる。</summary>
    public WidgetResult Result { get; init; }

    /// <summary>クリックされた、または値が変わった。</summary>
    public static implicit operator bool(CustomWidget widget) => widget.Result;

    /// <summary>ウィジェットの戻り値として返せるようにする。</summary>
    public static implicit operator WidgetResult(CustomWidget widget) => widget.Result;
}
