using System;
using System.Collections.Generic;

namespace EstellUtils.UI.Theming;

/// <summary>
/// 色・寸法・モーションをまとめたデザイントークン一式。
/// </summary>
/// <remarks>
/// <para>
/// プラグインごとに見た目を変えたい場合は、プリセットを <see cref="Clone"/> してから
/// 必要なトークンだけ書き換える。トークンに無い値が必要なら
/// <see cref="CustomColors"/> / <see cref="CustomMetrics"/> へ自由に追加できる。
/// </para>
/// <para>
/// 毎フレーム <see cref="Clone"/> するとアロケーションが発生するため、
/// 作ったテーマはプラグイン側で保持して使い回すこと。
/// </para>
/// </remarks>
public sealed class Theme
{
    private ThemeMetrics baseMetrics;
    private float scale = 1f;

    /// <summary>テーマを作る。</summary>
    public Theme(string name, ThemeColors colors, ThemeMetrics metrics, ThemeMotion motion)
    {
        this.Name = name;
        this.Colors = colors;
        this.baseMetrics = metrics;
        this.Metrics = metrics.Clone();
        this.Motion = motion;
    }

    /// <summary>テーマ名。設定画面での選択肢表示に使う。</summary>
    public string Name { get; set; }

    /// <summary>色トークン。</summary>
    public ThemeColors Colors { get; set; }

    /// <summary>寸法トークン (<see cref="Scale"/> 適用後)。</summary>
    public ThemeMetrics Metrics { get; private set; }

    /// <summary>モーショントークン。</summary>
    public ThemeMotion Motion { get; set; }

    /// <summary>追加の色トークン。ライブラリが用意していない色を足したいときに使う。</summary>
    public Dictionary<string, uint> CustomColors { get; } = new(StringComparer.Ordinal);

    /// <summary>追加の寸法トークン。</summary>
    public Dictionary<string, float> CustomMetrics { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 全体の拡大率。設定すると <see cref="Metrics"/> が再計算される。
    /// </summary>
    public float Scale
    {
        get => this.scale;
        set
        {
            var clamped = Math.Clamp(value, 0.5f, 4f);
            if (Math.Abs(clamped - this.scale) < 0.0001f)
                return;

            this.scale = clamped;
            this.Metrics = this.baseMetrics.Scaled(clamped);
        }
    }

    /// <summary>
    /// 寸法トークンを書き換える。<see cref="Scale"/> の基準値ごと差し替えるため、
    /// 以後の拡大率変更にも追従する。
    /// </summary>
    public void SetMetrics(ThemeMetrics metrics)
    {
        this.baseMetrics = metrics;
        this.Metrics = metrics.Scaled(this.scale);
    }

    /// <summary>追加の色トークンを引く。未登録なら <paramref name="fallback"/> を返す。</summary>
    public uint Color(string key, uint fallback)
        => this.CustomColors.TryGetValue(key, out var value) ? value : fallback;

    /// <summary>追加の寸法トークンを引く。未登録なら <paramref name="fallback"/> を返す。</summary>
    public float Metric(string key, float fallback)
        => this.CustomMetrics.TryGetValue(key, out var value) ? value : fallback;

    /// <summary>このテーマの複製を作る。プリセットを土台に調整するときに使う。</summary>
    public Theme Clone()
    {
        var clone = new Theme(this.Name, this.Colors.Clone(), this.baseMetrics.Clone(), this.Motion.Clone())
        {
            scale = this.scale,
        };

        clone.Metrics = this.baseMetrics.Scaled(this.scale);

        foreach (var pair in this.CustomColors)
            clone.CustomColors[pair.Key] = pair.Value;

        foreach (var pair in this.CustomMetrics)
            clone.CustomMetrics[pair.Key] = pair.Value;

        return clone;
    }

    /// <summary>
    /// 複製したうえで変更を適用する。
    /// <code>
    /// var myTheme = XivNativeTheme.Create().Derive("MyPlugin", t => t.Colors.Accent = myColor);
    /// </code>
    /// </summary>
    public Theme Derive(string name, Action<Theme> configure)
    {
        var clone = this.Clone();
        clone.Name = name;
        configure(clone);
        return clone;
    }
}
