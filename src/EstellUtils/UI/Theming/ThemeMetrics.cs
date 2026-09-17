using System.Numerics;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Theming;

/// <summary>
/// 寸法のトークン。単位はピクセル。<see cref="Theme.Scale"/> 適用後の値がここへ入る。
/// </summary>
public sealed class ThemeMetrics
{
    // ── ウィンドウ ────────────────────────────────────────────

    /// <summary>ウィンドウの角丸半径。</summary>
    public float WindowRounding { get; set; } = 4f;

    /// <summary>ウィンドウ外枠の太さ。</summary>
    public float WindowBorderWidth { get; set; } = 1f;

    /// <summary>ウィンドウ内側の余白。</summary>
    public EdgeInsets WindowPadding { get; set; } = EdgeInsets.All(10f);

    /// <summary>タイトルバーの高さ。</summary>
    public float TitleBarHeight { get; set; } = 28f;

    /// <summary>ウィンドウ端のリサイズ判定幅。</summary>
    public float ResizeGripSize { get; set; } = 14f;

    /// <summary>ウィンドウの影の広がり。</summary>
    public float WindowShadowSize { get; set; } = 10f;

    // ── ウィジェット ──────────────────────────────────────────

    /// <summary>ウィジェットの角丸半径。</summary>
    public float WidgetRounding { get; set; } = 3f;

    /// <summary>ウィジェットの枠線の太さ。</summary>
    public float WidgetBorderWidth { get; set; } = 1f;

    /// <summary>ボタン等の標準の高さ。</summary>
    public float WidgetHeight { get; set; } = 24f;

    /// <summary>ウィジェット内側の余白。</summary>
    public EdgeInsets WidgetPadding { get; set; } = EdgeInsets.Symmetric(8f, 4f);

    /// <summary>ウィジェットの最小幅。</summary>
    public float WidgetMinWidth { get; set; } = 40f;

    // ── カード・セクション ────────────────────────────────────

    /// <summary>カードの角丸半径。</summary>
    public float CardRounding { get; set; } = 4f;

    /// <summary>カード内側の余白。</summary>
    public EdgeInsets CardPadding { get; set; } = EdgeInsets.All(8f);

    /// <summary>セクション間の空き。</summary>
    public float SectionSpacing { get; set; } = 12f;

    // ── 間隔 ──────────────────────────────────────────────────

    /// <summary>最小の空き。</summary>
    public float SpacingXs { get; set; } = 2f;

    /// <summary>小さい空き。</summary>
    public float SpacingSm { get; set; } = 4f;

    /// <summary>標準の空き。</summary>
    public float SpacingMd { get; set; } = 8f;

    /// <summary>大きい空き。</summary>
    public float SpacingLg { get; set; } = 12f;

    /// <summary>最大の空き。</summary>
    public float SpacingXl { get; set; } = 20f;

    /// <summary>ウィジェットを縦横に並べるときの既定の空き。</summary>
    public Vector2 ItemSpacing { get; set; } = new(8f, 6f);

    /// <summary>ラベルとウィジェットの間の空き。</summary>
    public float LabelSpacing { get; set; } = 8f;

    // ── 部品 ──────────────────────────────────────────────────

    /// <summary>区切り線の太さ。</summary>
    public float SeparatorThickness { get; set; } = 1f;

    /// <summary>スクロールバーの幅。</summary>
    public float ScrollbarWidth { get; set; } = 10f;

    /// <summary>スクロールバーの角丸半径。</summary>
    public float ScrollbarRounding { get; set; } = 5f;

    /// <summary>アイコンの一辺。</summary>
    public float IconSize { get; set; } = 16f;

    /// <summary>チェックボックスの一辺。</summary>
    public float CheckboxSize { get; set; } = 16f;

    /// <summary>トグルスイッチの幅。</summary>
    public float ToggleWidth { get; set; } = 36f;

    /// <summary>トグルスイッチの高さ。</summary>
    public float ToggleHeight { get; set; } = 18f;

    /// <summary>
    /// スライダーの溝の高さ。0 以下ならウィジェットの高さいっぱいを使う。
    /// </summary>
    /// <remarks>
    /// 高さいっぱいのバーは埋まり具合が目立ちすぎて、画面の中で浮いてしまう。
    /// 細めの溝にして、つまみで位置を示すほうが落ち着いて見える。
    /// </remarks>
    public float SliderTrackHeight { get; set; } = 7f;

    /// <summary>スライダーのつまみの幅。</summary>
    public float SliderKnobWidth { get; set; } = 10f;

    /// <summary>
    /// スライダーのつまみの高さ。溝より高くすると、掴む場所がはっきりする。
    /// </summary>
    public float SliderKnobHeight { get; set; } = 18f;

    /// <summary>
    /// スライダーの値を表示する欄の幅。バーの右側に確保する。
    /// </summary>
    /// <remarks>
    /// 値をバーに重ねて描くと、つまみが数字へかぶって読めなくなる。
    /// 別の欄に分けておけば、つまみがどこにあっても値が読める。
    /// </remarks>
    public float SliderValueWidth { get; set; } = 58f;

