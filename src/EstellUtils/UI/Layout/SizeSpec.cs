using System;

namespace EstellUtils.UI.Layout;

/// <summary>サイズの決め方。</summary>
public enum SizeMode
{
    /// <summary>ピクセル指定。</summary>
    Fixed,

    /// <summary>残り領域を重み付きで分け合う。</summary>
    Fill,

    /// <summary>親の領域に対する比率 (0〜1)。</summary>
    Ratio,

    /// <summary>内容に合わせる。前フレームの実測値を使う。</summary>
    Auto,
}

/// <summary>
/// 幅や高さの指定。レイアウトの列定義やウィジェットのサイズ指定に使う。
/// </summary>
/// <remarks>
/// <c>float</c> からの暗黙変換を用意してあるので、単純なピクセル指定は
/// <c>EUi.Row(120f, SizeSpec.Fill)</c> のように数値だけで書ける。
/// </remarks>
public readonly record struct SizeSpec(SizeMode Mode, float Value)
{
    /// <summary>残り領域を均等に分け合う (重み 1)。</summary>
    public static readonly SizeSpec Fill = new(SizeMode.Fill, 1f);

    /// <summary>内容に合わせる。</summary>
    public static readonly SizeSpec Auto = new(SizeMode.Auto, 0f);

    /// <summary>ピクセル指定。</summary>
    public static SizeSpec Px(float pixels) => new(SizeMode.Fixed, pixels);

    /// <summary>親に対する比率 (0〜1) 指定。</summary>
    public static SizeSpec Ratio(float ratio) => new(SizeMode.Ratio, Math.Clamp(ratio, 0f, 1f));

    /// <summary>残り領域を重み付きで分け合う。重みが大きいほど広くなる。</summary>
    public static SizeSpec Weight(float weight) => new(SizeMode.Fill, MathF.Max(0f, weight));

    /// <summary>ピクセル指定として扱う。</summary>
    public static implicit operator SizeSpec(float pixels) => Px(pixels);

    /// <summary>ピクセル指定として扱う。</summary>
    public static implicit operator SizeSpec(int pixels) => Px(pixels);

    /// <summary>
    /// 単独で解決する。<see cref="SizeMode.Fill"/> は利用可能領域いっぱいになる。
    /// </summary>
    /// <param name="available">利用可能な長さ。</param>
    /// <param name="autoSize">内容サイズ (<see cref="SizeMode.Auto"/> のとき使う)。</param>
    public float Resolve(float available, float autoSize = 0f) => this.Mode switch
    {
        SizeMode.Fixed => this.Value,
        SizeMode.Ratio => available * this.Value,
        SizeMode.Auto => autoSize,
        _ => available,
    };
}
