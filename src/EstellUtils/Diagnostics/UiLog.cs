using System;

using Dalamud.Plugin.Services;

namespace EstellUtils.Diagnostics;

/// <summary>
/// ライブラリ内部のログ出力先。
/// </summary>
/// <remarks>
/// ライブラリ側から Dalamud のログサービスを直接取得することはできないため、
/// プラグインが <see cref="Sink"/> に自分のログサービスを差し込む。
/// 未設定のままでも動作し、その場合ログは捨てられる。
/// </remarks>
public static class UiLog
{
    /// <summary>ログの出力先。プラグイン起動時に設定する。</summary>
    public static IPluginLog? Sink { get; set; }

    /// <summary>エラーを記録する。</summary>
    public static void Error(string message, Exception? exception = null)
    {
        if (exception is null)
            Sink?.Error(message);
        else
            Sink?.Error(exception, message);
    }

    /// <summary>警告を記録する。</summary>
    public static void Warning(string message) => Sink?.Warning(message);

    /// <summary>情報を記録する。</summary>
    public static void Info(string message) => Sink?.Information(message);

    /// <summary>デバッグ情報を記録する。</summary>
    public static void Debug(string message) => Sink?.Debug(message);
}
