using System;
using System.Globalization;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 文字入力と選択のウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>ドロップダウンに一度に表示する項目数の上限。</summary>
    private const int ComboVisibleItems = 10;

    /// <summary>
    /// 1 行のテキスト入力。
    /// </summary>
    /// <param name="id">識別子。<c>##</c> 始まりにするとラベルは表示されない。</param>
    /// <param name="value">対象の文字列。</param>
    /// <param name="hint">空のときに薄く表示する案内文。</param>
    /// <param name="maxLength">最大文字数。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// 枠・地・フォーカスリングは自前で描くが、文字の編集そのもの
    /// (カーソル移動・選択・クリップボード・日本語入力の変換) は ImGui の入力欄へ委ねている。
    /// IME 対応を独自に実装するのは現実的でないため、ここだけは意図的に ImGui を使う。
    /// </remarks>
    public static WidgetResult TextInput(
        string id, ref string value, string? hint = null,
        int maxLength = 256, SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id);
        var height = Metrics.WidgetHeight;
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, height);

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        // ImGui.IsItemActive() は直前のアイテムの状態を指すため、ここでは使えない。
        // 入力欄がアクティブかどうかは、下で SetFocus した結果を次フレームに反映する
        var focused = ctx.FocusedId == euId;
        var visual = WidgetVisual.From(interaction) with { Rect = rect, Focused = focused };

        WidgetPainter.DrawInputFrame(visual);

        if (disabled)
        {
            var textRect = rect.Shrink(Metrics.WidgetPadding);
            TextPainter.TextIn(textRect, Colors.TextDisabled, value, Align.Start, Align.Center);
            return WidgetResult.From(interaction);
        }

        // ImGui の入力欄を枠の内側へ、背景・枠なしで重ねる
        var inner = rect.Shrink(Metrics.WidgetPadding);

        ImGui.SetCursorScreenPos(inner.Min);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, 0u);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0u);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0u);
        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Text);
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Colors.Selection);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.SetNextItemWidth(inner.Width);

        bool changed;
        if (string.IsNullOrEmpty(hint))
            changed = ImGui.InputText(id, ref value, maxLength);
        else
            changed = ImGui.InputTextWithHint(id, hint, ref value, maxLength);

        var active = ImGui.IsItemActive();

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);

        if (active)
            ctx.SetFocus(euId);
        else if (ctx.FocusedId == euId)
            ctx.ClearFocus();

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// 複数行のテキスト入力。
    /// </summary>
    public static WidgetResult TextArea(
        string id, ref string value, float height, int maxLength = 4096, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id);
        var rect = ctx.Allocate(SizeSpec.Fill, height);

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var focused = ctx.FocusedId == euId;
        WidgetPainter.DrawInputFrame(WidgetVisual.From(interaction) with { Rect = rect, Focused = focused });

        if (disabled)
            return WidgetResult.From(interaction);

        var inner = rect.Shrink(Metrics.WidgetPadding);

        ImGui.SetCursorScreenPos(inner.Min);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, 0u);
        ImGui.PushStyleColor(ImGuiCol.Text, Colors.Text);
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, Colors.Selection);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);

        var changed = ImGui.InputTextMultiline(id, ref value, maxLength, inner.Size);
        var active = ImGui.IsItemActive();

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(3);

        if (active)
            ctx.SetFocus(euId);
        else if (ctx.FocusedId == euId)
            ctx.ClearFocus();

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// 整数を直接入力する欄。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="value">対象の値。</param>
    /// <param name="step">増減ボタンの刻み。0 にするとボタンを出さない。</param>
    /// <param name="min">下限。省略すると制限しない。</param>
    /// <param name="max">上限。省略すると制限しない。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// 座標やピクセル数のように範囲の広い値は、スライダーでは合わせきれない。
    /// そうした値はこちらで直接打ち込む。
    /// </remarks>
    public static WidgetResult InputInt(
        string id, ref int value, int step = 1,
        int? min = null, int? max = null, SizeSpec? width = null, bool disabled = false)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        var result = NumberInput(id, ref text, width, disabled, step != 0, out var stepped);

        var changed = false;

        if (stepped != 0)
        {
            value = ApplyLimits(value + (stepped * step), min, max);
            changed = true;
        }
        else if (result.Changed && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = ApplyLimits(parsed, min, max);
            changed = true;
        }

        return result with { Changed = changed };
    }

    /// <summary>
    /// 小数を直接入力する欄。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="value">対象の値。</param>
    /// <param name="step">増減ボタンの刻み。0 にするとボタンを出さない。</param>
    /// <param name="min">下限。省略すると制限しない。</param>
    /// <param name="max">上限。省略すると制限しない。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult InputFloat(
        string id, ref float value, float step = 0f,
        float? min = null, float? max = null, SizeSpec? width = null, bool disabled = false)
    {
        var text = value.ToString("G", CultureInfo.InvariantCulture);
        var result = NumberInput(id, ref text, width, disabled, step != 0f, out var stepped);

        var changed = false;

        if (stepped != 0)
        {
            value = ApplyLimits(value + (stepped * step), min, max);
            changed = true;
        }
        else if (result.Changed && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            value = ApplyLimits(parsed, min, max);
            changed = true;
        }

        return result with { Changed = changed };
    }

    /// <summary>数値入力の共通部分。文字列として編集し、増減ボタンを添える。</summary>
    private static WidgetResult NumberInput(
        string id, ref string text, SizeSpec? width, bool disabled, bool withStepper, out int stepped)
    {
        stepped = 0;

        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        using var scope = ctx.ScopedId(id);

        var buttonWidth = withStepper ? Metrics.WidgetHeight : 0f;
        var totalWidth = width?.Resolve(AvailableWidth) ?? AvailableWidth;
        var fieldWidth = MathF.Max(Metrics.WidgetMinWidth, totalWidth - (buttonWidth * 2f));

        WidgetResult result;

        using (Row(SizeSpec.Px(fieldWidth), SizeSpec.Px(buttonWidth), SizeSpec.Px(buttonWidth)))
        {
            result = TextInput("##value", ref text, null, 32, SizeSpec.Fill, disabled);

            if (!withStepper)
                return result;

            if (Button("-##down", ButtonStyle.Normal, SizeSpec.Fill, disabled))
                stepped = -1;

            if (Button("+##up", ButtonStyle.Normal, SizeSpec.Fill, disabled))
                stepped = 1;
        }

        return result;
    }

    /// <summary>上下限があれば丸める。</summary>
    private static int ApplyLimits(int value, int? min, int? max)
    {
        if (min.HasValue)
            value = Math.Max(min.Value, value);

        if (max.HasValue)
            value = Math.Min(max.Value, value);

        return value;
    }

    /// <summary>上下限があれば丸める。</summary>
    private static float ApplyLimits(float value, float? min, float? max)
    {
        if (min.HasValue)
            value = MathF.Max(min.Value, value);

        if (max.HasValue)
            value = MathF.Min(max.Value, value);

        return value;
    }

    /// <summary>
    /// ドロップダウン。選択が変わると <c>Changed</c> が立つ。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="selectedIndex">選択中の添字。</param>
    /// <param name="items">選択肢。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// 一覧の重なり順と「外側をクリックで閉じる」挙動だけ ImGui のポップアップに任せ、
    /// 中身の描画と項目の選択判定はすべて自前で行う。
    /// </remarks>
    public static WidgetResult Combo(
        string id, ref int selectedIndex, ReadOnlySpan<string> items,
        SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id);
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, Metrics.WidgetHeight);

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var visual = WidgetVisual.From(interaction) with { Rect = rect };
        WidgetPainter.DrawInputFrame(visual);

        // 現在の選択内容とシェブロン
        var arrowRect = rect.CutRight(rect.Height, out var labelArea);
        var current = selectedIndex >= 0 && selectedIndex < items.Length ? items[selectedIndex] : string.Empty;

        TextPainter.TextIn(
            labelArea.Shrink(Metrics.WidgetPadding),
            disabled ? Colors.TextDisabled : Colors.Text,
            current, Align.Start, Align.Center);

        Painter.Chevron(
            arrowRect, Direction.Down,
            EuColor.Lerp(Colors.TextMuted, Colors.Accent, interaction.HoverAmount), 1.6f);

        var popupId = id + "##euComboPopup";

        // 開いている状態でもう一度押したときは、ImGui 側が「外側のクリック」として
        // 閉じてくれる。ここで開き直さないことで、クリックのたびに開閉が入れ替わる
        if (interaction.Clicked && !disabled && !ImGui.IsPopupOpen(popupId))
            ImGui.OpenPopup(popupId);

        var changed = false;

        var itemHeight = Metrics.WidgetHeight;
        var visibleCount = Math.Min(items.Length, ComboVisibleItems);
        var popupPadding = Metrics.SpacingXs;
        var popupHeight = (itemHeight * visibleCount) + (popupPadding * 2f);

        ImGui.SetNextWindowPos(new Vector2(rect.Min.X, rect.Max.Y + 2f));
        ImGui.SetNextWindowSize(new Vector2(rect.Width, popupHeight));

        const ImGuiWindowFlags popupFlags =
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.BeginPopup(popupId, popupFlags))
        {
            var popupRect = Rect.FromSize(ImGui.GetWindowPos(), ImGui.GetWindowSize());

            Painter.Rect(popupRect, Colors.TooltipBackground, Metrics.WidgetRounding);
            Painter.RectOutline(popupRect, Colors.TooltipBorder, 1f, Metrics.WidgetRounding);

            using (Region(popupRect, EdgeInsets.All(popupPadding), 0f))
            using (Scroll(id + "##euComboScroll", popupHeight - (popupPadding * 2f), 0f))
            {
                for (var i = 0; i < items.Length; i++)
                {
                    if (SelectableRow(euId.Child(i), items[i], i == selectedIndex, itemHeight))
                    {
                        if (i != selectedIndex)
                        {
                            selectedIndex = i;
                            changed = true;
                        }

                        ImGui.CloseCurrentPopup();
                    }
                }
            }

            ImGui.EndPopup();
        }

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// 一覧から 1 つ選ぶ。高さを指定した箱の中にスクロールする一覧を描く。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="selectedIndex">選択中の添字。</param>
    /// <param name="items">選択肢。</param>
    /// <param name="height">一覧の高さ。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult ListBox(
        string id, ref int selectedIndex, ReadOnlySpan<string> items,
        float height = 140f, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id);
        var available = ctx.Layout.AvailableRect;
        var frameRect = Rect.FromSize(available.Min, new Vector2(available.Width, height));

        WidgetPainter.DrawInputFrame(new WidgetVisual { Rect = frameRect });

        var changed = false;
        var itemHeight = Metrics.WidgetHeight;
        var padding = EdgeInsets.All(Metrics.SpacingXs);

        using (Region(frameRect, padding, 0f))
        using (Scroll(id + "##euListScroll", height - padding.TotalVertical, 0f))
        {
            for (var i = 0; i < items.Length; i++)
            {
                if (disabled)
                {
                    var rowRect = ctx.Allocate(SizeSpec.Fill, itemHeight);
                    TextPainter.TextIn(
                        rowRect.Shrink(Metrics.WidgetPadding), Colors.TextDisabled,
                        items[i], Align.Start, Align.Center);
                    continue;
                }

                if (SelectableRow(euId.Child(i), items[i], i == selectedIndex, itemHeight) && i != selectedIndex)
                {
                    selectedIndex = i;
                    changed = true;
                }
            }
        }

        // 一覧の分だけ領域を消費する
        ctx.Allocate(new Vector2(frameRect.Width, height));

        return new WidgetResult { Id = euId, Rect = frameRect, Changed = changed, Disabled = disabled };
    }

    /// <summary>一覧の 1 行を描き、クリックされたかを返す。</summary>
    private static bool SelectableRow(EuId id, ReadOnlySpan<char> label, bool selected, float height)
    {
        var ctx = UiContext.Current;
        var rect = ctx.Allocate(SizeSpec.Fill, height);
        var interaction = Interaction.Behavior(rect, id);

        if (selected)
        {
            Painter.Rect(rect, Colors.Selection, Metrics.WidgetRounding);
        }
        else if (interaction.HoverAmount > 0.01f)
        {
            Painter.Rect(
                rect,
                EuColor.WithAlpha(Colors.SurfaceHover, interaction.HoverAmount * 0.9f),
                Metrics.WidgetRounding);
        }

        var color = selected ? Colors.TextHeading : Colors.Text;
        TextPainter.TextIn(rect.Shrink(Metrics.WidgetPadding), color, label, Align.Start, Align.Center);

        return interaction.Clicked;
    }
}
