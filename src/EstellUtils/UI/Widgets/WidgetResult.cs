using System;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Widgets;

/// <summary>
/// ウィジェットの戻り値。
/// </summary>
/// <remarks>
/// <para>
/// <c>bool</c> への暗黙変換があるので <c>if (EUi.Button("OK")) { ... }</c> と書ける。
/// この変換は「クリックされた、または値が変わった」を意味する。
/// </para>
/// <para>
/// <see cref="Tip"/> などをチェーンして、ウィジェットの後から装飾を足せる。
/// </para>
/// </remarks>
public readonly record struct WidgetResult
{
    /// <summary>ウィジェット ID。</summary>
    public EuId Id { get; init; }

    /// <summary>ウィジェットが占める矩形。</summary>
    public Rect Rect { get; init; }

    /// <summary>クリックされたか。</summary>
    public bool Clicked { get; init; }

    /// <summary>値が変わったか。設定の保存判定に使う。</summary>
    public bool Changed { get; init; }

    /// <summary>マウスが乗っているか。</summary>
    public bool Hovered { get; init; }

    /// <summary>押下中か。</summary>
    public bool Held { get; init; }

    /// <summary>このフレームで操作が始まったか。</summary>
    public bool Activated { get; init; }

    /// <summary>
    /// このフレームで操作が終わったか。ドラッグ終了時にだけ保存したい場合に使う。
    /// </summary>
    /// <remarks>
    /// マウスのボタンを離したことを指す。文字入力の「編集が終わった」ではないので、
    /// 入力欄の確定を拾うには <see cref="Committed"/> を使う。
    /// </remarks>
    public bool Deactivated { get; init; }

    /// <summary>
    /// 入力の編集が終わり、値が変わったか。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 焦点が外れたとき、または Enter を押したときに、実際に書き換わっていれば立つ。
    /// ImGui の <c>IsItemDeactivatedAfterEdit()</c> と同じ意味。
    /// </para>
    /// <para>
    /// ファイルパスや URL のように、1 文字ごとに処理すると困る欄で使う。
    /// <c>Changed</c> は打つたびに立つので、そのまま保存や検証に繋ぐと無駄が多い。
    /// </para>
    /// <code>
    /// if (EUi.TextInput("保存先", ref path).Committed)
    ///     this.config.Save();
    /// </code>
    /// </remarks>
    public bool Committed { get; init; }

    /// <summary>ダブルクリックされたか。</summary>
    public bool DoubleClicked { get; init; }

    /// <summary>右クリックされたか。</summary>
    public bool RightClicked { get; init; }

    /// <summary>無効状態か。</summary>
    public bool Disabled { get; init; }

    /// <summary>ホバーが続いている秒数。</summary>
    public float HoveredDuration { get; init; }

    /// <summary>
    /// 文字が幅に収まらず、省略記号で切られたか。
    /// </summary>
    /// <remarks>
    /// 切られたことに気づけるよう、値として返している。
    /// 表示だけの問題なのか、渡した文字列がそもそも違うのかを切り分けられる。
    /// </remarks>
    public bool Truncated { get; init; }

    /// <summary>クリックされた、または値が変わった。</summary>
    public static implicit operator bool(WidgetResult result) => result.Clicked || result.Changed;

    /// <summary>
    /// 直前のウィジェットにツールチップを付ける。ホバーが一定時間続くと表示される。
    /// </summary>
    public WidgetResult Tip(ReadOnlySpan<char> text)
    {
        if (this.Hovered && !text.IsEmpty)
            Tooltip.Show(text, this.HoveredDuration);

        return this;
    }

    /// <summary>
    /// 条件を満たすときだけツールチップを付ける。
    /// </summary>
    public WidgetResult TipIf(bool condition, ReadOnlySpan<char> text)
        => condition ? this.Tip(text) : this;

    /// <summary>
    /// 入力結果からウィジェットの戻り値を作る。独自ウィジェットを書くときに使う。
    /// </summary>
    public static WidgetResult From(in InteractionResult interaction, bool changed = false)
        => new()
        {
            Id = interaction.Id,
            Rect = interaction.Rect,
            Clicked = interaction.Clicked,
            Changed = changed,
            Hovered = interaction.Hovered,
            Held = interaction.Held,
            Activated = interaction.Pressed,
            Deactivated = interaction.Released,
            DoubleClicked = interaction.DoubleClicked,
            RightClicked = interaction.RightClicked,
            Disabled = interaction.Disabled,
            HoveredDuration = interaction.HoveredDuration,
        };
}
