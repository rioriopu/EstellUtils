using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 1 行の中に色の違う断片を並べるウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>
    /// 色の違う断片を続けて描く。<c>TextColored</c> と <c>SameLine</c> の繰り返しを 1 行にする。
    /// <code>
    /// // 移行前
    /// ImGui.TextColored(gray, "状態: ");
    /// ImGui.SameLine();
    /// ImGui.TextColored(green, "動作中");
    ///
    /// // 移行後
    /// EUi.RichLabel("状態: ", new TextRun("動作中", EUi.Colors.Success));
    /// </code>
    /// </summary>
    /// <param name="parts">
    /// 並べる断片。文字列をそのまま渡すと標準の文字色になる。
    /// </param>
    /// <remarks>
    /// <para>
    /// <see cref="HStack"/> と違ってスコープを開かないので、既存の 1 行を 1 行へ置き換えられる。
    /// </para>
    /// <para>
    /// 幅に収まらない場合は折り返す。断片の途中では折り返さず、断片の切れ目で改行する。
    /// </para>
    /// </remarks>
    public static WidgetResult RichLabel(params ReadOnlySpan<TextRun> parts)
        => RichLabel(true, parts);

    /// <summary>
    /// 折り返しの有無を指定して、色の違う断片を続けて描く。
    /// </summary>
    /// <param name="wrap">幅に収まらないとき折り返すか。false だと 1 行に収める。</param>
    /// <param name="parts">並べる断片。</param>
    public static WidgetResult RichLabel(bool wrap, params ReadOnlySpan<TextRun> parts)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        if (parts.IsEmpty)
            return default;

        var lineHeight = TextPainter.LineHeight;
        // 列を宣言した行では列幅で折り返す。行の残り幅だと途中の列で突き抜ける
        var available = NextItemWidth;
        var spacing = 0f;

        // 何行になるかを先に数えて、必要な高さを確保する
        var rows = 1;
        var x = 0f;

        for (var i = 0; i < parts.Length; i++)
        {
            var width = TextPainter.Measure(parts[i].Text).X;

            if (wrap && x > 0f && x + width > available)
            {
                rows++;
                x = 0f;
            }

            x += width + spacing;
        }

        var rect = ctx.Allocate(SizeSpec.Fill, (lineHeight * rows) + (Metrics.SpacingXs * (rows - 1)));

        if (!Painter.IsVisible(rect))
            return MakeTextResult(ctx, rect);

        var cursor = rect.Min;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var width = TextPainter.Measure(part.Text).X;

            if (wrap && cursor.X > rect.Min.X && cursor.X + width > rect.Max.X)
            {
                cursor = new Vector2(rect.Min.X, cursor.Y + lineHeight + Metrics.SpacingXs);
            }

            var cell = Rect.FromSize(cursor, new Vector2(width, lineHeight));

            TextPainter.TextIn(
                cell,
                part.Color ?? Colors.Text,
                part.Text,
                Align.Start,
                Align.Center,
                ellipsize: !wrap);

            cursor = new Vector2(cursor.X + width, cursor.Y);
        }

        return MakeTextResult(ctx, rect);
    }
}

/// <summary>
/// <c>EUi.RichLabel</c> へ渡す、色の付いた文字の断片。
/// </summary>
/// <param name="Text">表示する文字。</param>
/// <param name="Color">文字色。省略するとテーマの標準色。</param>
/// <remarks>
/// 文字列からの暗黙変換があるので、色を変えない断片は文字列のまま渡せる。
/// <code>
/// EUi.RichLabel("残り ", new TextRun("3", EUi.Colors.Warning), " 件");
/// </code>
/// </remarks>
public readonly record struct TextRun(string Text, uint? Color = null)
{
    /// <summary>文字列をそのまま断片にする。色はテーマの標準色。</summary>
    public static implicit operator TextRun(string text) => new(text);

    /// <summary>状態色を指定して断片を作る。</summary>
    public static TextRun Of(string text, NoteKind kind) => new(text, EUi.NoteColor(kind));

    /// <summary>色を <see cref="Vector4"/> (RGBA, 0〜1) で指定して断片を作る。</summary>
    public static TextRun Of(string text, Vector4 color) => new(text, EuColor.FromVector(color));
}
