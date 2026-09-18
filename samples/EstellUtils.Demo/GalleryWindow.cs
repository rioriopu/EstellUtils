using System;
using System.Globalization;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

using EstellUtils.UI;
using EstellUtils.UI.Binding;
using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Theming.Presets;
using EstellUtils.UI.Widgets;
using EstellUtils.UI.Windowing;

namespace EstellUtils.Demo;

/// <summary>
/// EstellUtils の全ウィジェットを並べたギャラリー。実機で見た目と操作感を確認する。
/// </summary>
public sealed class GalleryWindow : EuWindow
{
    private static readonly string[] ThemeNames = ["FFXIV ネイティブ風", "モダンダーク", "すりガラス"];

    private static readonly string[] ComboItems =
    [
        "アイテム A", "アイテム B", "アイテム C", "アイテム D",
        "アイテム E", "アイテム F", "アイテム G",
    ];

    private static readonly TableColumn[] TableColumns =
    [
        new("名前", SizeSpec.Fill),
        new("種別", 90f),
        new("数", 56f, Align.End),
    ];

    private static readonly (string Name, string Kind, int Count)[] TableRows =
    [
        ("ハイスチール・ソード", "武器", 1),
        ("アラガンレザーベルト", "防具", 3),
        ("霊銀鉱", "素材", 128),
        ("バターロール", "食材", 42),
        ("マテリジャ", "マテリア", 7),
    ];

    /// <summary>負荷確認で使い回す文字列。毎フレームの文字列生成を避けるため。</summary>
    private static readonly string[] StressIds = ["#01", "#02", "#03", "#04", "#05", "#06", "#07", "#08"];

    private static readonly string[] StressNames =
    [
        "リムサ・ロミンサ：上甲板層",
        "グリダニア：新市街",
        "ウルダハ：ナル回廊",
        "イシュガルド：上層",
        "クガネ",
        "クリスタリウム",
        "オールド・シャーレアン",
        "トライヨラ",
    ];

    private static readonly string[] StressValues = ["12", "48", "105", "7", "230", "64", "19", "88"];

    private readonly DemoConfig config;
    private readonly Binder<DemoConfig> binder;
    private readonly Theme xivTheme = XivNativeTheme.Create();
    private readonly Theme modernTheme = ModernDarkTheme.Create();
    private readonly Theme glassTheme = FrostedGlassTheme.Create();

    private bool checkboxValue = true;
    private bool toggleValue;
    private bool disabledCheck = true;
    private int radioIndex;
    private int sliderInt = 3;
    private float sliderFloat = 0.6f;
    private float progress;
    private int comboIndex;
    private int listIndex = 1;
    private string textValue = string.Empty;
    private string multilineValue = "複数行の入力欄です。\n改行もそのまま扱えます。";
    private Vector4 colorValue = new(0.85f, 0.70f, 0.41f, 1f);
    private int themeIndex;
    private float scale = 1f;
    private int stressRows = 300;
    private int posX = 120;
    private float factor = 1.25f;
    private bool sectionEnabled = true;

    // ポップアップの確認用
    private string popupLog = "(まだ何も選ばれていません)";
    private bool menuShowGrid = true;
    private bool menuShowLabels;
    private int popupRowIndex = -1;
    private int ratingValue = 3;

    /// <summary>ギャラリーを作る。</summary>
    public GalleryWindow()
        : base("EstellUtils ギャラリー")
    {
        this.Size = new Vector2(640f, 520f);
        this.MinSize = new Vector2(420f, 320f);

        // 小窓を有効にすると、タイトルバーに切り替えボタンが出る。
        // 小窓は本体とは別のウィンドウとして開く
        this.HasCompanion = true;
        this.CompanionSize = new Vector2(300f, 170f);

        this.CloseOnEscape = true;
        this.ShowLockButton = true;

        // タイトルバーへ好きなボタンを足せる。アイコンは FontAwesome の文字を渡す
        this.TitleBarButtons.Add(new TitleBarButton
        {
            Id = "demoGithub",
            Icon = FontAwesomeIcon.Bell.ToIconString(),
            Tooltip = "通知を出す",
            OnClick = () => EUi.Toast(
                "タイトルバーから実行", "追加したボタンから処理を呼び出しました。", NoteKind.Info),
        });

        this.TitleBarButtons.Add(new TitleBarButton
        {
            Id = "demoTheme",
            Icon = FontAwesomeIcon.Palette.ToIconString(),
            Tooltip = "テーマを切り替える",
            IsActive = () => this.themeIndex != 0,
            OnClick = () =>
            {
                this.themeIndex = (this.themeIndex + 1) % ThemeNames.Length;
                this.ApplyTheme();
            },
        });

        this.config = new DemoConfig();
        this.binder = new Binder<DemoConfig>(this.config, () => this.config.SaveCount++);
    }

