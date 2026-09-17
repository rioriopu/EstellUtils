using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Render;

/// <summary>角丸を適用する角の指定。</summary>
[Flags]
public enum Corners
{
    None = 0,
    TopLeft = 1 << 0,
    TopRight = 1 << 1,
    BottomLeft = 1 << 2,
    BottomRight = 1 << 3,

    Top = TopLeft | TopRight,
    Bottom = BottomLeft | BottomRight,
    Left = TopLeft | BottomLeft,
    Right = TopRight | BottomRight,
    All = Top | Bottom,
}

/// <summary>矢印・シェブロンの向き。</summary>
public enum Direction
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// 描画プリミティブ。すべて <c>ImDrawList</c> への直接描画で、ImGui の標準ウィジェットは使わない。
/// </summary>
/// <remarks>
/// ウィジェットの見た目はすべてこの層の組み合わせで作る。独自ウィジェットを書く利用者も
/// 同じ API を使うため、ライブラリ内部専用の描画手段は存在しない。
/// </remarks>
public static class Painter
{
    private static readonly List<ImDrawListPtr> DrawListStack = new(4);

    /// <summary>
    /// 描画先。既定では現在のウィンドウの描画リスト。
    /// <see cref="UseDrawList"/> で一時的に差し替えられる。
    /// </summary>
    public static ImDrawListPtr DrawList
        => DrawListStack.Count > 0 ? DrawListStack[^1] : ImGui.GetWindowDrawList();

    /// <summary>ウィンドウより手前 (すべての UI の上) に描く描画リスト。ツールチップなどに使う。</summary>
    public static ImDrawListPtr ForegroundDrawList => ImGui.GetForegroundDrawList();

    /// <summary>
    /// 描画先を一時的に差し替える。<c>using</c> で元へ戻る。
    /// ウィジェットの描画コードをそのまま別のレイヤーへ出したいときに使う。
    /// </summary>
    public static DrawListScope UseDrawList(ImDrawListPtr drawList)
    {
        DrawListStack.Add(drawList);
        return new DrawListScope();
    }

    /// <summary>描画先を最前面レイヤーへ切り替える。</summary>
    public static DrawListScope UseForeground() => UseDrawList(ImGui.GetForegroundDrawList());

    /// <summary>差し替えた描画先を 1 段戻す。</summary>
    internal static void PopDrawList()
    {
        if (DrawListStack.Count > 0)
            DrawListStack.RemoveAt(DrawListStack.Count - 1);
    }

    /// <summary>描画先スタックを空にする。フレーム境界での保険。</summary>
    internal static void ResetDrawListStack() => DrawListStack.Clear();

    // ── 矩形 ──────────────────────────────────────────────────

    /// <summary>塗りつぶし矩形。</summary>
    public static void Rect(Rect rect, uint color, float rounding = 0f, Corners corners = Corners.All)
    {
        if (rect.IsEmpty || (color >> 24) == 0)
            return;

        DrawList.AddRectFilled(rect.Min, rect.Max, color, rounding, ToDrawFlags(rounding, corners));
    }

    /// <summary>枠線のみの矩形。</summary>
    public static void RectOutline(
        Rect rect, uint color, float thickness = 1f, float rounding = 0f, Corners corners = Corners.All)
    {
        if (rect.IsEmpty || (color >> 24) == 0 || thickness <= 0f)
            return;

        DrawList.AddRect(rect.Min, rect.Max, color, rounding, ToDrawFlags(rounding, corners), thickness);
    }

    /// <summary>
    /// 縦方向のグラデーション矩形。角丸にも対応する。
    /// </summary>
    public static void RectGradientV(
        Rect rect, uint top, uint bottom, float rounding = 0f, Corners corners = Corners.All)
    {
        if (rect.IsEmpty)
            return;

        if (rounding <= 0f)
        {
            DrawList.AddRectFilledMultiColor(rect.Min, rect.Max, top, top, bottom, bottom);
            return;
        }

        ShadeRounded(rect, top, bottom, rounding, corners, vertical: true);
    }

    /// <summary>横方向のグラデーション矩形。角丸にも対応する。</summary>
    public static void RectGradientH(
        Rect rect, uint left, uint right, float rounding = 0f, Corners corners = Corners.All)
    {
        if (rect.IsEmpty)
            return;

        if (rounding <= 0f)
        {
            DrawList.AddRectFilledMultiColor(rect.Min, rect.Max, left, right, right, left);
            return;
        }

        ShadeRounded(rect, left, right, rounding, corners, vertical: false);
    }

