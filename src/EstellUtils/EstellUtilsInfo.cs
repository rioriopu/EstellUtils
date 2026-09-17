namespace EstellUtils;

/// <summary>
/// ライブラリ自身のメタ情報。利用側プラグインが同梱している EstellUtils.dll の
/// 版を確認したいとき (バグ報告・互換チェック) に参照する。
/// </summary>
public static class EstellUtilsInfo
{
    /// <summary>ライブラリのバージョン。csproj の Version と一致させること。</summary>
    public const string Version = "0.1.0.0";

    /// <summary>ライブラリ名。ログ出力の接頭辞などに使う。</summary>
    public const string Name = "EstellUtils";

    /// <summary>想定している Dalamud の API レベル。</summary>
    public const int TargetDalamudApiLevel = 15;
}