    /// <inheritdoc/>
    public override void OnClose() => this.binder.Flush();

    /// <summary>
    /// 小窓の中身。よく使う操作だけを残す。
    /// </summary>
    public override void DrawCompact()
    {
        this.UpdateProgress();

        EUi.Muted("小窓（本体とは別のウィンドウ）");

        using (EUi.Row(SizeSpec.Fill, SizeSpec.Fill))
        {
            if (EUi.Combo("##compactTheme", ref this.themeIndex, ThemeNames))
                this.ApplyTheme();

            if (EUi.Button("通知##compact", ButtonStyle.Primary, SizeSpec.Fill))
                EUi.Toast("小窓から実行", "小窓のボタンから処理を呼び出しました。", NoteKind.Success);
        }

        EUi.Toggle("デバッグ表示##compact", ref this.toggleValue);
        EUi.ProgressBar(this.progress);
    }

    /// <inheritdoc/>
    public override void Draw()
    {
        this.UpdateProgress();

        var tabs = EUi.TabBar(
            "##galleryTabs",
            "ウィジェット", "レイアウト", "ポップアップ", "テーマ", "設定バインディング", "動作確認");

        EUi.Spacing(EUi.Metrics.SpacingSm);

        switch (tabs.Selected)
        {
            case 0:
                this.DrawWidgetsTab();
                break;
            case 1:
                this.DrawLayoutTab();
                break;
            case 2:
                this.DrawPopupsTab();
                break;
            case 3:
                this.DrawThemeTab();
                break;
            case 4:
                this.DrawBindingTab();
                break;
            default:
                this.DrawDiagnosticsTab();
                break;
        }
    }

