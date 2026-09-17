using System;

namespace EstellUtils.UI.Binding;

/// <summary>設定項目に表示するラベル。</summary>
/// <remarks>
/// フィールド名をそのまま画面に出すと英語のままになるため、日本語ラベルはここで与える。
/// <code>
/// [EuLabel("オーバーレイ更新間隔")]
/// public int UpdateInterval = 2;
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuLabelAttribute : Attribute
{
    /// <summary>ラベルを指定する。</summary>
    public EuLabelAttribute(string label) => this.Label = label;

    /// <summary>表示するラベル。</summary>
    public string Label { get; }
}

/// <summary>設定項目のツールチップ。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuTipAttribute : Attribute
{
    /// <summary>説明文を指定する。</summary>
    public EuTipAttribute(string tip) => this.Tip = tip;

    /// <summary>ホバー時に表示する説明。</summary>
    public string Tip { get; }
}

/// <summary>数値の範囲。スライダーの下限・上限になる。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuRangeAttribute : Attribute
{
    /// <summary>範囲を指定する。</summary>
    public EuRangeAttribute(float min, float max)
    {
        this.Min = min;
        this.Max = max;
    }

    /// <summary>最小値。</summary>
    public float Min { get; }

    /// <summary>最大値。</summary>
    public float Max { get; }

    /// <summary>小数の表示桁数。</summary>
    public int Decimals { get; set; } = 2;

    /// <summary>値の後ろに付ける単位。</summary>
    public string? Suffix { get; set; }
}

/// <summary>項目をまとめるグループ名。同じ名前の項目がセクションにまとまる。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuGroupAttribute : Attribute
{
    /// <summary>グループ名を指定する。</summary>
    public EuGroupAttribute(string group) => this.Group = group;

    /// <summary>グループ名。</summary>
    public string Group { get; }
}

/// <summary>表示順。小さいほど上に出る。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuOrderAttribute : Attribute
{
    /// <summary>順序を指定する。</summary>
    public EuOrderAttribute(int order) => this.Order = order;

    /// <summary>並び順。</summary>
    public int Order { get; }
}

/// <summary>自動生成の対象から外す。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuHiddenAttribute : Attribute
{
}

/// <summary>真偽値をトグルスイッチで表示する (既定はチェックボックス)。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuToggleAttribute : Attribute
{
}

/// <summary><c>uint</c> や <c>Vector4</c> を色として編集する。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuColorAttribute : Attribute
{
    /// <summary>不透明度も編集するか。</summary>
    public bool ShowAlpha { get; set; } = true;
}

/// <summary>列挙値に表示名を与える。</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class EuEnumLabelAttribute : Attribute
{
    /// <summary>表示名を指定する。</summary>
    public EuEnumLabelAttribute(string label) => this.Label = label;

    /// <summary>表示名。</summary>
    public string Label { get; }
}
