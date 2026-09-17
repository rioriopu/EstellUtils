using System;

using Dalamud.Plugin;

using EstellUtils.UI.Core;
using EstellUtils.UI.Theming;

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

    /// <summary>初期化済みか。</summary>
    public static bool IsInitialized => fontManager is not null;

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
    public static void Initialize(IDalamudPluginInterface pluginInterface, Theme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);

        PluginInterface = pluginInterface;
        fontManager?.Dispose();
        fontManager = new FontManager(pluginInterface);

        if (theme is not null)
            ThemeManager.SetDefault(theme);
    }

    /// <summary>ライブラリが確保した資源を解放する。プラグインの Dispose から呼ぶこと。</summary>
    public static void Shutdown()
    {
        fontManager?.Dispose();
        fontManager = null;
        PluginInterface = null;
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
}