    /// <summary>ウィジェットの一覧。</summary>
    private void DrawWidgetsTab()
    {
        using var id = EUi.PushId("widgets");

        EUi.Separator("ボタン");

        using (EUi.HStack())
        {
            EUi.Button("標準").Tip("ButtonStyle.Normal");
            EUi.Button("主要", ButtonStyle.Primary).Tip("ButtonStyle.Primary");
            EUi.Button("危険", ButtonStyle.Danger).Tip("ButtonStyle.Danger");
            EUi.Button("ゴースト", ButtonStyle.Ghost).Tip("ButtonStyle.Ghost");
            EUi.Button("リンク", ButtonStyle.Link).Tip("ButtonStyle.Link");
        }

        using (EUi.HStack())
        {
            EUi.Button("無効##disabled", ButtonStyle.Normal, null, true);
            EUi.Button("無効##disabled2", ButtonStyle.Primary, null, true);

            if (EUi.Button("通知を出す"))
                EUi.Toast("実行しました", "ボタンが押されたので処理を行いました。", NoteKind.Success);
        }

        EUi.Separator("切り替え");

        using (EUi.HStack(16f))
        {
            EUi.Checkbox("チェックボックス", ref this.checkboxValue).Tip("クリックで切り替わります。");
            EUi.Toggle("トグル", ref this.toggleValue);
            EUi.Checkbox("無効##disabledCheck", ref this.disabledCheck, true);
        }

        EUi.RadioGroup("##radios", ref this.radioIndex, ["選択肢 1", "選択肢 2", "選択肢 3"], horizontal: true);

        EUi.Separator("数値");

        EUi.SliderInt("整数スライダー", ref this.sliderInt, 1, 10, 220f, "回")
           .Tip("つまみを掴むか、溝をクリックしてください。");

        EUi.SliderFloat("小数スライダー", ref this.sliderFloat, 0f, 1f, 220f, default, false, 2)
           .Tip("ドラッグ中に Shift を押すと細かく動きます。");

        EUi.ProgressBar(this.progress, default, 220f);

        EUi.Separator("入力");

        using (EUi.LabelColumn("widgetFields"))
        {
            using (EUi.Field("テキスト"))
                EUi.TextInput("##text", ref this.textValue, "ここに入力", 64);

            using (EUi.Field("ドロップダウン"))
                EUi.Combo("##combo", ref this.comboIndex, ComboItems);

            using (EUi.Field("色"))
                EUi.ColorEdit("##color", ref this.colorValue);
        }

        EUi.Muted("複数行入力");
        EUi.TextArea("##multiline", ref this.multilineValue, 70f);

        EUi.Muted("一覧");
        EUi.ListBox("##list", ref this.listIndex, ComboItems, 110f);

        EUi.Separator("数値の直接入力");

        using (EUi.LabelColumn("numberFields"))
        {
            using (EUi.Field("X 座標"))
                EUi.InputInt("##posX", ref this.posX, 1, -4000, 4000);

            using (EUi.Field("倍率"))
                EUi.InputFloat("##factor", ref this.factor, 0.05f, 0f, 10f);
        }

        EUi.Muted("範囲の広い値は、スライダーより直接入力のほうが合わせやすい。");

        EUi.Separator("まとめて無効化");

        EUi.Checkbox("この節を有効にする", ref this.sectionEnabled);

        using (EUi.Disabled(!this.sectionEnabled))
        {
            EUi.SliderInt("内側のスライダー", ref this.sliderInt, 1, 10, 200f, "回");

            using (EUi.HStack())
            {
                EUi.Button("内側のボタン##grouped");
                EUi.Checkbox("内側のチェック##grouped", ref this.checkboxValue);
            }
        }

        EUi.Separator("長い文字列");

        EUi.LabelClipped(
            @"C:\Users\Administrator\AppData\Roaming\XIVLauncher\pluginConfigs\MaskedDalamud\shaders\overlay.hlsl",
            SizeSpec.Fill);

        EUi.Muted("幅に収まらない分は省略され、全文はマウスを乗せると出ます。");

        using (EUi.PushFont(FontRole.Mono))
            EUi.Label("等幅フォント:  0x1A2B3C4D   ( 1234, 5678 )");

        EUi.Separator("色付きテキスト — 文字の色だけが変わる");

        EUi.TextColored("⚠ 試験機能です。動作の保証はありません。", NoteKind.Warning);
        EUi.TextColored("色を直接渡すこともできます。", new Vector4(0.6f, 0.85f, 1f, 1f));
        EUi.WrapColored(
            "WrapColored は折り返します。ImGui の PushStyleColor + TextWrapped + PopStyleColor を " +
            "1 行に置き換えるためのものです。",
            NoteKind.Danger);

        EUi.Separator("注記ボックス — 枠と地とアイコンが付く");

        EUi.Note("情報: 通常の補足説明です。", NoteKind.Info);
        EUi.Note("注意: 設定によっては動作が重くなります。", NoteKind.Warning);
        EUi.Note("危険: この操作は元に戻せません。", NoteKind.Danger);
        EUi.Note("boxed: false にすると WrapColored と同じになります。", NoteKind.Info, boxed: false);

        EUi.Bullet("箇条書きの項目。長い文章でも領域の幅に合わせて折り返されます。");
    }

