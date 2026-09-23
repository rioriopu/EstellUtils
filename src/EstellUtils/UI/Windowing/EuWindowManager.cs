using System;
using System.Collections.Generic;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Windowing;

/// <summary>
/// ウィンドウの集合を管理し、毎フレーム描画する。
/// </summary>
/// <remarks>
/// Dalamud の <c>WindowSystem</c> は使わず、同等の役割を自前で持つ。
/// <see cref="EUi.Initialize"/> で <c>UiBuilder.Draw</c> へ自動的に接続される。
/// </remarks>
public sealed class EuWindowManager : IDisposable
{
    private readonly List<EuWindow> windows = new();
    private readonly List<EuWindow> drawBuffer = new();

    /// <summary>フォントの構築を待つ上限 (秒)。これを過ぎたら諦めて描く。</summary>
    private const float FontWaitLimit = 5f;

    private EuWindowLayout? layout;
    private Action? layoutSave;
    private bool disposed;
    private float fontWait;

    /// <summary>プラグイン一覧のボタンから外すための後始末。</summary>
    private readonly List<Action> openers = new(2);

    /// <summary>管理しているウィンドウの数。</summary>
    public int Count => this.windows.Count;

    /// <summary>管理しているウィンドウ。</summary>
    public IReadOnlyList<EuWindow> Windows => this.windows;

    /// <summary>ウィンドウを追加する。</summary>
    /// <param name="window">追加するウィンドウ。</param>
    /// <param name="mainUi">
    /// プラグイン一覧の「開く」からこのウィンドウを開くか。
    /// Dalamud はこれが無いプラグインを検査で指摘する。
    /// </param>
    /// <param name="configUi">歯車ボタンからこのウィンドウを開くか。</param>
    /// <remarks>
    /// <paramref name="mainUi"/> / <paramref name="configUi"/> は開くだけで、
    /// 開閉の切り替えはしない。すでに開いていれば手前へ出す。
    /// 解除は <c>EUi.Shutdown()</c> が面倒を見る。
    /// </remarks>
    public void Add(EuWindow window, bool mainUi = false, bool configUi = false)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!this.windows.Contains(window))
        {
            // 名前は ImGui の ID と保存キーの両方に使う。
            // 同名だと入力も位置の記憶も混ざるが、黙っていると原因に辿り着けない
            if (this.Find(window.Name) is not null)
            {
                EstellUtils.Diagnostics.UiLog.Warning(
                    $"ウィンドウ名「{window.Name}」が重複しています。" +
                    "入力の判定と、位置・大きさの記憶が混ざります。名前を変えてください。");
            }

            this.windows.Add(window);
            this.AttachState(window);
        }

        if (mainUi)
            this.BindOpener(window, main: true);

        if (configUi)
            this.BindOpener(window, main: false);
    }

    /// <summary>プラグイン一覧のボタンへウィンドウを繋ぐ。</summary>
    private void BindOpener(EuWindow window, bool main)
    {
        var ui = EUi.PluginInterface?.UiBuilder;

        if (ui is null)
        {
            EstellUtils.Diagnostics.UiLog.Warning(
                "EUi.Initialize より前に Add が呼ばれたため、プラグイン一覧のボタンへ繋げませんでした。");
            return;
        }

        // 一覧のボタンは「開く」であって「切り替え」ではない。
        // 押すたびに閉じると、見えていないところで消えたように感じる
        void Open()
        {
            window.IsOpen = true;
            window.BringToFront();
        }

        if (main)
        {
            ui.OpenMainUi += Open;
            this.openers.Add(() => ui.OpenMainUi -= Open);
        }
        else
        {
            ui.OpenConfigUi += Open;
            this.openers.Add(() => ui.OpenConfigUi -= Open);
        }
    }

    /// <summary>
    /// ウィンドウの位置と大きさを、まとめて保存・復元するようにする。
    /// </summary>
    /// <param name="windowLayout">
    /// 状態の入れ物。プラグインの設定クラスへ持たせて保存する。
    /// </param>
    /// <param name="save">
    /// 保存処理。ウィンドウを動かし終えた・大きさを変え終えた時点で呼ばれる。
    /// </param>
    /// <remarks>
    /// 起動時に一度呼べば、以降は登録済み・未登録を問わずすべてのウィンドウへ適用される。
    /// <code>
    /// // 設定クラス
    /// public EuWindowLayout WindowLayout { get; set; } = new();
    ///
    /// // 起動時
    /// EUi.Windows.BindLayout(this.config.WindowLayout, this.config.Save);
    /// </code>
    /// </remarks>
    public void BindLayout(EuWindowLayout windowLayout, Action? save = null)
    {
        ArgumentNullException.ThrowIfNull(windowLayout);

        this.layout = windowLayout;
        this.layoutSave = save;

        foreach (var window in this.windows)
            this.AttachState(window);
    }

    /// <summary>ウィンドウへ状態の入れ物を割り当てる。</summary>
    private void AttachState(EuWindow window)
    {
        if (this.layout is null)
            return;

        // 既に個別の入れ物が設定されている場合は、そちらを尊重する
        window.State ??= this.layout.GetOrCreate(window.Name);
        window.StateChanged ??= this.layoutSave;
    }

    /// <summary>ウィンドウを取り除く。</summary>
    public void Remove(EuWindow window) => this.windows.Remove(window);

    /// <summary>名前でウィンドウを探す。</summary>
    public EuWindow? Find(string name)
    {
        foreach (var window in this.windows)
        {
            if (string.Equals(window.Name, name, StringComparison.Ordinal))
                return window;
        }

        return null;
    }

    /// <summary>すべてのウィンドウを閉じる。</summary>
    public void CloseAll()
    {
        foreach (var window in this.windows)
            window.IsOpen = false;
    }

    /// <summary>
    /// すべてのウィンドウを描く。<c>UiBuilder.Draw</c> から毎フレーム呼ばれる。
    /// </summary>
    public void Draw()
    {
        if (this.disposed)
            return;

        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        // フォントのアトラスが構築できるまでは描かない。
        // ASCII しか持たない代替フォントで描くと、日本語がすべて ? になる。
        // ゲーム起動直後の数フレームがこれに当たる
        if (this.fontWait < FontWaitLimit && !EUi.Fonts.IsTextReady)
        {
            this.fontWait += ctx.DeltaTime;
            return;
        }

        // 通知はウィンドウが 1 つも開いていなくても表示する
        Widgets.ToastManager.Draw();

        if (this.windows.Count == 0)
            return;

        // 描画中にウィンドウが追加・削除されても壊れないよう、控えを取ってから回す
        this.drawBuffer.Clear();
        this.drawBuffer.AddRange(this.windows);

        foreach (var window in this.drawBuffer)
        {
            try
            {
                window.Render();
            }
            catch (Exception ex)
            {
                // 1 つのウィンドウの例外で他のウィンドウまで巻き込まないようにする
                EstellUtils.Diagnostics.UiLog.Error($"ウィンドウ「{window.Name}」の描画で例外が発生しました。", ex);
            }
        }

        this.drawBuffer.Clear();
    }

    /// <summary>管理しているウィンドウを解放する。</summary>
    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;

        foreach (var window in this.windows)
        {
            if (window is IDisposable disposable)
                disposable.Dispose();
        }

        foreach (var detach in this.openers)
        {
            try
            {
                detach();
            }
            catch
            {
                // 解除の失敗でプラグインの終了処理を止めない
            }
        }

        this.openers.Clear();
        this.windows.Clear();
        this.drawBuffer.Clear();
    }
}
