using System;
using System.Numerics;

using Dalamud.Bindings.ImGui;

namespace EstellUtils.UI.Core;

/// <summary>マウスボタン。ImGui の列挙を直接露出させないための独自定義。</summary>
public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
}

/// <summary>
/// 1 フレーム分の入力スナップショット。
/// </summary>
/// <remarks>
/// ImGui からは「生の入力」だけを受け取り、ウィジェットのヒットテストや押下判定は
/// 本ライブラリ側で行う (<see cref="Interaction"/>)。<c>ImGui.IsItemHovered</c> 等の
/// ImGui のウィジェット状態には一切依存しない。
/// </remarks>
public sealed class InputState
{
    private const int ButtonCount = 3;

    private readonly bool[] down = new bool[ButtonCount];
    private readonly bool[] pressed = new bool[ButtonCount];
    private readonly bool[] released = new bool[ButtonCount];
    private readonly bool[] doubleClicked = new bool[ButtonCount];
    private readonly Vector2[] pressPos = new Vector2[ButtonCount];

    /// <summary>現在のマウス座標 (画面座標)。</summary>
    public Vector2 MousePos { get; private set; }

    /// <summary>前フレームからのマウス移動量。</summary>
    public Vector2 MouseDelta { get; private set; }

    /// <summary>縦ホイールの回転量。奥へ回すと正。</summary>
    public float WheelY { get; private set; }

    /// <summary>横ホイールの回転量。</summary>
    public float WheelX { get; private set; }

    /// <summary>前フレームからの経過秒数。</summary>
    public float DeltaTime { get; private set; }

    /// <summary>ライブラリ初期化からの累計秒数。点滅や脈動の位相に使う。</summary>
    public float Time { get; private set; }

    /// <summary>Shift が押されているか。</summary>
    public bool Shift { get; private set; }

    /// <summary>Ctrl が押されているか。</summary>
    public bool Ctrl { get; private set; }

    /// <summary>Alt が押されているか。</summary>
    public bool Alt { get; private set; }

    /// <summary>マウス座標が有効か。ウィンドウ外へ出ると false になる。</summary>
    public bool HasMousePos { get; private set; }

    /// <summary>フレーム冒頭で ImGui から入力を取り込む。</summary>
    public void NewFrame()
    {
        var io = ImGui.GetIO();

        this.DeltaTime = io.DeltaTime;
        this.Time += this.DeltaTime;

        var pos = io.MousePos;

        // ImGui はマウスが画面外にあるとき -FLT_MAX を入れてくる
        this.HasMousePos = pos.X > -100000f && pos.Y > -100000f;
        this.MousePos = this.HasMousePos ? pos : new Vector2(float.MinValue, float.MinValue);
        this.MouseDelta = io.MouseDelta;

        this.WheelY = io.MouseWheel;
        this.WheelX = io.MouseWheelH;

        this.Shift = io.KeyShift;
        this.Ctrl = io.KeyCtrl;
        this.Alt = io.KeyAlt;

        for (var i = 0; i < ButtonCount; i++)
        {
            var button = (ImGuiMouseButton)i;
            var isDown = ImGui.IsMouseDown(button);

            this.pressed[i] = isDown && !this.down[i];
            this.released[i] = !isDown && this.down[i];
            this.doubleClicked[i] = ImGui.IsMouseDoubleClicked(button);
            this.down[i] = isDown;

            if (this.pressed[i])
                this.pressPos[i] = this.MousePos;
        }
    }

    /// <summary>ボタンが押され続けているか。</summary>
    public bool IsDown(MouseButton button = MouseButton.Left) => this.down[(int)button];

    /// <summary>このフレームで押し込まれたか。</summary>
    public bool IsPressed(MouseButton button = MouseButton.Left) => this.pressed[(int)button];

    /// <summary>このフレームで離されたか。</summary>
    public bool IsReleased(MouseButton button = MouseButton.Left) => this.released[(int)button];

    /// <summary>このフレームでダブルクリックが成立したか。</summary>
    public bool IsDoubleClicked(MouseButton button = MouseButton.Left) => this.doubleClicked[(int)button];

    /// <summary>ボタンを押し始めた座標。</summary>
    public Vector2 PressPosition(MouseButton button = MouseButton.Left) => this.pressPos[(int)button];

    /// <summary>押し始めてからの総移動量。押していないときはゼロ。</summary>
    public Vector2 DragDelta(MouseButton button = MouseButton.Left)
        => this.IsDown(button) ? this.MousePos - this.pressPos[(int)button] : Vector2.Zero;

    /// <summary>
    /// しきい値を超えてドラッグしているか。わずかな手ぶれをクリックとして扱うために使う。
    /// </summary>
    public bool IsDragging(MouseButton button = MouseButton.Left, float threshold = 4f)
        => this.IsDown(button) && this.DragDelta(button).LengthSquared() >= threshold * threshold;

    /// <summary>キーが押され続けているか。</summary>
    public bool IsKeyDown(ImGuiKey key) => ImGui.IsKeyDown(key);

    /// <summary>キーがこのフレームで押されたか (<paramref name="repeat"/> でオートリピート)。</summary>
    public bool IsKeyPressed(ImGuiKey key, bool repeat = false) => ImGui.IsKeyPressed(key, repeat);

    /// <summary>キーがこのフレームで離されたか。</summary>
    public bool IsKeyReleased(ImGuiKey key) => ImGui.IsKeyReleased(key);
}
