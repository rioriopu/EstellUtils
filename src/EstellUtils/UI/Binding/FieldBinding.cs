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

    /// <summary>画面に出すラベル。</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>ツールチップ。</summary>
    public string? Tip { get; init; }

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
                "##" + this.Name, ref value, this.Hint, this.MaxLength, SizeSpec.Fill, disabled);

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
            var result = EUi.Combo("##" + this.Name, ref index, this.Names, SizeSpec.Fill, disabled);

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
            var result = EUi.ColorEdit("##" + this.Name, ref value, this.ShowAlpha);

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
            var result = EUi.ColorEdit("##" + this.Name, ref value, this.ShowAlpha);

            if (this.Tip is not null)
                result.Tip(this.Tip);

            if (!result.Changed)
                return false;
        }

        this.Setter(target, value);
        return true;
    }
}
