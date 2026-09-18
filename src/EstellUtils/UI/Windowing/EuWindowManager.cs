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

    /// <summary>管理しているウィンドウの数。</summary>
    public int Count => this.windows.Count;

    /// <summary>管理しているウィンドウ。</summary>
    public IReadOnlyList<EuWindow> Windows => this.windows;

    /// <summary>ウィンドウを追加する。</summary>
    public void Add(EuWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (this.windows.Contains(window))
            return;

        this.windows.Add(window);
        this.AttachState(window);
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

        this.windows.Clear();
        this.drawBuffer.Clear();
    }
}
