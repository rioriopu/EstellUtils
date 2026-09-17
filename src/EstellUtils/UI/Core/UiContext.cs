using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace EstellUtils.UI.Core;

/// <summary>
/// フレーム全体で共有される UI の文脈。入力・ウィジェット状態・ID スタック・
/// ホバー/操作中のウィジェットを保持する。
/// </summary>
/// <remarks>
/// <para>
/// 利用側が明示的にフレーム開始を宣言しなくてよいように、<see cref="EnsureFrame"/> が
/// ImGui のフレーム番号を見て自動的に 1 フレーム 1 回だけ更新処理を走らせる。
/// ライブラリの公開 API はすべて冒頭でこれを呼ぶ。
/// </para>
/// <para>
/// ImGui への依存は「描画面 (<see cref="DrawList"/>)・入力・クリッピング・Z 順」に限定してある。
/// <c>ImGui.Button</c> のような標準ウィジェットは一切使わない。
/// </para>
/// </remarks>
public sealed class UiContext
{
    private static readonly Lazy<UiContext> Instance = new(() => new UiContext());

    private readonly List<ulong> idStack = new(16) { 0UL };

    private int lastImGuiFrame = -1;
    private bool activeIdIsAlive;

    private UiContext()
    {
        this.Input = new InputState();
        this.Store = new WidgetStore();
    }

    /// <summary>プロセス内で共有される唯一の文脈。</summary>
    public static UiContext Current => Instance.Value;

    /// <summary>入力スナップショット。</summary>
    public InputState Input { get; }

    /// <summary>ウィジェットの永続状態。</summary>
    public WidgetStore Store { get; }

    /// <summary>ライブラリが数えたフレーム番号。</summary>
    public uint FrameCount { get; private set; }

    /// <summary>前フレームからの経過秒数。</summary>
    public float DeltaTime => this.Input.DeltaTime;

    /// <summary>累計経過秒数。</summary>
    public float Time => this.Input.Time;

    /// <summary>マウスが乗っているウィジェット。重なっている場合は後に描かれた方 (手前) が勝つ。</summary>
    public EuId HotId { get; internal set; }

    /// <summary>操作中 (押下・ドラッグ中) のウィジェット。</summary>
    public EuId ActiveId { get; private set; }

    /// <summary>キーボード入力を受け取っているウィジェット (テキスト入力など)。</summary>
    public EuId FocusedId { get; private set; }

    /// <summary>このフレームで何らかのウィジェットがマウス入力を消費したか。</summary>
    public bool WantCaptureMouse { get; internal set; }

    /// <summary>現在のウィンドウの描画リスト。ウィジェットはここへ直接描画する。</summary>
    /// <remarks>
    /// 子ウィンドウごとに別の描画リストになるため、キャッシュせず都度取得する。
    /// </remarks>
    public ImDrawListPtr DrawList => ImGui.GetWindowDrawList();

    /// <summary>ID スタックの現在値。<see cref="EuId"/> 生成時のシードになる。</summary>
    public ulong IdSeed => this.idStack[^1];

    // ── フレーム管理 ──────────────────────────────────────────

    /// <summary>
    /// 必要ならフレームを進める。ImGui のフレーム番号を基準にするため、
    /// 1 フレーム中に何度呼んでも実処理は 1 回だけ走る。
    /// </summary>
    public void EnsureFrame()
    {
        var imguiFrame = ImGui.GetFrameCount();
        if (imguiFrame == this.lastImGuiFrame)
            return;

        this.lastImGuiFrame = imguiFrame;
        this.BeginFrame();
    }

    private void BeginFrame()
    {
        this.FrameCount++;
        this.Input.NewFrame();
        this.Store.NewFrame(this.FrameCount);

        // ID スタックが閉じ忘れで残っていたら戻す (例外で PopId が飛ばされた場合の保険)
        if (this.idStack.Count > 1)
            this.idStack.RemoveRange(1, this.idStack.Count - 1);

        // テーマスコープも同様に、閉じ忘れをフレーム境界で回収する
        Theming.ThemeManager.ResetStack();

        // 操作中のウィジェットが前フレームに描かれなかった (タブ切替などで消えた) 場合は解放する
        if (!this.activeIdIsAlive && !this.ActiveId.IsNone)
            this.ActiveId = EuId.None;

        this.activeIdIsAlive = false;
        this.HotId = EuId.None;
        this.WantCaptureMouse = false;
    }

