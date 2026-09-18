using System;
using System.Collections.Generic;

using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace EstellUtils.UI.Theming;

/// <summary>フォントの役割。</summary>
public enum FontRole
{
    /// <summary>Dalamud の既定フォント。</summary>
    Default,

    /// <summary>補足表示用の小さいフォント。</summary>
    Small,

    /// <summary>本文用。ゲームフォント (Axis) を使う。</summary>
    Body,

    /// <summary>見出し用の大きめフォント。</summary>
    Large,

    /// <summary>ウィンドウタイトル用。</summary>
    Title,

    /// <summary>等幅フォント。数値やログの整列に使う。</summary>
    Mono,

    /// <summary>アイコンフォント (FontAwesome)。</summary>
    Icon,
}

/// <summary>
/// フォントハンドルの生成と使い回しを担う。
/// </summary>
/// <remarks>
/// ゲームフォント (Axis) は日本語字形を含み、FFXIV のネイティブ UI と同じ見た目になる。
/// Dalamud の既定フォント・等幅・アイコンは <c>UiBuilder</c> が持つハンドルをそのまま使う
/// (自前で作ると二重にアトラスを構築することになるため)。
/// </remarks>
public sealed class FontManager : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly Dictionary<FontRole, IFontHandle> gameFonts = new();
    private readonly object gate = new();

    private bool disposed;

    /// <summary>フォント管理を初期化する。</summary>
    public FontManager(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
    }

    /// <summary>
    /// ゲームフォント (Axis) を使うか。false にすると本文系も Dalamud の既定フォントになる。
    /// </summary>
    public bool UseGameFont { get; set; } = true;

    /// <summary>役割ごとのフォントの大きさ (ピクセル)。</summary>
    public float SmallSize { get; set; } = 12f;

    /// <summary>本文の大きさ。</summary>
    public float BodySize { get; set; } = 14f;

    /// <summary>見出しの大きさ。</summary>
    public float LargeSize { get; set; } = 18f;

    /// <summary>タイトルの大きさ。</summary>
    public float TitleSize { get; set; } = 16f;

    /// <summary>役割に対応するフォントハンドルを取得する。</summary>
    public IFontHandle Get(FontRole role)
    {
        var ui = this.pluginInterface.UiBuilder;

        switch (role)
        {
            case FontRole.Icon:
                return ui.IconFontHandle;

            case FontRole.Mono:
                return ui.MonoFontHandle;

            case FontRole.Default:
                return ui.DefaultFontHandle;

            default:
                return this.UseGameFont ? this.GetGameFont(role) : ui.DefaultFontHandle;
        }
    }

    /// <summary>
    /// フォントを適用するスコープを開く。<c>using</c> で抜けると元へ戻る。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ゲームフォントのアトラスは非同期に構築される。まだ構築できていないハンドルを
    /// <c>Push</c> すると、Dalamud は ASCII しか持たない代替フォントを積むため、
    /// 日本語がすべて <c>?</c> になる。ゲーム起動直後の数フレームがこれに当たる。
    /// </para>
    /// <para>
    /// 構築が済むまでは Dalamud の既定フォントへ退避する。こちらは日本語字形を持つので、
    /// 字体が一瞬変わるだけで読めなくなることはない。
    /// </para>
    /// </remarks>
    public FontScope Push(FontRole role)
    {
        var handle = this.Get(role);

        if (handle is { Available: true })
            return new FontScope(handle.Push());

        var fallback = this.pluginInterface.UiBuilder.DefaultFontHandle;

        // 既定フォントも間に合っていなければ何も積まない。
        // Dalamud が描画前に積んでいるフォントがそのまま使われる
        return fallback is { Available: true }
            ? new FontScope(fallback.Push())
            : default;
    }

    /// <summary>
    /// 役割に対応するフォントが使える状態か。
    /// </summary>
    /// <remarks>
    /// 起動直後は false になることがある。描画を遅らせたい場合の判定に使う。
    /// </remarks>
    public bool IsReady(FontRole role) => this.Get(role) is { Available: true };

    /// <summary>
    /// 日本語を含む文字を正しく描ける状態か。
    /// </summary>
    /// <remarks>
    /// <para>
    /// フォントのアトラスが構築できるまでは、ASCII しか持たない代替フォントしか無い。
    /// その状態で描くと日本語がすべて <c>?</c> になるため、
    /// ウィンドウの描画はこれが true になるまで待つ。
    /// </para>
    /// <para>
    /// 参照した時点でゲームフォントの構築が始まるので、早めに呼ぶほど待ち時間は短くなる。
    /// </para>
    /// </remarks>
    public bool IsTextReady
    {
        get
        {
            if (this.pluginInterface.UiBuilder.DefaultFontHandle is not { Available: true })
                return false;

            return !this.UseGameFont || this.GetGameFont(FontRole.Body) is { Available: true };
        }
    }

    /// <summary>ゲームフォントを必要に応じて生成して返す。</summary>
    private IFontHandle GetGameFont(FontRole role)
    {
        lock (this.gate)
        {
            if (this.gameFonts.TryGetValue(role, out var cached))
                return cached;

            var size = role switch
            {
                FontRole.Small => this.SmallSize,
                FontRole.Large => this.LargeSize,
                FontRole.Title => this.TitleSize,
                _ => this.BodySize,
            };

            var handle = this.pluginInterface.UiBuilder.FontAtlas
                .NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, size));

            this.gameFonts[role] = handle;
            return handle;
        }
    }

    /// <summary>生成したゲームフォントを破棄する。</summary>
    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;

        lock (this.gate)
        {
            foreach (var handle in this.gameFonts.Values)
            {
                try
                {
                    handle.Dispose();
                }
                catch
                {
                    // 破棄時の失敗は無視する (プラグイン終了処理を止めないため)
                }
            }

            this.gameFonts.Clear();
        }

        // フォントが変わると計測結果も変わるので、キャッシュを捨てる
        Render.TextPainter.ClearMeasureCache();
    }
}

/// <summary><c>using</c> でフォントを元に戻すスコープ。</summary>
public readonly struct FontScope : IDisposable
{
    private readonly IDisposable? handle;

    internal FontScope(IDisposable? handle) => this.handle = handle;

    /// <inheritdoc/>
    public void Dispose() => this.handle?.Dispose();
}
