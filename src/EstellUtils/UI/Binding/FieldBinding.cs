using System;
using System.Numerics;
using System.Reflection;

using EstellUtils.UI.Layout;

namespace EstellUtils.UI.Binding;

/// <summary>
/// 設定の 1 項目を画面に出す方法。型ごとに実装が分かれる。
/// </summary>
/// <typeparam name="T">設定クラスの型。</typeparam>
/// <remarks>
/// アクセサは初回に式木からコンパイルしてキャッシュするため、
/// 毎フレームのリフレクション呼び出しやボクシングは発生しない。
/// </remarks>
public abstract class FieldBinding<T>
{
    /// <summary>メンバー名。<c>nameof</c> で指定するときの鍵になる。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// ウィジェットへ渡す識別子 (<c>"##" + メンバー名</c>)。
    /// 毎フレーム文字列を作らないよう、解析時に一度だけ組み立てておく。
    /// </summary>
    public string WidgetId { get; init; } = "##";

    /// <summary>
    /// 画面に出すラベル。
    /// </summary>
    /// <remarks>
    /// <see cref="LabelProvider"/> が設定されていればそちらが優先される。
    /// 表示言語を実行時に切り替える場合に使う。
    /// </remarks>
    public string Label
    {
        get => this.LabelProvider?.Invoke() ?? this.StaticLabel;
        init => this.StaticLabel = value;
    }

    /// <summary>ツールチップ。</summary>
    /// <remarks><see cref="TipProvider"/> が設定されていればそちらが優先される。</remarks>
    public string? Tip
    {
        get => this.TipProvider?.Invoke() ?? this.StaticTip;
        init => this.StaticTip = value;
    }

    /// <summary>
    /// ラベルを毎回求める関数。設定すると <see cref="Label"/> より優先される。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 属性に書けるのはコンパイル時定数だけなので、表示言語を実行時に切り替える場合は
    /// ここへ関数を渡す。<c>Binder.SetLabel</c> か <c>[EuLabelFrom]</c> で設定する。
    /// </para>
    /// <para>
    /// 毎フレーム呼ばれるので、文字列を組み立て直さずに済む形にしておくこと。
    /// </para>
    /// </remarks>
    public Func<string>? LabelProvider { get; set; }

    /// <summary>ツールチップを毎回求める関数。</summary>
    public Func<string?>? TipProvider { get; set; }

    /// <summary>属性などで与えられた、切り替わらないラベル。</summary>
    protected string StaticLabel { get; private set; } = string.Empty;

    /// <summary>属性などで与えられた、切り替わらないツールチップ。</summary>
    protected string? StaticTip { get; private set; }

    /// <summary>所属するグループ名。</summary>
    public string? Group { get; init; }

    /// <summary>並び順。</summary>
    public int Order { get; init; }

    /// <summary>既定値 (新しいインスタンスから採った値)。</summary>
    public object? DefaultValue { get; init; }

    /// <summary>項目を描き、値が変わったかを返す。</summary>
    public abstract bool Draw(T target, bool disabled);

    /// <summary>既定値へ戻す。</summary>
    public abstract void ResetToDefault(T target);

    /// <summary>現在値が既定値と異なるか。</summary>
    public abstract bool IsModified(T target);

    /// <summary>
    /// 現在値を型を問わない形で取り出す。巻き戻しの基準を覚えるのに使う。
    /// </summary>
    public abstract object? GetValue(T target);

    /// <summary>
    /// 型を問わない形で値を書き戻す。書き換わったら true。
    /// </summary>
    public abstract bool SetValue(T target, object? value);
}

/// <summary>型付きアクセサを持つ項目の共通実装。</summary>
/// <typeparam name="T">設定クラスの型。</typeparam>
/// <typeparam name="TValue">値の型。</typeparam>
public abstract class FieldBinding<T, TValue> : FieldBinding<T>
{
    /// <summary>値を読む。</summary>
    public required Func<T, TValue> Getter { get; init; }

    /// <summary>値を書く。</summary>
    public required Action<T, TValue> Setter { get; init; }

    /// <inheritdoc/>
    public override void ResetToDefault(T target)
    {
        if (this.DefaultValue is TValue value)
            this.Setter(target, value);
    }

    /// <inheritdoc/>
    public override bool IsModified(T target)
    {
        if (this.DefaultValue is not TValue value)
            return false;

        return !Equals(this.Getter(target), value);
    }

    /// <inheritdoc/>
    public override object? GetValue(T target) => this.Getter(target);

    /// <inheritdoc/>
    public override bool SetValue(T target, object? value)
    {
        if (value is not TValue typed || Equals(this.Getter(target), typed))
            return false;

        this.Setter(target, typed);
        return true;
    }
}

/// <summary>真偽値の項目。</summary>
public sealed class BoolBinding<T> : FieldBinding<T, bool>
{
    /// <summary>トグルスイッチで表示するか。</summary>
    public bool UseToggle { get; init; }

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var value = this.Getter(target);

        var result = this.UseToggle
            ? EUi.Toggle(this.Label, ref value, disabled)
            : EUi.Checkbox(this.Label, ref value, disabled);

        if (this.Tip is not null)
            result.Tip(this.Tip);

        if (!result.Changed)
            return false;

        this.Setter(target, value);
        return true;
    }
}

