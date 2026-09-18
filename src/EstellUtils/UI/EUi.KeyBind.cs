using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Dalamud.Game.ClientState.Keys;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Widgets;

namespace EstellUtils.UI;

/// <summary>
/// キー割り当ての入力。
/// </summary>
/// <remarks>
/// <para>
/// キーの判定には Dalamud の <c>IKeyState</c> を使い、ゲームのキー状態を直接読む。
/// ImGui 経由では、FFXIV 本体が先に処理してしまうキーを拾えないため。
/// </para>
/// <para>
/// <see cref="Initialize"/> で <c>keyState</c> を渡していない場合、
/// 割り当ての欄はその旨を表示して操作を受け付けない。
/// </para>
/// </remarks>
public static partial class EUi
{
    /// <summary>表示文字列を組み立てるのに十分な長さ。</summary>
    private const int KeyTextCapacity = 64;

    /// <summary>押し下がった瞬間を見るために、キーごとの前回の状態を覚えておく。</summary>
    private static readonly Dictionary<VirtualKey, KeyWatch> KeyWatches = new(16);

    /// <summary>割り当てに使えるキー。初めて必要になったときに作る。</summary>
    private static VirtualKey[]? bindableKeys;

    /// <summary>キーの表示名。毎フレーム <c>ToString</c> を呼ばないよう先に作っておく。</summary>
    private static Dictionary<VirtualKey, string>? keyNames;

    /// <summary>
    /// キー割り当ての欄。押すと待ち受け状態になり、次に押したキーを覚える。
    /// <code>
    /// if (EUi.KeyBind("切り替え", ref this.config.ToggleKey))
    ///     this.config.Save();
    ///
    /// // 別の場所で、毎フレーム
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

        var keyState = KeyState;

        // キー状態を読む手段がなければ、触れないことを見せる
        disabled |= IsDisabled || keyState is null;

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
        else
        {
            state.Open = false;
        }

        if (state.Open && keyState is not null)
        {
            if (WasPressed(VirtualKey.ESCAPE))
            {
                state.Open = false;
            }
            else if (TryCaptureKey(keyState, out var captured))
            {
                binding = captured;
                state.Open = false;
                changed = true;
            }
        }

        var visual = WidgetVisual.From(interaction) with
        {
            Rect = rect,
            Focused = state.Open,
        };

        WidgetPainter.DrawInputFrame(visual);

        var inner = rect.Shrink(Metrics.WidgetPadding);

        if (keyState is null)
        {
            TextPainter.TextIn(
                inner, Colors.TextDisabled, "キー状態を取得できません", Align.Center, Align.Center);
        }
        else if (state.Open)
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

        if (keyState is null)
        {
            return result.Tip(
                "EUi.Initialize へ IKeyState を渡すと、キーを割り当てられるようになります。");
        }

        if (!state.Open && !disabled)
        {
            result = result.Tip(
                binding.IsSet
                    ? "クリックで割り当て直し、右クリックで解除します。"
                    : "クリックしてキーを押してください。");
        }

