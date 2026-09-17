using System;
using System.Globalization;
using System.Numerics;

namespace EstellUtils.UI.Render;

/// <summary>
/// 色の生成・変換ユーティリティ。
/// </summary>
/// <remarks>
/// ImGui の描画リストは色を <c>uint</c> (バイト順 ABGR、すなわち 0xAABBGGRR) で扱う。
/// テーマ側では扱いやすさを優先して <see cref="Vector4"/> (RGBA, 0〜1) を使うため、
/// 両者の変換をここへ集約している。
/// </remarks>
public static class EuColor
{
    /// <summary>完全な透明。</summary>
    public const uint Transparent = 0x00000000u;

    /// <summary>不透明な白。</summary>
    public const uint White = 0xFFFFFFFFu;

    /// <summary>不透明な黒。</summary>
    public const uint Black = 0xFF000000u;

    /// <summary>0〜1 の RGBA から描画用の色を作る。</summary>
    public static uint Rgba(float r, float g, float b, float a = 1f)
    {
        var ri = (uint)(Math.Clamp(r, 0f, 1f) * 255f + 0.5f);
        var gi = (uint)(Math.Clamp(g, 0f, 1f) * 255f + 0.5f);
        var bi = (uint)(Math.Clamp(b, 0f, 1f) * 255f + 0.5f);
        var ai = (uint)(Math.Clamp(a, 0f, 1f) * 255f + 0.5f);
        return (ai << 24) | (bi << 16) | (gi << 8) | ri;
    }

    /// <summary>0〜255 の RGBA から描画用の色を作る。</summary>
    public static uint Bytes(int r, int g, int b, int a = 255)
        => ((uint)(a & 0xFF) << 24) | ((uint)(b & 0xFF) << 16) | ((uint)(g & 0xFF) << 8) | (uint)(r & 0xFF);

    /// <summary>
    /// 16 進表記から色を作る。<c>"#RRGGBB"</c> / <c>"#RRGGBBAA"</c> / 先頭の <c>#</c> 省略に対応。
    /// </summary>
    public static uint Hex(ReadOnlySpan<char> hex, float alphaScale = 1f)
    {
        if (!hex.IsEmpty && hex[0] == '#')
            hex = hex[1..];

        if (hex.Length is not (6 or 8))
            return White;

        var r = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var a = hex.Length == 8
            ? byte.Parse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : (byte)255;

        return Bytes(r, g, b, (int)(a * Math.Clamp(alphaScale, 0f, 1f)));
    }

    /// <summary>0xRRGGBB 形式の整数とアルファから色を作る。テーマ定義で使いやすい形。</summary>
    public static uint Rgb(uint rrggbb, float alpha = 1f)
    {
        var r = (int)((rrggbb >> 16) & 0xFF);
        var g = (int)((rrggbb >> 8) & 0xFF);
        var b = (int)(rrggbb & 0xFF);
        return Bytes(r, g, b, (int)(Math.Clamp(alpha, 0f, 1f) * 255f + 0.5f));
    }

    /// <summary><see cref="Vector4"/> (RGBA) から描画用の色を作る。</summary>
    public static uint FromVector(Vector4 rgba) => Rgba(rgba.X, rgba.Y, rgba.Z, rgba.W);

    /// <summary>描画用の色を <see cref="Vector4"/> (RGBA) へ戻す。</summary>
    public static Vector4 ToVector(uint color)
        => new(
            (color & 0xFF) / 255f,
            ((color >> 8) & 0xFF) / 255f,
            ((color >> 16) & 0xFF) / 255f,
            ((color >> 24) & 0xFF) / 255f);

    /// <summary>アルファ成分 (0〜1)。</summary>
    public static float AlphaOf(uint color) => ((color >> 24) & 0xFF) / 255f;

    /// <summary>アルファを差し替える。</summary>
    public static uint WithAlpha(uint color, float alpha)
    {
        var a = (uint)(Math.Clamp(alpha, 0f, 1f) * 255f + 0.5f);
        return (color & 0x00FFFFFFu) | (a << 24);
    }

    /// <summary>アルファを倍率で調整する。無効表示のフェードなどに使う。</summary>
    public static uint ScaleAlpha(uint color, float scale)
        => WithAlpha(color, AlphaOf(color) * Math.Clamp(scale, 0f, 1f));

