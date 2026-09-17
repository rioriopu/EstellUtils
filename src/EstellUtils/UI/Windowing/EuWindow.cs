using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI.Windowing;

/// <summary>
/// 独自に描くウィンドウ。
/// </summary>
/// <remarks>
/// <para>
/// ImGui のウィンドウ装飾 (タイトルバー・枠・スクロールバー・リサイズグリップ) は
/// すべて無効化し、透明な箱としてだけ使う。位置・大きさ・移動・リサイズも自前で持つため、
/// 見た目と操作感を完全に制御できる。ImGui に任せているのは、フォーカスと重なり順、
/// クリッピング、入力の受け取りだけ。
/// </para>
/// <para>
/// 位置と大きさは <see cref="Position"/> / <see cref="Size"/> で読み書きできる。
/// ImGui の ini には保存されないので、覚えておきたい場合はプラグインの設定へ保存すること。
/// </para>
/// </remarks>
public abstract class EuWindow
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoBackground |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.NoMove |
        ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoSavedSettings;

    private readonly string imguiId;

    private bool wasOpen;
    private bool placed;

    /// <summary>ウィンドウを作る。</summary>
    /// <param name="name">タイトルバーに表示する名前。ID にも使われる。</param>
    protected EuWindow(string name)
    {
        this.Name = name;
        this.imguiId = $"###EstellUtils_{name}";
    }

    /// <summary>タイトル。</summary>
    public string Name { get; set; }

    /// <summary>開いているか。</summary>
    public bool IsOpen { get; set; }

    /// <summary>左上の座標 (画面座標)。</summary>
    public Vector2 Position { get; set; }

    /// <summary>大きさ。</summary>
    public Vector2 Size { get; set; } = new(440f, 340f);

    /// <summary>最小の大きさ。</summary>
    public Vector2 MinSize { get; set; } = new(220f, 120f);

    /// <summary>最大の大きさ。</summary>
    public Vector2 MaxSize { get; set; } = new(4000f, 3000f);

    /// <summary>タイトルバーを表示するか。</summary>
    public bool HasTitleBar { get; set; } = true;

    /// <summary>タイトルバーのドラッグで移動できるか。</summary>
    public bool Movable { get; set; } = true;

    /// <summary>右下を掴んで大きさを変えられるか。</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>閉じるボタンを表示するか。</summary>
    public bool Closable { get; set; } = true;

    /// <summary>内容がはみ出したときに自動でスクロールさせるか。</summary>
    public bool AutoScroll { get; set; } = true;

    /// <summary>内側の余白。省略するとテーマの既定値。</summary>
    public EdgeInsets? Padding { get; set; }

    /// <summary>このウィンドウだけで使うテーマ。省略すると既定テーマ。</summary>
    public Theme? Theme { get; set; }

    /// <summary>追加で指定する ImGui のウィンドウフラグ。</summary>
    public ImGuiWindowFlags ExtraFlags { get; set; } = ImGuiWindowFlags.None;

    /// <summary>ウィンドウの中身を描く。</summary>
    public abstract void Draw();

    /// <summary>描画するかどうかの判定。戦闘中だけ出す、といった制御に使う。</summary>
    public virtual bool DrawConditions() => true;

    /// <summary>開かれたときに呼ばれる。</summary>
    public virtual void OnOpen()
    {
    }

    /// <summary>閉じられたときに呼ばれる。</summary>
    public virtual void OnClose()
    {
    }

    /// <summary>描画の直前に呼ばれる。大きさの調整などに使う。</summary>
    public virtual void PreDraw()
    {
    }

    /// <summary>開閉を切り替える。</summary>
    public void Toggle() => this.IsOpen = !this.IsOpen;

    /// <summary>
    /// 位置を確定済みとして扱い、初回の自動中央寄せを行わないようにする。
    /// 設定から読み込んだ位置を使う場合などに呼ぶ。
    /// </summary>
    protected void MarkPlaced() => this.placed = true;

    /// <summary>画面の中央へ移動する。</summary>
    public void CenterOnScreen()
    {
        var viewport = ImGui.GetMainViewport();
        this.Position = viewport.WorkPos + ((viewport.WorkSize - this.Size) * 0.5f);
        this.placed = true;
    }

    /// <summary>ウィンドウを 1 フレーム分描く。ウィンドウ管理から呼ばれる。</summary>
    internal void Render()
    {
        // 開閉の変化を通知する
        if (this.IsOpen != this.wasOpen)
        {
            this.wasOpen = this.IsOpen;

            if (this.IsOpen)
                this.OnOpen();
            else
                this.OnClose();
        }

        if (!this.IsOpen || !this.DrawConditions())
            return;

        this.PreDraw();

        if (!this.placed)
            this.CenterOnScreen();

        this.Size = Vector2.Clamp(this.Size, this.MinSize, this.MaxSize);

        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        using var themeScope = this.Theme is not null
            ? ThemeManager.Push(this.Theme)
            : default;

        ImGui.SetNextWindowPos(this.Position);
        ImGui.SetNextWindowSize(this.Size);

        if (!ImGui.Begin(this.imguiId, BaseFlags | this.ExtraFlags))
        {
            ImGui.End();
            return;
        }

        try
        {
            this.RenderBody(ctx);
        }
        finally
        {
            ImGui.End();
        }
    }

    /// <summary>クローム・操作・中身を描く。</summary>
    private void RenderBody(UiContext ctx)
    {
        var theme = ThemeManager.Current;
        var metrics = theme.Metrics;
        var painter = theme.Painter;

        var windowRect = Rect.FromSize(this.Position, this.Size);
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);

        var titleHeight = this.HasTitleBar ? metrics.TitleBarHeight : 0f;
        var titleRect = this.HasTitleBar ? windowRect.WithHeight(titleHeight) : Rect.Zero;

        // 影や枠がウィンドウ矩形の外へはみ出すので、クリップを画面全体へ広げる
        var drawList = ctx.DrawList;
        drawList.PushClipRectFullScreen();
        painter.DrawWindowChrome(windowRect, titleRect, this.Name, focused);
        drawList.PopClipRect();

        if (this.HasTitleBar)
            this.HandleTitleBar(ctx, titleRect, painter);

        if (this.Resizable)
            this.HandleResizeGrip(ctx, windowRect, painter, metrics.ResizeGripSize);

        // 中身の領域
        var padding = this.Padding ?? metrics.WindowPadding;
        var contentRect = new Rect(
            new Vector2(windowRect.Min.X, windowRect.Min.Y + titleHeight),
            windowRect.Max);

        if (this.AutoScroll)
        {
            using (EUi.Region(contentRect, padding))
            using (ScrollArea.Begin("##euWindowScroll", contentRect.Height - padding.TotalVertical))
            {
                this.Draw();
            }
        }
        else
        {
            using (EUi.Region(contentRect, padding))
            {
                this.Draw();
            }
        }
    }

    /// <summary>タイトルバーのドラッグ移動と閉じるボタンを処理する。</summary>
    private void HandleTitleBar(UiContext ctx, Rect titleRect, IWidgetPainter painter)
    {
        var buttonArea = titleRect;

        if (this.Closable)
        {
            var buttonSize = titleRect.Height;
            var closeRect = titleRect.CutRight(buttonSize, out buttonArea).Shrink(3f);

            var closeId = ctx.GetId("##euWindowClose");
            var interaction = Interaction.Behavior(closeRect, closeId);

            painter.DrawWindowButton(WidgetVisual.From(interaction), WindowButtonKind.Close);

            if (interaction.Clicked)
                this.IsOpen = false;
        }

        if (!this.Movable)
            return;

        var dragId = ctx.GetId("##euWindowDrag");
        var drag = Interaction.Behavior(buttonArea, dragId, InteractionFlags.AllowDragOutside);

        if (drag.Held)
            this.Position += ctx.Input.MouseDelta;

        // タイトルバーをクリックしたらウィンドウを手前へ持ってくる
        if (drag.Pressed)
            ImGui.SetWindowFocus(this.imguiId);
    }

    /// <summary>右下のリサイズグリップを処理する。</summary>
    private void HandleResizeGrip(UiContext ctx, Rect windowRect, IWidgetPainter painter, float gripSize)
    {
        var gripRect = Rect.FromSize(
            new Vector2(windowRect.Max.X - gripSize, windowRect.Max.Y - gripSize),
            new Vector2(gripSize, gripSize));

        var gripId = ctx.GetId("##euWindowGrip");
        var interaction = Interaction.Behavior(gripRect, gripId, InteractionFlags.AllowDragOutside);

        painter.DrawResizeGrip(WidgetVisual.From(interaction));

        if (interaction.Held)
            this.Size = Vector2.Clamp(this.Size + ctx.Input.MouseDelta, this.MinSize, this.MaxSize);
    }
}