        return result;
    }

    /// <summary>
    /// キーがこのフレームで押し下がったか。
    /// </summary>
    /// <remarks>
    /// <c>IKeyState</c> は「今押されているか」しか持たないので、
    /// 前回の状態を覚えて立ち上がりを見る。同じフレーム内で何度呼んでも同じ答えを返す。
    /// </remarks>
    internal static bool WasPressed(VirtualKey key)
    {
        var keyState = KeyState;

        if (keyState is null || !keyState.IsVirtualKeyValid(key))
            return false;

        var frame = UiContext.Current.FrameCount;
        var down = keyState[key];

        ref var watch = ref CollectionsMarshal.GetValueRefOrAddDefault(KeyWatches, key, out var existed);

        // 同じフレームでの二度目以降は、最初に出した答えをそのまま返す
        if (existed && watch.Frame == frame)
            return watch.Result;

        var result = down && !(existed && watch.Down);
        watch = new KeyWatch(down, frame, result);

        return result;
    }

    /// <summary>キーが押されているか。</summary>
    internal static bool IsKeyDown(VirtualKey key)
    {
        var keyState = KeyState;

        return keyState is not null && keyState.IsVirtualKeyValid(key) && keyState[key];
    }

    /// <summary>このフレームで押されたキーを 1 つ拾う。</summary>
    private static bool TryCaptureKey(Dalamud.Plugin.Services.IKeyState keyState, out KeyBinding binding)
    {
        foreach (var key in GetBindableKeys(keyState))
        {
            if (!WasPressed(key))
                continue;

            binding = new KeyBinding(
                key,
                IsKeyDown(VirtualKey.CONTROL),
                IsKeyDown(VirtualKey.SHIFT),
                IsKeyDown(VirtualKey.MENU));

            return true;
        }

        binding = KeyBinding.None;
        return false;
    }

    /// <summary>キーの表示名を返す。</summary>
    internal static string NameOf(VirtualKey key)
    {
        keyNames ??= new Dictionary<VirtualKey, string>(256);

        if (keyNames.TryGetValue(key, out var name))
            return name;

        name = FormatKeyName(key);
        keyNames[key] = name;

        return name;
    }

    /// <summary>
    /// 割り当てに使えるキーを返す。修飾キーそのものは除いてある。
    /// </summary>
    /// <remarks>
    /// <c>GetValidVirtualKeys</c> は呼ぶたびに列挙を作るので、一度だけ配列にして持つ。
    /// </remarks>
    private static VirtualKey[] GetBindableKeys(Dalamud.Plugin.Services.IKeyState keyState)
    {
        if (bindableKeys is not null)
            return bindableKeys;

        var keys = new List<VirtualKey>(128);

        foreach (var key in keyState.GetValidVirtualKeys())
        {
            if (!IsModifier(key) && key != VirtualKey.ESCAPE)
                keys.Add(key);
        }

        bindableKeys = keys.ToArray();
        return bindableKeys;
    }

    /// <summary>修飾キーそのものか。単体では割り当てさせない。</summary>
    private static bool IsModifier(VirtualKey key)
        => key is VirtualKey.CONTROL or VirtualKey.LCONTROL or VirtualKey.RCONTROL
            or VirtualKey.SHIFT or VirtualKey.LSHIFT or VirtualKey.RSHIFT
            or VirtualKey.MENU or VirtualKey.LMENU or VirtualKey.RMENU
            or VirtualKey.LWIN or VirtualKey.RWIN
            or VirtualKey.NO_KEY;

    /// <summary>キーの名前を、人が読む形へ整える。</summary>
    private static string FormatKeyName(VirtualKey key)
    {
        var name = key.ToString();

        // 英数字は先頭の接頭辞を落とす (KEY_A → A)
        if (name.StartsWith("KEY_", StringComparison.Ordinal))
            name = name[4..];

        return name switch
        {
            "LEFT" => "←",
            "RIGHT" => "→",
            "UP" => "↑",
            "DOWN" => "↓",
            "ESCAPE" => "Esc",
            "DELETE" => "Del",
            "INSERT" => "Ins",
            "PRIOR" => "PgUp",
            "NEXT" => "PgDn",
            "BACK" => "BS",
            "RETURN" => "Enter",
            "CAPITAL" => "CapsLock",
            "SPACE" => "Space",
            _ => name,
        };
    }

    /// <summary>キー 1 つ分の監視状態。</summary>
    private readonly record struct KeyWatch(bool Down, uint Frame, bool Result);
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
public readonly record struct KeyBinding(VirtualKey Key, bool Ctrl, bool Shift, bool Alt)
{
    /// <summary>割り当てなし。</summary>
    public static KeyBinding None => default;

    /// <summary>割り当てがあるか。</summary>
    public bool IsSet => this.Key != VirtualKey.NO_KEY;

    /// <summary>
    /// この割り当てがこのフレームで押されたか。毎フレーム呼ぶ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 修飾キーは指定どおりでなければ成立しない。Ctrl だけを割り当てた場合に
    /// Ctrl+Shift で反応してしまう、ということはない。
    /// </para>
    /// <para>
    /// 立ち上がりは前回の呼び出しとの差で見るため、毎フレーム呼ぶ必要がある。
    /// 呼ばないフレームがあると、その間の押し下げを取りこぼす。
    /// </para>
    /// </remarks>
    public bool IsPressed()
    {
        if (!this.IsSet || !this.ModifiersMatch())
            return false;

        return EUi.WasPressed(this.Key);
    }

    /// <summary>この割り当てのキーが押されているか。</summary>
    public bool IsDown()
        => this.IsSet && this.ModifiersMatch() && EUi.IsKeyDown(this.Key);

    /// <summary>修飾キーの状態が、割り当てと一致しているか。</summary>
    private bool ModifiersMatch()
        => EUi.IsKeyDown(VirtualKey.CONTROL) == this.Ctrl
           && EUi.IsKeyDown(VirtualKey.SHIFT) == this.Shift
           && EUi.IsKeyDown(VirtualKey.MENU) == this.Alt;

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
