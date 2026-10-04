using System;
using System.Globalization;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;
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
    /// <param name="label">
    /// ラベル兼識別子。<c>##</c> より前が画面に出て、全体が識別子になる。
    /// <c>"##name"</c> のようにするとラベルは表示されない。
    /// </param>
    /// <param name="value">対象の文字列。</param>
    /// <param name="hint">空のときに薄く表示する案内文。</param>
    /// <param name="maxLength">最大文字数。</param>
    /// <param name="width">入力欄の幅。省略するとラベルの分を残した残り幅。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// <para>
    /// 枠・地・フォーカスリングは自前で描くが、文字の編集そのもの
    /// (カーソル移動・選択・クリップボード・日本語入力の変換) は ImGui の入力欄へ委ねている。
    /// IME 対応を独自に実装するのは現実的でないため、ここだけは意図的に ImGui を使う。
    /// </para>
    /// <para>
    /// 1 文字ごとに <c>Changed</c> が立つ。確定したときだけ処理したい場合は
    /// <c>Committed</c> を見る。
    /// </para>
    /// </remarks>
    public static WidgetResult TextInput(
        string label, ref string value, string? hint = null,
        int maxLength = 256, SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = label;
        var euId = ctx.GetId(label, out var display);
        var height = Metrics.WidgetHeight;
        var rect = AllocateLabeledRow(display, width, height, out var labelRect);

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        // ImGui.IsItemActive() は直前のアイテムの状態を指すため、ここでは使えない。
        // 入力欄がアクティブかどうかは、下で SetFocus した結果を次フレームに反映する
        var focused = ctx.FocusedId == euId;
        var visual = WidgetVisual.From(interaction) with { Rect = rect, Focused = focused };

        WidgetPainter.DrawInputFrame(visual);
        DrawTrailingLabel(labelRect, display, disabled);

        if (disabled)
        {
            var textRect = rect.Shrink(Metrics.WidgetPadding);
            TextPainter.TextIn(textRect, Colors.TextDisabled, value, Align.Start, Align.Center);
            return WidgetResult.From(interaction);
        }

        // ImGui の入力欄を枠の内側へ、背景・枠なしで重ねる
        var inner = rect.Shrink(Metrics.WidgetPadding);
        var changed = TextInputRaw(id, ref value, hint, maxLength, inner, euId, out var committed);

        return WidgetResult.From(interaction, changed) with { Committed = committed };
    }

    /// <summary>
    /// 枠を描かずに、ImGui の入力欄を矩形へ重ねる。
    /// </summary>
    /// <remarks>
    /// 枠と装飾は呼び出し側が描く。文字の編集そのもの (カーソル移動・選択・
    /// クリップボード・日本語入力の変換) だけを ImGui へ委ねるための部分。
    /// 枠付きの入力欄と絞り込み欄で、この部分を共有している。
    /// </remarks>
    private static bool TextInputRaw(
        string id, ref string value, string? hint, int maxLength, Rect inner, EuId euId)
        => TextInputRaw(id, ref value, hint, maxLength, inner, euId, out _);

    /// <summary>編集の確定も受け取る版。</summary>
    private static bool TextInputRaw(
        string id, ref string value, string? hint, int maxLength, Rect inner, EuId euId,
        out bool committed)
    {
        var ctx = UiContext.Current;

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

        // 焦点が外れた、または Enter が押された時点で、実際に書き換わっていたか。
        // Changed は 1 文字ごとに立つので、確定の合図にはこちらを使う
        committed = ImGui.IsItemDeactivatedAfterEdit();

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);

        if (active)
            ctx.SetFocus(euId);
        else if (ctx.FocusedId == euId)
            ctx.ClearFocus();

        return changed;
    }

    /// <summary>
    /// 絞り込み欄。虫眼鏡と、文字が入っているときだけ出る消しボタンが付く。
    /// <code>
    /// EUi.SearchBox("##filter", ref this.filter);
    ///
    /// foreach (var item in this.items)
    /// {
    ///     if (this.filter.Length > 0 &amp;&amp;
    ///         !item.Name.Contains(this.filter, StringComparison.OrdinalIgnoreCase))
    ///         continue;
    ///
    ///     EUi.Selectable(item.Name, item == this.selected);
    /// }
    /// </code>
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="query">絞り込みの文字列。</param>
    /// <param name="hint">空のときに薄く表示する案内文。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// 消しボタンで空にしたときも <c>Changed</c> が立つので、
    /// 絞り込みの反映は戻り値を見るだけで済む。
    /// </remarks>
    public static WidgetResult SearchBox(
        string id, ref string query, string? hint = null,
        SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(id, out var display);
        var rect = AllocateLabeledRow(display, width, Metrics.WidgetHeight, out var labelRect);

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var focused = ctx.FocusedId == euId;
        WidgetPainter.DrawInputFrame(
            WidgetVisual.From(interaction) with { Rect = rect, Focused = focused });

        DrawTrailingLabel(labelRect, display, disabled);

        var inner = rect.Shrink(Metrics.WidgetPadding);
        var iconWidth = MathF.Ceiling(TextPainter.LineHeight);

        var iconArea = inner.CutLeft(iconWidth, out inner);

        using (PushFont(FontRole.Icon))
        {
            TextPainter.TextIn(
                iconArea,
                focused ? Colors.Accent : Colors.TextMuted,
                FontAwesomeIcon.Search.ToIconString(),
                Align.Start, Align.Center, ellipsize: false);
        }

        inner.CutLeft(Metrics.SpacingXs, out inner);

        var changed = false;

        // 消しボタンは文字が入っているときだけ。空のときに押せる的が残っていると紛らわしい
        if (!disabled && query.Length > 0)
        {
            var clearArea = inner.CutRight(iconWidth, out inner);
            var clear = CustomAt(id + "##euSearchClear", clearArea);

            using (PushFont(FontRole.Icon))
            {
                TextPainter.TextIn(
                    clearArea,
                    EuColor.Lerp(Colors.TextDisabled, Colors.Text, clear.Visual.Hover),
                    FontAwesomeIcon.TimesCircle.ToIconString(),
                    Align.Center, Align.Center, ellipsize: false);
            }

            if (clear.Result.Clicked)
            {
                query = string.Empty;
                changed = true;
            }
        }

        if (disabled)
        {
            TextPainter.TextIn(inner, Colors.TextDisabled, query, Align.Start, Align.Center);
        }
        else if (!changed)
        {
            changed = TextInputRaw(id, ref query, hint ?? "絞り込み", 128, inner, euId);
        }

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>
    /// 複数行のテキスト入力。
    /// </summary>
    public static WidgetResult TextArea(
        string label, ref string value, float height, int maxLength = 4096, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = label;
        var euId = ctx.GetId(label, out var display);
        var rect = AllocateLabeledRow(display, null, height, out var labelRect);

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var focused = ctx.FocusedId == euId;
        WidgetPainter.DrawInputFrame(WidgetVisual.From(interaction) with { Rect = rect, Focused = focused });
        DrawTrailingLabel(labelRect, display, disabled);

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
    /// <param name="label">ラベル兼識別子。<c>##</c> より前が画面に出る。</param>
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
        string label, ref int value, int step = 1,
        int? min = null, int? max = null, SizeSpec? width = null, bool disabled = false)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        var result = NumberInput(label, ref text, width, disabled, step != 0, out var stepped);

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
    /// <param name="label">ラベル兼識別子。<c>##</c> より前が画面に出る。</param>
    /// <param name="value">対象の値。</param>
    /// <param name="step">増減ボタンの刻み。0 にするとボタンを出さない。</param>
    /// <param name="min">下限。省略すると制限しない。</param>
    /// <param name="max">上限。省略すると制限しない。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    public static WidgetResult InputFloat(
        string label, ref float value, float step = 0f,
        float? min = null, float? max = null, SizeSpec? width = null, bool disabled = false)
    {
        var text = value.ToString("G", CultureInfo.InvariantCulture);
        var result = NumberInput(label, ref text, width, disabled, step != 0f, out var stepped);

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
        string label, ref string text, SizeSpec? width, bool disabled, bool withStepper, out int stepped)
    {
        stepped = 0;

        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        // ラベルは外側で 1 列として持つ。内側の入力欄へそのまま渡すと、
        // 入力欄が二重にラベルを描こうとして位置が合わない
        ctx.GetId(label, out var display);

        using var scope = ctx.ScopedId(label);

        var buttonWidth = withStepper ? Metrics.WidgetHeight : 0f;

        var labelSpace = display.IsEmpty
            ? 0f
            : MathF.Ceiling(TextPainter.Measure(display).X) + Metrics.LabelSpacing;

        // 列の間には隙間が入る。その分を引かずに幅を決めると、
        // 右端のボタンが行からはみ出して枠を突き抜ける
        var columnCount = (withStepper ? 3 : 1) + (display.IsEmpty ? 0 : 1);
        var available = MathF.Max(0f, NextItemWidth - labelSpace);
        var totalWidth = width?.Resolve(available) ?? available;

        var fieldWidth = MathF.Max(
            Metrics.WidgetMinWidth,
            totalWidth - (buttonWidth * 2f) - ColumnSpacing(columnCount));

        Span<SizeSpec> columns = stackalloc SizeSpec[4];
        var count = 0;

        columns[count++] = SizeSpec.Px(fieldWidth);

        if (withStepper)
        {
            columns[count++] = SizeSpec.Px(buttonWidth);
            columns[count++] = SizeSpec.Px(buttonWidth);
        }

        if (!display.IsEmpty)
            columns[count++] = SizeSpec.Px(labelSpace);

        WidgetResult result;

        using (Row(Align.Center, columns[..count]))
        {
            result = TextInput("##value", ref text, null, 32, SizeSpec.Fill, disabled);

            if (withStepper)
            {
                // 増減ボタンは押した時点で確定とみなす
                if (Button("-##down", ButtonStyle.Normal, SizeSpec.Fill, disabled))
                    stepped = -1;

                if (Button("+##up", ButtonStyle.Normal, SizeSpec.Fill, disabled))
                    stepped = 1;

                if (stepped != 0)
                    result = result with { Committed = true };
            }

            if (!display.IsEmpty)
                Label(display, disabled ? Colors.TextDisabled : Colors.Text);
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
        var current = selectedIndex >= 0 && selectedIndex < items.Length
            ? items[selectedIndex]
            : string.Empty;

        var itemHeight = Metrics.WidgetHeight;
        var visibleCount = Math.Min(Math.Max(items.Length, 1), ComboVisibleItems);

        var euId = UiContext.Current.GetId(id);
        var changed = false;

        using (var list = ComboBody(id, current, width, itemHeight * visibleCount, disabled))
        {
            if (list.IsOpen)
            {
                for (var i = 0; i < items.Length; i++)
                {
                    if (!SelectableRow(euId.Child(i), items[i], i == selectedIndex, itemHeight))
                        continue;

                    if (i != selectedIndex)
                    {
                        selectedIndex = i;
                        changed = true;
                    }

                    list.Close();
                }
            }
        }

        return new WidgetResult { Id = euId, Changed = changed, Disabled = disabled };
    }

    /// <summary>
    /// 一覧の中身を自分で描くドロップダウン。
    /// </summary>
    /// <param name="label">ラベル兼識別子。<c>##</c> より前が画面に出る。</param>
    /// <param name="preview">閉じているときに欄へ出す文字。ふつうは選択中のものの名前。</param>
    /// <param name="width">欄の幅。省略するとラベルの分を残した残り幅。</param>
    /// <param name="listHeight">開いたときの一覧の高さ。省略すると 10 項目ぶん。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// <para>
    /// <c>ImGui.BeginCombo</c> に当たるもの。見出しを差し込む・項目ごとに色を変える・
    /// 薄く見せるが押せる、といった一覧は文字列の並びでは表せないので、こちらを使う。
    /// </para>
    /// <code>
    /// using (var list = EUi.ComboBody("監視する通貨##cur", current.Name, width: 320f))
    /// {
    ///     if (list.IsOpen)
    ///     {
    ///         foreach (var group in groups)
    ///         {
    ///             EUi.Muted(group.Kind);                 // 見出しを差し込む
    ///
    ///             foreach (var item in group.Items)
    ///             {
    ///                 if (EUi.Selectable(item.Name, item == current, color: item.Color))
    ///                 {
    ///                     Pick(item);
    ///                     list.Close();
    ///                 }
    ///             }
    ///         }
    ///     }
    /// }
    /// </code>
    /// <para>
    /// 一覧の中は縦に積まれ、はみ出すと送りが付く。
    /// 選んだら <c>Close()</c> を呼ぶこと。呼ばないと開いたままになる。
    /// </para>
    /// </remarks>
    public static ComboScope ComboBody(
        string label, ReadOnlySpan<char> preview, SizeSpec? width = null,
        float? listHeight = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var euId = ctx.GetId(label, out var display);
        var rect = AllocateLabeledRow(display, width, Metrics.WidgetHeight, out var labelRect);

        var interaction = Interaction.Behavior(
            rect, euId, disabled ? InteractionFlags.Disabled : InteractionFlags.None);

        var visual = WidgetVisual.From(interaction) with { Rect = rect };
        WidgetPainter.DrawInputFrame(visual);
        DrawTrailingLabel(labelRect, display, disabled);

        // 閉じているときの中身。選択中のものとシェブロン
        var arrowRect = rect.CutRight(rect.Height, out var previewArea);

        TextPainter.TextIn(
            previewArea.Shrink(Metrics.WidgetPadding),
            disabled ? Colors.TextDisabled : Colors.Text,
            preview, Align.Start, Align.Center);

        Painter.Chevron(
            arrowRect, Direction.Down,
            EuColor.Lerp(Colors.TextMuted, Colors.Accent, interaction.HoverAmount), 1.6f);

        var popupId = ResolvePopupId(label + "##euComboPopup");

        // 開いている状態でもう一度押したときは、ImGui 側が「外側のクリック」として
        // 閉じてくれる。ここで開き直さないことで、クリックのたびに開閉が入れ替わる
        if (interaction.Clicked && !disabled && !ImGui.IsPopupOpen(popupId))
            ImGui.OpenPopup(popupId);

        var popupPadding = Metrics.SpacingXs;
        var bodyHeight = listHeight ?? (Metrics.WidgetHeight * ComboVisibleItems);
        var popupHeight = bodyHeight + (popupPadding * 2f);

        ImGui.SetNextWindowPos(new Vector2(rect.Min.X, rect.Max.Y + 2f));
        ImGui.SetNextWindowSize(new Vector2(rect.Width, popupHeight));

        if (!BeginPopupBox(popupId))
            return default;

        var popupRect = Rect.FromSize(ImGui.GetWindowPos(), ImGui.GetWindowSize());

        // 親ウィンドウのクリップを持ち込まない。引き継ぐと地や中身が切り取られる
        var popupClip = Painter.ClipFullScreen();
        DrawPopupSurface(popupRect);

        var region = Region(popupRect, EdgeInsets.All(popupPadding), 0f);
        var scroll = Scroll(label + "##euComboScroll", bodyHeight, 0f);

        return new ComboScope(region, scroll, popupClip);
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

        var euId = ctx.GetId(id, out var display);
        var frameRect = AllocateLabeledRow(display, null, height, out var labelRect);

        WidgetPainter.DrawInputFrame(new WidgetVisual { Rect = frameRect });
        DrawTrailingLabel(labelRect, display, disabled);

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

        // 領域はラベル行の確保で済んでいる。ここで取り直すと二重に消費する
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

/// <summary>
/// <c>using</c> でドロップダウンの一覧を閉じるハンドル。
/// </summary>
/// <remarks>
/// 開いていないときは <see cref="IsOpen"/> が false になり、中身を描く必要はない。
/// </remarks>
public readonly struct ComboScope : IDisposable
{
    private readonly LayoutHandle region;
    private readonly ScrollHandle scroll;
    private readonly ClipScope clip;
    private readonly bool open;

    internal ComboScope(LayoutHandle region, ScrollHandle scroll, ClipScope clip)
    {
        this.region = region;
        this.scroll = scroll;
        this.clip = clip;
        this.open = true;
    }

    /// <summary>一覧が開いているか。中身はこれが true のときだけ描く。</summary>
    public bool IsOpen => this.open;

    /// <summary>一覧を閉じる。項目を選んだときに呼ぶ。</summary>
    public readonly void Close()
    {
        if (this.open)
            Dalamud.Bindings.ImGui.ImGui.CloseCurrentPopup();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.open)
            return;

        this.scroll.Dispose();
        this.region.Dispose();
        this.clip.Dispose();

        Dalamud.Bindings.ImGui.ImGui.EndPopup();
    }
}