    /// <summary>ポップアップ・メニュー・確認ダイアログの確認。</summary>
    private void DrawPopupsTab()
    {
        using var id = EUi.PushId("popups");

        EUi.Separator("メニュー");

        using (EUi.HStack())
        {
            if (EUi.Button("メニューを開く"))
                EUi.OpenPopup("demoMenu");

            EUi.Muted("押した位置の下に開きます。");
        }

        // 開く操作と中身の描画は別々に書く。ここは毎フレーム呼ばれる
        switch (EUi.Menu(
            "demoMenu",
            new MenuEntry("開く") { Shortcut = "Ctrl+O" },
            new MenuEntry("保存") { Shortcut = "Ctrl+S" },
            new MenuEntry("名前を付けて保存") { Disabled = true },
            MenuEntry.Separator,
            new MenuEntry("グリッドを表示") { Checked = this.menuShowGrid },
            new MenuEntry("ラベルを表示") { Checked = this.menuShowLabels },
            MenuEntry.Separator,
            new MenuEntry("削除") { Kind = NoteKind.Danger, Shortcut = "Del" }))
        {
            case 0:
                this.popupLog = "「開く」が選ばれました。";
                break;
            case 1:
                this.popupLog = "「保存」が選ばれました。";
                break;
            case 4:
                this.menuShowGrid = !this.menuShowGrid;
                this.popupLog = $"グリッドの表示を {(this.menuShowGrid ? "ON" : "OFF")} にしました。";
                break;
            case 5:
                this.menuShowLabels = !this.menuShowLabels;
                this.popupLog = $"ラベルの表示を {(this.menuShowLabels ? "ON" : "OFF")} にしました。";
                break;
            case 7:
                EUi.OpenPopup("demoConfirm");
                break;
        }

        EUi.Separator("右クリックメニュー");

        EUi.Muted("行を右クリックしてください。");

        for (var i = 0; i < 4; i++)
        {
            // 行ごとに ID を分ける。同じ id を使い回すと、どの行で開いたか区別できない
            using var rowId = EUi.PushId(i);

            var row = EUi.Selectable(ComboItems[i], i == this.popupRowIndex);

            if (row.Clicked)
                this.popupRowIndex = i;

            switch (EUi.ContextMenu(
                "rowMenu", row,
                "コピー",
                "名前を変更",
                MenuEntry.Separator,
                new MenuEntry("削除") { Kind = NoteKind.Danger }))
            {
                case 0:
                    this.popupLog = $"「{ComboItems[i]}」をコピーしました。";
                    break;
                case 1:
                    this.popupLog = $"「{ComboItems[i]}」の名前を変更します。";
                    break;
                case 3:
                    this.popupLog = $"「{ComboItems[i]}」を削除しました。";
                    break;
            }
        }

        EUi.Separator("確認ダイアログ");

        using (EUi.HStack())
        {
            if (EUi.Button("初期化", ButtonStyle.Danger))
                EUi.OpenPopup("demoConfirm");

            if (EUi.Button("中身が自由なポップアップ"))
                EUi.OpenPopup("demoFreePopup");
        }

        switch (EUi.Confirm(
            "demoConfirm",
            "設定の初期化",
            "すべての設定を既定値へ戻します。この操作は元に戻せません。",
            "初期化する",
            danger: true))
        {
            case ConfirmResult.Ok:
                this.popupLog = "初期化を実行しました。";
                EUi.Toast("初期化しました", "すべての設定を既定値へ戻しました。", NoteKind.Warning);
                break;
            case ConfirmResult.Cancel:
                this.popupLog = "初期化を取り消しました。";
                break;
        }

        using (var popup = EUi.Popup("demoFreePopup", new Vector2(280f, 150f)))
        {
            if (popup.IsOpen)
            {
                EUi.Heading("好きなものを置けます");
                EUi.Paragraph("ポップアップの中でも、通常のウィジェットがそのまま使えます。");
                EUi.SliderInt("値", ref this.sliderInt, 1, 10);

                if (EUi.Button("閉じる", ButtonStyle.Primary, SizeSpec.Fill))
                    EUi.ClosePopup();
            }
        }

        EUi.Separator("結果");

        EUi.Note(this.popupLog);

        EUi.Separator("独自ウィジェット");

        EUi.Muted("下の星は EUi.Custom だけで書いてあります。ライブラリ内のウィジェットと同じ部品です。");

        using (EUi.Field("評価"))
        {
            if (Rating("demoRating", ref this.ratingValue))
                this.popupLog = $"評価を {this.ratingValue} にしました。";
        }
    }

    /// <summary>
    /// <see cref="EUi.Custom"/> だけで書いた独自ウィジェットの例。星で評価を選ぶ。
    /// </summary>
    /// <remarks>
    /// 「領域を取る → 入力を判定する → 描く」の 3 段だけで書けることを示すための見本。
    /// ライブラリ内のウィジェットも、これと同じ部品しか使っていない。
    /// </remarks>
    private static bool Rating(ReadOnlySpan<char> id, ref int value, int max = 5)
    {
        var cellSize = EUi.Metrics.WidgetHeight;
        var widget = EUi.Custom(id, SizeSpec.Px(cellSize * max), cellSize);

        // マウスがどの星の上にいるか。乗っている間はそこまでを点灯させて見せる
        var hoverIndex = -1;

        if (widget.Result.Hovered)
        {
            var offset = (EUi.Input.MousePos.X - widget.Rect.Min.X) / cellSize;
            hoverIndex = Math.Clamp((int)offset, 0, max - 1);
        }

        var changed = false;

        if (widget.Result.Clicked && hoverIndex >= 0 && hoverIndex + 1 != value)
        {
            value = hoverIndex + 1;
            changed = true;
        }

        var lit = hoverIndex >= 0 ? hoverIndex + 1 : value;

        using (EUi.PushFont(FontRole.Icon))
        {
            for (var i = 0; i < max; i++)
            {
                var cell = Rect.FromSize(
                    new Vector2(widget.Rect.Min.X + (cellSize * i), widget.Rect.Min.Y),
                    new Vector2(cellSize, cellSize));

                TextPainter.TextIn(
                    cell,
                    i < lit ? EUi.Colors.Warning : EUi.Colors.TextDisabled,
                    FontAwesomeIcon.Star.ToIconString(),
                    Align.Center,
                    Align.Center,
                    ellipsize: false);
            }
        }

        return changed;
    }

