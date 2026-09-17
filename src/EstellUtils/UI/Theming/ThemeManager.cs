using System;
using System.Collections.Generic;

using EstellUtils.UI.Theming.Presets;

namespace EstellUtils.UI.Theming;

/// <summary>
/// 現在のテーマを管理する。
/// </summary>
/// <remarks>
/// プラグインは起動時に <see cref="SetDefault"/> で既定テーマを設定し、
/// 一部分だけ別テーマで描きたい場合は <see cref="Push"/> のスコープを使う。
/// </remarks>
public static class ThemeManager
{
    private static readonly List<Theme> Stack = new(4);

    private static Theme defaultTheme = XivNativeTheme.Create();

    /// <summary>現在有効なテーマ。</summary>
    public static Theme Current => Stack.Count > 0 ? Stack[^1] : defaultTheme;

    /// <summary>既定テーマ。スコープが積まれていないときはこれが使われる。</summary>
    public static Theme Default
    {
        get => defaultTheme;
        set => defaultTheme = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>既定テーマを差し替える。</summary>
    public static void SetDefault(Theme theme) => Default = theme;

    /// <summary>
    /// 一時的に別のテーマを適用する。<c>using</c> で抜けると元へ戻る。
    /// </summary>
    public static ThemeScope Push(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        Stack.Add(theme);
        return new ThemeScope();
    }

    /// <summary>テーマスコープを 1 段戻す。</summary>
    public static void Pop()
    {
        if (Stack.Count > 0)
            Stack.RemoveAt(Stack.Count - 1);
    }

    /// <summary>
    /// 積まれているスコープをすべて捨てる。例外などでスコープが閉じられなかった場合の保険として
    /// フレーム開始時に呼ばれる。
    /// </summary>
    internal static void ResetStack() => Stack.Clear();
}

/// <summary><c>using</c> でテーマを元に戻すスコープ。</summary>
public readonly struct ThemeScope : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => ThemeManager.Pop();
}
