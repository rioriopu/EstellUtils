using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Render;

/// <summary>
/// テキストの計測と描画。
/// </summary>
/// <remarks>
/// <c>ImGui.Text</c> は使わず、<c>ImDrawList.AddText</c> へ直接描画する。
/// ImGui のカーソルを進めないため、配置は本ライブラリのレイアウトが完全に制御できる。
/// </remarks>
public static class TextPainter
{
    /// <summary>省略記号。</summary>
    private const string Ellipsis = "…";

    /// <summary>切り詰め用の作業バッファ。毎フレームのアロケーションを避けるため使い回す。</summary>
    [ThreadStatic]
    private static char[]? truncationBuffer;

    /// <summary>計測結果のキャッシュ。これを超えたら丸ごと捨てる。</summary>
    private const int MeasureCacheLimit = 1024;

    private static readonly Dictionary<MeasureKey, Vector2> MeasureCache = new(256);

    /// <summary>現在のフォントでの行の高さ。</summary>
    public static float LineHeight => ImGui.GetTextLineHeight();

    /// <summary>行間を含む行の高さ。</summary>
    public static float LineHeightWithSpacing => ImGui.GetTextLineHeightWithSpacing();

    /// <summary>現在のフォントサイズ。</summary>
    public static float FontSize => ImGui.GetFontSize();

    /// <summary>
    /// テキストの描画サイズを計測する。
    /// </summary>
    /// <remarks>
    /// 計測は UTF-8 への変換とグリフの走査を伴うため、同じ文字列を毎フレーム測り直すと
    /// 項目の多い画面では無視できないコストになる。結果はフォントごとにキャッシュする。
    /// </remarks>
    public static Vector2 Measure(ReadOnlySpan<char> text)
        => text.IsEmpty ? Vector2.Zero : MeasureCached(text, -1f);

    /// <summary>折り返しありでテキストの描画サイズを計測する。</summary>
    public static Vector2 Measure(ReadOnlySpan<char> text, float wrapWidth)
        => text.IsEmpty ? Vector2.Zero : MeasureCached(text, wrapWidth);

    /// <summary>キャッシュを見てから計測する。</summary>
    private static Vector2 MeasureCached(ReadOnlySpan<char> text, float wrapWidth)
    {
        var key = MeasureKey.For(text, wrapWidth);

        if (MeasureCache.TryGetValue(key, out var cached))
            return cached;

        var size = wrapWidth < 0f
            ? ImGui.CalcTextSize(text)
            : ImGui.CalcTextSize(text, false, wrapWidth);

        // 際限なく溜めても仕方がないので、一定数を超えたら捨てて作り直す
        if (MeasureCache.Count >= MeasureCacheLimit)
            MeasureCache.Clear();

        MeasureCache[key] = size;
        return size;
    }

    /// <summary>計測キャッシュを捨てる。フォントを作り直したときに呼ぶ。</summary>
    public static void ClearMeasureCache() => MeasureCache.Clear();

    /// <summary>
    /// 計測キャッシュの鍵。文字列そのものを持つとアロケーションが発生するため、
    /// ハッシュに加えて長さと両端の文字も混ぜて取り違えを防いでいる。
    /// </summary>
    private readonly record struct MeasureKey(
        int Hash, int Length, char First, char Last, float FontSize, float WrapWidth, nint Font)
    {
        public static unsafe MeasureKey For(ReadOnlySpan<char> text, float wrapWidth)
            => new(
                string.GetHashCode(text),
                text.Length,
                text[0],
                text[^1],
                ImGui.GetFontSize(),
                wrapWidth,
                (nint)ImGui.GetFont().Handle);
    }

    /// <summary>指定座標を左上としてテキストを描く。</summary>
    public static void Text(Vector2 position, uint color, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || (color >> 24) == 0)
            return;

        // 幅は測らないと分からないので、縦方向だけで隠れているかを判定する。
        // スクロール領域では大半がこれで省ける
        var clip = Painter.CurrentClip;
        if (position.Y > clip.Max.Y || position.Y + LineHeight < clip.Min.Y)
            return;