    /// <summary>レイアウトの確認。</summary>
    private void DrawLayoutTab()
    {
        using var id = EUi.PushId("layout");

        EUi.Separator("Row — 列幅を先に宣言する");

        using (EUi.Row(100f, SizeSpec.Fill, 80f))
        {
            EUi.Label("固定 100px");
            EUi.Button("残り幅いっぱい##rowFill", ButtonStyle.Normal, SizeSpec.Fill);
            EUi.Button("固定 80##row80", ButtonStyle.Normal, SizeSpec.Fill);
        }

        using (EUi.Row(SizeSpec.Weight(2f), SizeSpec.Weight(1f)))
        {
            EUi.Button("重み 2##w2", ButtonStyle.Normal, SizeSpec.Fill);
            EUi.Button("重み 1##w1", ButtonStyle.Normal, SizeSpec.Fill);
        }

        EUi.Separator("縦方向の揃え");

        EUi.Muted("高さの違う要素を並べたとき、既定では縦中央に揃います。");

        using (EUi.HStack(align: Align.Start))
        {
            EUi.Label("上端揃え:");
            EUi.Button("ボタン##alignStart");
            EUi.Checkbox("チェック##alignStart", ref this.checkboxValue);
        }

        using (EUi.HStack())
        {
            EUi.Label("中央揃え:");
            EUi.Button("ボタン##alignCenter");
            EUi.Checkbox("チェック##alignCenter", ref this.checkboxValue);
        }

        EUi.Separator("Grid — 均等割りで折り返す");

        using (EUi.Grid(4))
        {
            for (var i = 1; i <= 8; i++)
                EUi.Button($"{i}##grid{i}", ButtonStyle.Normal, SizeSpec.Fill);
        }

        EUi.Separator("Card と Section");

        using (EUi.Card("demoCard"))
        {
            EUi.Label("カードの中身です。");
            EUi.Muted("高さは中身に合わせて自動で決まります。");

            using (EUi.HStack())
            {
                EUi.Button("操作 A##card");
                EUi.Button("操作 B##card");
            }
        }

        using (var section = EUi.Section("折りたためるセクション"))
        {
            if (section.IsVisible)
            {
                EUi.Paragraph(
                    "見出しをクリックすると開閉します。開閉はアニメーションし、状態はフレームをまたいで保持されます。");
                EUi.Checkbox("セクション内のチェック##sectionCheck", ref this.checkboxValue);
            }
        }

        EUi.Separator("Table");

        EUi.TableHeader(TableColumns);

        for (var i = 0; i < TableRows.Length; i++)
        {
            var row = TableRows[i];

            using (EUi.TableRow(TableColumns, i, selected: i == this.listIndex))
            {
                EUi.TableCell(row.Name);
                EUi.TableCell(row.Kind);
                EUi.TableCell(row.Count.ToString(CultureInfo.InvariantCulture), Align.End);
            }
        }

        EUi.Separator("ScrollArea");

        using (EUi.Scroll("demoScroll", 120f))
        {
            for (var i = 1; i <= 30; i++)
                EUi.Label($"スクロールする行 {i}");
        }
    }

