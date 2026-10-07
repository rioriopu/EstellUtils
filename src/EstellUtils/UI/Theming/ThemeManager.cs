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
    /// <summary>1 色だけ差し替えるために使い回すテーマ。積む深さごとに 1 つ持つ。</summary>
    private static readonly List<Theme> ColorOverlays = new(4);

    /// <summary>
    /// 今のテーマから 1 色だけ差し替えて積む。
    /// </summary>
    /// <param name="role">差し替える色。</param>
    /// <param name="color">新しい色。</param>
    /// <remarks>
    /// <para>
    /// 丸ごとテーマを派生させると毎回 10 個ほど確保することになる。
    /// ここでは積む深さごとにテーマを使い回し、色だけを写すので、
    /// 2 回目以降は確保が起きない。
    /// </para>
    /// <para>
    /// 土台は常に今のテーマなので、ウィンドウ単位のテーマの中でも正しく効く。
    /// 寸法・動き・描画担当は今のテーマのものをそのまま共有する。
    /// </para>
    /// </remarks>
    public static ThemeScope PushColor(ThemeColor role, uint color)
        => PushColors(stackalloc (ThemeColor Role, uint Color)[] { (role, color) });

    /// <summary>複数の色をまとめて差し替えて積む。</summary>
    /// <param name="overrides">差し替える色の並び。</param>
    public static ThemeScope PushColors(ReadOnlySpan<(ThemeColor Role, uint Color)> overrides)
    {
        var current = Current;
        var depth = Stack.Count;

        while (ColorOverlays.Count <= depth)
            ColorOverlays.Add(current.Clone());

        var overlay = ColorOverlays[depth];

        // 寸法や動きは今のテーマのものを使う。色だけを写して差し替える
        overlay.Name = current.Name;
        overlay.SetMetrics(current.Metrics);
        overlay.Motion.CopyFrom(current.Motion);
        overlay.Painter = current.Painter;
        overlay.Colors.CopyFrom(current.Colors);

        foreach (var (role, color) in overrides)
            overlay.Colors[role] = color;

        return Push(overlay);
    }

    public static ThemeScope Push(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        Stack.Add(theme);
        return new ThemeScope(true);
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
/// <remarks>
/// 既定値 (<c>default</c>) のスコープは何もしない。条件によってテーマを積むかどうかが
/// 変わる場面で <c>cond ? Push(t) : default</c> と書けるようにするため。
/// </remarks>
public readonly struct ThemeScope : IDisposable
{
    private readonly bool active;

    internal ThemeScope(bool active) => this.active = active;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.active)
            ThemeManager.Pop();
    }
}
