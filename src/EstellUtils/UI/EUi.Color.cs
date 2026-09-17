using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// 色を選ぶウィジェット。
/// </summary>
public static partial class EUi
{
    /// <summary>色選択パネルの幅。</summary>
    private const float ColorPickerWidth = 220f;

    /// <summary>彩度・明度を選ぶ四角の高さ。</summary>
    private const float SaturationValueHeight = 140f;

    /// <summary>色相・不透明度バーの高さ。</summary>
    private const float ColorBarHeight = 14f;

    /// <summary>
    /// 色見本のボタン。クリックすると色選択パネルが開く。
    /// </summary>
    /// <param name="id">識別子。</param>
    /// <param name="color">対象の色 (RGBA, 0〜1)。</param>
    /// <param name="showAlpha">不透明度も編集するか。</param>
    /// <param name="width">見本の幅。省略すると標準のウィジェット幅。</param>
    /// <remarks>
    /// 色相・彩度・明度の選択面はすべて自前描画。ImGui のカラーピッカーは使わない。
    /// </remarks>
    public static WidgetResult ColorEdit(
        string id, ref Vector4 color, bool showAlpha = true, SizeSpec? width = null)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        var euId = ctx.GetId(id);
        var rect = ctx.Allocate(width ?? SizeSpec.Px(Metrics.WidgetHeight * 2.5f), Metrics.WidgetHeight);

        var interaction = Interaction.Behavior(rect, euId);
        var packed = EuColor.FromVector(color);

        // 半透明のときだけ、下地に市松模様を敷いて透け具合が分かるようにする
        if (EuColor.AlphaOf(packed) < 0.999f)
            DrawCheckerboard(rect, Metrics.WidgetRounding);

        Painter.Rect(rect, packed, Metrics.WidgetRounding);

        var border = EuColor.Lerp(Colors.WidgetBorder, Colors.WidgetBorderHover, interaction.HoverAmount);
        Painter.RectOutline(rect, border, Metrics.WidgetBorderWidth, Metrics.WidgetRounding);

        var popupId = id + "##euColorPopup";

        if (interaction.Clicked && !ImGui.IsPopupOpen(popupId))
            ImGui.OpenPopup(popupId);

        var changed = false;

        var panelHeight = SaturationValueHeight + ((ColorBarHeight + Metrics.SpacingSm) * (showAlpha ? 2 : 1)) +
                          (Metrics.SpacingMd * 2f) + Metrics.WidgetHeight + Metrics.SpacingSm;

        ImGui.SetNextWindowPos(new Vector2(rect.Min.X, rect.Max.Y + 2f));
        ImGui.SetNextWindowSize(new Vector2(ColorPickerWidth, panelHeight));

        const ImGuiWindowFlags popupFlags =
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings;

        if (ImGui.BeginPopup(popupId, popupFlags))
        {
            var popupRect = Rect.FromSize(ImGui.GetWindowPos(), ImGui.GetWindowSize());

            Painter.Rect(popupRect, Colors.TooltipBackground, Metrics.WidgetRounding);
            Painter.RectOutline(popupRect, Colors.TooltipBorder, 1f, Metrics.WidgetRounding);

            using (Region(popupRect, EdgeInsets.All(Metrics.SpacingMd), Metrics.SpacingSm))
            {
                changed = DrawColorPicker(euId, ref color, showAlpha);
            }

            ImGui.EndPopup();
        }

