using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// ポップアップ・メニュー・確認ダイアログ。
/// </summary>
/// <remarks>
/// <para>
/// 重なり順・入力の横取り・「外側をクリックで閉じる」だけを ImGui のポップアップに任せ、
/// 中身の描画と判定はすべて自前で行う。ウィンドウと同じ考え方。
/// </para>
/// <para>
/// 大きさは呼び出し側が決める。即時モードでは中身を描き終えるまで高さが分からず、
/// 前フレームの実測に頼るとメニューが開いた瞬間にちらつくため。
/// メニューと確認ダイアログは、内容から大きさを自分で計算する。
/// </para>
/// </remarks>
public static partial class EUi
{
    /// <summary>ポップアップの箱に使う ImGui のフラグ。装飾はすべて自前で描く。</summary>
    private const ImGuiWindowFlags PopupWindowFlags =
        ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings;

    /// <summary>メニューの区切り線が占める高さ。</summary>
    private const float MenuSeparatorHeight = 9f;

    /// <summary>メニューのチェック欄の幅。</summary>
    private const float MenuCheckWidth = 20f;

    /// <summary>確認ダイアログの幅。</summary>
    private const float ConfirmWidth = 380f;

    /// <summary>ポップアップを開く。</summary>
    /// <param name="id">識別子。<see cref="Popup"/> などへ渡すものと同じ文字列を使う。</param>
    /// <remarks>
    /// 開く操作と中身の描画は別々に書く。ボタンの中で開き、
    /// 描画は同じ階層のどこかで毎フレーム呼ぶ、という形になる。
    /// </remarks>
    public static void OpenPopup(string id) => ImGui.OpenPopup(id);

    /// <summary>ポップアップが開いているか。</summary>
    public static bool IsPopupOpen(string id) => ImGui.IsPopupOpen(id);

    /// <summary>開いているポップアップを閉じる。ポップアップの中から呼ぶ。</summary>
    public static void ClosePopup() => ImGui.CloseCurrentPopup();

