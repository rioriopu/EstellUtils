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
    private bool resizing;
    private Vector2 animatedSize;
    private bool sizeInitialized;

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

    /// <summary>
    /// タイトルバーだけに畳めるか。ボタンか、タイトルバーのダブルクリックで切り替わる。
    /// </summary>
    public bool Collapsible { get; set; } = true;

    /// <summary>畳まれているか。</summary>
    public bool IsCollapsed { get; set; }

    /// <summary>
    /// 小窓モードを持つか。true にすると、タイトルバーに小窓ボタンが出る。
    /// </summary>
    /// <remarks>
    /// 小窓モードは「設定画面をしまいつつ、よく使う操作だけ手元に残す」ための表示。
    /// <see cref="DrawCompact"/> をオーバーライドして中身を書く。
    /// </remarks>
    public bool HasCompactMode { get; set; }

    /// <summary>小窓モードになっているか。</summary>
    public bool IsCompact { get; set; }

    /// <summary>小窓モードのときの大きさ。</summary>
    public Vector2 CompactSize { get; set; } = new(260f, 140f);

    /// <summary>
    /// ウィジェットの無い余白をドラッグしても移動できるか。
    /// </summary>
    /// <remarks>
    /// タイトルバー以外を掴んでも動かせると、設定画面を寄せるのが楽になる。
    /// 掴めるのは、その位置に反応するウィジェットが無いときだけ。
    /// </remarks>
    public bool MoveFromAnywhere { get; set; } = true;

    /// <summary>画面の端に近づけたとき、そこへ吸い付かせるか。</summary>
    public bool SnapToScreenEdges { get; set; } = true;

    /// <summary>
    /// 開いている間、背後を暗く覆うか。設定画面を前面に集中させたいときに使う。
    /// </summary>
    /// <remarks>
    /// 背後をぼかす代わりの手段。ImGui は背後のピクセルを読めないため本物のぼかしは
    /// 描けないが、暗く落とすだけでも UI の見やすさは十分に上がる。
    /// </remarks>
    public bool DimBackground { get; set; }

    /// <summary>背後を覆う濃さ。<see cref="DimBackground"/> が true のときに使う。</summary>
    public float DimAmount { get; set; } = 0.45f;

    /// <summary>
    /// このウィンドウにフォーカスがある状態で Esc を押したら閉じるか。
    /// 文字入力中は反応しない。
    /// </summary>
    public bool CloseOnEscape { get; set; }

    /// <summary>内側の余白。省略するとテーマの既定値。</summary>
    public EdgeInsets? Padding { get; set; }

    /// <summary>このウィンドウだけで使うテーマ。省略すると既定テーマ。</summary>
    public Theme? Theme { get; set; }

    /// <summary>追加で指定する ImGui のウィンドウフラグ。</summary>
    public ImGuiWindowFlags ExtraFlags { get; set; } = ImGuiWindowFlags.None;

    /// <summary>ウィンドウの中身を描く。</summary>
    public abstract void Draw();

    /// <summary>
    /// 小窓モードのときの中身を描く。既定では通常の中身をそのまま描く。
    /// </summary>
    /// <remarks>
    /// よく使う操作だけを並べた、短い内容にするとよい。
    /// </remarks>
    public virtual void DrawCompact() => this.Draw();

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

        // 最小化・小窓への切り替えは大きさを滑らかに変える。
        // リサイズグリップを掴んでいる間は、追従が遅れないようアニメーションを止める
        var targetSize = this.ResolveTargetSize();

        if (!this.sizeInitialized)
        {
            this.animatedSize = targetSize;
            this.sizeInitialized = true;
        }
        else
        {
            var motion = ThemeManager.Current.Motion;
            var duration = motion.Enabled && !this.resizing ? motion.WindowResizeDuration : 0f;

            this.animatedSize = duration > 0f
                ? Anim.Approach(this.animatedSize, targetSize, 1f / duration, ctx.DeltaTime)
                : targetSize;
        }

        // 画面内へ収める判定は、実際に表示される大きさが決まってから行う
        this.ClampToViewport();

        if (this.DimBackground)
            EUi.Scrim(this.DimAmount);

        ImGui.SetNextWindowPos(this.Position);
        ImGui.SetNextWindowSize(this.animatedSize);

        if (!ImGui.Begin(this.imguiId, BaseFlags | this.ExtraFlags))
        {
            ImGui.End();
            return;
        }

        try
        {
            // ウィンドウ名で ID スコープを切る。これが無いと、複数のウィンドウで
            // 同じラベルのウィジェット (閉じるボタンや同名の項目) が同じ ID になってしまう
            using (ctx.ScopedId(this.imguiId))
            {
                this.RenderBody(ctx);
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    /// <summary>表示状態 (通常 / 小窓 / 最小化) に応じた目標の大きさを求める。</summary>
    private Vector2 ResolveTargetSize()
    {
        if (this.IsCollapsed && this.HasTitleBar)
            return new Vector2(this.IsCompact ? this.CompactSize.X : this.Size.X, this.TitleBarHeight());

        if (this.IsCompact)
            return this.CompactSize;

        return this.Size;
    }

    /// <summary>現在のテーマでのタイトルバーの高さ。</summary>
    private float TitleBarHeight()
        => this.HasTitleBar ? ThemeManager.Current.Metrics.TitleBarHeight : 0f;

    /// <summary>
    /// ウィンドウが画面外へ行きすぎないよう位置を丸める。
    /// タイトルバーが必ず画面内に残るので、掴み直せなくなることがない。
    /// </summary>
    private void ClampToViewport()
    {
        var viewport = ImGui.GetMainViewport();
        var min = viewport.WorkPos;
        var max = viewport.WorkPos + viewport.WorkSize;

        // 最低限これだけは画面内に残す
        const float KeepVisible = 80f;

        var titleHeight = this.HasTitleBar
            ? ThemeManager.Current.Metrics.TitleBarHeight
            : 24f;

        var position = this.Position;

        var width = this.sizeInitialized ? this.animatedSize.X : this.Size.X;

        position.X = Math.Clamp(position.X, min.X - width + KeepVisible, max.X - KeepVisible);
        position.Y = Math.Clamp(position.Y, min.Y, MathF.Max(min.Y, max.Y - titleHeight));

        this.Position = position;
    }

    /// <summary>クローム・操作・中身を描く。</summary>
    private void RenderBody(UiContext ctx)
    {
        var theme = ThemeManager.Current;
        var metrics = theme.Metrics;
        var painter = theme.Painter;

        // 見た目上の大きさはアニメーション中の値を使う
        var windowRect = Rect.FromSize(this.Position, this.animatedSize);
        var focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows);

        var titleHeight = this.HasTitleBar ? metrics.TitleBarHeight : 0f;
        var titleRect = this.HasTitleBar ? windowRect.WithHeight(titleHeight) : Rect.Zero;

        // タイトル文字を置ける範囲は、右側に並ぶボタンの分だけ狭める
        var buttonCount = (this.Closable ? 1 : 0) + (this.Collapsible ? 1 : 0) + (this.HasCompactMode ? 1 : 0);
        var titleTextArea = titleRect.Shrink(new EdgeInsets(
            metrics.SpacingMd, 0f, (buttonCount * titleHeight) + metrics.SpacingSm, 0f));

        // 影や枠がウィンドウ矩形の外へはみ出すので、クリップを画面全体へ広げる
        var drawList = ctx.DrawList;
        drawList.PushClipRectFullScreen();
        painter.DrawWindowChrome(windowRect, titleRect, titleTextArea, this.Name, focused);
        drawList.PopClipRect();

        if (this.HasTitleBar)
            this.HandleTitleBar(ctx, titleRect, painter);

        // 文字入力の最中は Esc が入力の取り消しに使われるので、そこでは反応させない
        if (this.CloseOnEscape && focused && !ImGui.GetIO().WantTextInput &&
            ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            this.IsOpen = false;
        }

        // 畳まれている途中は中身を描かない (タイトルバーからはみ出して見えてしまうため)
        var contentHeight = windowRect.Height - titleHeight;
        var showContent = contentHeight > 4f;

        if (this.Resizable && !this.IsCollapsed && !this.IsCompact)
            this.HandleResizeGrip(ctx, windowRect, painter, metrics.ResizeGripSize);

        if (showContent)
        {
            var padding = this.Padding ?? metrics.WindowPadding;
            var contentRect = new Rect(
                new Vector2(windowRect.Min.X, windowRect.Min.Y + titleHeight),
                windowRect.Max);

            // 大きさが変わっている最中は、中身がはみ出さないよう切り取る
            using var clip = Painter.Clip(contentRect);

            if (this.AutoScroll)
            {
                using (EUi.Region(contentRect, padding))
                using (ScrollArea.Begin("##euWindowScroll", contentRect.Height - padding.TotalVertical))
                {
                    this.DrawContent();
                }
            }
            else
            {
                using (EUi.Region(contentRect, padding))
                {
                    this.DrawContent();
                }
            }
        }

        // 中身を描いた後に、余白のドラッグで移動できるか判定する。
        // ここで判定するのは、どのウィジェットがホバーされたかが確定してからでないと
        // ウィジェットの上でもウィンドウが動いてしまうため
        if (this.Movable && this.MoveFromAnywhere && !this.IsCollapsed)
            this.HandleBackgroundDrag(ctx, windowRect, titleRect);
    }

    /// <summary>表示状態に応じて中身を描き分ける。</summary>
    private void DrawContent()
    {
        if (this.IsCompact)
            this.DrawCompact();
        else
            this.Draw();
    }

    /// <summary>
    /// ウィジェットの無い余白をドラッグしてウィンドウを動かす。
    /// </summary>
    private void HandleBackgroundDrag(UiContext ctx, Rect windowRect, Rect titleRect)
    {
        var dragId = ctx.GetId("##euWindowBackgroundDrag");
        var isDragging = ctx.ActiveId == dragId;

        // 何かのウィジェットが反応している場所では掴ませない
        if (!isDragging && !ctx.HotId.IsNone)
            return;

        // タイトルバーは専用の処理があるので除く
        var area = this.HasTitleBar
            ? new Rect(new Vector2(windowRect.Min.X, titleRect.Max.Y), windowRect.Max)
            : windowRect;

        if (area.IsEmpty)
            return;

        var drag = Interaction.Behavior(area, dragId, InteractionFlags.AllowDragOutside);

        if (drag.Pressed)
            ImGui.SetWindowFocus(this.imguiId);

        if (drag.Held)
            this.MoveBy(ctx.Input.MouseDelta, ctx.Input.MousePos);
    }

    /// <summary>タイトルバーのボタンとドラッグ移動を処理する。</summary>
    private void HandleTitleBar(UiContext ctx, Rect titleRect, IWidgetPainter painter)
    {
        var buttonArea = titleRect;
        var buttonSize = titleRect.Height;

        // 右から順に「閉じる」「最小化」「小窓」
        if (this.Closable)
        {
            var closeRect = buttonArea.CutRight(buttonSize, out buttonArea).Shrink(3f);
            var interaction = Interaction.Behavior(closeRect, ctx.GetId("##euWindowClose"));

            painter.DrawWindowButton(WidgetVisual.From(interaction), WindowButtonKind.Close);

            if (interaction.Clicked)
                this.IsOpen = false;
        }

        if (this.Collapsible)
        {
            var rect = buttonArea.CutRight(buttonSize, out buttonArea).Shrink(3f);
            var interaction = Interaction.Behavior(rect, ctx.GetId("##euWindowCollapse"));

            painter.DrawWindowButton(
                WidgetVisual.From(interaction, !this.IsCollapsed), WindowButtonKind.Collapse);

            if (interaction.Clicked)
                this.IsCollapsed = !this.IsCollapsed;

            if (interaction.Hovered)
                Tooltip.Show(this.IsCollapsed ? "元の大きさに戻す" : "タイトルバーだけに畳む", interaction.HoveredDuration);
        }

        if (this.HasCompactMode)
        {
            var rect = buttonArea.CutRight(buttonSize, out buttonArea).Shrink(3f);
            var interaction = Interaction.Behavior(rect, ctx.GetId("##euWindowCompact"));

            painter.DrawWindowButton(
                WidgetVisual.From(interaction, this.IsCompact), WindowButtonKind.Compact);

            if (interaction.Clicked)
            {
                this.IsCompact = !this.IsCompact;
                this.IsCollapsed = false;
            }

            if (interaction.Hovered)
                Tooltip.Show(this.IsCompact ? "通常の大きさに戻す" : "小窓にする", interaction.HoveredDuration);
        }

        if (!this.Movable)
            return;

        var dragId = ctx.GetId("##euWindowDrag");
        var drag = Interaction.Behavior(buttonArea, dragId, InteractionFlags.AllowDragOutside);

        // タイトルバーのダブルクリックで畳む (ウィンドウ操作としてよくある挙動)
        if (drag.DoubleClicked && this.Collapsible)
            this.IsCollapsed = !this.IsCollapsed;
        else if (drag.Held)
            this.MoveBy(ctx.Input.MouseDelta, ctx.Input.MousePos);

        // タイトルバーをクリックしたらウィンドウを手前へ持ってくる
        if (drag.Pressed)
            ImGui.SetWindowFocus(this.imguiId);
    }

    /// <summary>ウィンドウを動かす。画面端への吸着もここで行う。</summary>
    private void MoveBy(Vector2 delta, Vector2 mousePos)
    {
        this.Position += delta;

        if (this.SnapToScreenEdges)
            this.ApplyEdgeSnap();
    }

    /// <summary>
    /// 画面の端に近づいたら、そこへ吸い付かせる。
    /// </summary>
    /// <remarks>
    /// 吸着は見た目の位置だけを合わせるもので、マウスの移動量は常に反映している。
    /// そのまま動かし続ければ吸着から外れる。
    /// </remarks>
    private void ApplyEdgeSnap()
    {
        const float SnapDistance = 12f;

        var viewport = ImGui.GetMainViewport();
        var min = viewport.WorkPos;
        var max = viewport.WorkPos + viewport.WorkSize;
        var size = this.animatedSize;

        var position = this.Position;

        if (MathF.Abs(position.X - min.X) < SnapDistance)
            position.X = min.X;
        else if (MathF.Abs(position.X + size.X - max.X) < SnapDistance)
            position.X = max.X - size.X;

        if (MathF.Abs(position.Y - min.Y) < SnapDistance)
            position.Y = min.Y;
        else if (MathF.Abs(position.Y + size.Y - max.Y) < SnapDistance)
            position.Y = max.Y - size.Y;

        this.Position = position;
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

        this.resizing = interaction.Held;

        if (interaction.Held)
            this.Size = Vector2.Clamp(this.Size + ctx.Input.MouseDelta, this.MinSize, this.MaxSize);
    }
}