    /// <summary>ウィジェットを操作中にする。</summary>
    internal void SetActiveId(EuId id)
    {
        this.ActiveId = id;
        this.activeIdIsAlive = true;

        if (!id.IsNone)
        {
            ref var state = ref this.Store.GetRef(id);
            state.ActivatedAt = this.Time;
        }
    }

    /// <summary>操作中のウィジェットが今フレームも生存していることを伝える。</summary>
    internal void KeepActiveIdAlive(EuId id)
    {
        if (this.ActiveId == id)
            this.activeIdIsAlive = true;
    }

    /// <summary>操作中の状態を解除する。</summary>
    internal void ClearActiveId()
    {
        this.ActiveId = EuId.None;
        this.activeIdIsAlive = false;
    }

    /// <summary>キーボードフォーカスを移す。</summary>
    public void SetFocus(EuId id) => this.FocusedId = id;

    /// <summary>キーボードフォーカスを外す。</summary>
    public void ClearFocus() => this.FocusedId = EuId.None;

    // ── ID スタック ───────────────────────────────────────────

    /// <summary>ID スタックへ積む。同じラベルのウィジェットを区別できるようになる。</summary>
    public void PushId(ReadOnlySpan<char> text) => this.idStack.Add(EuId.Hash(text, this.IdSeed));

    /// <summary>ID スタックへ積む (ループの添字など)。</summary>
    public void PushId(int value) => this.idStack.Add(EuId.Hash(value, this.IdSeed));

    /// <summary>ID スタックから降ろす。</summary>
    public void PopId()
    {
        if (this.idStack.Count > 1)
            this.idStack.RemoveAt(this.idStack.Count - 1);
    }

    /// <summary><c>using</c> で自動的に降ろせる ID スコープ。</summary>
    public IdScope ScopedId(ReadOnlySpan<char> text)
    {
        this.PushId(text);
        return new IdScope(this);
    }

    /// <summary><c>using</c> で自動的に降ろせる ID スコープ。</summary>
    public IdScope ScopedId(int value)
    {
        this.PushId(value);
        return new IdScope(this);
    }

    /// <summary>現在の ID スタックをシードにして ID を作る。</summary>
    public EuId GetId(ReadOnlySpan<char> text) => EuId.From(text, this.IdSeed);

    /// <summary>ラベルから ID と表示文字列を取り出す。</summary>
    public EuId GetId(ReadOnlySpan<char> label, out ReadOnlySpan<char> display)
        => EuId.FromLabel(label, this.IdSeed, out display);

    // ── 領域の確保 ────────────────────────────────────────────

    /// <summary>
    /// 現在のカーソル位置に指定サイズの領域を確保し、その矩形を返す。
    /// </summary>
    /// <remarks>
    /// <c>ImGui.Dummy</c> で ImGui 側のカーソルも進めるため、同じフレーム内で
    /// 生の <c>ImGui.*</c> 呼び出しと混在させても配置が崩れない。
    /// レイアウトコンテナ (フェーズ 4) はこの処理を差し替える形で拡張される。
    /// </remarks>
    public Rect Allocate(Vector2 size)
    {
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        return Rect.FromSize(origin, size);
    }

    /// <summary>横幅いっぱいに指定高さの領域を確保する。</summary>
    public Rect AllocateFullWidth(float height)
        => this.Allocate(new Vector2(this.AvailableWidth, height));

    /// <summary>現在のカーソル位置から右端までの利用可能幅。</summary>
    public float AvailableWidth => ImGui.GetContentRegionAvail().X;

    /// <summary>現在のカーソル位置から下端までの利用可能高さ。</summary>
    public float AvailableHeight => ImGui.GetContentRegionAvail().Y;

    /// <summary>現在のカーソル位置 (画面座標)。</summary>
    public Vector2 CursorScreenPos
    {
        get => ImGui.GetCursorScreenPos();
        set => ImGui.SetCursorScreenPos(value);
    }

    /// <summary>現在のウィンドウ全体の矩形 (画面座標)。</summary>
    public Rect WindowRect => Rect.FromSize(ImGui.GetWindowPos(), ImGui.GetWindowSize());

    /// <summary>
    /// マウスが現在のウィンドウ上にあるか。ウィンドウの重なりは ImGui が解決する。
    /// </summary>
    public bool IsWindowHovered
        => ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
}

/// <summary>
/// <c>using</c> で ID スタックを自動的に戻すためのスコープ。
/// </summary>
public readonly struct IdScope : IDisposable
{
    private readonly UiContext context;

    internal IdScope(UiContext context) => this.context = context;

    /// <inheritdoc/>
    public void Dispose() => this.context.PopId();
}
