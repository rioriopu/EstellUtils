using System;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 押して使うウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>
    /// ボタン。押された瞬間に <c>true</c> を返す。
    /// <code>
    /// if (EUi.Button("保存"))
    ///     config.Save();
    /// </code>
    /// </summary>
    /// <param name="label">表示するラベル。<c>##</c> 以降は ID にのみ使われる。</param>
    /// <param name="style">見た目の種類。</param>
    /// <param name="width">幅。省略するとラベルに合わせる。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult Button(
        ReadOnlySpan<char> label, ButtonStyle style = ButtonStyle.Normal,
        SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var textSize = TextPainter.Measure(display);

        var height = MathF.Max(Metrics.WidgetHeight, textSize.Y + Metrics.WidgetPadding.TotalVertical);
        var rect = ctx.Allocate(width ?? SizeSpec.Px(ButtonWidth(display)), height);

        var interaction = Interaction.Behavior(rect, id, disabled ? InteractionFlags.Disabled : InteractionFlags.None);
        WidgetPainter.DrawButton(WidgetVisual.From(interaction), display, style);

        return WidgetResult.From(interaction);
    }

    /// <summary>
    /// ラベルに合わせたボタンの幅。<see cref="Button"/> が幅を省略したときに使うものと同じ。
    /// </summary>
    /// <remarks>
    /// 行の幅を自分で配るときに使う。ボタン側と別々に計算すると必ず食い違うので、
    /// 幅の求め方はここ 1 箇所に置いてある。
    /// </remarks>
    public static float ButtonWidth(ReadOnlySpan<char> label)
        => MathF.Max(
            Metrics.WidgetMinWidth,
            MathF.Ceiling(TextPainter.Measure(label).X) + Metrics.WidgetPadding.TotalHorizontal);

    /// <summary>
    /// 矩形を指定して描くボタン。レイアウトは進めない。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="rect">描く場所。</param>
    /// <param name="label">表示するラベル。</param>
    /// <param name="style">見た目の種類。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// 確保済みの領域を自分で切り分けて置きたいときに使う。
    /// 行の右端へ確実に揃えたい、といった場合は、列の配分に頼るより
    /// <c>rect.CutRight(...)</c> で切り出すほうが確実になる。
    /// </remarks>
    public static WidgetResult ButtonAt(
        ReadOnlySpan<char> id, Rect rect, ReadOnlySpan<char> label,
        ButtonStyle style = ButtonStyle.Normal, bool disabled = false)
    {
        var widget = CustomAt(id, rect, InteractionFlags.None, disabled);
        WidgetPainter.DrawButton(widget.Visual, label, style);

        return widget.Result;
    }

    /// <summary>
    /// アイコンだけの正方形ボタン。<paramref name="icon"/> には FontAwesome の文字を渡す。
    /// </summary>
    /// <param name="icon">アイコン文字 (Dalamud の <c>FontAwesomeIcon.X.ToIconString()</c> など)。</param>
    /// <param name="id">同じアイコンを複数置くときの識別子。</param>
    /// <param name="style">見た目の種類。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult IconButton(
        ReadOnlySpan<char> icon, ReadOnlySpan<char> id,
        ButtonStyle style = ButtonStyle.Ghost, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var widgetId = ctx.GetId(id);
        var size = MathF.Max(Metrics.WidgetHeight, Metrics.IconSize + Metrics.SpacingSm);
        var rect = ctx.Allocate(new Vector2(size, size));

        var interaction = Interaction.Behavior(rect, widgetId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        // 計測も描画もアイコンフォントの下で行う
        using (PushFont(FontRole.Icon))
        {
            WidgetPainter.DrawButton(WidgetVisual.From(interaction), icon, style);
        }

        return WidgetResult.From(interaction);
    }

    /// <summary>
    /// チェックボックス。ラベル部分もクリックできる。
    /// </summary>
    /// <param name="label">ラベル。</param>
    /// <param name="value">対象の真偽値。変更されると書き換わる。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult Checkbox(ReadOnlySpan<char> label, ref bool value, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var boxSize = Metrics.CheckboxSize;
        var textSize = TextPainter.Measure(display);

        var height = MathF.Max(Metrics.WidgetHeight, MathF.Max(boxSize, textSize.Y));
        var width = boxSize + (display.IsEmpty ? 0f : Metrics.LabelSpacing + MathF.Ceiling(textSize.X));
        var rect = ctx.Allocate(SizeSpec.Px(width), height);

        var interaction = Interaction.Behavior(rect, id, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var changed = false;
        if (interaction.Clicked)
        {
            value = !value;
            changed = true;
        }

        var amount = AnimateOn(id, value);

        var boxRect = Rect.FromCenter(
            new Vector2(rect.Min.X + (boxSize * 0.5f), rect.Center.Y),
            new Vector2(boxSize, boxSize));

        var visual = WidgetVisual.From(interaction, value, amount) with { Rect = boxRect };
        WidgetPainter.DrawCheckbox(visual);

        DrawWidgetLabel(rect, boxRect.Max.X, display, interaction);

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// トグルスイッチ。意味はチェックボックスと同じだが、ON/OFF の切り替えを強調したいときに使う。
    /// </summary>
    public static WidgetResult Toggle(ReadOnlySpan<char> label, ref bool value, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var switchSize = new Vector2(Metrics.ToggleWidth, Metrics.ToggleHeight);
        var textSize = TextPainter.Measure(display);

        var height = MathF.Max(Metrics.WidgetHeight, MathF.Max(switchSize.Y, textSize.Y));
        var width = switchSize.X + (display.IsEmpty ? 0f : Metrics.LabelSpacing + MathF.Ceiling(textSize.X));
        var rect = ctx.Allocate(SizeSpec.Px(width), height);

        var interaction = Interaction.Behavior(rect, id, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var changed = false;
        if (interaction.Clicked)
        {
            value = !value;
            changed = true;
        }

        var amount = AnimateOn(id, value);

        var switchRect = Rect.FromCenter(
            new Vector2(rect.Min.X + (switchSize.X * 0.5f), rect.Center.Y),
            switchSize);

        var visual = WidgetVisual.From(interaction, value, amount) with { Rect = switchRect };
        WidgetPainter.DrawToggle(visual);

        DrawWidgetLabel(rect, switchRect.Max.X, display, interaction);

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// ラジオボタン。選択されたときに <c>Clicked</c> が立つので、呼び出し側で値を代入する。
    /// </summary>
    /// <param name="label">ラベル。</param>
    /// <param name="selected">現在選択されているか。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult Radio(ReadOnlySpan<char> label, bool selected, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out var display);
        var circleSize = Metrics.CheckboxSize;
        var textSize = TextPainter.Measure(display);

        var height = MathF.Max(Metrics.WidgetHeight, MathF.Max(circleSize, textSize.Y));
        var width = circleSize + (display.IsEmpty ? 0f : Metrics.LabelSpacing + MathF.Ceiling(textSize.X));
        var rect = ctx.Allocate(SizeSpec.Px(width), height);

        var interaction = Interaction.Behavior(rect, id, disabled ? InteractionFlags.Disabled : InteractionFlags.None);
        var amount = AnimateOn(id, selected);

        var circleRect = Rect.FromCenter(
            new Vector2(rect.Min.X + (circleSize * 0.5f), rect.Center.Y),
            new Vector2(circleSize, circleSize));

        var visual = WidgetVisual.From(interaction, selected, amount) with { Rect = circleRect };
        WidgetPainter.DrawRadio(visual);

        DrawWidgetLabel(rect, circleRect.Max.X, display, interaction);

        return WidgetResult.From(interaction, interaction.Clicked && !selected);
    }

    /// <summary>
    /// 選択肢から 1 つを選ぶラジオグループ。選択が変わると <c>Changed</c> が立つ。
    /// </summary>
    /// <param name="id">グループの識別子。</param>
    /// <param name="selectedIndex">選択中の添字。</param>
    /// <param name="labels">選択肢。</param>
    /// <param name="horizontal">横に並べるか。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult RadioGroup(
        ReadOnlySpan<char> id, ref int selectedIndex, ReadOnlySpan<string> labels,
        bool horizontal = false, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var changed = false;
        var groupRect = Rect.Zero;

        using (ctx.ScopedId(id))
        using (horizontal ? HStack() : VStack(Metrics.SpacingSm))
        {
            for (var i = 0; i < labels.Length; i++)
            {
                var result = Radio(labels[i], i == selectedIndex, disabled);

                if (result.Clicked && i != selectedIndex)
                {
                    selectedIndex = i;
                    changed = true;
                }

                groupRect = i == 0 ? result.Rect : groupRect.Union(result.Rect);
            }
        }

        return new WidgetResult { Rect = groupRect, Changed = changed, Disabled = disabled };
    }

    /// <summary>ON 状態の遷移量を更新して返す。</summary>
    private static float AnimateOn(EuId id, bool on)
    {
        var ctx = UiContext.Current;
        ref var state = ref ctx.Store.GetRef(id);

        var target = on ? 1f : 0f;
        state.OpenAmount = Motion.Enabled
            ? Anim.Approach(state.OpenAmount, target, Motion.OpenSpeed, ctx.DeltaTime)
            : target;

        return state.OpenAmount;
    }

    /// <summary>チェックボックス等の右側に置くラベルを描く。</summary>
    /// <summary>
    /// 排他選択を横並びのひと続きで見せる。選択肢が少ないときはタブより軽く収まる。
    /// <code>
    /// if (EUi.SegmentedControl("方式", ref this.config.Method, ["GPU-GDI", "DComp", "CPU"]))
    ///     this.config.Save();
    /// </code>
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="index">選択中の添字。</param>
    /// <param name="options">選択肢。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// 選択の印は隣の区画へ滑って移る。どこから動いたのかが分かるようにするため。
    /// </remarks>
    public static WidgetResult SegmentedControl(
        ReadOnlySpan<char> id, ref int index, ReadOnlySpan<string> options,
        SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id);
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, Metrics.WidgetHeight);

        if (options.Length == 0)
            return new WidgetResult { Id = euId, Rect = rect };

        index = Math.Clamp(index, 0, options.Length - 1);

        var rounding = Metrics.WidgetRounding;
        Painter.Rect(rect, Colors.Track, rounding);

        var segmentWidth = rect.Width / options.Length;

        // 選択の印は位置を覚えておき、目標へ寄せていく
        ref var state = ref ctx.Store.GetRef(euId);
        var target = segmentWidth * index;

        if (!state.Initialized)
        {
            state.Initialized = true;
            state.Custom0 = target;
        }

        state.Custom0 = Motion.Enabled
            ? Anim.Approach(state.Custom0, target, Motion.OpenSpeed, ctx.DeltaTime)
            : target;

        var markerRect = Rect.FromSize(
            new Vector2(rect.Min.X + state.Custom0, rect.Min.Y),
            new Vector2(segmentWidth, rect.Height));

        Painter.Rect(
            markerRect.Shrink(2f),
            disabled ? Colors.SurfaceHover : Colors.Accent,
            MathF.Max(0f, rounding - 1f));

        var changed = false;

        for (var i = 0; i < options.Length; i++)
        {
            var segment = Rect.FromSize(
                new Vector2(rect.Min.X + (segmentWidth * i), rect.Min.Y),
                new Vector2(segmentWidth, rect.Height));

            var interaction = Interaction.Behavior(
                segment, euId.Child(i),
                disabled ? InteractionFlags.Disabled : InteractionFlags.None);

            if (interaction.Clicked && i != index)
            {
                index = i;
                changed = true;
            }

            var selected = i == index;

            // 選択されていない区画は、乗せたときだけ薄く反応させる
            if (!selected && interaction.HoverAmount > 0.01f)
            {
                Painter.Rect(
                    segment.Shrink(2f),
                    EuColor.WithAlpha(Colors.SurfaceHover, interaction.HoverAmount * 0.7f),
                    MathF.Max(0f, rounding - 1f));
            }

            uint color;

            if (disabled)
                color = Colors.TextDisabled;
            else if (selected)
                color = EuColor.ReadableOn(Colors.Accent);
            else
                color = EuColor.Lerp(Colors.TextMuted, Colors.Text, interaction.HoverAmount);

            TextPainter.TextIn(segment, color, options[i], Align.Center, Align.Center);
        }

        Painter.RectOutline(rect, Colors.WidgetBorder, Metrics.WidgetBorderWidth, rounding);

        return new WidgetResult
        {
            Id = euId,
            Rect = rect,
            Changed = changed,
            Disabled = disabled,
        };
    }

    private static void DrawWidgetLabel(
        Rect rowRect, float startX, ReadOnlySpan<char> label, in InteractionResult interaction)
    {
        if (label.IsEmpty)
            return;

        var color = interaction.Disabled
            ? Colors.TextDisabled
            : EuColor.Lerp(Colors.Text, Colors.TextHeading, interaction.HoverAmount * 0.5f);

        // 確保した幅が丸めの差でわずかに足りなくても、ラベルが削られないようにする
        var left = startX + Metrics.LabelSpacing;
        var width = MathF.Max(rowRect.Max.X - left, TextPainter.Measure(label).X + 1f);

        var textRect = Rect.FromSize(
            new Vector2(left, rowRect.Min.Y), new Vector2(width, rowRect.Height));

        TextPainter.TextIn(textRect, color, label, Align.Start, Align.Center);
    }
}