    /// <summary>
    /// 角丸矩形を塗ってから、生成された頂点の色をグラデーションで塗り替える。
    /// ImGui 本体の <c>ShadeVertsLinearColorGradientKeepAlpha</c> と同じ考え方。
    /// </summary>
    private static unsafe void ShadeRounded(
        Rect rect, uint from, uint to, float rounding, Corners corners, bool vertical)
    {
        var dl = DrawList;
        var native = dl.Handle;
        var vtxStart = native->VtxBuffer.Size;

        dl.AddRectFilled(rect.Min, rect.Max, from, rounding, ToDrawFlags(rounding, corners));

        var vtxEnd = native->VtxBuffer.Size;
        if (vtxEnd <= vtxStart)
            return;

        var p0 = rect.Min;
        var p1 = vertical ? new Vector2(rect.Min.X, rect.Max.Y) : new Vector2(rect.Max.X, rect.Min.Y);
        var extent = p1 - p0;
        var invLengthSq = 1f / extent.LengthSquared();

        var verts = native->VtxBuffer.Data;
        for (var i = vtxStart; i < vtxEnd; i++)
        {
            var v = verts + i;
            var d = Vector2.Dot(v->Pos - p0, extent) * invLengthSq;
            v->Col = EuColor.Lerp(from, to, d);
        }
    }

    /// <summary>
    /// 矩形の外側に落とす影。半透明の角丸矩形を段階的に広げながら重ねて表現する。
    /// </summary>
    /// <param name="rect">影を落とす対象の矩形。</param>
    /// <param name="color">影の色 (アルファ込み)。</param>
    /// <param name="size">にじみの幅 (ピクセル)。</param>
    /// <param name="rounding">対象の角丸半径。</param>
    /// <param name="offset">影のずらし量。</param>
    public static void Shadow(
        Rect rect, uint color, float size, float rounding = 0f, Vector2 offset = default)
    {
        if (rect.IsEmpty || size <= 0f || (color >> 24) == 0)
            return;

        var baseAlpha = EuColor.AlphaOf(color);
        var steps = Math.Clamp((int)MathF.Ceiling(size), 1, 24);
        var shifted = rect.Offset(offset);

        for (var i = steps; i >= 1; i--)
        {
            var t = i / (float)steps;
            var grow = size * t;

            // 外側ほど急速に薄くする (ガウスぼかしの近似)
            var alpha = baseAlpha * (1f - t) * (1f - t);
            if (alpha <= 0.002f)
                continue;

            Rect(shifted.Expand(grow), EuColor.WithAlpha(color, alpha), rounding + grow);
        }
    }