    /// <summary>2 色を線形補間する (アルファも含む)。</summary>
    public static uint Lerp(uint a, uint b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        var ar = a & 0xFF;
        var ag = (a >> 8) & 0xFF;
        var ab = (a >> 16) & 0xFF;
        var aa = (a >> 24) & 0xFF;

        var br = b & 0xFF;
        var bg = (b >> 8) & 0xFF;
        var bb = (b >> 16) & 0xFF;
        var ba = (b >> 24) & 0xFF;

        var r = (uint)(ar + ((br - (float)ar) * t) + 0.5f);
        var g = (uint)(ag + ((bg - (float)ag) * t) + 0.5f);
        var bl = (uint)(ab + ((bb - (float)ab) * t) + 0.5f);
        var al = (uint)(aa + ((ba - (float)aa) * t) + 0.5f);

        return (al << 24) | (bl << 16) | (g << 8) | r;
    }

    /// <summary>明るくする (白へ寄せる)。</summary>
    public static uint Lighten(uint color, float amount)
        => Lerp(color, WithAlpha(White, AlphaOf(color)), amount);

    /// <summary>暗くする (黒へ寄せる)。</summary>
    public static uint Darken(uint color, float amount)
        => Lerp(color, WithAlpha(Black, AlphaOf(color)), amount);

    /// <summary>
    /// RGB 各成分に倍率を掛ける。アルファは維持する。
    /// ホバー時に少し明るくするといった調整に使う。
    /// </summary>
    public static uint Multiply(uint color, float factor)
    {
        var r = Math.Clamp((color & 0xFF) * factor, 0f, 255f);
        var g = Math.Clamp(((color >> 8) & 0xFF) * factor, 0f, 255f);
        var b = Math.Clamp(((color >> 16) & 0xFF) * factor, 0f, 255f);
        var a = color & 0xFF000000u;
        return a | ((uint)(b + 0.5f) << 16) | ((uint)(g + 0.5f) << 8) | (uint)(r + 0.5f);
    }

    /// <summary>知覚的な明るさ (0〜1)。文字色を白/黒どちらにするかの判定に使う。</summary>
    public static float Luminance(uint color)
    {
        var r = (color & 0xFF) / 255f;
        var g = ((color >> 8) & 0xFF) / 255f;
        var b = ((color >> 16) & 0xFF) / 255f;
        return (0.2126f * r) + (0.7152f * g) + (0.0722f * b);
    }

    /// <summary>背景色の上で読みやすい文字色 (白か黒) を返す。</summary>
    public static uint ReadableOn(uint background)
        => Luminance(background) > 0.55f ? Black : White;

    /// <summary>HSV から色を作る。色相は 0〜1。</summary>
    public static uint FromHsv(float h, float s, float v, float a = 1f)
    {
        if (s <= 0f)
            return Rgba(v, v, v, a);

        h = (h - MathF.Floor(h)) * 6f;
        var i = (int)h;
        var f = h - i;
        var p = v * (1f - s);
        var q = v * (1f - (s * f));
        var t = v * (1f - (s * (1f - f)));

        return i switch
        {
            0 => Rgba(v, t, p, a),
            1 => Rgba(q, v, p, a),
            2 => Rgba(p, v, t, a),
            3 => Rgba(p, q, v, a),
            4 => Rgba(t, p, v, a),
            _ => Rgba(v, p, q, a),
        };
    }

    /// <summary>色を HSV へ分解する。</summary>
    public static (float H, float S, float V) ToHsv(uint color)
    {
        var r = (color & 0xFF) / 255f;
        var g = ((color >> 8) & 0xFF) / 255f;
        var b = ((color >> 16) & 0xFF) / 255f;

        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;

        if (delta < 1e-5f)
            return (0f, 0f, max);

        var h = 0f;
        if (max == r)
            h = (g - b) / delta % 6f;
        else if (max == g)
            h = ((b - r) / delta) + 2f;
        else
            h = ((r - g) / delta) + 4f;

        h /= 6f;
        if (h < 0f)
            h += 1f;

        return (h, max <= 0f ? 0f : delta / max, max);
    }
}
