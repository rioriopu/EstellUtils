using System;
using System.Numerics;

using Dalamud.Interface;

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
    /// <summary>注記ボックスの左端に引く色帯の幅。</summary>
    private const float NoteAccentBarWidth = 3f;

    /// <summary>ウィジェットの描画担当。テーマごとに差し替えられる。</summary>
    public static IWidgetPainter WidgetPainter => ThemeManager.Current.Painter;

    /// <summary>
    /// 1 行のテキストを表示する。
    /// </summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="color">文字色。省略するとテーマの標準色。</param>
    /// <param name="align">横方向の寄せ。</param>
    /// <param name="ellipsize">
    /// 幅に収まらないとき、末尾を省略記号にするか。
    /// false にすると収まらない分がはみ出すので、レイアウトの不足に気づきやすい。
    /// </param>
    /// <param name="tipWhenTruncated">
    /// 省略したときに、全文をツールチップで見せるか。
    /// </param>
    /// <remarks>
    /// 省略された場合は戻り値の <see cref="WidgetResult.Truncated"/> が立つ。
    /// 黙って切られて気づけない、ということがないようにしてある。
    /// </remarks>
    public static WidgetResult Label(
        ReadOnlySpan<char> text, uint? color = null, Align align = Align.Start,
        bool ellipsize = true, bool tipWhenTruncated = true)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var lineHeight = TextPainter.LineHeight;

        // 画面の外に出ている行は、計測すらせず領域だけ確保して終える。
        // 長い一覧をスクロールさせたときの負荷はこれでほぼ消える
        if (!IsRowVisible(ctx, lineHeight))
            return new WidgetResult { Rect = ctx.Allocate(SizeSpec.Fill, lineHeight) };

        var size = TextPainter.Measure(text);

        // 列が宣言された行の中では、ここで渡した幅より列幅が優先される
        var width = align == Align.Start ? SizeSpec.Px(size.X) : SizeSpec.Fill;
        var rect = ctx.Allocate(width, MathF.Max(size.Y, lineHeight));

        // 確保できた幅に収まらなければ切られる。1px の丸め差では立てない
        var truncated = size.X > rect.Width + 1f;

        TextPainter.TextIn(rect, color ?? Colors.Text, text, align, Align.Center, ellipsize);

        var result = MakeTextResult(ctx, rect) with { Truncated = truncated };

        if (truncated && ellipsize && tipWhenTruncated)
            result.Tip(text);

        return result;
    }

    /// <summary>次に配置される行が、クリップ範囲に入っているか。</summary>
    private static bool IsRowVisible(UiContext ctx, float height)
    {
        var clip = Painter.CurrentClip;
        var top = ctx.Layout.AvailableRect.Min.Y;

        return top <= clip.Max.Y && top + height >= clip.Min.Y;
    }

    /// <summary>
    /// 補足説明用の控えめな色のテキスト。
    /// </summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="align">横方向の寄せ。<paramref name="wrap"/> が true のときは効かない。</param>
    /// <param name="wrap">
    /// 幅に合わせて折り返すか。既定は 1 行で、収まらない分は末尾を省略記号にする。
    /// </param>
    /// <remarks>
    /// 補足説明には長い文が来やすい。既定のままだと黙って切られたように見えるので
    /// (実際には省略記号が付き、全文はツールチップで読める)、
    /// 説明文を置くときは <paramref name="wrap"/> を true にする。
    /// </remarks>
    public static WidgetResult Muted(
        ReadOnlySpan<char> text, Align align = Align.Start, bool wrap = false)
        => wrap
            ? Paragraph(text, Colors.TextMuted)
            : Label(text, Colors.TextMuted, align);

    /// <summary>
    /// 補足説明用の、幅に合わせて折り返すテキスト。<c>Muted(text, wrap: true)</c> と同じ。
    /// </summary>
    public static WidgetResult MutedParagraph(ReadOnlySpan<char> text)
        => Paragraph(text, Colors.TextMuted);

    /// <summary>色を <see cref="Vector4"/> (RGBA, 0〜1) で指定する版。</summary>
    public static WidgetResult Label(ReadOnlySpan<char> text, Vector4 color, Align align = Align.Start)
        => Label(text, EuColor.FromVector(color), align);

    // ── 色付きテキスト ────────────────────────────────────────
    //
    // ImGui からの移行で真っ先に探すことになる名前なので、
    // Label / Paragraph への別名として用意しておく。

    /// <summary>
    /// 色を指定した 1 行テキスト。<c>ImGui.TextColored</c> の置き換え。
    /// </summary>
    /// <remarks>
    /// 枠や地は付かない。囲みたい場合は <see cref="Note"/> を使う。
    /// </remarks>
    public static WidgetResult TextColored(
        ReadOnlySpan<char> text, Vector4 color, Align align = Align.Start)
        => Label(text, EuColor.FromVector(color), align);

    /// <summary>色を 0xAABBGGRR で指定する版。</summary>
    public static WidgetResult TextColored(
        ReadOnlySpan<char> text, uint color, Align align = Align.Start)
        => Label(text, color, align);

    /// <summary>テーマの状態色で表示する版。</summary>
    public static WidgetResult TextColored(
        ReadOnlySpan<char> text, NoteKind kind, Align align = Align.Start)
        => Label(text, NoteColor(kind), align);

    /// <summary>
    /// 色を指定した折り返しテキスト。
    /// <c>PushStyleColor</c> + <c>TextWrapped</c> + <c>PopStyleColor</c> の置き換え。
    /// </summary>
    /// <remarks>
    /// 枠や地は付かない。囲みたい場合は <see cref="Note"/> を使う。
    /// </remarks>
    public static WidgetResult WrapColored(ReadOnlySpan<char> text, Vector4 color)
        => Paragraph(text, EuColor.FromVector(color));

    /// <summary>色を 0xAABBGGRR で指定する版。</summary>
    public static WidgetResult WrapColored(ReadOnlySpan<char> text, uint color)
        => Paragraph(text, color);

    /// <summary>テーマの状態色で表示する版。</summary>
    public static WidgetResult WrapColored(ReadOnlySpan<char> text, NoteKind kind)
        => Paragraph(text, NoteColor(kind));

    /// <summary>注意書きの種類に対応するテーマの色。</summary>
    /// <remarks>
    /// 独自のウィジェットで同じ色を使いたいときのために公開している。
    /// </remarks>
    public static uint NoteColor(NoteKind kind) => kind switch
    {
        NoteKind.Success => Colors.Success,
        NoteKind.Warning => Colors.Warning,
        NoteKind.Danger => Colors.Danger,
        _ => Colors.Info,
    };

    /// <summary>
    /// 幅を決めて 1 行を表示する。収まらない場合は末尾を省略記号にする。
    /// </summary>
    /// <param name="text">表示する文字列。</param>
    /// <param name="maxWidth">この幅に収める。</param>
    /// <param name="color">文字色。省略するとテーマの標準色。</param>
    /// <param name="align">横方向の寄せ。</param>
    /// <param name="tipWhenTruncated">省略したときに、全文をツールチップで見せるか。</param>
    /// <remarks>
    /// 長いファイルパスなどをそのまま置くと、ウィンドウの幅が押し広げられてしまう。
    /// 幅を決めておけばレイアウトが崩れない。
    /// </remarks>
    public static WidgetResult LabelClipped(
        ReadOnlySpan<char> text, SizeSpec maxWidth, uint? color = null,
        Align align = Align.Start, bool tipWhenTruncated = true)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var lineHeight = TextPainter.LineHeight;
        var rect = ctx.Allocate(maxWidth, lineHeight);

        if (!Painter.IsVisible(rect))
            return MakeTextResult(ctx, rect);

        var truncated = TextPainter.Measure(text).X > rect.Width + 1f;
        TextPainter.TextIn(rect, color ?? Colors.Text, text, align, Align.Center, ellipsize: true);

        var result = MakeTextResult(ctx, rect) with { Truncated = truncated };

        // 省略したときは、全文をツールチップで読めるようにする
        if (truncated && tipWhenTruncated)
            result.Tip(text);

        return result;
    }

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

    /// <summary>色を <see cref="Vector4"/> (RGBA, 0〜1) で指定する版。</summary>
    public static WidgetResult Paragraph(ReadOnlySpan<char> text, Vector4 color)
        => Paragraph(text, EuColor.FromVector(color));

    /// <summary>
    /// 注記ボックス。アイコンと色帯の付いた囲みの中へ、折り返した本文を置く。
    /// </summary>
    /// <param name="text">本文。</param>
    /// <param name="kind">種類。色とアイコンが変わる。</param>
    /// <param name="boxed">
    /// 囲みを描くか。false にすると状態色を付けただけの折り返しテキストになる。
    /// </param>
    /// <remarks>
    /// 本文そのものを色付きにしたいだけなら <see cref="WrapColored(ReadOnlySpan{char}, NoteKind)"/>
    /// を使う。こちらは囲みが主役で、本文は通常の文字色で描く。
    /// </remarks>
    public static WidgetResult Note(
        ReadOnlySpan<char> text, NoteKind kind = NoteKind.Info, bool boxed = true)
    {
        var accent = NoteColor(kind);

        if (!boxed)
            return Paragraph(text, accent);

        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var pad = Metrics.SpacingMd;
        var lineHeight = TextPainter.LineHeight;
        var iconWidth = lineHeight + Metrics.SpacingSm;

        var width = AvailableWidth;
        var textWidth = MathF.Max(1f, width - NoteAccentBarWidth - (pad * 2f) - iconWidth);
        var textHeight = MathF.Max(TextPainter.Measure(text, textWidth).Y, lineHeight);

        var rect = ctx.Allocate(new Vector2(width, textHeight + (pad * 2f)));

        if (!Painter.IsVisible(rect))
            return MakeTextResult(ctx, rect);

        var rounding = Metrics.CardRounding;

        // 地は状態色を薄く敷く。枠まで同じ色にすると主張が強すぎるので薄める
        Painter.Rect(rect, EuColor.WithAlpha(accent, 0.12f), rounding);
        Painter.Rect(rect.WithWidth(NoteAccentBarWidth), accent, rounding, Corners.Left);
        Painter.RectOutline(rect, EuColor.WithAlpha(accent, 0.4f), 1f, rounding);

        var inner = rect.Shrink(new EdgeInsets(NoteAccentBarWidth + pad, pad, pad, pad));
        var iconArea = inner.CutLeft(iconWidth, out var textArea);

        using (PushFont(FontRole.Icon))
        {
            TextPainter.TextIn(
                iconArea.WithHeight(lineHeight),
                accent,
                NoteIcon(kind),
                Align.Start,
                Align.Center,
                ellipsize: false);
        }

        // 囲みが種類を伝えるので、本文は読みやすい通常色のままにする
        TextPainter.TextWrapped(textArea.Min, Colors.Text, text, textArea.Width);

        return MakeTextResult(ctx, rect);
    }

    /// <summary>注意書きの種類に対応するアイコン。</summary>
    private static string NoteIcon(NoteKind kind) => kind switch
    {
        NoteKind.Success => FontAwesomeIcon.CheckCircle.ToIconString(),
        NoteKind.Warning => FontAwesomeIcon.ExclamationTriangle.ToIconString(),
        NoteKind.Danger => FontAwesomeIcon.TimesCircle.ToIconString(),
        _ => FontAwesomeIcon.InfoCircle.ToIconString(),
    };

    /// <summary>
    /// FontAwesome のアイコンを 1 つ描く。
    /// <code>
    /// using (EUi.HStack())
    /// {
    ///     EUi.Icon(FontAwesomeIcon.ExclamationTriangle, EUi.Colors.Warning);
    ///     EUi.Label("対象が見つかりません");
    /// }
    /// </code>
    /// </summary>
    /// <param name="icon">アイコン。</param>
    /// <param name="color">色。省略するとテーマの標準色。</param>
    /// <remarks>
    /// 計測も描画もアイコンフォントの下で行うので、文字と並べても位置がずれない。
    /// 押せるようにしたい場合は <c>EUi.IconButton</c> を使う。
    /// </remarks>
    public static WidgetResult Icon(FontAwesomeIcon icon, uint? color = null)
    {
        using var font = PushFont(FontRole.Icon);
        return Label(icon.ToIconString(), color, Align.Start, ellipsize: false);
    }

    /// <summary>アイコンと文字を並べて描く。</summary>
    /// <param name="icon">アイコン。</param>
    /// <param name="text">アイコンの右に置く文字。</param>
    /// <param name="color">アイコンの色。省略するとテーマの標準色。</param>
    /// <param name="textColor">文字の色。省略するとテーマの標準色。</param>
    public static WidgetResult IconText(
        FontAwesomeIcon icon, ReadOnlySpan<char> text,
        uint? color = null, uint? textColor = null)
    {
        using (HStack(Metrics.SpacingSm))
        {
            Icon(icon, color);
            return Label(text, textColor);
        }
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

    /// <summary>
    /// 画面隅に通知を出す。ウィンドウが閉じていても表示される。
    /// </summary>
    /// <param name="message">本文。</param>
    /// <param name="kind">種類。色とアイコンが変わる。</param>
    /// <param name="duration">表示し続ける秒数。</param>
    /// <remarks>
    /// マウスを乗せている間は時間が止まり、クリックすると閉じる。
    /// </remarks>
    public static void Toast(string message, NoteKind kind = NoteKind.Info, float duration = 4f)
        => ToastManager.Show(message, kind, duration);

    /// <summary>
    /// 見出し付きの通知を出す。何が起きたのかを一目で伝えたいときに使う。
    /// </summary>
    /// <param name="title">見出し。</param>
    /// <param name="message">本文。</param>
    /// <param name="kind">種類。色とアイコンが変わる。</param>
    /// <param name="duration">表示し続ける秒数。</param>
    public static void Toast(string title, string message, NoteKind kind = NoteKind.Info, float duration = 4f)
        => ToastManager.Show(title, message, kind, duration);

    // ── 文字の大きさを測る ────────────────────────────────────

    /// <summary>現在のフォントでの行の高さ。</summary>
    public static float LineHeight => TextPainter.LineHeight;

    /// <summary>文字列の描画サイズを測る。</summary>
    /// <remarks>結果はフォントごとにキャッシュされるので、毎フレーム呼んでも構わない。</remarks>
    public static Vector2 Measure(ReadOnlySpan<char> text) => TextPainter.Measure(text);

    /// <summary>
    /// 折り返したときの描画サイズを測る。領域の高さを先に決めたいときに使う。
    /// </summary>
    /// <param name="text">測る文字列。</param>
    /// <param name="width">折り返す幅。</param>
    /// <remarks>
    /// <code>
    /// var height = EUi.MeasureWrapped(description, EUi.AvailableWidth).Y;
    /// using (EUi.Scroll("desc", height + EUi.Metrics.SpacingMd))
    ///     EUi.Paragraph(description);
    /// </code>
    /// </remarks>
    public static Vector2 MeasureWrapped(ReadOnlySpan<char> text, float width)
        => TextPainter.Measure(text, width);

    /// <summary>
    /// 指定幅に収まるよう切り詰めた文字列を返す。収まる場合はそのまま返す。
    /// </summary>
    /// <remarks>
    /// 戻り値は使い回しのバッファを指すので、次に呼ぶまでの間に使い切ること。
    /// </remarks>
    public static ReadOnlySpan<char> Truncate(ReadOnlySpan<char> text, float maxWidth)
        => TextPainter.Truncate(text, maxWidth);

    /// <summary>ID を持たないテキスト系ウィジェットの戻り値を組み立てる。</summary>
    private static WidgetResult MakeTextResult(UiContext ctx, Rect rect)
    {
        var duration = ctx.TrackHover(rect);
        ctx.SetLastItem(rect, duration);

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
