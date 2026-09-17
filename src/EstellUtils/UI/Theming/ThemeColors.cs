namespace EstellUtils.UI.Theming;

/// <summary>
/// テーマの色トークン。すべて描画用の <c>uint</c> (0xAABBGGRR) で保持する。
/// </summary>
/// <remarks>
/// グラデーションを使う箇所は「上/下」または「明/暗」の 2 色で持つ。
/// FFXIV のネイティブ UI は金属質の縦グラデーションが特徴なので、
/// 単色で持つより表現の幅が広い。
/// </remarks>
public sealed class ThemeColors
{
    // ── ウィンドウ ────────────────────────────────────────────

    /// <summary>ウィンドウ地の上端色。</summary>
    public uint WindowTop { get; set; }

    /// <summary>ウィンドウ地の下端色。</summary>
    public uint WindowBottom { get; set; }

    /// <summary>ウィンドウの外枠。</summary>
    public uint WindowBorder { get; set; }

    /// <summary>ウィンドウ外枠の内側に引く細い線。立体感を出す。</summary>
    public uint WindowBorderInner { get; set; }

    /// <summary>
    /// ウィンドウ上部に薄く乗せる光沢。すりガラス風の質感を出すのに使う。
    /// 透明にすると描かれない。
    /// </summary>
    public uint WindowGloss { get; set; }

    /// <summary>タイトルバーの上端色。</summary>
    public uint TitleTop { get; set; }

    /// <summary>タイトルバーの下端色。</summary>
    public uint TitleBottom { get; set; }

    /// <summary>非アクティブ時のタイトルバー色。</summary>
    public uint TitleInactive { get; set; }

    /// <summary>タイトル文字。</summary>
    public uint TitleText { get; set; }

    /// <summary>タイトルバー下端の区切り線。</summary>
    public uint TitleUnderline { get; set; }

    // ── 面 (カード・セクション) ───────────────────────────────

    /// <summary>カードやセクションの地。</summary>
    public uint Surface { get; set; }

    /// <summary>ホバー時の面。</summary>
    public uint SurfaceHover { get; set; }

    /// <summary>選択・押下時の面。</summary>
    public uint SurfaceActive { get; set; }

    /// <summary>面の枠線。</summary>
    public uint SurfaceBorder { get; set; }

    // ── ウィジェット (ボタン等) ───────────────────────────────

    /// <summary>ウィジェット地の上端色。</summary>
    public uint WidgetTop { get; set; }

    /// <summary>ウィジェット地の下端色。</summary>
    public uint WidgetBottom { get; set; }

    /// <summary>ホバー時のウィジェット地 (上端)。</summary>
    public uint WidgetHoverTop { get; set; }

    /// <summary>ホバー時のウィジェット地 (下端)。</summary>
    public uint WidgetHoverBottom { get; set; }

    /// <summary>押下時のウィジェット地 (上端)。</summary>
    public uint WidgetActiveTop { get; set; }

    /// <summary>押下時のウィジェット地 (下端)。</summary>
    public uint WidgetActiveBottom { get; set; }

    /// <summary>ウィジェットの枠線。</summary>
    public uint WidgetBorder { get; set; }

    /// <summary>ホバー時のウィジェット枠線。</summary>
    public uint WidgetBorderHover { get; set; }

    /// <summary>無効なウィジェットの地。</summary>
    public uint WidgetDisabled { get; set; }

    // ── 文字 ──────────────────────────────────────────────────

    /// <summary>標準の文字色。</summary>
    public uint Text { get; set; }

    /// <summary>補足・説明文の色。</summary>
    public uint TextMuted { get; set; }

    /// <summary>無効状態の文字色。</summary>
    public uint TextDisabled { get; set; }

    /// <summary>見出しや強調の文字色。</summary>
    public uint TextHeading { get; set; }

    /// <summary>アクセント色の上に載る文字色。</summary>
    public uint TextOnAccent { get; set; }

    /// <summary>リンクの文字色。</summary>
    public uint TextLink { get; set; }

    // ── アクセント ────────────────────────────────────────────

    /// <summary>主役の強調色。</summary>
    public uint Accent { get; set; }

    /// <summary>ホバー時のアクセント。</summary>
    public uint AccentHover { get; set; }

    /// <summary>押下時のアクセント。</summary>
    public uint AccentActive { get; set; }

    /// <summary>控えめなアクセント (下線や背景)。</summary>
    public uint AccentMuted { get; set; }

    // ── 状態色 ────────────────────────────────────────────────

    /// <summary>成功・有効。</summary>
    public uint Success { get; set; }

    /// <summary>注意。</summary>
    public uint Warning { get; set; }

    /// <summary>危険・エラー。</summary>
    public uint Danger { get; set; }

    /// <summary>情報。</summary>
    public uint Info { get; set; }

    // ── 部品 ──────────────────────────────────────────────────

    /// <summary>スライダー等の溝。</summary>
    public uint Track { get; set; }

    /// <summary>溝の埋まっている部分。</summary>
    public uint TrackFill { get; set; }

    /// <summary>つまみ。</summary>
    public uint Knob { get; set; }

    /// <summary>つまみの枠線。</summary>
    public uint KnobBorder { get; set; }

    /// <summary>チェックマーク。</summary>
    public uint Checkmark { get; set; }

    /// <summary>区切り線。</summary>
    public uint Separator { get; set; }

    /// <summary>スクロールバーの溝。</summary>
    public uint ScrollbarTrack { get; set; }

    /// <summary>スクロールバーのつまみ。</summary>
    public uint ScrollbarGrab { get; set; }

    /// <summary>ホバー時のスクロールバーつまみ。</summary>
    public uint ScrollbarGrabHover { get; set; }

    /// <summary>選択範囲の背景。</summary>
    public uint Selection { get; set; }

    /// <summary>キーボードフォーカスの輪郭。</summary>
    public uint FocusRing { get; set; }

    /// <summary>影。</summary>
    public uint Shadow { get; set; }

    /// <summary>モーダル表示時に背後を覆う色。</summary>
    public uint Overlay { get; set; }

    /// <summary>ツールチップの地。</summary>
    public uint TooltipBackground { get; set; }

    /// <summary>ツールチップの枠線。</summary>
    public uint TooltipBorder { get; set; }

    /// <summary>この色セットの複製を作る。</summary>
    public ThemeColors Clone() => (ThemeColors)this.MemberwiseClone();
}