/// <summary>整数の項目。</summary>
public sealed class IntBinding<T> : FieldBinding<T, int>
{
    /// <summary>最小値。</summary>
    public int Min { get; init; }

    /// <summary>最大値。</summary>
    public int Max { get; init; } = 100;

    /// <summary>単位。</summary>
    public string? Suffix { get; init; }

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var value = this.Getter(target);
        var result = EUi.SliderInt(
            this.Label, ref value, this.Min, this.Max, null, this.Suffix, disabled);

        if (this.Tip is not null)
            result.Tip(this.Tip);

        if (!result.Changed)
            return false;

        this.Setter(target, value);
        return true;
    }
}

/// <summary>小数の項目。</summary>
public sealed class FloatBinding<T> : FieldBinding<T, float>
{
    /// <summary>最小値。</summary>
    public float Min { get; init; }

    /// <summary>最大値。</summary>
    public float Max { get; init; } = 1f;

    /// <summary>小数の表示桁数。</summary>
    public int Decimals { get; init; } = 2;

    /// <summary>単位。</summary>
    public string? Suffix { get; init; }

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var value = this.Getter(target);
        var result = EUi.SliderFloat(
            this.Label, ref value, this.Min, this.Max, null, this.Suffix, disabled, this.Decimals);

        if (this.Tip is not null)
            result.Tip(this.Tip);

        if (!result.Changed)
            return false;

        this.Setter(target, value);
        return true;
    }
}

/// <summary>文字列の項目。</summary>
public sealed class StringBinding<T> : FieldBinding<T, string>
{
    /// <summary>空のときに表示する案内文。</summary>
    public string? Hint { get; init; }

    /// <summary>最大文字数。</summary>
    public int MaxLength { get; init; } = 256;

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var value = this.Getter(target) ?? string.Empty;

        using (EUi.Field(this.Label))
        {
            var result = EUi.TextInput(
                this.WidgetId, ref value, this.Hint, this.MaxLength, SizeSpec.Fill, disabled);

            if (this.Tip is not null)
                result.Tip(this.Tip);

            if (!result.Changed)
                return false;
        }

        this.Setter(target, value);
        return true;
    }
}

/// <summary>列挙値の項目。</summary>
public sealed class EnumBinding<T> : FieldBinding<T, int>
{
    /// <summary>選択肢の表示名。</summary>
    public required string[] Names { get; init; }

    /// <summary>選択肢に対応する実際の値。</summary>
    public required int[] Values { get; init; }

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var current = this.Getter(target);
        var index = Array.IndexOf(this.Values, current);
        if (index < 0)
            index = 0;

        using (EUi.Field(this.Label))
        {
            var result = EUi.Combo(this.WidgetId, ref index, this.Names, SizeSpec.Fill, disabled);

            if (this.Tip is not null)
                result.Tip(this.Tip);

            if (!result.Changed)
                return false;
        }

        this.Setter(target, this.Values[index]);
        return true;
    }
}

/// <summary>色 (<see cref="Vector4"/>) の項目。</summary>
public sealed class ColorVectorBinding<T> : FieldBinding<T, Vector4>
{
    /// <summary>不透明度も編集するか。</summary>
    public bool ShowAlpha { get; init; } = true;

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var value = this.Getter(target);

        using (EUi.Field(this.Label))
        {
            var result = EUi.ColorEdit(this.WidgetId, ref value, this.ShowAlpha);

            if (this.Tip is not null)
                result.Tip(this.Tip);

            if (!result.Changed)
                return false;
        }

        this.Setter(target, value);
        return true;
    }
}

/// <summary>色 (<c>uint</c>) の項目。</summary>
public sealed class ColorPackedBinding<T> : FieldBinding<T, uint>
{
    /// <summary>不透明度も編集するか。</summary>
    public bool ShowAlpha { get; init; } = true;

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var value = this.Getter(target);

        using (EUi.Field(this.Label))
        {
            var result = EUi.ColorEdit(this.WidgetId, ref value, this.ShowAlpha);

            if (this.Tip is not null)
                result.Tip(this.Tip);

            if (!result.Changed)
                return false;
        }

        this.Setter(target, value);
        return true;
    }
}

/// <summary>
/// <see cref="Vector4"/> を数値の並びとして編集する項目。
/// </summary>
/// <typeparam name="T">設定クラスの型。</typeparam>
/// <remarks>
/// 色として扱いたい場合は <c>[EuColor]</c> を付ける。付いていないベクトルはこちらになる。
/// </remarks>
public sealed class VectorBinding<T> : FieldBinding<T, Vector4>
{
    /// <summary>各成分に添える文字。</summary>
    public string[] Labels { get; init; } = [];

    /// <summary>増減ボタンの刻み。</summary>
    public float Step { get; init; }

    /// <summary>下限。</summary>
    public float? Min { get; init; }

    /// <summary>上限。</summary>
    public float? Max { get; init; }

    /// <inheritdoc/>
    public override bool Draw(T target, bool disabled)
    {
        var value = this.Getter(target);

        // 成分ごとに欄が並ぶので、ラベルは上に置いて幅を確保する
        EUi.Label(this.Label);

        var result = EUi.InputVector4(
            this.WidgetId, ref value, this.Labels, this.Step, this.Min, this.Max, disabled);

        if (this.Tip is not null)
            result.Tip(this.Tip);

        if (!result.Changed)
            return false;

        this.Setter(target, value);
        return true;
    }
}