        Painter.DrawList.AddText(position, Painter.Tint(color), text);
    }

    /// <summary>フォントとサイズを指定してテキストを描く。</summary>
    public static void Text(ImFontPtr font, float fontSize, Vector2 position, uint color, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || (color >> 24) == 0)
            return;

        Painter.DrawList.AddText(font, fontSize, position, Painter.Tint(color), text);
    }

    /// <summary>幅で折り返してテキストを描く。</summary>
    public static void TextWrapped(Vector2 position, uint color, ReadOnlySpan<char> text, float wrapWidth)
    {
        if (text.IsEmpty || (color >> 24) == 0)
            return;

        Painter.DrawList.AddText(
            ImGui.GetFont(), ImGui.GetFontSize(), position, Painter.Tint(color), text, wrapWidth);
    }

    /// <summary>
    /// 矩形の中にテキストを配置して描く。
    /// </summary>
    /// <param name="rect">配置先。</param>
    /// <param name="color">文字色。</param>
    /// <param name="text">描画する文字列。</param>
    /// <param name="horizontal">横方向の寄せ。</param>
    /// <param name="vertical">縦方向の寄せ。</param>
    /// <param name="ellipsize">収まらないときに末尾を省略記号へ置き換えるか。</param>
    public static void TextIn(
        Rect rect, uint color, ReadOnlySpan<char> text,
        Align horizontal = Align.Start, Align vertical = Align.Center, bool ellipsize = true)
    {
        if (text.IsEmpty || rect.IsEmpty || (color >> 24) == 0 || !Painter.IsVisible(rect))
            return;

        var size = Measure(text);

        if (ellipsize && size.X > rect.Width)
        {
            var truncated = Truncate(text, rect.Width);
            size = Measure(truncated);
            var pos = PlaceText(rect, size, horizontal, vertical);
            Text(pos, color, truncated);
            return;
        }

        var position = PlaceText(rect, size, horizontal, vertical);
        Text(position, color, text);
    }

    /// <summary>矩形の中で折り返しながらテキストを描き、実際に使った高さを返す。</summary>
    public static float TextWrappedIn(Rect rect, uint color, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || rect.IsEmpty)
            return 0f;

        var size = Measure(text, rect.Width);
        TextWrapped(rect.Min, color, text, rect.Width);
        return size.Y;
    }

    /// <summary>
    /// 指定幅に収まるようテキストを切り詰め、末尾に省略記号を付ける。
    /// 収まる場合は元の文字列をそのまま返す。
    /// </summary>
    public static ReadOnlySpan<char> Truncate(ReadOnlySpan<char> text, float maxWidth)
    {
        if (text.IsEmpty || maxWidth <= 0f)
            return ReadOnlySpan<char>.Empty;

        if (Measure(text).X <= maxWidth)
            return text;

        var ellipsisWidth = Measure(Ellipsis).X;
        var available = maxWidth - ellipsisWidth;

        if (available <= 0f)
            return ReadOnlySpan<char>.Empty;

        // 収まる最大の文字数を二分探索で求める
        var low = 0;
        var high = text.Length;

        while (low < high)
        {
            var mid = (low + high + 1) / 2;

            // サロゲートペアの途中で切らない
            if (mid < text.Length && char.IsLowSurrogate(text[mid]))
                mid--;

            if (mid <= low)
                break;

            if (Measure(text[..mid]).X <= available)
                low = mid;
            else
                high = mid - 1;
        }

        if (low <= 0)
            return Ellipsis;

        var buffer = truncationBuffer;
        if (buffer is null || buffer.Length < low + Ellipsis.Length)
        {
            buffer = new char[Math.Max(256, (low + Ellipsis.Length) * 2)];
            truncationBuffer = buffer;
        }

        text[..low].CopyTo(buffer);
        Ellipsis.AsSpan().CopyTo(buffer.AsSpan(low));

        return buffer.AsSpan(0, low + Ellipsis.Length);
    }

    /// <summary>矩形内でのテキストの描画開始座標を求める。</summary>
    private static Vector2 PlaceText(Rect rect, Vector2 textSize, Align horizontal, Align vertical)
    {
        var x = horizontal switch
        {
            Align.Center => rect.Min.X + ((rect.Width - textSize.X) * 0.5f),
            Align.End => rect.Max.X - textSize.X,
            _ => rect.Min.X,
        };

        var y = vertical switch
        {
            Align.Center => rect.Min.Y + ((rect.Height - textSize.Y) * 0.5f),
            Align.End => rect.Max.Y - textSize.Y,
            _ => rect.Min.Y,
        };

        // 半端な座標だと文字がにじむため整数へ丸める
        return new Vector2(MathF.Round(x), MathF.Round(y));
    }
}