    /// <summary>テーマの切り替えと色トークンの一覧。</summary>
    private void DrawThemeTab()
    {
        using var id = EUi.PushId("theme");

        EUi.Paragraph("テーマはトークンの集まりです。切り替えても画面の構造は変わりません。");

        using (EUi.Row(120f, SizeSpec.Fill))
        {
            EUi.Label("テーマ");

            if (EUi.Combo("##themeSelect", ref this.themeIndex, ThemeNames))
                this.ApplyTheme();
        }

        if (EUi.SliderFloat("拡大率", ref this.scale, 0.75f, 2f, 220f, default, false, 2))
            this.ApplyScale();

        var opacity = this.Opacity;
        if (EUi.SliderFloat("ウィンドウの不透明度", ref opacity, 0.25f, 1f, 220f, default, false, 2))
            this.Opacity = opacity;

        EUi.Muted("「すりガラス」テーマは、透過・上端の光沢・明るい細枠で厚みのある板に見せています。");

        EUi.Separator("角の丸み");

        var metrics = EUi.Metrics;

        var widgetRounding = metrics.WidgetRounding;
        if (EUi.SliderFloat("ウィジェット", ref widgetRounding, 0f, 12f, 220f, "px", false, 1))
            metrics.WidgetRounding = widgetRounding;

        var windowRounding = metrics.WindowRounding;
        if (EUi.SliderFloat("ウィンドウ", ref windowRounding, 0f, 16f, 220f, "px", false, 1))
            metrics.WindowRounding = windowRounding;

        EUi.Separator("スライダーの形");

        var trackHeight = metrics.SliderTrackHeight;
        if (EUi.SliderFloat("溝の高さ", ref trackHeight, 0f, 24f, 200f, "px", false, 0)
                .Tip("0 にするとウィジェットの高さいっぱいのバーになります。"))
        {
            metrics.SliderTrackHeight = trackHeight;
        }

        var knobWidth = metrics.SliderKnobWidth;
        if (EUi.SliderFloat("つまみの幅", ref knobWidth, 3f, 20f, 200f, "px", false, 0))
            metrics.SliderKnobWidth = knobWidth;

        var knobHeight = metrics.SliderKnobHeight;
        if (EUi.SliderFloat("つまみの高さ", ref knobHeight, 6f, 28f, 200f, "px", false, 0))
            metrics.SliderKnobHeight = knobHeight;

        EUi.Muted("拡大率を変えると、テーマの既定値へ戻ります。");

        EUi.Separator("動きの速さ");

        var motion = EUi.Motion;

        var animEnabled = motion.Enabled;
        if (EUi.Toggle("アニメーションを使う", ref animEnabled))
            motion.Enabled = animEnabled;

        var hoverSpeed = motion.HoverSpeed;
        if (EUi.SliderFloat("ホバーの速さ", ref hoverSpeed, 6f, 40f, 220f, default, !animEnabled, 0))
            motion.HoverSpeed = hoverSpeed;

        var pressSpeed = motion.PressSpeed;
        if (EUi.SliderFloat("押下の戻り", ref pressSpeed, 6f, 40f, 220f, default, !animEnabled, 0)
                .Tip("押した瞬間は待たせないので、これは離したあとの戻り速度です。"))
        {
            motion.PressSpeed = pressSpeed;
        }

        var collapse = motion.CollapseDuration;
        if (EUi.SliderFloat("折りたたみの時間", ref collapse, 0.05f, 0.6f, 220f, "秒", !animEnabled, 2))
            motion.CollapseDuration = collapse;

        EUi.Separator("色トークン");

        var colors = EUi.Colors;

        DrawSwatchRow("Accent / AccentHover / AccentActive", colors.Accent, colors.AccentHover, colors.AccentActive);
        DrawSwatchRow("Text / TextMuted / TextDisabled", colors.Text, colors.TextMuted, colors.TextDisabled);
        DrawSwatchRow("Success / Warning / Danger", colors.Success, colors.Warning, colors.Danger);
        DrawSwatchRow("Widget 上 / 下 / 枠", colors.WidgetTop, colors.WidgetBottom, colors.WidgetBorder);
        DrawSwatchRow("Window 上 / 下 / 枠", colors.WindowTop, colors.WindowBottom, colors.WindowBorder);

        EUi.Separator("描画プリミティブ");

        var canvas = EUi.Reserve(SizeSpec.Fill, 90f);
        DrawPrimitiveShowcase(canvas);
    }

