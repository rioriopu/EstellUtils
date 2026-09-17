using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;

namespace EstellUtils.UI;

/// <summary>
/// 生の ImGui と行き来するための API。
/// </summary>
public static partial class EUi
{
    /// <summary>
    /// 生の <c>ImGui.*</c> を書くためのスコープを開く。
    /// </summary>
    /// <remarks>
    /// <para>
    /// EstellUtils のレイアウトは独自のカーソルで位置を決めるため、その途中で
    /// 生の ImGui を呼んでも、ImGui 側のカーソルは前の位置のままです。
    /// ウィンドウの中では見えない場所へ描かれてしまい、何も出ていないように見えます。
    /// </para>
    /// <para>
    /// このスコープは、開くときに ImGui のカーソルを「次に置かれるはずの位置」へ合わせ、
    /// 閉じるときに ImGui が進めた分だけレイアウトを進めます。
    /// <c>BeginChild</c> や <c>Columns</c> のように ImGui の仕組みへ依存した部分を、
    /// そのまま残したまま移行できます。
    /// </para>
    /// <code>
    /// EUi.Heading("プレビュー");
    ///
    /// using (EUi.RawImGui())
    /// {
    ///     ImGui.BeginChild("preview", new Vector2(0, 120), true);
    ///     ImGui.Image(handle, size);
    ///     ImGui.EndChild();
    /// }
    ///
    /// EUi.Muted("続きはここから");     // 正しい位置に出る
    /// </code>
    /// </remarks>
    /// <param name="width">
    /// 使う幅。省略すると残り幅いっぱい。ImGui へ渡す幅を揃えたいときに指定する。
    /// </param>
    public static RawImGuiScope RawImGui(SizeSpec? width = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var area = ctx.Layout.AvailableRect;
        var resolved = width?.Resolve(area.Width) ?? area.Width;

        // ImGui のカーソルを、EstellUtils が次に置くはずの位置へ合わせる
        ImGui.SetCursorScreenPos(area.Min);

        return new RawImGuiScope(area.Min, resolved);
    }

    /// <summary>
    /// クリップボードへ文字列を書き込む。
    /// </summary>
    public static void SetClipboard(string text) => ImGui.SetClipboardText(text ?? string.Empty);

    /// <summary>
    /// クリップボードの文字列を読み取る。取得できなければ空文字。
    /// </summary>
    public static string GetClipboard()
    {
        try
        {
            return ImGui.GetClipboardText().ToString() ?? string.Empty;
        }
        catch
        {
            // クリップボードは他のアプリに掴まれていると読めないことがある
            return string.Empty;
        }
    }
}

/// <summary>
/// <c>using</c> で生 ImGui のスコープを閉じるハンドル。
/// </summary>
/// <remarks>
/// 閉じるときに、ImGui のカーソルがどこまで進んだかを見て、
/// その分だけ EstellUtils のレイアウトを進める。
/// </remarks>
public readonly struct RawImGuiScope : IDisposable
{
    private readonly Vector2 origin;
    private readonly float width;
    private readonly bool active;

    internal RawImGuiScope(Vector2 origin, float width)
    {
        this.origin = origin;
        this.width = width;
        this.active = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.active)
            return;

        var ctx = UiContext.Current;
        var end = ImGui.GetCursorScreenPos();
        var consumed = MathF.Max(0f, end.Y - this.origin.Y);

        if (consumed > 0f)
            ctx.Allocate(new Vector2(this.width, consumed));
    }
}
