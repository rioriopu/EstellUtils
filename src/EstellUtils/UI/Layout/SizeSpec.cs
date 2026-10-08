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
public readonly record struct SizeSpec(SizeMode Mode, float Value, float Min = 0f)
{
    /// <summary>残り領域を均等に分け合う (重み 1)。</summary>
    public static readonly SizeSpec Fill = new(SizeMode.Fill, 1f);

    /// <summary>内容に合わせる。</summary>
    public static readonly SizeSpec Auto = new(SizeMode.Auto, 0f);

    /// <summary>ピクセル指定。</summary>
    public static SizeSpec Px(float pixels) => new(SizeMode.Fixed, pixels);

    /// <summary>
    /// 拡大率に追従する固定幅。<c>px × テーマの拡大率</c> になる。
    /// </summary>
    /// <remarks>
    /// <see cref="Px"/> は生のピクセルなので、拡大率を上げても文字だけが大きくなり、
    /// 固定幅の列が文字に追い越される。寸法を文字と一緒に広げたい場合はこちらを使う。
    /// </remarks>
    public static SizeSpec Scaled(float pixels)
        => new(SizeMode.Fixed, pixels * Theming.ThemeManager.Current.Scale);

    /// <summary>
    /// 文字の大きさを基準にした幅。<c>em × 現在のフォントの大きさ</c> になる。
    /// </summary>
    /// <remarks>
    /// 「おおよそ何文字ぶん」で決めたい列に使う。フォントを変えても比率が保たれる。
    /// </remarks>
    public static SizeSpec Em(float em)
        => new(SizeMode.Fixed, em * Render.TextPainter.FontSize);

    /// <summary>親に対する比率 (0〜1) 指定。</summary>
    public static SizeSpec Ratio(float ratio) => new(SizeMode.Ratio, Math.Clamp(ratio, 0f, 1f));

    /// <summary>残り領域を重み付きで分け合う。重みが大きいほど広くなる。</summary>
    public static SizeSpec Weight(float weight) => new(SizeMode.Fill, MathF.Max(0f, weight));

    /// <summary>
    /// 下限を付ける。これより細くならない。
    /// </summary>
    /// <param name="pixels">下限の幅 (px)。</param>
    /// <remarks>
    /// <para>
    /// 列の配分で使う。固定幅の列の合計が窓の幅を超えると、
    /// <see cref="SizeMode.Fill"/> の列が極端に細くなって中身が読めなくなる。
    /// 下限を付けると、代わりに下限の無い列が縮む。
    /// </para>
    /// <code>
    /// new TableColumn("ドロップ品", SizeSpec.Fill.AtLeast(200f))
    /// </code>
    /// <para>
    /// 下限の合計が使える幅を超えた場合は、全部の列を比例で縮めて行の中へ収める。
    /// 下限は「入るならこの幅を確保する」という意味であり、はみ出す許可ではない。
    /// </para>
    /// </remarks>
    public SizeSpec AtLeast(float pixels) => this with { Min = MathF.Max(0f, pixels) };

    /// <summary>ピクセル指定として扱う。</summary>
    public static implicit operator SizeSpec(float pixels) => Px(pixels);

    /// <summary>ピクセル指定として扱う。</summary>
    public static implicit operator SizeSpec(int pixels) => Px(pixels);

    /// <summary>
    /// 単独で解決する。<see cref="SizeMode.Fill"/> は利用可能領域いっぱいになる。
    /// </summary>
    /// <param name="available">利用可能な長さ。</param>
    /// <param name="autoSize">内容サイズ (<see cref="SizeMode.Auto"/> のとき使う)。</param>
    public float Resolve(float available, float autoSize = 0f)
    {
        var raw = this.Mode switch
        {
            SizeMode.Fixed => this.Value,
            SizeMode.Ratio => available * this.Value,
            SizeMode.Auto => autoSize,
            _ => available,
        };

        return this.Min > 0f ? MathF.Max(raw, this.Min) : raw;
    }
}
