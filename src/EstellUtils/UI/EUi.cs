using System;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using EstellUtils.Diagnostics;
using EstellUtils.UI.Core;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Windowing;

namespace EstellUtils.UI;

/// <summary>
/// ライブラリの入口となる静的ファサード。
/// </summary>
/// <remarks>
/// <para>
/// 下層 (即時モード) の API はすべてここに集約される。上層の宣言的 API は
/// <c>EUi.Window(...)</c> から始まるビルダーを使う。
/// </para>
/// <para>
/// プラグイン起動時に <see cref="Initialize"/> を、終了時に <see cref="Shutdown"/> を呼ぶこと。
/// フォント生成に Dalamud のプラグインインターフェースが必要なため。
/// </para>
/// </remarks>
public static partial class EUi
{
    private static FontManager? fontManager;
    private static EuWindowManager? windowManager;
    private static Action? drawHandler;

    /// <summary>初期化済みか。</summary>
    public static bool IsInitialized => fontManager is not null;

    /// <summary>ウィンドウ管理。<see cref="Initialize"/> 後に使える。</summary>
    public static EuWindowManager Windows
        => windowManager ?? throw new InvalidOperationException(
            "EstellUtils が初期化されていません。プラグイン起動時に EUi.Initialize(pluginInterface) を呼んでください。");

    /// <summary>Dalamud のプラグインインターフェース。</summary>
    public static IDalamudPluginInterface? PluginInterface { get; private set; }

    /// <summary>現在有効なテーマ。</summary>
    public static Theme Theme => ThemeManager.Current;

    /// <summary>現在有効なテーマの色トークン。</summary>
    public static ThemeColors Colors => ThemeManager.Current.Colors;

    /// <summary>現在有効なテーマの寸法トークン。</summary>
    public static ThemeMetrics Metrics => ThemeManager.Current.Metrics;

    /// <summary>現在有効なテーマのモーショントークン。</summary>
    public static ThemeMotion Motion => ThemeManager.Current.Motion;

    /// <summary>フォント管理。<see cref="Initialize"/> 前に触ると例外になる。</summary>
    public static FontManager Fonts
        => fontManager ?? throw new InvalidOperationException(
            "EstellUtils が初期化されていません。プラグイン起動時に EUi.Initialize(pluginInterface) を呼んでください。");

    /// <summary>フレームの文脈。独自ウィジェットを書くときに使う。</summary>
    public static UiContext Context => UiContext.Current;

    /// <summary>入力スナップショット。</summary>
    public static InputState Input => UiContext.Current.Input;

    /// <summary>前フレームからの経過秒数。</summary>
    public static float DeltaTime => UiContext.Current.DeltaTime;

