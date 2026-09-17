using System;
using System.Numerics;

namespace EstellUtils.UI.Core;

/// <summary>
/// アニメーション用の補間ヘルパー。
/// </summary>
/// <remarks>
/// 「1 フレームあたり一定割合だけ寄せる」方式はフレームレートによって速度が変わってしまうため、
/// ここでは指数減衰 (<see cref="Approach(float, float, float, float)"/>) を基本にしている。
/// 60fps でも 144fps でも同じ体感速度になる。
/// </remarks>
public static class Anim
{
    /// <summary>ホバー遷移などに使う既定の追従速度。大きいほど速い。</summary>
    public const float DefaultSpeed = 14f;

    /// <summary>
    /// 現在値を目標値へ指数的に近づける。フレームレートに依存しない。
    /// </summary>
    /// <param name="current">現在値。</param>
    /// <param name="target">目標値。</param>
    /// <param name="speed">追従速度。大きいほど速く到達する。</param>
    /// <param name="deltaTime">前フレームからの経過秒数。</param>
    public static float Approach(float current, float target, float speed, float deltaTime)
    {
        if (deltaTime <= 0f || speed <= 0f)
            return target;

        var t = 1f - MathF.Exp(-speed * deltaTime);
        var next = current + ((target - current) * t);

        // 十分近づいたら吸着させる (いつまでも微小な差分が残って再描画され続けるのを防ぐ)
        return MathF.Abs(target - next) < 0.0005f ? target : next;
    }

    /// <summary>ベクトルを目標へ指数的に近づける。</summary>
    public static Vector2 Approach(Vector2 current, Vector2 target, float speed, float deltaTime)
        => new(Approach(current.X, target.X, speed, deltaTime),
               Approach(current.Y, target.Y, speed, deltaTime));

    /// <summary>色を目標へ指数的に近づける (RGBA を個別に補間)。</summary>
    public static Vector4 Approach(Vector4 current, Vector4 target, float speed, float deltaTime)
        => new(Approach(current.X, target.X, speed, deltaTime),
               Approach(current.Y, target.Y, speed, deltaTime),
               Approach(current.Z, target.Z, speed, deltaTime),
               Approach(current.W, target.W, speed, deltaTime));

    /// <summary>線形補間。</summary>
    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    /// <summary>線形補間。</summary>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + ((b - a) * t);

    /// <summary>線形補間。</summary>
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => a + ((b - a) * t);

    /// <summary>イージングを噛ませた補間。</summary>
    public static float Ease(float a, float b, float t, EaseKind kind)
        => Lerp(a, b, Easing.Apply(kind, t));

    /// <summary>イージングを噛ませた補間。</summary>
    public static Vector4 Ease(Vector4 a, Vector4 b, float t, EaseKind kind)
        => Lerp(a, b, Easing.Apply(kind, t));

    /// <summary>
    /// 値を 0〜1 の範囲へ正規化する。<paramref name="min"/> と <paramref name="max"/> が
    /// 同値のときは 0 を返す (ゼロ除算回避)。
    /// </summary>
    public static float InverseLerp(float min, float max, float value)
    {
        var range = max - min;
        return MathF.Abs(range) < float.Epsilon ? 0f : Math.Clamp((value - min) / range, 0f, 1f);
    }

    /// <summary>
    /// 0〜1 を往復する三角波。時間経過で点滅・脈動させたいときに使う。
    /// </summary>
    public static float PingPong(float time, float period)
    {
        if (period <= 0f)
            return 0f;

        var t = (time % period) / period;
        return t < 0.5f ? t * 2f : 2f - (t * 2f);
    }
}

/// <summary>
/// 目標値へ滑らかに追従する float。ウィジェット状態に埋め込んで使う。
/// </summary>
public struct AnimFloat
{
    /// <summary>現在値。</summary>
    public float Value;

    /// <summary>追従速度。0 以下なら <see cref="Anim.DefaultSpeed"/> を使う。</summary>
    public float Speed;

    public AnimFloat(float value, float speed = 0f)
    {
        this.Value = value;
        this.Speed = speed;
    }

    /// <summary>目標値へ 1 フレーム分近づけ、更新後の値を返す。</summary>
    public float Update(float target, float deltaTime)
    {
        var speed = this.Speed > 0f ? this.Speed : Anim.DefaultSpeed;
        this.Value = Anim.Approach(this.Value, target, speed, deltaTime);
        return this.Value;
    }

    /// <summary>アニメーションを飛ばして即座に値を設定する。</summary>
    public void Snap(float value) => this.Value = value;

    public static implicit operator float(AnimFloat a) => a.Value;
}
