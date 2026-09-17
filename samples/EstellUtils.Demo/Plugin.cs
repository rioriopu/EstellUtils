using System;

using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using EstellUtils.UI;

namespace EstellUtils.Demo;

/// <summary>
/// EstellUtils のデモプラグイン。
/// </summary>
/// <remarks>
/// ライブラリの使い方はこのクラスがそのまま最小の手本になる。
/// 起動時に <see cref="EUi.Initialize"/>、終了時に <see cref="EUi.Shutdown"/> を呼び、
/// ウィンドウを <c>EUi.Windows</c> へ登録するだけでよい。
/// </remarks>
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/eudemo";

    private readonly ICommandManager commandManager;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly GalleryWindow window;

    /// <summary>プラグインを初期化する。</summary>
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;

        // ライブラリの初期化。UiBuilder.Draw への接続もここで行われる
        EUi.Initialize(pluginInterface, log: log);

        this.window = new GalleryWindow { IsOpen = true };
        EUi.Windows.Add(this.window);

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "EstellUtils のウィジェットギャラリーを開きます。",
        });

        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenWindow;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenWindow;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenWindow;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenWindow;

        this.commandManager.RemoveHandler(CommandName);

        EUi.Shutdown();
    }

    private void OnCommand(string command, string arguments) => this.window.Toggle();

    private void OpenWindow() => this.window.IsOpen = true;
}
