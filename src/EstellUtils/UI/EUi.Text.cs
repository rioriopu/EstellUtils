using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// テキスト系ウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>ウィジェットの描画担当。テーマごとに差し替えられる。</summary>
    public static IWidgetPainter WidgetPainter => ThemeManager.Current.Painter;

    /// <summary>
    /// 1 行のテキストを表示する。
    /// </summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="color">文字色。省略するとテーマの標準色。</param>
    /// <param name="align">横方向の寄せ。</param>
    public static WidgetResult Label(ReadOnlySpan<char> text, uint? color = null, Align align = Align.Start)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var size = TextPainter.Measure(text);
        var width = align == Align.Start ? size.X : AvailableWidth;
        var rect = ctx.Allocate(new Vector2(width, MathF.Max(size.Y, TextPainter.LineHeight)));

        TextPainter.TextIn(rect, color ?? Colors.Text, text, align, Align.Center, ellipsize: true);

        return MakeTextResult(ctx, rect);
    }

    /// <summary>補足説明用の控えめな色のテキスト。</summary>
    public static WidgetResult Muted(ReadOnlySpan<char> text, Align align = Align.Start)
        => Label(text, Colors.TextMuted, align);

    /// <summary>
    /// 幅に合わせて折り返すテキスト。長い説明文に使う。
    /// </summary>
    public static WidgetResult Paragraph(ReadOnlySpan<char> text, uint? color = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var width = AvailableWidth;
        var size = TextPainter.Measure(text, width);
        var rect = ctx.Allocate(new Vector2(width, size.Y));

        TextPainter.TextWrapped(rect.Min, color ?? Colors.Text, text, width);

        return MakeTextResult(ctx, rect);
    }

    /// <summary>状態色付きの折り返しテキスト。注意書きなどに使う。</summary>
    public static WidgetResult Note(ReadOnlySpan<char> text, NoteKind kind = NoteKind.Info)
    {
        var color = kind switch
        {
            NoteKind.Success => Colors.Success,
            NoteKind.Warning => Colors.Warning,
            NoteKind.Danger => Colors.Danger,
            _ => Colors.Info,
        };

        return Paragraph(text, color);
    }

    /// <summary>
    /// 見出し。テーマの見出し色と大きめのフォントで表示する。
    /// </summary>
    public static WidgetResult Heading(ReadOnlySpan<char> text, uint? color = null)
    {
        using var font = PushFont(FontRole.Large);
        return Label(text, color ?? Colors.TextHeading);
    }

    /// <summary>
    /// 区切り線。ラベルを与えると線の途中に文字を挟む。
    /// </summary>
    public static void Separator(ReadOnlySpan<char> label = default)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var height = label.IsEmpty
            ? MathF.Max(Metrics.SpacingMd, Metrics.SeparatorThickness)
            : TextPainter.LineHeight;

        var rect = ctx.Allocate(SizeSpec.Fill, height);
        WidgetPainter.DrawSeparator(rect, label);
    }

    /// <summary>
    /// 箇条書きの 1 項目。行頭に点を打つ。
    /// </summary>
    public static WidgetResult Bullet(ReadOnlySpan<char> text, uint? color = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var indent = Metrics.SpacingMd;
        var width = MathF.Max(0f, AvailableWidth - indent);
        var size = TextPainter.Measure(text, width);
        var rect = ctx.Allocate(new Vector2(AvailableWidth, size.Y));

        var dotCenter = new Vector2(
            rect.Min.X + (indent * 0.5f),
            rect.Min.Y + (TextPainter.LineHeight * 0.5f));

        Painter.Circle(dotCenter, 2f, color ?? Colors.TextMuted);
        TextPainter.TextWrapped(
            new Vector2(rect.Min.X + indent, rect.Min.Y), color ?? Colors.Text, text, width);

        return MakeTextResult(ctx, rect);
    }

    /// <summary>ID を持たないテキスト系ウィジェットの戻り値を組み立てる。</summary>
    private static WidgetResult MakeTextResult(UiContext ctx, Rect rect)
    {
        var duration = ctx.TrackHover(rect);

        return new WidgetResult
        {
            Rect = rect,
            Hovered = duration > 0f,
            HoveredDuration = duration,
        };
    }
}

/// <summary>注意書きの種類。</summary>
public enum NoteKind
{
    /// <summary>情報。</summary>
    Info,

    /// <summary>成功。</summary>
    Success,

    /// <summary>注意。</summary>
    Warning,

    /// <summary>危険。</summary>
    Danger,
}