    /// <summary>矩形の内側に落とす影 (くぼんで見せる)。</summary>
    public static void InnerShadow(Rect rect, uint color, float size, float rounding = 0f)
    {
        if (rect.IsEmpty || size <= 0f)
            return;

        var baseAlpha = EuColor.AlphaOf(color);
        var steps = Math.Clamp((int)MathF.Ceiling(size), 1, 16);

        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var alpha = baseAlpha * (1f - t) * (1f - t);
            if (alpha <= 0.002f)
                continue;

            RectOutline(
                rect.Shrink(i - 0.5f), EuColor.WithAlpha(color, alpha),
                1f, MathF.Max(0f, rounding - i));
        }
    }

    // ── 線・円・多角形 ────────────────────────────────────────

    /// <summary>直線。</summary>
    public static void Line(Vector2 a, Vector2 b, uint color, float thickness = 1f)
    {
        if ((color >> 24) == 0 || thickness <= 0f)
            return;

        DrawList.AddLine(a, b, color, thickness);
    }

    /// <summary>水平線。1px 線がぼやけないよう座標を半ピクセルに合わせる。</summary>
    public static void HLine(float x0, float x1, float y, uint color, float thickness = 1f)
    {
        var yy = MathF.Round(y) + (thickness % 2f == 1f ? 0.5f : 0f);
        Line(new Vector2(x0, yy), new Vector2(x1, yy), color, thickness);
    }

    /// <summary>垂直線。</summary>
    public static void VLine(float x, float y0, float y1, uint color, float thickness = 1f)
    {
        var xx = MathF.Round(x) + (thickness % 2f == 1f ? 0.5f : 0f);
        Line(new Vector2(xx, y0), new Vector2(xx, y1), color, thickness);
    }

    /// <summary>塗りつぶし円。</summary>
    public static void Circle(Vector2 center, float radius, uint color, int segments = 0)
    {
        if (radius <= 0f || (color >> 24) == 0)
            return;

        DrawList.AddCircleFilled(center, radius, color, segments);
    }

    /// <summary>円の輪郭。</summary>
    public static void CircleOutline(
        Vector2 center, float radius, uint color, float thickness = 1f, int segments = 0)
    {
        if (radius <= 0f || (color >> 24) == 0)
            return;

        DrawList.AddCircle(center, radius, color, segments, thickness);
    }

    /// <summary>円弧。角度はラジアン、時計回り (画面座標系のため)。</summary>
    public static void Arc(
        Vector2 center, float radius, float fromRad, float toRad, uint color, float thickness = 1f)
    {
        if (radius <= 0f || (color >> 24) == 0)
            return;

        var dl = DrawList;
        dl.PathArcTo(center, radius, fromRad, toRad);
        dl.PathStroke(color, ImDrawFlags.None, thickness);
    }

    /// <summary>ドーナツ状のリング。進捗表示などに使う。</summary>
    public static void Ring(
        Vector2 center, float innerRadius, float outerRadius, uint color,
        float fromRad = 0f, float toRad = MathF.PI * 2f)
    {
        if (outerRadius <= innerRadius || (color >> 24) == 0)
            return;

        var dl = DrawList;
        dl.PathArcTo(center, outerRadius, fromRad, toRad);
        dl.PathArcTo(center, innerRadius, toRad, fromRad);
        dl.PathFillConvex(color);
    }

    /// <summary>塗りつぶし三角形。</summary>
    public static void Triangle(Vector2 a, Vector2 b, Vector2 c, uint color)
    {
        if ((color >> 24) == 0)
            return;

        DrawList.AddTriangleFilled(a, b, c, color);
    }

    /// <summary>
    /// 矩形の中央に「く」の字 (シェブロン) を描く。折りたたみ・コンボボックスの矢印に使う。
    /// </summary>
    public static void Chevron(Rect area, Direction direction, uint color, float thickness = 2f)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.28f;
        var dl = DrawList;

        Vector2 p0, p1, p2;
        switch (direction)
        {
            case Direction.Down:
                p0 = new Vector2(c.X - s, c.Y - (s * 0.5f));
                p1 = new Vector2(c.X, c.Y + (s * 0.5f));
                p2 = new Vector2(c.X + s, c.Y - (s * 0.5f));
                break;
            case Direction.Up:
                p0 = new Vector2(c.X - s, c.Y + (s * 0.5f));
                p1 = new Vector2(c.X, c.Y - (s * 0.5f));
                p2 = new Vector2(c.X + s, c.Y + (s * 0.5f));
                break;
            case Direction.Left:
                p0 = new Vector2(c.X + (s * 0.5f), c.Y - s);
                p1 = new Vector2(c.X - (s * 0.5f), c.Y);
                p2 = new Vector2(c.X + (s * 0.5f), c.Y + s);
                break;
            default:
                p0 = new Vector2(c.X - (s * 0.5f), c.Y - s);
                p1 = new Vector2(c.X + (s * 0.5f), c.Y);
                p2 = new Vector2(c.X - (s * 0.5f), c.Y + s);
                break;
        }

        dl.PathLineTo(p0);
        dl.PathLineTo(p1);
        dl.PathLineTo(p2);
        dl.PathStroke(color, ImDrawFlags.None, thickness);
    }

    /// <summary>
    /// 矩形の中央にチェックマークを描く。<paramref name="progress"/> で描画途中を表現でき、
    /// チェックボックスの ON アニメーションに使う。
    /// </summary>
    public static void Check(Rect area, uint color, float thickness = 2f, float progress = 1f)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        if (progress <= 0f)
            return;

        var size = MathF.Min(area.Width, area.Height);
        var c = area.Center;

        // チェックマークを 2 本の線分で構成する
        var a = c + new Vector2(-size * 0.26f, size * 0.02f);
        var b = c + new Vector2(-size * 0.07f, size * 0.20f);
        var d = c + new Vector2(size * 0.27f, -size * 0.20f);

        // 線分の長さ比で進捗を配分する
        var len1 = (b - a).Length();
        var len2 = (d - b).Length();
        var total = len1 + len2;
        var drawn = total * progress;

        var dl = DrawList;
        dl.PathLineTo(a);

        if (drawn <= len1)
        {
            dl.PathLineTo(a + ((b - a) * (drawn / len1)));
        }
        else
        {
            dl.PathLineTo(b);
            dl.PathLineTo(b + ((d - b) * ((drawn - len1) / len2)));
        }

        dl.PathStroke(color, ImDrawFlags.None, thickness);
    }

    // ── 画像 ──────────────────────────────────────────────────

    /// <summary>テクスチャを矩形いっぱいに描く。</summary>
    public static void Image(
        ImTextureID texture, Rect rect, uint tint, Vector2 uv0 = default, Vector2 uv1 = default)
    {
        if (rect.IsEmpty)
            return;

        if (uv1 == default)
            uv1 = Vector2.One;

        DrawList.AddImage(texture, rect.Min, rect.Max, uv0, uv1, tint);
    }

    /// <summary>
    /// 9 分割 (ナインスライス) でテクスチャを描く。四隅を引き伸ばさずに枠を拡大できる。
    /// ゲーム本体の UI テクスチャを流用する際に使う。
    /// </summary>
    /// <param name="texture">元テクスチャ。</param>
    /// <param name="rect">描画先の矩形。</param>
    /// <param name="border">四辺の固定幅 (テクスチャ上のピクセル)。</param>
    /// <param name="textureSize">テクスチャ全体のサイズ (ピクセル)。</param>
    /// <param name="tint">乗算色。</param>
    public static void NineSlice(
        ImTextureID texture, Rect rect, EdgeInsets border, Vector2 textureSize, uint tint)
    {
        if (rect.IsEmpty || textureSize.X <= 0f || textureSize.Y <= 0f)
            return;

        // 描画先が小さすぎて枠が重なる場合は縮める
        var scaleX = MathF.Min(1f, rect.Width / MathF.Max(1f, border.TotalHorizontal));
        var scaleY = MathF.Min(1f, rect.Height / MathF.Max(1f, border.TotalVertical));

        Span<float> xs =
        [
            rect.Min.X,
            rect.Min.X + (border.Left * scaleX),
            rect.Max.X - (border.Right * scaleX),
            rect.Max.X,
        ];
        Span<float> ys =
        [
            rect.Min.Y,
            rect.Min.Y + (border.Top * scaleY),
            rect.Max.Y - (border.Bottom * scaleY),
            rect.Max.Y,
        ];
        Span<float> us =
        [
            0f,
            border.Left / textureSize.X,
            1f - (border.Right / textureSize.X),
            1f,
        ];
        Span<float> vs =
        [
            0f,
            border.Top / textureSize.Y,
            1f - (border.Bottom / textureSize.Y),
            1f,
        ];

        var dl = DrawList;
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var x0 = xs[col];
                var x1 = xs[col + 1];
                var y0 = ys[row];
                var y1 = ys[row + 1];

                if (x1 - x0 <= 0.01f || y1 - y0 <= 0.01f)
                    continue;

                dl.AddImage(
                    texture,
                    new Vector2(x0, y0), new Vector2(x1, y1),
                    new Vector2(us[col], vs[row]), new Vector2(us[col + 1], vs[row + 1]),
                    tint);
            }
        }
    }

    // ── クリッピング ──────────────────────────────────────────

    /// <summary><c>using</c> で解除できるクリップ領域。</summary>
    public static ClipScope Clip(Rect rect, bool intersectWithCurrent = true)
    {
        var dl = DrawList;
        dl.PushClipRect(rect.Min, rect.Max, intersectWithCurrent);
        return new ClipScope(dl);
    }

    // ── 補助 ──────────────────────────────────────────────────

    /// <summary>独自の角指定を ImGui の描画フラグへ変換する。</summary>
    public static ImDrawFlags ToDrawFlags(float rounding, Corners corners)
    {
        if (rounding <= 0f || corners == Corners.None)
            return ImDrawFlags.RoundCornersNone;

        if (corners == Corners.All)
            return ImDrawFlags.RoundCornersAll;

        var flags = ImDrawFlags.None;
        if ((corners & Corners.TopLeft) != 0)
            flags |= ImDrawFlags.RoundCornersTopLeft;
        if ((corners & Corners.TopRight) != 0)
            flags |= ImDrawFlags.RoundCornersTopRight;
        if ((corners & Corners.BottomLeft) != 0)
            flags |= ImDrawFlags.RoundCornersBottomLeft;
        if ((corners & Corners.BottomRight) != 0)
            flags |= ImDrawFlags.RoundCornersBottomRight;

        return flags;
    }
}

/// <summary><c>using</c> でクリップ領域を解除するスコープ。</summary>
public readonly struct ClipScope : IDisposable
{
    private readonly ImDrawListPtr drawList;

    internal ClipScope(ImDrawListPtr drawList) => this.drawList = drawList;

    /// <inheritdoc/>
    public void Dispose() => this.drawList.PopClipRect();
}

/// <summary><c>using</c> で描画先を元へ戻すスコープ。</summary>
public readonly struct DrawListScope : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => Painter.PopDrawList();
}
