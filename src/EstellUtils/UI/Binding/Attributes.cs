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

/// <summary>
/// ラベルを、静的なプロパティ・フィールド・メソッドから毎回引く。
/// </summary>
/// <remarks>
/// <para>
/// 属性に書けるのはコンパイル時定数だけなので、表示言語を実行時に切り替える場合は
/// 定数のラベルを持てない。これは「どこから引くか」だけを定数で指定する。
/// </para>
/// <code>
/// [EuLabelFrom(typeof(Language.Settings), nameof(Language.Settings.UpdateInterval))]
/// public int UpdateInterval = 2;
/// </code>
/// <para>
/// 指定先は <c>static</c> で、<c>string</c> を返すプロパティ・フィールド・
/// 引数なしメソッドのいずれか。毎フレーム読まれるので、値を作り直さない形にしておくこと。
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuLabelFromAttribute : Attribute
{
    /// <summary>引き先を指定する。</summary>
    /// <param name="type">静的メンバーを持つ型。</param>
    /// <param name="member">メンバー名。</param>
    public EuLabelFromAttribute(Type type, string member)
    {
        this.Type = type;
        this.Member = member;
    }

    /// <summary>静的メンバーを持つ型。</summary>
    public Type Type { get; }

    /// <summary>メンバー名。</summary>
    public string Member { get; }
}

/// <summary>ツールチップを、静的なメンバーから毎回引く。</summary>
/// <remarks><see cref="EuLabelFromAttribute"/> と同じ仕組み。</remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuTipFromAttribute : Attribute
{
    /// <summary>引き先を指定する。</summary>
    /// <param name="type">静的メンバーを持つ型。</param>
    /// <param name="member">メンバー名。</param>
    public EuTipFromAttribute(Type type, string member)
    {
        this.Type = type;
        this.Member = member;
    }

    /// <summary>静的メンバーを持つ型。</summary>
    public Type Type { get; }

    /// <summary>メンバー名。</summary>
    public string Member { get; }
}

/// <summary>
/// <see cref="System.Numerics.Vector4"/> などを、色ではなく数値の並びとして編集する。
/// </summary>
/// <remarks>
/// 座標や余白のように、色ではないベクトルに付ける。
/// 各成分のラベルを与えると、入力欄の上に出る。
/// <code>
/// [EuVector("L", "D", "R", "U")]
/// public Vector4 ScreenMarkConstraint = new(10f, 10f, 10f, 10f);
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuVectorAttribute : Attribute
{
    /// <summary>成分のラベルを指定する。</summary>
    /// <param name="labels">各成分に添える文字。省略すると X / Y / Z / W。</param>
    public EuVectorAttribute(params string[] labels) => this.Labels = labels;

    /// <summary>各成分に添える文字。</summary>
    public string[] Labels { get; }

    /// <summary>増減ボタンの刻み。</summary>
    public float Step { get; set; }

    /// <summary>下限。</summary>
    public float Min { get; set; } = float.MinValue;

    /// <summary>上限。</summary>
    public float Max { get; set; } = float.MaxValue;
}

/// <summary>
/// 入れ子になった設定クラスを、その中身ごと展開する。
/// </summary>
/// <remarks>
/// <para>
/// 設定をいくつかのクラスへ分けている場合に付ける。付けた先のクラスの項目が、
/// あたかも直下にあるかのように並ぶ。
/// </para>
/// <code>
/// [EuNested(Group = "モブハント")]
/// public MobHuntConfig MobHunt { get; set; } = new();
/// </code>
/// <para>
/// 入れ子は 4 段まで辿る。循環している場合はそこで止まる。
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class EuNestedAttribute : Attribute
{
    /// <summary>
    /// 展開した項目をまとめるグループ名。省略すると、項目ごとの指定に従う。
    /// </summary>
    public string? Group { get; set; }
}