    /// <summary>
    /// ライブラリを初期化する。
    /// </summary>
    /// <param name="pluginInterface">Dalamud のプラグインインターフェース。</param>
    /// <param name="theme">既定テーマ。省略すると FFXIV ネイティブ風テーマを使う。</param>
    /// <param name="log">ライブラリ内部のログ出力先。省略するとログは捨てられる。</param>
    /// <param name="hookDraw">
    /// <c>UiBuilder.Draw</c> へウィンドウ描画を自動接続するか。
    /// false にした場合は、プラグイン側で毎フレーム <c>EUi.Windows.Draw()</c> を呼ぶこと。
    /// </param>
    /// <param name="keyState">
    /// ゲームのキー状態。<c>EUi.KeyBind</c> を使う場合に渡す。
    /// ImGui 経由では FFXIV 本体が先に処理してしまうキーを拾えないため、
    /// キー割り当ての判定にはこちらを使う。
    /// </param>
    public static void Initialize(
        IDalamudPluginInterface pluginInterface, Theme? theme = null,
        IPluginLog? log = null, bool hookDraw = true,
        Dalamud.Plugin.Services.IKeyState? keyState = null)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);

        Shutdown();

        PluginInterface = pluginInterface;
        UiLog.Sink = log;
        KeyState = keyState;

        fontManager = new FontManager(pluginInterface);
        windowManager = new EuWindowManager();

        if (theme is not null)
            ThemeManager.SetDefault(theme);

        if (hookDraw)
        {
            drawHandler = windowManager.Draw;
            pluginInterface.UiBuilder.Draw += drawHandler;
        }
    }

    /// <summary>
    /// ゲームのキー状態。<see cref="Initialize"/> で渡されていなければ null。
    /// </summary>
    /// <remarks>
    /// キー割り当ての判定に使う。ImGui 経由でキーを見ると、FFXIV 本体が先に
    /// 処理してしまうキーを拾えない。ゲームのキー状態を直接読むことでそれを避ける。
    /// </remarks>
    public static Dalamud.Plugin.Services.IKeyState? KeyState { get; private set; }

    /// <summary>ライブラリが確保した資源を解放する。プラグインの Dispose から呼ぶこと。</summary>
    public static void Shutdown()
    {
        if (drawHandler is not null && PluginInterface is not null)
            PluginInterface.UiBuilder.Draw -= drawHandler;

        drawHandler = null;

        windowManager?.Dispose();
        windowManager = null;

        fontManager?.Dispose();
        fontManager = null;

        UiLog.Sink = null;
        PluginInterface = null;
        KeyState = null;
    }

    /// <summary>既定テーマを差し替える。</summary>
    public static void SetTheme(Theme theme) => ThemeManager.SetDefault(theme);

    /// <summary>一時的に別テーマを適用するスコープを開く。</summary>
    public static ThemeScope PushTheme(Theme theme) => ThemeManager.Push(theme);

    /// <summary>フォントを適用するスコープを開く。</summary>
    public static FontScope PushFont(FontRole role) => Fonts.Push(role);

    /// <summary>ID スタックを積むスコープを開く。</summary>
    public static IdScope PushId(ReadOnlySpan<char> text) => UiContext.Current.ScopedId(text);

    /// <summary>ID スタックを積むスコープを開く。</summary>
    public static IdScope PushId(int value) => UiContext.Current.ScopedId(value);

    /// <summary>
    /// フレームを進める。通常は各 API が自動で呼ぶため、明示的に呼ぶ必要はない。
    /// </summary>
    public static void NewFrame() => UiContext.Current.EnsureFrame();

    private static int disabledDepth;

    /// <summary>まとめて無効化されている最中か。</summary>
    public static bool IsDisabled => disabledDepth > 0;

    /// <summary>
    /// このスコープの中のウィジェットをまとめて無効にする。
    /// </summary>
    /// <param name="disabled">無効にするか。false なら何もしない。</param>
    /// <remarks>
    /// 前提条件が揃わないときに、ひとまとまりの操作を丸ごと止めたい場面で使う。
    /// ウィジェットごとに <c>disabled:</c> を書いて回ると、条件が変わったときに
    /// 直し漏れが起きる。
    /// <code>
    /// using (EUi.Disabled(!this.config.OverlayEnabled))
    /// {
    ///     EUi.SliderInt("更新間隔", ref interval, 1, 6);
    ///     EUi.Checkbox("デバッグ表示", ref debug);
    /// }
    /// </code>
    /// </remarks>
    public static DisabledScope Disabled(bool disabled = true)
    {
        if (!disabled)
            return default;

        disabledDepth++;
        return new DisabledScope(true);
    }

    /// <summary>無効化スコープを 1 段戻す。</summary>
    internal static void PopDisabled()
    {
        if (disabledDepth > 0)
            disabledDepth--;
    }

    /// <summary>無効化スコープを空にする。フレーム境界での保険。</summary>
    internal static void ResetDisabled() => disabledDepth = 0;

    /// <summary>
    /// 直前に置いたウィジェットへツールチップを付ける。
    /// </summary>
    /// <remarks>
    /// 戻り値へ <c>.Tip()</c> をつなげられない場面 (戻り値を返さない自前のラッパーや、
    /// <c>using</c> を返す <see cref="Section(ReadOnlySpan{char}, bool, bool)"/> のあと) で使う。
    /// <code>
    /// using (var s = EUi.Section("試験機能"))
    /// {
    ///     EUi.Tip("動作が不安定になる場合があります。");
    ///     // ...
    /// }
    /// </code>
    /// </remarks>
    public static void Tip(ReadOnlySpan<char> text)
    {
        var ctx = UiContext.Current;

        if (!text.IsEmpty && ctx.LastItemHoveredDuration > 0f)
            Widgets.Tooltip.Show(text, ctx.LastItemHoveredDuration);
    }

    /// <summary>条件を満たすときだけ、直前のウィジェットへツールチップを付ける。</summary>
    public static void TipIf(bool condition, ReadOnlySpan<char> text)
    {
        if (condition)
            Tip(text);
    }
}

/// <summary>
/// <c>using</c> でまとめ無効化を解除するスコープ。
/// 既定値 (<c>default</c>) のスコープは何もしない。
/// </summary>
public readonly struct DisabledScope : IDisposable
{
    private readonly bool active;

    internal DisabledScope(bool active) => this.active = active;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.active)
            EUi.PopDisabled();
    }
}
