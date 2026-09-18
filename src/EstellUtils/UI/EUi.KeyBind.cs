using System;
using System.Collections.Generic;
using System.Linq;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// キー割り当ての入力。
/// </summary>
public static partial class EUi
{
    /// <summary>表示文字列を組み立てるのに十分な長さ。</summary>
    private const int KeyTextCapacity = 64;

    /// <summary>割り当てに使えるキー。修飾キーそのものは除いてある。</summary>
    private static readonly ImGuiKey[] BindableKeys = BuildBindableKeys();

    /// <summary>キーの表示名。毎フレーム <c>ToString</c> を呼ばないよう先に作っておく。</summary>
    private static readonly Dictionary<ImGuiKey, string> KeyNames =
        BindableKeys.ToDictionary(key => key, FormatKeyName);

    /// <summary>
    /// キー割り当ての欄。押すと待ち受け状態になり、次に押したキーを覚える。
    /// <code>
    /// if (EUi.KeyBind("切り替え", ref this.config.ToggleKey))
    ///     this.config.Save();
    ///
    /// // 別の場所で
    /// if (this.config.ToggleKey.IsPressed())
    ///     this.Toggle();
    /// </code>
    /// </summary>
    /// <param name="label">識別子 (兼ラベル)。<c>##</c> 以降は ID にのみ使われる。</param>
    /// <param name="binding">対象の割り当て。</param>
    /// <param name="width">幅。省略すると残り幅いっぱい。</param>
    /// <param name="disabled">無効にするか。</param>
    /// <remarks>
    /// 待ち受け中は Esc で取り消せる。右クリックで割り当てを解除する。
    /// 修飾キー (Ctrl / Shift / Alt) は単体では割り当てられず、
    /// 他のキーと一緒に押すことで組み合わせになる。
    /// </remarks>
    public static WidgetResult KeyBind(
        ReadOnlySpan<char> label, ref KeyBinding binding,
        SizeSpec? width = null, bool disabled = false)
    {
        var ctx = UiContext.Current;
        ctx.EnsureFrame();

        disabled |= IsDisabled;

        var id = ctx.GetId(label, out _);
        var rect = ctx.Allocate(width ?? SizeSpec.Fill, Metrics.WidgetHeight);

        var interaction = Interaction.Behavior(
            rect, id,
            disabled ? InteractionFlags.Disabled : InteractionFlags.AllowRightClick);

        ref var state = ref ctx.Store.GetRef(id);
        var changed = false;

        if (!disabled)
        {
            // 押すと待ち受けを始める。もう一度押すとやめる
            if (interaction.Clicked)
                state.Open = !state.Open;

            // 右クリックで解除
            if (interaction.RightClicked && binding.IsSet)
            {
                binding = KeyBinding.None;
                state.Open = false;
                changed = true;
            }
        }

        if (state.Open && !disabled)
        {
            // 待ち受け中は ImGui にキーボードを押さえさせる。
            // でないと押したキーがゲーム側のホットキーとして処理されてしまう
            ImGui.SetNextFrameWantCaptureKeyboard(true);

            if (ctx.Input.IsKeyPressed(ImGuiKey.Escape))
            {
                state.Open = false;
            }
            else if (TryCaptureKey(ctx.Input, out var captured))
            {
                binding = captured;
                state.Open = false;
                changed = true;
            }
        }
        else if (disabled)
        {
            state.Open = false;
        }

        var visual = WidgetVisual.From(interaction) with
        {
            Rect = rect,
            Focused = state.Open,
        };

        WidgetPainter.DrawInputFrame(visual);

        var inner = rect.Shrink(Metrics.WidgetPadding);

        if (state.Open)
        {
            TextPainter.TextIn(
                inner, Colors.Accent, "キーを押してください…", Align.Center, Align.Center);
        }
        else if (binding.IsSet)
        {
            Span<char> buffer = stackalloc char[KeyTextCapacity];
            var length = binding.Format(buffer);

            TextPainter.TextIn(
                inner,
                disabled ? Colors.TextDisabled : Colors.Text,
                buffer[..length],
                Align.Center,
                Align.Center);
        }
        else
        {
            TextPainter.TextIn(inner, Colors.TextDisabled, "未設定", Align.Center, Align.Center);
        }

        var result = WidgetResult.From(interaction, changed);

        if (!state.Open && !disabled)
        {
            result = result.Tip(
                binding.IsSet
                    ? "クリックで割り当て直し、右クリックで解除します。"
                    : "クリックしてキーを押してください。");
        }

        return result;
    }

    /// <summary>このフレームで押されたキーを 1 つ拾う。</summary>
    private static bool TryCaptureKey(InputState input, out KeyBinding binding)
    {
        foreach (var key in BindableKeys)
        {
            if (!input.IsKeyPressed(key))
                continue;

            binding = new KeyBinding(key, input.Ctrl, input.Shift, input.Alt);
            return true;
        }

        binding = KeyBinding.None;
        return false;
    }

    /// <summary>キーの表示名を返す。</summary>
    internal static string NameOf(ImGuiKey key)
        => KeyNames.TryGetValue(key, out var name) ? name : key.ToString();

