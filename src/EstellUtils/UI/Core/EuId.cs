using System;
using System.Runtime.CompilerServices;

namespace EstellUtils.UI.Core;

/// <summary>
/// ウィジェットを一意に識別する ID。フレームをまたいで状態 (ホバー量・アクティブ状態・
/// テキスト入力のカーソル位置など) を引き当てるための鍵になる。
/// </summary>
/// <remarks>
/// <para>
/// 文字列ラベルから FNV-1a (64bit) で生成する。ID スタック (<see cref="UiContext.PushId(ReadOnlySpan{char})"/>) を
/// シードとして混ぜるため、同じラベルでも親が違えば別の ID になる。
/// </para>
/// <para>
/// ラベルの記法は ImGui の慣習に合わせてある (生 ImGui と併用する利用者が混乱しないため)。
/// <list type="bullet">
///   <item><c>"保存"</c> — 表示は「保存」、ID もラベル全体から生成</item>
///   <item><c>"保存##left"</c> — 表示は「保存」、ID はラベル全体 (<c>##left</c> 込み) から生成。
///         同じ見た目のボタンを複数置くときに使う</item>
///   <item><c>"保存###saveBtn"</c> — 表示は「保存」、ID は <c>saveBtn</c> のみから生成。
///         表示文字列が動的に変わっても ID を固定したいときに使う</item>
/// </list>
/// </para>
/// </remarks>
public readonly record struct EuId(ulong Value)
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>無効な ID。ホバー中・操作中のウィジェットが無い状態を表すのにも使う。</summary>
    public static readonly EuId None = default;

    /// <summary>無効な ID か。</summary>
    public bool IsNone => this.Value == 0UL;

    /// <summary>
    /// 文字列からハッシュを計算する。<paramref name="seed"/> に親 ID を渡すと階層化できる。
    /// </summary>
    public static ulong Hash(ReadOnlySpan<char> text, ulong seed = 0UL)
    {
        var hash = seed == 0UL ? FnvOffsetBasis : seed;
        foreach (var c in text)
        {
            // char を 2 バイトとして処理する (UTF-16 のまま扱うので日本語ラベルでも衝突しにくい)
            hash = (hash ^ (byte)(c & 0xFF)) * FnvPrime;
            hash = (hash ^ (byte)(c >> 8)) * FnvPrime;
        }

        // 0 は「無効な ID」に予約してあるので、万一 0 になったら 1 にずらす
        return hash == 0UL ? 1UL : hash;
    }

    /// <summary>整数からハッシュを計算する。ループでの連番 ID などに使う。</summary>
    public static ulong Hash(int value, ulong seed = 0UL)
    {
        var hash = seed == 0UL ? FnvOffsetBasis : seed;
        var v = unchecked((uint)value);
        for (var i = 0; i < 4; i++)
        {
            hash = (hash ^ (byte)(v & 0xFF)) * FnvPrime;
            v >>= 8;
        }

        return hash == 0UL ? 1UL : hash;
    }

    /// <summary>文字列から ID を作る。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EuId From(ReadOnlySpan<char> text, ulong seed = 0UL) => new(Hash(text, seed));

    /// <summary>整数から ID を作る。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EuId From(int value, ulong seed = 0UL) => new(Hash(value, seed));

    /// <summary>
    /// ラベルから ID と「実際に表示する文字列」を切り出す。
    /// <c>##</c> / <c>###</c> の扱いは <see cref="EuId"/> のドキュメントを参照。
    /// </summary>
    /// <param name="label">ウィジェットに渡されたラベル。</param>
    /// <param name="seed">ID スタックの現在値。</param>
    /// <param name="display">表示に使う部分文字列 (ラベルの切り出しなのでアロケーションは起きない)。</param>
    public static EuId FromLabel(ReadOnlySpan<char> label, ulong seed, out ReadOnlySpan<char> display)
    {
        // "###" は ID 部分のみを使う (表示文字列が変わっても ID が変わらない)
        var tripleIndex = label.IndexOf("###", StringComparison.Ordinal);
        if (tripleIndex >= 0)
        {
            display = label[..tripleIndex];
            return new EuId(Hash(label[(tripleIndex + 3)..], seed));
        }

        // "##" は表示だけ切り詰め、ID はラベル全体から作る
        var doubleIndex = label.IndexOf("##", StringComparison.Ordinal);
        if (doubleIndex >= 0)
        {
            display = label[..doubleIndex];
            return new EuId(Hash(label, seed));
        }

        display = label;
        return new EuId(Hash(label, seed));
    }

    /// <summary>この ID をシードにして子 ID を作る。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EuId Child(ReadOnlySpan<char> text) => new(Hash(text, this.Value));

    /// <summary>この ID をシードにして子 ID を作る。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EuId Child(int value) => new(Hash(value, this.Value));

    public override string ToString() => $"EuId(0x{this.Value:X16})";
}