    /// <summary>
    /// 中身を自由に書けるポップアップ。
    /// <code>
    /// if (EUi.Button("詳細")) EUi.OpenPopup("detail");
    ///
    /// using (var p = EUi.Popup("detail", new Vector2(260f, 120f)))
    /// {
    ///     if (p.IsOpen)
    ///     {
    ///         EUi.Heading("詳細");
    ///         EUi.Paragraph("好きなものを置ける。");
    ///     }
    /// }
    /// </code>
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="size">大きさ。</param>
    /// <param name="anchor">どこを基準に置くか。</param>
    /// <param name="padding">内側の余白。</param>
    public static PopupScope Popup(
        string id, Vector2 size,
        PopupAnchor anchor = PopupAnchor.BelowLastItem, EdgeInsets? padding = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var position = ResolveAnchor(ctx, anchor, size);

        // 位置は開いた時点で決めて固定する。毎フレーム置き直すと、
        // マウスの位置を基準にしたときにメニューが指へ追従してしまう
        ImGui.SetNextWindowPos(position, ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(size);

        if (!BeginPopupBox(id))
            return default;

        // ImGui が画面内へ収め直すことがあるので、実際の位置を取り直す
        var rect = Rect.FromSize(ImGui.GetWindowPos(), ImGui.GetWindowSize());

        // 親ウィンドウのクリップを持ち込まない。ポップアップは親の外へも出るため、
        // 引き継ぐと地や中身が切り取られる
        var clip = Painter.ClipFullScreen();
        DrawPopupSurface(rect);

        var region = Region(rect, padding ?? EdgeInsets.All(Metrics.SpacingSm));
        return new PopupScope(region, clip, rect);
    }

    /// <summary>
    /// メニューを描く。開いていなければ何もしない。
    /// </summary>
    /// <param name="id">識別子。<see cref="OpenPopup"/> で開く。</param>
    /// <param name="anchor">どこを基準に置くか。</param>
    /// <param name="entries">項目。文字列をそのまま渡せる。</param>
    /// <returns>選ばれた項目の添字。選ばれなければ -1。</returns>
    /// <remarks>
    /// 添字は渡した並びのままなので、区切り線もひとつ分を数える。
    /// <code>
    /// switch (EUi.Menu("fileMenu", PopupAnchor.BelowLastItem,
    ///                  "開く", "保存", MenuEntry.Separator,
    ///                  new MenuEntry("削除") { Kind = NoteKind.Danger }))
    /// {
    ///     case 0: Open(); break;
    ///     case 1: Save(); break;
    ///     case 3: Delete(); break;
    /// }
    /// </code>
    /// </remarks>
    public static int Menu(string id, PopupAnchor anchor, params ReadOnlySpan<MenuEntry> entries)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var padding = Metrics.SpacingXs;
        var size = MeasureMenu(entries, padding, out var itemHeight, out var hasCheckColumn);
        var position = ResolveAnchor(ctx, anchor, size);

        // 開いた時点の位置に固定する。毎フレーム置き直すとマウスへ追従してしまう
        ImGui.SetNextWindowPos(position, ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(size);

        if (!BeginPopupBox(id))
            return -1;

        var rect = Rect.FromSize(ImGui.GetWindowPos(), ImGui.GetWindowSize());

        using var clip = Painter.ClipFullScreen();
        DrawPopupSurface(rect);

        var chosen = -1;
        var euId = ctx.GetId(id);

        using (Region(rect, EdgeInsets.All(padding), 0f))
        {
            for (var i = 0; i < entries.Length; i++)
            {
                if (DrawMenuEntry(euId.Child(i), entries[i], itemHeight, hasCheckColumn))
                {
                    chosen = i;
                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.EndPopup();
        return chosen;
    }

    /// <summary>直前のウィジェットの下にメニューを描く。</summary>
    public static int Menu(string id, params ReadOnlySpan<MenuEntry> entries)
        => Menu(id, PopupAnchor.BelowLastItem, entries);

    /// <summary>
    /// 右クリックで開くメニュー。
    /// <code>
    /// var row = EUi.Selectable(item.Name, item == selected);
    /// switch (EUi.ContextMenu("rowMenu", row, "コピー", "削除"))
    /// {
    ///     case 0: Copy(item); break;
    ///     case 1: Delete(item); break;
    /// }
    /// </code>
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="target">右クリックを見るウィジェットの戻り値。</param>
    /// <param name="entries">項目。</param>
    /// <returns>選ばれた項目の添字。選ばれなければ -1。</returns>
    /// <remarks>
    /// 一覧の各行に付ける場合は、行ごとに違う <paramref name="id"/> を渡すか、
    /// <c>EUi.PushId(index)</c> の中で呼ぶ。同じ id を使い回すと、
    /// どの行で開いたのか区別できなくなる。
    /// </remarks>
    public static int ContextMenu(string id, in WidgetResult target, params ReadOnlySpan<MenuEntry> entries)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        // RightClicked は InteractionFlags.AllowRightClick を指定したウィジェットしか立てない。
        // どのウィジェットにも後付けできるよう、乗っているかどうかから自前でも判定する
        var opened = target.RightClicked
            || (target.Hovered && !target.Disabled && ctx.Input.IsPressed(MouseButton.Right));

        if (opened)
            ImGui.OpenPopup(id);

        return Menu(id, PopupAnchor.MousePosition, entries);
    }

    /// <summary>
    /// 確認ダイアログ。開いていなければ <see cref="ConfirmResult.None"/> を返す。
    /// <code>
    /// if (EUi.Button("初期化", ButtonStyle.Danger))
    ///     EUi.OpenPopup("confirmReset");
    ///
    /// if (EUi.Confirm("confirmReset", "設定の初期化",
    ///                 "すべての設定を既定値へ戻します。この操作は元に戻せません。",
    ///                 "初期化する", danger: true) == ConfirmResult.Ok)
    /// {
    ///     ResetAll();
    /// }
    /// </code>
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="title">見出し。</param>
    /// <param name="message">本文。</param>
    /// <param name="okLabel">実行ボタンの文字。省略すると「OK」。</param>
    /// <param name="cancelLabel">取り消しボタンの文字。省略すると「キャンセル」。</param>
    /// <param name="danger">実行ボタンを危険な操作の見た目にするか。</param>
    /// <param name="dimBackground">背後を暗く覆うか。既定は覆わない。</param>
    /// <remarks>
    /// 外側をクリックしても閉じない。Esc で取り消しになる。
    /// </remarks>
    public static ConfirmResult Confirm(
        string id, ReadOnlySpan<char> title, ReadOnlySpan<char> message,
        ReadOnlySpan<char> okLabel = default, ReadOnlySpan<char> cancelLabel = default,
        bool danger = false, bool dimBackground = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (!ImGui.IsPopupOpen(id))
            return ConfirmResult.None;

        var padding = Metrics.CardPadding;
        var lineHeight = TextPainter.LineHeight;
        var textWidth = ConfirmWidth - padding.TotalHorizontal;

        var messageHeight = message.IsEmpty ? 0f : TextPainter.Measure(message, textWidth).Y;
        var height =
            padding.TotalVertical
            + (title.IsEmpty ? 0f : lineHeight + Metrics.SpacingSm)
            + messageHeight
            + Metrics.SpacingLg
            + Metrics.WidgetHeight;

        var size = new Vector2(ConfirmWidth, height);

        ImGui.SetNextWindowPos(ResolveAnchor(ctx, PopupAnchor.ScreenCenter, size));
        ImGui.SetNextWindowSize(size);

        // モーダルの暗幕は ImGui が描く。既定では透明にして、ウィンドウの手前に
        // 出ていることだけで伝える
        if (!dimBackground)
            ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, 0u);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        var open = true;
        var began = ImGui.BeginPopupModal(id, ref open, PopupWindowFlags);

        ImGui.PopStyleVar();

        if (!dimBackground)
            ImGui.PopStyleColor();

        if (!began)
            return ConfirmResult.None;

        var rect = Rect.FromSize(ImGui.GetWindowPos(), ImGui.GetWindowSize());

        using var clip = Painter.ClipFullScreen();
        DrawPopupSurface(rect, strong: true);

        var result = ConfirmResult.None;

        using (Region(rect, padding))
        {
            if (!title.IsEmpty)
            {
                Label(title, Colors.TextHeading);
                Spacing(Metrics.SpacingSm);
            }

            if (!message.IsEmpty)
                Paragraph(message);

            Spacing(Metrics.SpacingLg);

            // ボタンは右寄せ。取り消しを左、実行を右に置く
            var cancelText = cancelLabel.IsEmpty ? "キャンセル" : cancelLabel;
            var okText = okLabel.IsEmpty ? "OK" : okLabel;

            var cancelWidth = ButtonWidthFor(cancelText);
            var okWidth = ButtonWidthFor(okText);

            // 3 列なので隙間は 2 つ。引き忘れるとボタンが枠からはみ出す
            var spacer = MathF.Max(
                0f, AvailableWidth - cancelWidth - okWidth - ColumnSpacing(3));

            using (Row(SizeSpec.Px(spacer), SizeSpec.Px(cancelWidth), SizeSpec.Px(okWidth)))
            {
                Spacing(0f);

                if (Button(cancelText, ButtonStyle.Normal, SizeSpec.Fill))
                    result = ConfirmResult.Cancel;

                if (Button(okText, danger ? ButtonStyle.Danger : ButtonStyle.Primary, SizeSpec.Fill))
                    result = ConfirmResult.Ok;
            }
        }

        // Esc と、タイトルバー相当の閉じる操作は取り消し扱い
        if (result == ConfirmResult.None && (!open || ctx.Input.IsKeyPressed(ImGuiKey.Escape)))
            result = ConfirmResult.Cancel;

        if (result != ConfirmResult.None)
            ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
        return result;
    }

    /// <summary>
    /// ポップアップの箱を開く。開けたら true。
    /// </summary>
    /// <remarks>
    /// ImGui の余白を 0 にしてから開く。余白が残っていると、ImGui はその分だけ
    /// 内側にクリップ範囲を張るため、ウィンドウの端いっぱいに描いた地と枠が
    /// 切り取られてしまう。内側の余白は自前のレイアウトで取る。
    /// </remarks>
    private static bool BeginPopupBox(string id)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        var began = ImGui.BeginPopup(id, PopupWindowFlags);
        ImGui.PopStyleVar();

        return began;
    }

    /// <summary>ポップアップの地と枠を描く。</summary>
    /// <param name="rect">ポップアップ全体の矩形。</param>
    /// <param name="strong">確認ダイアログのように、より前に出して見せるか。</param>
    private static void DrawPopupSurface(Rect rect, bool strong = false)
    {
        var rounding = Metrics.CardRounding;

        Painter.Shadow(
            rect, Colors.Shadow, strong ? 18f : 10f, rounding,
            new Vector2(0f, strong ? 6f : 3f));

        Painter.Rect(rect, strong ? Colors.Surface : Colors.TooltipBackground, rounding);
        Painter.RectOutline(rect, strong ? Colors.SurfaceBorder : Colors.TooltipBorder, 1f, rounding);
    }

    /// <summary>基準の位置から、実際に置く座標を求める。</summary>
    private static Vector2 ResolveAnchor(UiContext ctx, PopupAnchor anchor, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();

        var position = anchor switch
        {
            PopupAnchor.MousePosition => ctx.Input.MousePos,
            PopupAnchor.ScreenCenter => viewport.WorkPos + ((viewport.WorkSize - size) * 0.5f),
            PopupAnchor.AboveLastItem => new Vector2(
                ctx.LastItemRect.Min.X, ctx.LastItemRect.Min.Y - size.Y - 2f),
            _ => new Vector2(ctx.LastItemRect.Min.X, ctx.LastItemRect.Max.Y + 2f),
        };

        // 画面の外へはみ出すと読めなくなるので、作業領域へ収める
        var min = viewport.WorkPos;
        var max = viewport.WorkPos + viewport.WorkSize;

        return new Vector2(
            Math.Clamp(position.X, min.X, MathF.Max(min.X, max.X - size.X)),
            Math.Clamp(position.Y, min.Y, MathF.Max(min.Y, max.Y - size.Y)));
    }

    /// <summary>メニュー全体の大きさを求める。</summary>
    private static Vector2 MeasureMenu(
        ReadOnlySpan<MenuEntry> entries, float padding,
        out float itemHeight, out bool hasCheckColumn)
    {
        itemHeight = Metrics.WidgetHeight;
        hasCheckColumn = false;

        var labelWidth = 0f;
        var shortcutWidth = 0f;
        var height = 0f;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];

            if (entry.IsSeparator)
            {
                height += MenuSeparatorHeight;
                continue;
            }

            height += itemHeight;
            hasCheckColumn |= entry.Checked;

            labelWidth = MathF.Max(labelWidth, TextPainter.Measure(entry.Label).X);

            if (!string.IsNullOrEmpty(entry.Shortcut))
                shortcutWidth = MathF.Max(shortcutWidth, TextPainter.Measure(entry.Shortcut).X);
        }