    /// <summary>
    /// 割り当てに使えるキーを集める。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ImGui が扱える「名前付きキー」は 512 以降に並ぶ。それより小さい互換用の値や、
    /// 修飾フラグ (4096 以降) を <c>IsKeyPressed</c> へ渡すと弾かれるため、範囲で先に絞る。
    /// </para>
    /// <para>
    /// 列挙名はバインディングの版で変わりうるので、除外は名前の完全一致で行う。
    /// 部分一致にすると <c>End</c> が <c>NamedKey_END</c> の判定に巻き込まれる。
    /// </para>
    /// </remarks>
    private static ImGuiKey[] BuildBindableKeys()
    {
        const int NamedKeyFirst = 512;
        const int ModifierFlagFirst = 4096;

        return Enum.GetValues<ImGuiKey>()
            .Where(key => (int)key is >= NamedKeyFirst and < ModifierFlagFirst)
            .Where(key => !IsExcluded(key.ToString()))
            .Distinct()
            .ToArray();

        static bool IsExcluded(string name)
        {
            // 数字キーは _0 のように始まる。それ以外で下線を含むものは境界値
            if (name.IndexOf('_', 1) >= 0)
                return true;

            if (name.StartsWith("Gamepad", StringComparison.Ordinal) ||
                name.StartsWith("Mouse", StringComparison.Ordinal) ||
                name.StartsWith("Reserved", StringComparison.Ordinal) ||
                name.StartsWith("Mod", StringComparison.Ordinal))
                return true;

            // 修飾キーそのものは単体では割り当てさせない
            return name is "None"
                or "LeftCtrl" or "RightCtrl"
                or "LeftShift" or "RightShift"
                or "LeftAlt" or "RightAlt"
                or "LeftSuper" or "RightSuper";
        }
    }

    /// <summary>列挙の名前を、人が読む形へ整える。</summary>
    private static string FormatKeyName(ImGuiKey key)
    {
        var name = key.ToString();

        // 先頭の下線は数字キー (_0 など) のためのもの
        name = name.TrimStart('_');

        return name switch
        {
            "LeftArrow" => "←",
            "RightArrow" => "→",
            "UpArrow" => "↑",
            "DownArrow" => "↓",
            "Escape" => "Esc",
            "Delete" => "Del",
            "Insert" => "Ins",
            "PageUp" => "PgUp",
            "PageDown" => "PgDn",
            "Backspace" => "BS",
            _ => name,
        };
    }
}

/// <summary>
/// キーの割り当て。修飾キーとの組み合わせを持つ。
/// </summary>
/// <param name="Key">主となるキー。</param>
/// <param name="Ctrl">Ctrl を伴うか。</param>
/// <param name="Shift">Shift を伴うか。</param>
/// <param name="Alt">Alt を伴うか。</param>
/// <remarks>
/// 単純なプロパティだけで構成してあるので、設定クラスへそのまま持たせて保存できる。
/// </remarks>
public readonly record struct KeyBinding(ImGuiKey Key, bool Ctrl, bool Shift, bool Alt)
{
    /// <summary>割り当てなし。</summary>
    public static KeyBinding None => default;

    /// <summary>割り当てがあるか。</summary>
    public bool IsSet => this.Key != ImGuiKey.None;

    /// <summary>
    /// この割り当てがこのフレームで押されたか。
    /// </summary>
    /// <param name="repeat">押しっぱなしで繰り返し反応させるか。</param>
    /// <remarks>
    /// 修飾キーは指定どおりでなければ成立しない。Ctrl だけを割り当てた場合に
    /// Ctrl+Shift で反応してしまう、ということはない。
    /// </remarks>
    public bool IsPressed(bool repeat = false)
    {
        if (!this.IsSet)
            return false;

        var io = ImGui.GetIO();

        if (io.KeyCtrl != this.Ctrl || io.KeyShift != this.Shift || io.KeyAlt != this.Alt)
            return false;

        return ImGui.IsKeyPressed(this.Key, repeat);
    }

    /// <summary>この割り当てのキーが押されているか。</summary>
    public bool IsDown()
    {
        if (!this.IsSet)
            return false;

        var io = ImGui.GetIO();

        if (io.KeyCtrl != this.Ctrl || io.KeyShift != this.Shift || io.KeyAlt != this.Alt)
            return false;

        return ImGui.IsKeyDown(this.Key);
    }

    /// <summary>
    /// 表示用の文字列を組み立てる。書き込んだ長さを返す。
    /// </summary>
    /// <remarks>
    /// 毎フレーム呼ばれるため、文字列を作らずに書き込む形にしてある。
    /// </remarks>
    public int Format(Span<char> destination)
    {
        if (!this.IsSet)
            return 0;

        var written = 0;

        if (this.Ctrl)
            Append(destination, ref written, "Ctrl+");

        if (this.Shift)
            Append(destination, ref written, "Shift+");

        if (this.Alt)
            Append(destination, ref written, "Alt+");

        Append(destination, ref written, EUi.NameOf(this.Key));

        return written;

        static void Append(Span<char> destination, ref int written, ReadOnlySpan<char> text)
        {
            var room = destination.Length - written;

            if (room <= 0)
                return;

            if (text.Length > room)
                text = text[..room];

            text.CopyTo(destination[written..]);
            written += text.Length;
        }
    }

    /// <summary>「Ctrl+Shift+F1」のような表示を返す。</summary>
    public override string ToString()
    {
        if (!this.IsSet)
            return string.Empty;

        Span<char> buffer = stackalloc char[64];
        var length = this.Format(buffer);

        return new string(buffer[..length]);
    }
}