    /// <summary>設定バインディングの確認。</summary>
    private void DrawBindingTab()
    {
        using var id = EUi.PushId("binding");

        EUi.Paragraph(
            "この画面は DemoConfig クラスの属性だけから生成されています。" +
            "画面側のコードは binder.DrawAll() の 1 行だけです。");

        this.binder.DrawSearchBox();
        EUi.Spacing();

        this.binder.DrawAll();

        EUi.Separator();

        using (EUi.HStack())
        {
            if (EUi.Button("既定値へ戻す", ButtonStyle.Danger))
            {
                this.binder.ResetAll();
                EUi.Toast("既定値へ戻しました", "すべての設定を初期状態に戻しました。", NoteKind.Warning);
            }

            EUi.Muted(this.binder.HasChanges() ? "既定値から変更されています" : "すべて既定値です");
        }

        EUi.Muted($"保存が実行された回数: {this.config.SaveCount}");
        EUi.Muted("スライダーをドラッグ中は保存されず、マウスを離したときにまとめて保存されます。");
    }

    /// <summary>動作状況の表示。</summary>
    private void DrawDiagnosticsTab()
    {
        using var id = EUi.PushId("diagnostics");

        var ctx = EUi.Context;
        var io = ImGui.GetIO();

        using (EUi.LabelColumn("diag"))
        {
            DrawStat("フレームレート", $"{io.Framerate:F1} fps");
            DrawStat("フレーム時間", $"{ctx.DeltaTime * 1000f:F2} ms");
            DrawStat("経過時間", $"{ctx.Time:F1} 秒");
            DrawStat("ライブラリのフレーム番号", ctx.FrameCount.ToString(CultureInfo.InvariantCulture));
            DrawStat("保持中のウィジェット状態", ctx.Store.StateCount.ToString(CultureInfo.InvariantCulture));
            DrawStat("ホバー中の ID", ctx.HotId.IsNone ? "なし" : ctx.HotId.ToString());
            DrawStat("操作中の ID", ctx.ActiveId.IsNone ? "なし" : ctx.ActiveId.ToString());
            DrawStat("レイアウトの深さ", ctx.Layout.Depth.ToString(CultureInfo.InvariantCulture));
            DrawStat("タイトルバー高さ", $"{EUi.Metrics.TitleBarHeight:F0} px");
            DrawStat("ウィンドウ実寸", $"{io.DisplaySize.X:F0} x {io.DisplaySize.Y:F0} の画面 / 本体 {this.Size.X:F0} x {this.Size.Y:F0}");
        }

        EUi.Separator("通知");

        EUi.Muted("マウスを乗せると時間が止まり、クリックで閉じられます。");

        using (EUi.HStack(wrap: true))
        {
            if (EUi.Button("情報##toastInfo"))
                EUi.Toast("読み込み完了", "アイテムの一覧を 1,284 件読み込みました。", NoteKind.Info);

            if (EUi.Button("成功##toastOk"))
                EUi.Toast("保存しました", "設定をファイルへ書き出しました。", NoteKind.Success);

            if (EUi.Button("注意##toastWarn"))
                EUi.Toast("設定を確認してください", "オーバーレイ更新間隔が 1 のため、動作が重くなる場合があります。", NoteKind.Warning);

            if (EUi.Button("危険##toastErr"))
                EUi.Toast("処理に失敗しました", "テクスチャの読み込みに失敗しました。詳細は /xllog を確認してください。", NoteKind.Danger);

            if (EUi.Button("見出しなし##toastPlain"))
                EUi.Toast("見出しを付けない通知です。", NoteKind.Info);
        }

        EUi.Separator("負荷の確認");

        using (EUi.Row(160f, SizeSpec.Fill))
        {
            EUi.Label("行数");
            EUi.SliderInt("##stressRows", ref this.stressRows, 0, 2000, SizeSpec.Fill, "行");
        }

        EUi.Muted("画面に映っていない行は描画を省くので、行数を増やしても fps はほとんど落ちません。");

        if (this.stressRows > 0)
        {
            // 文字列を毎フレーム作ると、測っているのが描画性能ではなく
            // 文字列生成と GC になってしまうので、あらかじめ用意したものを使い回す
            using (EUi.Scroll("stressList", 140f, 0f))
            {
                for (var i = 0; i < this.stressRows; i++)
                {
                    using (EUi.Row(70f, SizeSpec.Fill, 80f))
                    {
                        EUi.Label(StressIds[i % StressIds.Length]);
                        EUi.Label(StressNames[i % StressNames.Length]);
                        EUi.Label(StressValues[i % StressValues.Length], null, Align.End);
                    }
                }
            }
        }

        EUi.Separator("生 ImGui との混在");

        EUi.Paragraph(
            "EUi.RawImGui() のスコープで囲むと、生の ImGui をそのまま書けます。" +
            "囲まないと ImGui 側のカーソルが合わず、見えない場所へ描かれてしまいます。");

        using (EUi.RawImGui())
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), "これは ImGui.TextColored です");
            ImGui.SmallButton("これは ImGui.SmallButton です");

            ImGui.BeginChild("rawChild", new Vector2(0f, 60f), true);
            ImGui.TextUnformatted("ImGui.BeginChild の中です");
            ImGui.TextUnformatted("スクロールも ImGui 側の仕組みがそのまま動きます");
            ImGui.EndChild();
        }

        EUi.Label("ここから再び EstellUtils の描画に戻ります。");
    }

    /// <summary>色見本を 3 つ並べた行を描く。</summary>
    private static void DrawSwatchRow(string label, uint a, uint b, uint c)
    {
        using (EUi.Row(SizeSpec.Fill, 40f, 40f, 40f))
        {
            EUi.Label(label);
            DrawSwatch(a);
            DrawSwatch(b);
            DrawSwatch(c);
        }
    }

    /// <summary>色見本を 1 つ描く。</summary>
    private static void DrawSwatch(uint color)
    {
        var rect = EUi.Reserve(SizeSpec.Fill, EUi.Metrics.WidgetHeight);

        Painter.Rect(rect, color, EUi.Metrics.WidgetRounding);
        Painter.RectOutline(rect, EUi.Colors.WidgetBorder, 1f, EUi.Metrics.WidgetRounding);
    }

    /// <summary>描画プリミティブの見本。</summary>
    private static void DrawPrimitiveShowcase(Rect canvas)
    {
        var colors = EUi.Colors;
        var y = canvas.Center.Y;
        var x = canvas.Min.X + 40f;

        // グラデーション付きの角丸矩形
        var box = Rect.FromCenter(new Vector2(x, y), new Vector2(70f, 40f));
        Painter.RectGradientV(box, colors.WidgetHoverTop, colors.WidgetBottom, 6f);
        Painter.RectOutline(box, colors.WidgetBorderHover, 1f, 6f);
        x += 100f;

        // 影
        var shadowBox = Rect.FromCenter(new Vector2(x, y), new Vector2(60f, 36f));
        Painter.Shadow(shadowBox, colors.Shadow, 8f, 4f, new Vector2(0f, 3f));
        Painter.Rect(shadowBox, colors.Surface, 4f);
        x += 100f;

        // リング
        Painter.Ring(new Vector2(x, y), 14f, 22f, colors.Accent, 0f, MathF.PI * 1.4f);
        x += 80f;

        // チェックマークとシェブロン
        var markRect = Rect.FromCenter(new Vector2(x, y), new Vector2(32f, 32f));
        Painter.Check(markRect, colors.Success, 3f);
        x += 60f;

        Painter.Chevron(Rect.FromCenter(new Vector2(x, y), new Vector2(32f, 32f)), Direction.Right, colors.Accent, 2.5f);
    }

    /// <summary>ラベル付きの 1 行を描く。</summary>
    private static void DrawStat(string label, string value)
    {
        using (EUi.Field(label))
            EUi.Label(value, EUi.Colors.TextHeading);
    }

    /// <summary>
    /// 進捗バーのデモ用の値を進める。
    /// </summary>
    /// <remarks>
    /// 以前は「ウィジェット」タブの描画中にしか更新しておらず、小窓だけを開いていると
    /// 止まって見えていた。表示している場所に関係なく進むよう、描画の入口で更新する。
    /// </remarks>
    private void UpdateProgress()
        => this.progress = (MathF.Sin(EUi.Context.Time * 0.8f) * 0.5f) + 0.5f;

    /// <summary>選択中のテーマを反映する。</summary>
    private void ApplyTheme()
    {
        var theme = this.themeIndex switch
        {
            0 => this.xivTheme,
            1 => this.modernTheme,
            _ => this.glassTheme,
        };

        theme.Scale = this.scale;

        ThemeManager.SetDefault(theme);
        EUi.Toast("テーマを切り替えました", $"「{theme.Name}」を適用しました。", NoteKind.Info);
    }

    /// <summary>拡大率を反映する。</summary>
    private void ApplyScale()
    {
        this.xivTheme.Scale = this.scale;
        this.modernTheme.Scale = this.scale;
        this.glassTheme.Scale = this.scale;
    }
}