        var width =
            labelWidth
            + (shortcutWidth > 0f ? shortcutWidth + Metrics.SpacingXl : 0f)
            + (hasCheckColumn ? MenuCheckWidth : 0f)
            + (Metrics.WidgetPadding.TotalHorizontal * 2f)
            + (padding * 2f);

        return new Vector2(
            MathF.Ceiling(MathF.Max(width, Metrics.WidgetMinWidth)),
            MathF.Ceiling(height + (padding * 2f)));
    }

    /// <summary>メニュー項目を 1 つ描く。押されたら true。</summary>
    private static bool DrawMenuEntry(EuId id, in MenuEntry entry, float itemHeight, bool hasCheckColumn)
    {
        var ctx = UiContext.Current;

        if (entry.IsSeparator)
        {
            var line = ctx.Allocate(SizeSpec.Fill, MenuSeparatorHeight);
            var y = MathF.Round(line.Center.Y);

            Painter.Line(
                new Vector2(line.Min.X + Metrics.SpacingXs, y),
                new Vector2(line.Max.X - Metrics.SpacingXs, y),
                Colors.Separator,
                Metrics.SeparatorThickness);

            return false;
        }

        var rect = ctx.Allocate(SizeSpec.Fill, itemHeight);
        var disabled = entry.Disabled || IsDisabled;

        var interaction = Interaction.Behavior(
            rect, id, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var accent = entry.Kind is { } kind ? NoteColor(kind) : Colors.Text;

        if (!disabled && interaction.HoverAmount > 0.01f)
        {
            Painter.Rect(
                rect,
                EuColor.WithAlpha(
                    entry.Kind is null ? Colors.SurfaceHover : accent,
                    interaction.HoverAmount * (entry.Kind is null ? 0.9f : 0.22f)),
                Metrics.WidgetRounding);
        }

        var inner = rect.Shrink(Metrics.WidgetPadding);
        var color = disabled ? Colors.TextDisabled : accent;

        if (hasCheckColumn)
        {
            var checkArea = inner.CutLeft(MenuCheckWidth, out inner);

            if (entry.Checked)
            {
                using (PushFont(FontRole.Icon))
                {
                    TextPainter.TextIn(
                        checkArea, color, FontAwesomeIcon.Check.ToIconString(),
                        Align.Start, Align.Center, ellipsize: false);
                }
            }
        }

        if (!string.IsNullOrEmpty(entry.Shortcut))
        {
            var shortcutArea = inner.CutRight(
                TextPainter.Measure(entry.Shortcut).X + Metrics.SpacingMd, out inner);

            TextPainter.TextIn(
                shortcutArea, Colors.TextDisabled, entry.Shortcut,
                Align.End, Align.Center, ellipsize: false);
        }

        TextPainter.TextIn(inner, color, entry.Label, Align.Start, Align.Center);

        return interaction.Clicked && !disabled;
    }

    /// <summary>文字に合わせたボタンの幅。</summary>
    private static float ButtonWidthFor(ReadOnlySpan<char> label)
        => MathF.Max(
            Metrics.WidgetMinWidth,
            MathF.Ceiling(TextPainter.Measure(label).X) + (Metrics.WidgetPadding.TotalHorizontal * 2f));
}

