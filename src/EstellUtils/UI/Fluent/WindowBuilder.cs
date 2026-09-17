using System;
using System.Collections.Generic;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Windowing;

namespace EstellUtils.UI.Fluent;

/// <summary>
/// ウィンドウを宣言的に組み立てるビルダー。
/// </summary>
/// <remarks>
/// <see cref="EuWindow"/> を継承するほどではない画面を、その場で定義するためのもの。
/// プラグインの起動時に 1 度だけ呼び、返ってきたウィンドウを保持して開閉する。
/// <code>
/// this.configWindow = EUi.Window("Masked Dalamud 設定")
///     .Size(460, 360)
///     .MinSize(400, 280)
///     .Tab("基本", this.DrawBasicTab)
///     .Tab("設定", this.DrawSettingsTab)
///     .Register();
/// </code>
/// </remarks>
public sealed class WindowBuilder
{
    private readonly FluentWindow window;

    internal WindowBuilder(string name) => this.window = new FluentWindow(name);

    /// <summary>初期の大きさ。</summary>
    public WindowBuilder Size(float width, float height)
    {
        this.window.Size = new Vector2(width, height);
        return this;
    }

    /// <summary>最小の大きさ。</summary>
    public WindowBuilder MinSize(float width, float height)
    {
        this.window.MinSize = new Vector2(width, height);
        return this;
    }

    /// <summary>最大の大きさ。</summary>
    public WindowBuilder MaxSize(float width, float height)
    {
        this.window.MaxSize = new Vector2(width, height);
        return this;
    }

    /// <summary>初期位置。指定しなければ画面中央。</summary>
    public WindowBuilder At(float x, float y)
    {
        this.window.Position = new Vector2(x, y);
        this.window.SkipAutoCenter();
        return this;
    }

    /// <summary>タイトルバーを表示しない。</summary>
    public WindowBuilder NoTitleBar()
    {
        this.window.HasTitleBar = false;
        return this;
    }

    /// <summary>大きさを変えられないようにする。</summary>
    public WindowBuilder NoResize()
    {
        this.window.Resizable = false;
        return this;
    }

    /// <summary>移動できないようにする。</summary>
    public WindowBuilder NoMove()
    {
        this.window.Movable = false;
        return this;
    }

    /// <summary>閉じるボタンを出さない。</summary>
    public WindowBuilder NoClose()
    {
        this.window.Closable = false;
        return this;
    }

    /// <summary>内容を自動スクロールさせない。</summary>
    public WindowBuilder NoScroll()
    {
        this.window.AutoScroll = false;
        return this;
    }

    /// <summary>内側の余白。</summary>
    public WindowBuilder Padding(EdgeInsets padding)
    {
        this.window.Padding = padding;
        return this;
    }

    /// <summary>このウィンドウだけで使うテーマ。</summary>
    public WindowBuilder UseTheme(Theme theme)
    {
        this.window.Theme = theme;
        return this;
    }

    /// <summary>タブを使わない場合の中身。</summary>
    public WindowBuilder Content(Action body)
    {
        this.window.Body = body;
        return this;
    }

    /// <summary>タブを追加する。1 つでも追加するとタブバーが表示される。</summary>
    public WindowBuilder Tab(string label, Action body)
    {
        this.window.AddTab(label, body, null);
        return this;
    }

    /// <summary>表示条件付きのタブを追加する。条件が false のときタブごと消える。</summary>
    public WindowBuilder Tab(string label, Action body, Func<bool> visible)
    {
        this.window.AddTab(label, body, visible);
        return this;
    }

    /// <summary>ウィンドウ自体の表示条件。</summary>
    public WindowBuilder Condition(Func<bool> condition)
    {
        this.window.Condition = condition;
        return this;
    }

    /// <summary>開かれたときに呼ばれる処理。</summary>
    public WindowBuilder OnOpen(Action handler)
    {
        this.window.Opened = handler;
        return this;
    }

    /// <summary>閉じられたときに呼ばれる処理。保存の確定などに使う。</summary>
    public WindowBuilder OnClose(Action handler)
    {
        this.window.Closed = handler;
        return this;
    }

    /// <summary>
    /// ウィンドウ管理へ登録して返す。返り値を保持して <c>IsOpen</c> を切り替える。
    /// </summary>
    /// <param name="open">登録直後から開いた状態にするか。</param>
    public EuWindow Register(bool open = false)
    {
        this.window.IsOpen = open;
        EUi.Windows.Add(this.window);
        return this.window;
    }
}

/// <summary>ビルダーが組み立てたウィンドウ。</summary>
internal sealed class FluentWindow : EuWindow
{
    private readonly List<TabEntry> tabs = new();
    private readonly List<string> visibleLabels = new();

    private bool autoCenter = true;

    public FluentWindow(string name)
        : base(name)
    {
    }

    internal Action? Body { get; set; }

    internal Func<bool>? Condition { get; set; }

    internal Action? Opened { get; set; }

    internal Action? Closed { get; set; }

    internal void AddTab(string label, Action body, Func<bool>? visible)
        => this.tabs.Add(new TabEntry(label, body, visible));

    internal void SkipAutoCenter() => this.autoCenter = false;

    /// <inheritdoc/>
    public override bool DrawConditions() => this.Condition?.Invoke() ?? true;

    /// <inheritdoc/>
    public override void OnOpen() => this.Opened?.Invoke();

    /// <inheritdoc/>
    public override void OnClose() => this.Closed?.Invoke();

    /// <inheritdoc/>
    public override void PreDraw()
    {
        if (this.autoCenter)
            return;

        // At() で位置を指定した場合は自動中央寄せを行わない
        this.MarkPlaced();
    }

    /// <inheritdoc/>
    public override void Draw()
    {
        this.Body?.Invoke();

        if (this.tabs.Count == 0)
            return;

        // 条件付きタブがあるため、表示するラベルは毎フレーム組み直す
        this.visibleLabels.Clear();

        foreach (var tab in this.tabs)
        {
            if (tab.Visible?.Invoke() ?? true)
                this.visibleLabels.Add(tab.Label);
        }

        if (this.visibleLabels.Count == 0)
            return;

        var result = EUi.TabBar("##euFluentTabs", System.Runtime.InteropServices.CollectionsMarshal.AsSpan(this.visibleLabels));
        EUi.Spacing(EUi.Metrics.SpacingSm);

        var selectedLabel = result.SelectedLabel;
        if (selectedLabel is null)
            return;

        foreach (var tab in this.tabs)
        {
            if (string.Equals(tab.Label, selectedLabel, StringComparison.Ordinal))
            {
                tab.Body();
                return;
            }
        }
    }

    private readonly record struct TabEntry(string Label, Action Body, Func<bool>? Visible);
}
