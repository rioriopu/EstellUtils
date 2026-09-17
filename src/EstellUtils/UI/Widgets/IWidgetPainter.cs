using System;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Widgets;

/// <summary>ウィンドウのタイトルバーに並ぶボタンの種類。</summary>
public enum WindowButtonKind
{
    /// <summary>閉じる。</summary>
    Close,

    /// <summary>折りたたむ / 展開する。</summary>
    Collapse,

    /// <summary>小窓にする / 元へ戻す。</summary>
    Compact,

    /// <summary>元のウィンドウへ戻す。</summary>
    Restore,

    /// <summary>位置と大きさを固定する / 解除する。</summary>
    Lock,

    /// <summary>設定を開く。</summary>
    Settings,
}

/// <summary>ボタンの見た目の種類。</summary>
public enum ButtonStyle
{
    /// <summary>標準。</summary>
    Normal,

    /// <summary>主要な操作。アクセント色で塗る。</summary>
    Primary,

    /// <summary>破壊的な操作。</summary>
    Danger,

    /// <summary>枠と地を持たない。ホバー時だけ薄く反応する。</summary>
    Ghost,

    /// <summary>文字だけ。リンクのように見せる。</summary>
    Link,
}

/// <summary>
/// ウィジェットを描くのに必要な状態。
/// </summary>
/// <remarks>
/// 入力判定の結果 (<see cref="InteractionResult"/>) から作られる。
/// 描画側は矩形とこの値だけを見て絵を決めるため、入力処理と描画を独立に差し替えられる。
/// </remarks>
public readonly record struct WidgetVisual
{
    /// <summary>ウィジェットの矩形。</summary>
    public Rect Rect { get; init; }

    /// <summary>ホバー遷移量 (0〜1)。</summary>
    public float Hover { get; init; }

    /// <summary>押下遷移量 (0〜1)。</summary>
    public float Press { get; init; }

    /// <summary>無効遷移量 (0〜1)。</summary>
    public float Disabled { get; init; }

    /// <summary>ON / 選択状態か。</summary>
    public bool On { get; init; }

    /// <summary>ON への遷移量 (0〜1)。チェックマークの描き込みなどに使う。</summary>
    public float OnAmount { get; init; }

    /// <summary>0〜1 に正規化した値。スライダーや進捗表示で使う。</summary>
    public float Value { get; init; }

    /// <summary>キーボードフォーカスを持っているか。</summary>
    public bool Focused { get; init; }

    /// <summary>入力結果から作る。</summary>
    public static WidgetVisual From(in InteractionResult interaction, bool on = false, float onAmount = 0f, float value = 0f)
        => new()
        {
            Rect = interaction.Rect,
            Hover = interaction.HoverAmount,
            Press = interaction.PressAmount,
            Disabled = interaction.DisabledAmount,
            On = on,
            OnAmount = onAmount,
            Value = value,
        };
}

/// <summary>
/// ウィジェットの見た目を描く責務。
/// </summary>
/// <remarks>
/// <para>
/// テーマのトークン変更だけでは足りない見た目にしたい場合は、
/// <see cref="DefaultWidgetPainter"/> を継承して必要なメソッドだけ差し替え、
/// <c>theme.Painter</c> に設定する。ボタンだけ画像にする、といったことができる。
/// </para>
/// <para>
/// 入力判定はライブラリ側が持つため、実装側は描画だけに集中すればよい。
/// </para>
/// </remarks>
public interface IWidgetPainter
{
    /// <summary>ボタンを描く。</summary>
    void DrawButton(in WidgetVisual visual, ReadOnlySpan<char> label, ButtonStyle style);

    /// <summary>チェックボックスの四角部分を描く。</summary>
    void DrawCheckbox(in WidgetVisual visual);

    /// <summary>ラジオボタンの丸部分を描く。</summary>
    void DrawRadio(in WidgetVisual visual);

    /// <summary>トグルスイッチを描く。</summary>
    void DrawToggle(in WidgetVisual visual);

    /// <summary>スライダーの溝とつまみを描く。</summary>
    void DrawSlider(in WidgetVisual visual, Rect knob);

    /// <summary>進捗バーを描く。</summary>
    void DrawProgressBar(in WidgetVisual visual, ReadOnlySpan<char> overlay);

    /// <summary>カード (枠付きの面) を描く。</summary>
    void DrawCard(in WidgetVisual visual);

    /// <summary>セクションの見出しを描く。</summary>
    void DrawSectionHeader(in WidgetVisual visual, ReadOnlySpan<char> label, bool collapsible);

    /// <summary>区切り線を描く。ラベルがあれば線の中に挟む。</summary>
    void DrawSeparator(Rect rect, ReadOnlySpan<char> label);

    /// <summary>タブを描く。</summary>
    void DrawTab(in WidgetVisual visual, ReadOnlySpan<char> label);

    /// <summary>入力欄の枠を描く。</summary>
    void DrawInputFrame(in WidgetVisual visual);

    /// <summary>ウィンドウの枠・タイトルバーを描く。</summary>
    /// <param name="window">ウィンドウ全体の矩形。</param>
    /// <param name="titleBar">タイトルバーの矩形 (背景を描く範囲)。</param>
    /// <param name="titleTextArea">
    /// タイトル文字を置ける範囲。右側のボタンを除いた領域が渡される。
    /// </param>
    /// <param name="title">タイトル。</param>
    /// <param name="focused">このウィンドウが手前にあるか。</param>
    void DrawWindowChrome(Rect window, Rect titleBar, Rect titleTextArea, ReadOnlySpan<char> title, bool focused);

    /// <summary>タイトルバーのボタンを描く。</summary>
    void DrawWindowButton(in WidgetVisual visual, WindowButtonKind kind);

    /// <summary>
    /// タイトルバーへ追加されたアイコンボタンを描く。
    /// アイコンフォントが適用された状態で呼ばれる。
    /// </summary>
    void DrawTitleBarIconButton(in WidgetVisual visual, ReadOnlySpan<char> icon);

    /// <summary>ウィンドウ右下のリサイズグリップを描く。</summary>
    void DrawResizeGrip(in WidgetVisual visual);
}
