using System;

namespace EstellUtils.UI.Core;

/// <summary>
/// 1 フレームぶんの描画統計。
/// </summary>
/// <remarks>
/// <para>
/// 重さの原因を実機で切り分けるための数値。immediate mode では毎フレーム全部を
/// 組み直すため、コストはほぼ「いくつ処理したか」で決まる。
/// 描かずに済ませた数 (<see cref="Culled"/>) が伸びているほど、うまく間引けている。
/// </para>
/// <para>
/// 数え上げ自体は加算だけなので、常に有効でも負荷にはならない。
/// </para>
/// </remarks>
public struct UiStats
{
    /// <summary>領域を確保した回数。おおよそのウィジェット数。</summary>
    public int Allocations;

    /// <summary>入力判定を行った回数。</summary>
    public int Interactions;

    /// <summary>文字の大きさを測った回数 (キャッシュに当たった分を含む)。</summary>
    public int TextMeasures;

    /// <summary>そのうち、実際に測り直した回数。</summary>
    public int TextMeasureMisses;

    /// <summary>クリップ範囲の外で、描画を省いた回数。</summary>
    public int Culled;

    /// <summary>描画命令を出した回数。</summary>
    public int DrawCalls;

    /// <summary>開いたレイアウトスコープの数。</summary>
    public int LayoutScopes;

    /// <summary>文字計測がキャッシュに当たった割合 (0〜1)。</summary>
    public readonly float TextCacheHitRate
        => this.TextMeasures == 0
            ? 1f
            : 1f - (this.TextMeasureMisses / (float)this.TextMeasures);

    /// <summary>数値をすべて 0 に戻す。</summary>
    public void Reset()
    {
        this.Allocations = 0;
        this.Interactions = 0;
        this.TextMeasures = 0;
        this.TextMeasureMisses = 0;
        this.Culled = 0;
        this.DrawCalls = 0;
        this.LayoutScopes = 0;
    }
}
