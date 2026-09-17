using System;

namespace EstellUtils.UI.Windowing;

/// <summary>
/// タイトルバーへ追加するボタン。
/// </summary>
/// <remarks>
/// 閉じる・畳む・小窓の標準ボタンの左側に、追加した順で並ぶ。
/// <code>
/// this.TitleBarButtons.Add(new TitleBarButton
/// {
///     Id = "settings",
///     Icon = FontAwesomeIcon.Cog.ToIconString(),
///     Tooltip = "設定を開く",
///     OnClick = () => this.plugin.OpenConfig(),
/// });
/// </code>
/// </remarks>
public sealed class TitleBarButton
{
    /// <summary>ボタンの識別子。他のボタンと重ならない名前にする。</summary>
    public required string Id { get; init; }

    /// <summary>
    /// アイコン文字。Dalamud の <c>FontAwesomeIcon.X.ToIconString()</c> を渡す。
    /// </summary>
    public required string Icon { get; init; }

    /// <summary>ホバー時に出す説明。</summary>
    public string? Tooltip { get; init; }

    /// <summary>押されたときの処理。</summary>
    public Action? OnClick { get; init; }

    /// <summary>ON 状態か。強調表示に使う。</summary>
    public Func<bool>? IsActive { get; init; }

    /// <summary>表示するか。条件付きで出し分けたいときに使う。</summary>
    public Func<bool>? IsVisible { get; init; }

    /// <summary>今このボタンを出すか。</summary>
    internal bool ShouldShow => this.IsVisible?.Invoke() ?? true;

    /// <summary>今このボタンが ON か。</summary>
    internal bool Active => this.IsActive?.Invoke() ?? false;
}