    /// <summary>フォーカスリングの太さ。</summary>
    public float FocusRingWidth { get; set; } = 2f;

    /// <summary>ツールチップの角丸半径。</summary>
    public float TooltipRounding { get; set; } = 4f;

    /// <summary>ツールチップ内側の余白。</summary>
    public EdgeInsets TooltipPadding { get; set; } = EdgeInsets.Symmetric(8f, 6f);

    /// <summary>ツールチップの最大幅。これを超える説明文は折り返す。</summary>
    public float TooltipMaxWidth { get; set; } = 360f;

    /// <summary>タブの高さ。</summary>
    public float TabHeight { get; set; } = 26f;

    /// <summary>タブ内側の余白。</summary>
    public EdgeInsets TabPadding { get; set; } = EdgeInsets.Symmetric(12f, 4f);

    /// <summary>この寸法セットの複製を作る。</summary>
    public ThemeMetrics Clone() => (ThemeMetrics)this.MemberwiseClone();

    /// <summary>
    /// すべての寸法へ倍率を掛けた複製を返す。DPI や利用者の好みに合わせた拡大に使う。
    /// </summary>
    public ThemeMetrics Scaled(float scale)
    {
        if (scale == 1f)
            return this.Clone();

        var m = this.Clone();

        m.WindowRounding *= scale;
        m.WindowBorderWidth *= scale;
        m.WindowPadding *= scale;
        m.TitleBarHeight *= scale;
        m.ResizeGripSize *= scale;
        m.WindowShadowSize *= scale;

        m.WidgetRounding *= scale;
        m.WidgetBorderWidth *= scale;
        m.WidgetHeight *= scale;
        m.WidgetPadding *= scale;
        m.WidgetMinWidth *= scale;

        m.CardRounding *= scale;
        m.CardPadding *= scale;
        m.SectionSpacing *= scale;

        m.SpacingXs *= scale;
        m.SpacingSm *= scale;
        m.SpacingMd *= scale;
        m.SpacingLg *= scale;
        m.SpacingXl *= scale;
        m.ItemSpacing *= scale;
        m.LabelSpacing *= scale;

        m.SeparatorThickness *= scale;
        m.ScrollbarWidth *= scale;
        m.ScrollbarRounding *= scale;
        m.IconSize *= scale;
        m.CheckboxSize *= scale;
        m.ToggleWidth *= scale;
        m.ToggleHeight *= scale;
        m.SliderTrackHeight *= scale;
        m.SliderKnobWidth *= scale;
        m.SliderKnobHeight *= scale;
        m.SliderValueWidth *= scale;
        m.FocusRingWidth *= scale;

        m.TooltipRounding *= scale;
        m.TooltipPadding *= scale;
        m.TooltipMaxWidth *= scale;

        m.TabHeight *= scale;
        m.TabPadding *= scale;

        return m;
    }
}

/// <summary>
/// アニメーションのトークン。
/// </summary>
public sealed class ThemeMotion
{
    /// <summary>アニメーションを使うか。false にすると全ての遷移が即座に切り替わる。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>ホバー遷移の速度。大きいほど速く反応する。</summary>
    public float HoverSpeed { get; set; } = 22f;

    /// <summary>
    /// 押下から戻るときの速度。押した瞬間の反映は待たせないので、これは戻り専用。
    /// </summary>
    public float PressSpeed { get; set; } = 26f;

    /// <summary>チェックやトグルなど、ON/OFF が切り替わる速度。</summary>
    public float OpenSpeed { get; set; } = 14f;

    /// <summary>
    /// 折りたたみの開閉にかける時間 (秒)。
    /// </summary>
    /// <remarks>
    /// 開閉は「目標値へ指数的に近づく」方式だと最後がいつまでも終わらず、
    /// 畳まれ切る瞬間がはっきりしない。一定時間で進めてイージングを掛けるほうが
    /// 開いた・閉じたが伝わりやすい。
    /// </remarks>
    public float CollapseDuration { get; set; } = 0.17f;

    /// <summary>ウィンドウの最小化・小窓化にかける時間 (秒)。</summary>
    public float WindowResizeDuration { get; set; } = 0.15f;

    /// <summary>スクロールの追従速度。</summary>
    public float ScrollSpeed { get; set; } = 20f;

    /// <summary>通知の出入りの速度。</summary>
    public float ToastSpeed { get; set; } = 10f;

    /// <summary>既定のイージング。</summary>
    public EaseKind Ease { get; set; } = EaseKind.OutCubic;

    /// <summary>ツールチップが出るまでの待ち時間 (秒)。</summary>
    public float TooltipDelay { get; set; } = 0.35f;

    /// <summary>この設定セットの複製を作る。</summary>
    public ThemeMotion Clone() => (ThemeMotion)this.MemberwiseClone();
}
