using System;

namespace EstellUtils.UI.Core;

/// <summary>イージングの種類。</summary>
public enum EaseKind
{
    /// <summary>等速。</summary>
    Linear,

    /// <summary>減速して止まる。UI の既定。押下 → 戻りなど大半の遷移に向く。</summary>
    OutQuad,

    /// <summary>加速して始まる。要素が退場するときに向く。</summary>
    InQuad,

    /// <summary>加速してから減速する。移動距離が大きいときに向く。</summary>
    InOutQuad,

    /// <summary>OutQuad より強く減速する。素早く反応して静かに止まる印象。</summary>
    OutCubic,

    /// <summary>InOutQuad より強い緩急。</summary>
    InOutCubic,

    /// <summary>ほぼ瞬時に目標へ寄り、最後だけ滑らかに止まる。ホバー表現に向く。</summary>
    OutExpo,

    /// <summary>行き過ぎてから戻る。ポップアップの出現などアクセントに使う。</summary>
    OutBack,
}

/// <summary>
/// イージング関数。入力・出力ともに 0.0〜1.0 を想定する。
/// </summary>
public static class Easing
{
    /// <summary>指定した種類のイージングを適用する。</summary>
    public static float Apply(EaseKind kind, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return kind switch
        {
            EaseKind.Linear => t,
            EaseKind.OutQuad => OutQuad(t),
            EaseKind.InQuad => InQuad(t),
            EaseKind.InOutQuad => InOutQuad(t),
            EaseKind.OutCubic => OutCubic(t),
            EaseKind.InOutCubic => InOutCubic(t),
            EaseKind.OutExpo => OutExpo(t),
            EaseKind.OutBack => OutBack(t),
            _ => t,
        };
    }

    public static float InQuad(float t) => t * t;

    public static float OutQuad(float t) => 1f - ((1f - t) * (1f - t));

    public static float InOutQuad(float t)
        => t < 0.5f ? 2f * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 2f) * 0.5f);

    public static float InCubic(float t) => t * t * t;

    public static float OutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);

    public static float InOutCubic(float t)
        => t < 0.5f ? 4f * t * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 3f) * 0.5f);

    public static float OutExpo(float t) => t >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);

    /// <summary>行き過ぎてから戻る。<paramref name="overshoot"/> で行き過ぎ量を調整する。</summary>
    public static float OutBack(float t, float overshoot = 1.70158f)
    {
        var c = overshoot + 1f;
        var p = t - 1f;
        return 1f + (c * p * p * p) + (overshoot * p * p);
    }
}