/// <summary>ポップアップを置く基準。</summary>
public enum PopupAnchor
{
    /// <summary>直前のウィジェットのすぐ下。ドロップダウン向き。</summary>
    BelowLastItem,

    /// <summary>直前のウィジェットのすぐ上。</summary>
    AboveLastItem,

    /// <summary>マウスの位置。右クリックメニュー向き。</summary>
    MousePosition,

    /// <summary>画面の中央。確認ダイアログ向き。</summary>
    ScreenCenter,
}

/// <summary>確認ダイアログの結果。</summary>
public enum ConfirmResult
{
    /// <summary>まだ選ばれていない。開いていない場合もこれ。</summary>
    None,

    /// <summary>実行が選ばれた。</summary>
    Ok,

    /// <summary>取り消された。</summary>
    Cancel,
}

/// <summary>メニューの項目 1 つ分。</summary>
/// <param name="Label">表示する文字。</param>
/// <remarks>
/// 文字列からの暗黙変換があるので、単純な項目は文字列のまま渡せる。
/// <code>
/// EUi.Menu("m", "コピー", "貼り付け", MenuEntry.Separator,
///          new MenuEntry("削除") { Kind = NoteKind.Danger, Shortcut = "Del" });
/// </code>
/// </remarks>
public readonly record struct MenuEntry(string Label)
{
    /// <summary>区切り線。押せない。</summary>
    public static MenuEntry Separator { get; } = new(string.Empty) { IsSeparator = true };

    /// <summary>押せなくするか。</summary>
    public bool Disabled { get; init; }

    /// <summary>チェックを付けるか。1 つでもあると、全体にチェック欄が空く。</summary>
    public bool Checked { get; init; }

    /// <summary>右端に薄く出すショートカットの表示。動作は割り当てない。</summary>
    public string? Shortcut { get; init; }

    /// <summary>区切り線か。</summary>
    public bool IsSeparator { get; init; }

    /// <summary>状態色。危険な操作を赤く見せたいときに使う。</summary>
    public NoteKind? Kind { get; init; }

    /// <summary>文字列をそのまま項目にする。</summary>
    public static implicit operator MenuEntry(string label) => new(label);
}

/// <summary><c>using</c> でポップアップを閉じるハンドル。</summary>
public readonly struct PopupScope : IDisposable
{
    private readonly LayoutHandle region;
    private readonly ClipScope clip;
    private readonly bool open;

    internal PopupScope(LayoutHandle region, ClipScope clip, Rect rect)
    {
        this.region = region;
        this.clip = clip;
        this.Rect = rect;
        this.open = true;
    }

    /// <summary>開いているか。中身はこれが true のときだけ描く。</summary>
    public bool IsOpen => this.open;

    /// <summary>ポップアップ全体の矩形。</summary>
    public Rect Rect { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.open)
            return;

        this.region.Dispose();
        this.clip.Dispose();
        ImGui.EndPopup();
    }
}
