using System.Numerics;

namespace EstellUtils.UI.Windowing;

/// <summary>
/// 本体のウィンドウに紐づく小窓。
/// </summary>
/// <remarks>
/// <para>
/// 本体とは独立したウィンドウとして開き、位置も大きさも別に持つ。
/// 中身は本体の <see cref="EuWindow.DrawCompact"/> が描く。
/// </para>
/// <para>
/// 設定画面をしまったまま、よく使う操作だけ画面の隅へ置いておく、といった使い方を想定している。
/// タイトルバーの「元へ戻す」ボタンで本体へ戻れる。
/// </para>
/// </remarks>
internal sealed class EuCompanionWindow : EuWindow
{
    private readonly EuWindow owner;

    internal EuCompanionWindow(EuWindow owner)
        : base(owner.Name + "##companion")
    {
        this.owner = owner;

        this.Size = owner.CompanionSize;
        this.MinSize = new Vector2(150f, 60f);
        this.HasCompanion = false;
        this.Collapsible = true;
        this.Closable = true;
        this.AutoScroll = true;
    }

    /// <summary>タイトルは本体の名前をそのまま使う。</summary>
    public override string GetTitle() => this.owner.Name;

    /// <inheritdoc/>
    protected internal override bool ShowRestoreButton => true;

    /// <inheritdoc/>
    protected internal override void OnRestore()
    {
        this.owner.IsOpen = true;
        this.IsOpen = false;
    }

    /// <inheritdoc/>
    public override void Draw() => this.owner.DrawCompact();

    /// <inheritdoc/>
    public override bool DrawConditions() => this.owner.DrawConditions();
}
