using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;

namespace EstellUtils.UI;

/// <summary>
/// 生の ImGui と行き来するための API。
/// </summary>
public static partial class EUi
{
    /// <summary>同じフレームで開かれる生 ImGui スコープの ID。毎回作らずに使い回す。</summary>
    private static readonly string[] RawImGuiIds =
    [
        "##euRaw0", "##euRaw1", "##euRaw2", "##euRaw3",
        "##euRaw4", "##euRaw5", "##euRaw6", "##euRaw7",
    ];

    /// <summary>このフレームで開かれた生 ImGui スコープの数。</summary>
    private static int rawImGuiDepth;

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
    /// 既定では ImGui 側にも同じ大きさの領域 (<c>BeginChild</c>) を作ります。
    /// カーソルを合わせるだけでは、<c>GetContentRegionAvail</c> /
    /// <c>TextWrapped</c> / <c>SetNextItemWidth(-1)</c> / 幅 0 の表が
    /// EstellUtils の矩形ではなく ImGui ウィンドウの右端を基準にしてしまい、
    /// 右側がはみ出したり切れたりします。
    /// </para>
    /// <code>
    /// // 移行の途中、中身がまだ生 ImGui のとき
    /// public override void Draw()
    /// {
    ///     using var raw = EUi.RawImGui();
    ///
    ///     using var tabs = ImRaii.TabBar("##tabs");
    ///     // ...
    /// }
    /// </code>
    /// <code>
    /// // 一部分だけ混ぜるとき
    /// EUi.Heading("プレビュー");
    ///
    /// using (EUi.RawImGui(height: 120f))
    ///     ImGui.Image(handle, size);
    ///
    /// EUi.Muted("続きはここから");     // 正しい位置に出る
    /// </code>
    /// </remarks>
    /// <param name="width">使う幅。省略すると残り幅いっぱい。</param>
    /// <param name="height">
    /// 使う高さ。省略すると残り高さいっぱい。
    /// 一部分だけ混ぜる場合は、その部分に必要な高さを渡す。
    /// </param>
    /// <param name="child">
    /// ImGui 側にも領域を作るか。既定は true。
    /// false にするとカーソルと折り返し位置だけを合わせ、高さは実際に描いた分になる。
    /// </param>
    public static RawImGuiScope RawImGui(
        SizeSpec? width = null, float? height = null, bool child = true)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var area = ctx.Layout.AvailableRect;
        var resolvedWidth = MathF.Max(1f, width?.Resolve(area.Width) ?? area.Width);
        var resolvedHeight = MathF.Max(1f, height ?? area.Height);

        // ImGui のカーソルを、EstellUtils が次に置くはずの位置へ合わせる
        ImGui.SetCursorScreenPos(area.Min);

        if (!child)
        {
            // 領域を作らない場合でも、折り返し位置とクリップは合わせておく
            ImGui.PushTextWrapPos(area.Min.X + resolvedWidth);
            ImGui.PushClipRect(area.Min, new Vector2(area.Min.X + resolvedWidth, area.Max.Y), true);

            return new RawImGuiScope(area.Min, resolvedWidth, resolvedHeight, false, -1);
        }

        var slot = rawImGuiDepth;
        var id = slot < RawImGuiIds.Length
            ? RawImGuiIds[slot]
            : "##euRaw" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture);

        rawImGuiDepth++;

        // 枠も地も EstellUtils 側で描くので、ImGui の装飾は消しておく
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, 0u);

        ImGui.BeginChild(id, new Vector2(resolvedWidth, resolvedHeight), false);

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);

        return new RawImGuiScope(area.Min, resolvedWidth, resolvedHeight, true, slot);
    }

    /// <summary>生 ImGui スコープの深さを戻す。</summary>
    internal static void PopRawImGui(int slot)
    {
        if (slot >= 0)
            rawImGuiDepth = Math.Max(0, slot);
    }

    /// <summary>生 ImGui スコープの数え上げを初期化する。フレーム境界での保険。</summary>
    internal static void ResetRawImGuiDepth() => rawImGuiDepth = 0;

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
public readonly struct RawImGuiScope : IDisposable
{
    private readonly Vector2 origin;
    private readonly float width;
    private readonly float height;
    private readonly bool child;
    private readonly int slot;
    private readonly bool active;

    internal RawImGuiScope(Vector2 origin, float width, float height, bool child, int slot)
    {
        this.origin = origin;
        this.width = width;
        this.height = height;
        this.child = child;
        this.slot = slot;
        this.active = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.active)
            return;

        var ctx = UiContext.Current;

        if (this.child)
        {
            // BeginChild が false を返していても EndChild は必ず呼ぶ
            ImGui.EndChild();
            EUi.PopRawImGui(this.slot);

            ctx.Allocate(new Vector2(this.width, this.height));
            return;
        }

        ImGui.PopClipRect();
        ImGui.PopTextWrapPos();

        var end = ImGui.GetCursorScreenPos();
        var consumed = MathF.Max(0f, end.Y - this.origin.Y);

        // ImGui.SameLine() で終わると縦には進まない。
        // 横に進んでいれば 1 行分は使っているので、そこを確保する
        if (consumed <= 0f && end.X > this.origin.X)
            consumed = TextPainter.LineHeight;

        if (consumed > 0f)
            ctx.Allocate(new Vector2(this.width, consumed));
    }
}