        return WidgetResult.From(interaction, changed);
    }

    /// <summary>色を <c>uint</c> (0xAABBGGRR) で扱う版。</summary>
    public static WidgetResult ColorEdit(
        string id, ref uint color, bool showAlpha = true, SizeSpec? width = null)
    {
        var vector = EuColor.ToVector(color);
        var result = ColorEdit(id, ref vector, showAlpha, width);

        if (result.Changed)
            color = EuColor.FromVector(vector);

        return result;
    }

    /// <summary>色選択パネルの中身を描く。</summary>
    private static bool DrawColorPicker(EuId id, ref Vector4 color, bool showAlpha)
    {
        var ctx = UiContext.Current;
        ref var state = ref ctx.Store.GetRef(id);

        var packed = EuColor.FromVector(color);
        var (hue, saturation, value) = EuColor.ToHsv(packed);

        // 彩度 0 や明度 0 では色相が失われるので、直前の色相を覚えておく
        if (saturation <= 0.0001f || value <= 0.0001f)
            hue = state.Custom1;
        else
            state.Custom1 = hue;

        var alpha = color.W;
        var changed = false;

        // ── 彩度・明度の面 ──
        var svRect = Reserve(SizeSpec.Fill, SaturationValueHeight);
        var pureHue = EuColor.FromHsv(hue, 1f, 1f);

        Painter.RectGradientH(svRect, EuColor.White, pureHue, 2f);
        Painter.RectGradientV(svRect, EuColor.WithAlpha(EuColor.Black, 0f), EuColor.Black, 2f);
        Painter.RectOutline(svRect, Colors.WidgetBorder, 1f, 2f);

        var svInteraction = Interaction.Behavior(
            svRect, id.Child("sv"), InteractionFlags.AllowDragOutside | InteractionFlags.ClickOnPress);

        if (svInteraction.Held)
        {
            var local = ctx.Input.MousePos - svRect.Min;
            saturation = Math.Clamp(local.X / MathF.Max(1f, svRect.Width), 0f, 1f);
            value = 1f - Math.Clamp(local.Y / MathF.Max(1f, svRect.Height), 0f, 1f);
            changed = true;
        }

        var svCursor = new Vector2(
            svRect.Min.X + (svRect.Width * saturation),
            svRect.Min.Y + (svRect.Height * (1f - value)));

        Painter.CircleOutline(svCursor, 5f, EuColor.Black, 2f);
        Painter.CircleOutline(svCursor, 5f, EuColor.White, 1f);

        // ── 色相バー ──
        var hueRect = Reserve(SizeSpec.Fill, ColorBarHeight);
        DrawHueBar(hueRect);

        var hueInteraction = Interaction.Behavior(
            hueRect, id.Child("hue"), InteractionFlags.AllowDragOutside | InteractionFlags.ClickOnPress);

        if (hueInteraction.Held)
        {
            hue = Math.Clamp((ctx.Input.MousePos.X - hueRect.Min.X) / MathF.Max(1f, hueRect.Width), 0f, 1f);
            state.Custom1 = hue;
            changed = true;
        }

        DrawBarCursor(hueRect, hue);

        // ── 不透明度バー ──
        if (showAlpha)
        {
            var alphaRect = Reserve(SizeSpec.Fill, ColorBarHeight);

            DrawCheckerboard(alphaRect, 2f);
            Painter.RectGradientH(
                alphaRect,
                EuColor.WithAlpha(EuColor.FromHsv(hue, saturation, value), 0f),
                EuColor.FromHsv(hue, saturation, value),
                2f);
            Painter.RectOutline(alphaRect, Colors.WidgetBorder, 1f, 2f);

            var alphaInteraction = Interaction.Behavior(
                alphaRect, id.Child("alpha"), InteractionFlags.AllowDragOutside | InteractionFlags.ClickOnPress);

            if (alphaInteraction.Held)
            {
                alpha = Math.Clamp(
                    (ctx.Input.MousePos.X - alphaRect.Min.X) / MathF.Max(1f, alphaRect.Width), 0f, 1f);
                changed = true;
            }

            DrawBarCursor(alphaRect, alpha);
        }

        // ── 16 進表記 ──
        var hexRect = Reserve(SizeSpec.Fill, Metrics.WidgetHeight);
        Span<char> buffer = stackalloc char[16];
        var result = EuColor.FromHsv(hue, saturation, value, alpha);

        buffer[0] = '#';
        var written = 1;
        WriteHexByte(buffer, ref written, (byte)(result & 0xFF));
        WriteHexByte(buffer, ref written, (byte)((result >> 8) & 0xFF));
        WriteHexByte(buffer, ref written, (byte)((result >> 16) & 0xFF));

        if (showAlpha)
            WriteHexByte(buffer, ref written, (byte)((result >> 24) & 0xFF));

        TextPainter.TextIn(hexRect, Colors.TextMuted, buffer[..written], Align.Center, Align.Center);

        if (changed)
            color = EuColor.ToVector(result);

        return changed;
    }

    /// <summary>色相の帯を 6 区間のグラデーションで描く。</summary>
    private static void DrawHueBar(Rect rect)
    {
        const int segments = 6;
        var width = rect.Width / segments;

        for (var i = 0; i < segments; i++)
        {
            var from = EuColor.FromHsv(i / (float)segments, 1f, 1f);
            var to = EuColor.FromHsv((i + 1) / (float)segments, 1f, 1f);

            var segmentRect = Rect.FromSize(
                new Vector2(rect.Min.X + (width * i), rect.Min.Y),
                new Vector2(width + 1f, rect.Height));

            Painter.RectGradientH(segmentRect, from, to);
        }

        Painter.RectOutline(rect, Colors.WidgetBorder, 1f, 2f);
    }

    /// <summary>バー上の位置を示すつまみを描く。</summary>
    private static void DrawBarCursor(Rect rect, float position)
    {
        var x = rect.Min.X + (rect.Width * Math.Clamp(position, 0f, 1f));
        var cursorRect = Rect.FromCenter(
            new Vector2(x, rect.Center.Y),
            new Vector2(5f, rect.Height + 4f));

        Painter.Rect(cursorRect, EuColor.White, 2f);
        Painter.RectOutline(cursorRect, EuColor.Black, 1f, 2f);
    }

    /// <summary>半透明を見せるための市松模様を描く。</summary>
    private static void DrawCheckerboard(Rect rect, float rounding, float cellSize = 5f)
    {
        using var clip = Painter.Clip(rect);

        Painter.Rect(rect, EuColor.Bytes(160, 160, 160), rounding);

        var dark = EuColor.Bytes(110, 110, 110);
        var rows = (int)MathF.Ceiling(rect.Height / cellSize);
        var columns = (int)MathF.Ceiling(rect.Width / cellSize);

        for (var row = 0; row < rows; row++)
        {
            for (var col = (row % 2); col < columns; col += 2)
            {
                var cell = Rect.FromSize(
                    new Vector2(rect.Min.X + (col * cellSize), rect.Min.Y + (row * cellSize)),
                    new Vector2(cellSize, cellSize));

                Painter.Rect(cell.Intersect(rect), dark);
            }
        }
    }

    /// <summary>1 バイトを 16 進 2 桁で書き出す。</summary>
    private static void WriteHexByte(Span<char> buffer, ref int index, byte value)
    {
        const string digits = "0123456789ABCDEF";

        if (index + 2 > buffer.Length)
            return;

        buffer[index++] = digits[value >> 4];
        buffer[index++] = digits[value & 0xF];
    }
}
