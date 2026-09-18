# ウィジェットとレイアウト

すべて `EUi` の静的メソッドとして提供されます。

## 戻り値の扱い

ウィジェットは `WidgetResult` を返します。`bool` への暗黙変換があり、
「クリックされた、または値が変わった」を意味します。

```csharp
if (EUi.Button("保存"))
    config.Save();

if (EUi.Checkbox("デバッグ", ref debug))
    config.Save();
```

チェーンで装飾を足せます。

```csharp
EUi.Button("削除", ButtonStyle.Danger)
   .Tip("この操作は元に戻せません。")
   .TipIf(locked, "ロック中は実行できません。");
```

| プロパティ | 意味 |
|---|---|
| `Clicked` | クリックされた |
| `Changed` | 値が変わった |
| `Hovered` / `Held` | マウスが乗っている / 押下中 |
| `Activated` / `Deactivated` | 操作が始まった / 終わった |
| `DoubleClicked` / `RightClicked` | ダブルクリック / 右クリック |
| `Rect` | ウィジェットが占める矩形 |

ドラッグ終了時にだけ保存したい場合は `Deactivated` を使います。

### 文字が切られたとき

幅に収まらない文字は省略記号で切られますが、**黙って消えることはありません**。

- 戻り値の `Truncated` が立つので、コードから判定できます
- 既定では全文がツールチップで出ます（`tipWhenTruncated: false` で止められます）
- `ellipsize: false` にすると切らずにはみ出すので、レイアウトの不足に気づけます

```csharp
var result = EUi.Label(path);

if (result.Truncated)
    EUi.Muted("(幅が足りていません)");
```

## 通知

```csharp
EUi.Toast("保存しました", "設定をファイルへ書き出しました。", NoteKind.Success);
EUi.Toast("見出しなしでも出せます。", NoteKind.Info);
```

種類ごとにアイコンと色が変わり（情報・成功・注意・危険）、下端に残り時間が出ます。
**マウスを乗せている間は時間が止まる**ので、読んでいる最中に消えません。
クリックするとその場で閉じます。

## テキスト

| API | 説明 |
|---|---|
| `EUi.Label(text, color, align)` | 1 行。幅に収まらない場合は末尾を省略記号にする |
| `EUi.Muted(text)` | 控えめな色のラベル |
| `EUi.TextColored(text, color)` | 色を指定した 1 行。`ImGui.TextColored` の置き換え |
| `EUi.Paragraph(text, color)` | 幅に合わせて折り返す |
| `EUi.WrapColored(text, color)` | 色を指定して折り返す。`Paragraph` と同じもの |
| `EUi.Note(text, kind)` | **囲み付き**の注記ボックス（Info / Success / Warning / Danger） |
| `EUi.Heading(text)` | 大きめのフォントの見出し |
| `EUi.Bullet(text)` | 行頭に点を打つ箇条書き |
| `EUi.LabelClipped(text, maxWidth, color, align)` | 幅を決めて 1 行表示。溢れたら省略し、全文をツールチップで見せる |
| `EUi.Selectable(label, selected, width, height)` | 選択できる 1 行。一覧を自前で組むときに |
| `EUi.Image(texture, size, tint)` | 画像。アイテムアイコンなどの表示に |
| `EUi.ImageButton(texture, id, size)` | 押せる画像 |
| `EUi.Separator(label)` | 区切り線。ラベルを渡すと線の中に文字を挟む |
| `EUi.Toast(message, kind, duration)` | 画面隅に出る通知。ウィンドウが閉じていても見える |
| `EUi.Toast(title, message, kind, duration)` | 見出し付きの通知 |

### 色付きテキストと注記ボックスの違い

名前が似ていますが、出るものが違います。

| | 見た目 | 使いどころ |
|---|---|---|
| `TextColored` / `WrapColored` | 文字の色が変わるだけ | 本文の一部を目立たせる。`ImGui.TextColored` の置き換え |
| `Note` | 枠・地・アイコン付きの囲み | 独立した注意書き。文章の流れから切り離したいとき |

色を直接渡すほか、`NoteKind` を渡してテーマに任せることもできます。

```csharp
EUi.TextColored("⚠ 試験機能です。", new Vector4(1f, 0.4f, 0.4f, 1f));
EUi.TextColored("⚠ 試験機能です。", NoteKind.Warning);   // 色はテーマ任せ

EUi.Note("この機能は試験中です。動作の保証はありません。", NoteKind.Warning);
EUi.Note(text, NoteKind.Warning, boxed: false);          // WrapColored と同じ
```

`Note` の本文は通常の文字色で描きます。囲みの側が種類を伝えるので、
文字まで状態色にすると読みづらくなるためです。
`EUi.NoteColor(kind)` で同じ色を取り出せるので、独自のウィジェットでも揃えられます。

## 操作

| API | 説明 |
|---|---|
| `EUi.Button(label, style, width, disabled)` | `Normal` / `Primary` / `Danger` / `Ghost` / `Link` |
| `EUi.IconButton(icon, id, style, disabled)` | FontAwesome の文字を渡す正方形ボタン |
| `EUi.Checkbox(label, ref value, disabled)` | ラベル部分もクリックできる |
| `EUi.Toggle(label, ref value, disabled)` | トグルスイッチ |
| `EUi.Radio(label, selected, disabled)` | 単体のラジオボタン |
| `EUi.RadioGroup(id, ref index, labels, horizontal)` | 選択肢から 1 つ選ぶ |

## 数値

| API | 説明 |
|---|---|
| `EUi.SliderInt(label, ref value, min, max, width, suffix, disabled)` | 整数スライダー |
| `EUi.SliderFloat(label, ref value, min, max, width, suffix, disabled, decimals)` | 小数スライダー。ドラッグ中に Shift で微調整 |
| `EUi.ProgressBar(fraction, overlay, width, height)` | 進捗バー。文字を省略すると百分率 |

スライダーは「バー / 値 / ラベル」の 3 つを横に並べます。
値をバーへ重ねると、つまみが数字にかぶって読めなくなるため、欄を分けています。

```
[████████░░░░░░]   22   ホバーの速さ
```

見た目はテーマトークンで調整できます。

| トークン | 既定 | 説明 |
|---|---|---|
| `SliderTrackHeight` | 7 | 溝の高さ。0 にするとウィジェットの高さいっぱいのバーになる |
| `SliderKnobWidth` | 10 | つまみの幅 |
| `SliderKnobHeight` | 18 | つまみの高さ。溝より高くすると掴む場所がはっきりする |
| `SliderValueWidth` | 58 | 値を表示する欄の幅 |

当たり判定はウィジェットの高さ全体なので、溝を細くしても掴みにくくはなりません。

## 入力・選択

| API | 説明 |
|---|---|
| `EUi.TextInput(id, ref value, hint, maxLength, width, disabled)` | 1 行入力。IME 対応 |
| `EUi.TextArea(id, ref value, height, maxLength, disabled)` | 複数行入力 |
| `EUi.Combo(id, ref index, items, width, disabled)` | ドロップダウン |
| `EUi.ListBox(id, ref index, items, height, disabled)` | スクロールする一覧 |
| `EUi.ColorEdit(id, ref color, showAlpha, width)` | 色見本 + 自前のカラーピッカー |
| `EUi.InputInt(id, ref value, step, min, max, width)` | 整数の直接入力。増減ボタン付き |
| `EUi.InputFloat(id, ref value, step, min, max, width)` | 小数の直接入力 |

座標やピクセル数のように範囲の広い値は、スライダーでは合わせきれません。
そうした値は `InputInt` / `InputFloat` で直接打ち込みます。

`ColorEdit` は `Vector4` と `uint`(0xAABBGGRR) の両方に対応します。

## ポップアップ・メニュー・確認ダイアログ

| API | 説明 |
|---|---|
| `EUi.OpenPopup(id)` | ポップアップを開く |
| `EUi.IsPopupOpen(id)` / `EUi.ClosePopup()` | 開閉の確認と、中からの明示的な閉じ |
| `EUi.Popup(id, size, anchor, padding)` | 中身を自由に書けるポップアップ |
| `EUi.Menu(id, anchor, entries)` | メニュー。選ばれた項目の添字を返す |
| `EUi.ContextMenu(id, target, entries)` | 右クリックで開くメニュー |
| `EUi.Confirm(id, title, message, ok, cancel, danger)` | 確認ダイアログ |

**開く操作と中身の描画は別々に書きます。** 描画側は毎フレーム呼び、
開いていなければ何もしません。即時モードなので、これが自然な形になります。

```csharp
if (EUi.Button("初期化", ButtonStyle.Danger))
    EUi.OpenPopup("confirmReset");

// 毎フレーム呼ぶ。開いていなければ ConfirmResult.None が返るだけ
if (EUi.Confirm("confirmReset", "設定の初期化",
                "すべての設定を既定値へ戻します。この操作は元に戻せません。",
                "初期化する", danger: true) == ConfirmResult.Ok)
{
    ResetAll();
}
```

確認ダイアログは外側をクリックしても閉じません。Esc で取り消しになります。
背後を暗く覆いたい場合は `dimBackground: true` を渡します（既定は覆いません）。

### メニュー

項目は文字列のまま渡せます。細かく指定したいものだけ `MenuEntry` にします。

```csharp
switch (EUi.Menu("fileMenu",
                 new MenuEntry("開く") { Shortcut = "Ctrl+O" },
                 new MenuEntry("保存") { Shortcut = "Ctrl+S" },
                 MenuEntry.Separator,
                 new MenuEntry("グリッドを表示") { Checked = this.showGrid },
                 MenuEntry.Separator,
                 new MenuEntry("削除") { Kind = NoteKind.Danger }))
{
    case 0: Open(); break;
    case 1: Save(); break;
    case 3: this.showGrid = !this.showGrid; break;
    case 5: Delete(); break;
}
```

**添字は渡した並びのままです。** 区切り線もひとつ分を数えるので、上の例では
「削除」が 5 になります。項目の増減で添字がずれる点に注意してください。

`MenuEntry` で指定できるもの: `Disabled` / `Checked` / `Shortcut`（表示のみ）/
`Kind`（状態色。危険な操作を赤くする）/ `IsSeparator`。

### 右クリックメニュー

`ContextMenu` は、渡したウィジェットの戻り値が右クリックされていれば開きます。

```csharp
var row = EUi.Selectable(item.Name, item == selected);

switch (EUi.ContextMenu("rowMenu", row, "コピー", "名前を変更",
                        MenuEntry.Separator, new MenuEntry("削除") { Kind = NoteKind.Danger }))
{
    case 0: Copy(item); break;
    case 1: Rename(item); break;
    case 3: Delete(item); break;
}
```

一覧の各行に付ける場合は、**行ごとに ID を分けてください。**
`EUi.PushId(index)` の中で呼ぶのが確実です。同じ id を使い回すと、
どの行で開いたのか区別できなくなります。

### 中身が自由なポップアップ

大きさは呼び出し側が決めます。即時モードでは中身を描き終えるまで高さが分からず、
前フレームの実測に頼ると開いた瞬間にちらつくためです。

```csharp
using (var popup = EUi.Popup("detail", new Vector2(280f, 150f)))
{
    if (popup.IsOpen)
    {
        EUi.Heading("詳細");
        EUi.SliderInt("値", ref this.value, 1, 10);

        if (EUi.Button("閉じる", ButtonStyle.Primary, SizeSpec.Fill))
            EUi.ClosePopup();
    }
}
```

`PopupAnchor` で置く位置を選べます。
`BelowLastItem`（既定）/ `AboveLastItem` / `MousePosition` / `ScreenCenter`。
どれを選んでも、画面の外へはみ出さないよう収められます。

## 器

| API | 説明 |
|---|---|
| `EUi.Card(id, padding)` | 枠と地を持つ箱 |
| `EUi.Section(label, collapsible, defaultOpen)` | 折りたためる見出し付きの区画 |
| `EUi.Section(label, body)` | コールバック版。閉じているときは中身が呼ばれない |
| `EUi.LabelColumn(id, minWidth, maxWidth)` | この中の `Field` のラベル幅を揃える |
| `EUi.Field(label, labelWidth)` | 「ラベル + ウィジェット」の 1 行 |
| `EUi.TabBar(id, labels...)` | タブ。選択中のタブを返す |

```csharp
using (var section = EUi.Section("共通設定"))
{
    if (!section.IsVisible)
        return;

    using (EUi.LabelColumn("common"))
    {
        using (EUi.Field("更新間隔"))
            EUi.SliderInt("##interval", ref interval, 1, 6);

        using (EUi.Field("表示方式"))
            EUi.Combo("##mode", ref mode, ModeNames);
    }
}
```

## まとめて無効にする

前提条件が揃わないときは、ひとまとまりの操作を丸ごと止められます。
ウィジェットごとに `disabled:` を書いて回る必要はありません。

```csharp
using (EUi.Disabled(!this.config.OverlayEnabled))
{
    EUi.SliderInt("更新間隔", ref interval, 1, 6);
    EUi.Checkbox("デバッグ表示", ref debug);
}
```

## 後からツールチップを付ける

戻り値へ `.Tip()` をつなげられない場面（戻り値を返さない自前のラッパーや、
`using` を返す `Section` のあと）では `EUi.Tip()` を使います。

```csharp
using (var s = EUi.Section("試験機能"))
{
    EUi.Tip("動作が不安定になる場合があります。");
    // ...
}
```

## 文字の大きさを測る

| API | 説明 |
|---|---|
| `EUi.Measure(text)` | 描画サイズ。結果はフォントごとにキャッシュされる |
| `EUi.MeasureWrapped(text, width)` | 折り返したときのサイズ。領域の高さを先に決めたいときに |
| `EUi.Truncate(text, maxWidth)` | 幅に収まるよう切り詰めた文字列 |
| `EUi.LineHeight` | 現在のフォントでの行の高さ |

```csharp
var height = EUi.MeasureWrapped(description, EUi.AvailableWidth).Y;

using (EUi.Scroll("desc", height + EUi.Metrics.SpacingMd))
    EUi.Paragraph(description);
```

## クリップボード

```csharp
EUi.SetClipboard(diagnosticsText);
var pasted = EUi.GetClipboard();
```

## 色の指定

色は `uint`（0xAABBGGRR）で受け取りますが、`Vector4`（RGBA, 0〜1）のオーバーロードも
用意しています。ImGui / Dalamud 由来のコードをそのまま移せます。

```csharp
EUi.Label("警告", new Vector4(1f, 0.4f, 0.3f, 1f));
EUi.Paragraph(text, someVector4Color);

// 変換もできる
var packed = EuColor.FromVector(vector4);
var vector = EuColor.ToVector(packed);
```

## レイアウト

| API | 説明 |
|---|---|
| `EUi.VStack(spacing)` | 縦に積む |
| `EUi.HStack(spacing, wrap, align, rowHeight)` | 横に並べる。`wrap` で折り返す |
| `EUi.Row(columns...)` | 列幅を先に宣言した横並び |
| `EUi.Grid(columnCount, spacing)` | 均等幅で折り返す |
| `EUi.Inset(padding, spacing)` | 内側に余白を取った縦積み |
| `EUi.Region(bounds, padding, spacing)` | 明示した矩形の中へ配置する |
| `EUi.Sized(width, height, padding)` | 大きさを固定した領域の中へ配置する |
| `EUi.Scroll(id, height, spacing)` | はみ出すとスクロールする領域 |
| `EUi.Spacing(amount)` / `EUi.NewLine()` | 空きを入れる / 次の行へ |
| `EUi.Reserve(size)` | 領域だけ確保して矩形を得る（独自描画用） |

### 縦方向の揃え

ボタン (24px) と文字 (16px) のように高さの違う要素を並べると、上端で揃えた場合に
文字だけ浮いて見えます。横並びは**既定で縦中央に揃えます**。

```csharp
using (EUi.HStack())                      // 既定: 縦中央
using (EUi.HStack(align: Align.Start))    // 上端で揃える
using (EUi.HStack(align: Align.Stretch))  // 行の高さいっぱいに引き伸ばす
using (EUi.Row(Align.End, 120f, SizeSpec.Fill))   // 列宣言つきの行でも指定できる
```

行の基準の高さは既定でテーマの標準ウィジェット高さです。`rowHeight` で変えられ、
`0` を渡すと揃えを行わず要素の高さをそのまま使います。
基準より背の高い要素が来た場合は、その要素に合わせて行が広がります。

### 列幅の指定

`SizeSpec` は `float` から暗黙変換されるので、固定幅は数値だけで書けます。

```csharp
using (EUi.Row(120f, SizeSpec.Fill, 60f))          // 固定 / 残り全部 / 固定
using (EUi.Row(SizeSpec.Weight(2f), SizeSpec.Weight(1f)))  // 2:1 で分ける
using (EUi.Row(SizeSpec.Ratio(0.3f), SizeSpec.Fill))       // 3 割 / 残り
```

**列幅の指定は「入るならこの幅で」という意味で、はみ出す許可ではありません。**
固定幅の合計が行に収まらない場合は、はみ出す代わりに比例で縮みます。

### 行からはみ出させない

固定幅を自分で計算するとき、**列の間の隙間を引き忘れる**のがよくある間違いです。
右端の要素が枠を突き抜ける形で必ず表に出ます。

```csharp
// 誤り: 3 列なら隙間は 2 つ入る。その分だけ右へはみ出す
var fieldWidth = totalWidth - (buttonWidth * 2f);

// 正しい
var fieldWidth = totalWidth - (buttonWidth * 2f) - EUi.ColumnSpacing(3);
```

**そもそも 1 列を `SizeSpec.Fill` にすれば、残り幅の計算はレイアウト側が行います。**
自分で引き算をしないのが一番確実です。

```csharp
using (EUi.Row(SizeSpec.Fill, 24f, 24f))   // 入力欄が残りを取る
```

配分そのものは `ColumnLayout.Resolve` に切り出してあり、
「合計が行幅を超えない」ことを自己検証で機械的に確かめています。

## 表

列定義を呼び出し側で保持し、見出しと各行へ同じものを渡します。

```csharp
private static readonly TableColumn[] Columns =
[
    new("名前", SizeSpec.Fill),
    new("種別", 90f),
    new("数", 56f, Align.End),
];

EUi.TableHeader(Columns);

for (var i = 0; i < items.Count; i++)
{
    using (EUi.TableRow(Columns, i, selected: i == selectedIndex))
    {
        EUi.TableCell(items[i].Name);
        EUi.TableCell(items[i].Kind);
        EUi.TableCell(items[i].Count.ToString(), Align.End);
    }
}
```

## 独自ウィジェットを書く

ライブラリ内のウィジェットも、ここで公開しているものと同じ部品だけで作られています。
内部だけが使える近道はありません。

1 つのウィジェットは「**領域を取る → 入力を判定する → 描く**」の 3 段です。
最初の 2 段を `EUi.Custom` がまとめて行うので、描画だけが残ります。

```csharp
public static bool Rating(ReadOnlySpan<char> id, ref int value, int max = 5)
{
    var cellSize = EUi.Metrics.WidgetHeight;
    var widget = EUi.Custom(id, SizeSpec.Px(cellSize * max), cellSize);

    // マウスがどの星の上にいるか
    var hoverIndex = -1;

    if (widget.Result.Hovered)
        hoverIndex = Math.Clamp(
            (int)((EUi.Input.MousePos.X - widget.Rect.Min.X) / cellSize), 0, max - 1);

    var changed = false;

    if (widget.Result.Clicked && hoverIndex >= 0 && hoverIndex + 1 != value)
    {
        value = hoverIndex + 1;
        changed = true;
    }

    // 乗っている間はそこまでを点灯させて見せる
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
                Align.Center, Align.Center, ellipsize: false);
        }
    }

    return changed;
}
```

これはデモの「ポップアップ」タブで実際に動いています。

| API | 説明 |
|---|---|
| `EUi.Custom(id, width, height, flags, disabled)` | 領域の確保と入力判定をまとめて行う |
| `EUi.Custom(id, size, flags, disabled)` | 大きさを `Vector2` で指定する版 |
| `EUi.CustomAt(id, rect, flags, disabled)` | 矩形を指定する版。レイアウトは進めない |
| `EUi.State(id)` | フレームをまたいで値を覚える（`ref` で返る） |
| `EUi.Input` / `EUi.DeltaTime` | マウス・キーの状態、前フレームからの経過秒数 |

`CustomWidget` が持つもの:

- `Rect` — 確保した矩形
- `Visual` — ホバー・押下・無効の遷移量。**既存の見た目を借りることもできます**
  （`EUi.WidgetPainter.DrawButton(w.Visual, "文字", ButtonStyle.Primary)` のように）
- `Result` — 入力の結果。そのまま `return` して呼び出し側へ返せます

### 状態を覚える

開閉やアニメーションの進み具合など、フレームをまたいで覚えたい値は `EUi.State` に置きます。
`Custom0` / `Custom1` が自由に使える枠です。しばらく使われなかった状態は自動で捨てられます。

```csharp
var w = EUi.Custom("spinner", SizeSpec.Px(24f), 24f);
ref var state = ref EUi.State(w.Id);

state.Custom0 += EUi.DeltaTime;   // 回転角として使う
```

### もっと低い層から書く

`EUi.Custom` を使わず、`Interaction.Behavior` を直接呼ぶこともできます。
1 つのウィジェットの中で複数の当たり判定を持たせる場合など、
細かく制御したいときはこちらです。

```csharp
var ctx = EUi.Context;
ctx.EnsureFrame();

var id = ctx.GetId(label, out var display);
var rect = EUi.Reserve(SizeSpec.Fill, EUi.Metrics.WidgetHeight);
var interaction = Interaction.Behavior(rect, id);

var color = EuColor.Lerp(EUi.Colors.Surface, EUi.Colors.Accent, interaction.HoverAmount);

Painter.Rect(rect, color, EUi.Metrics.WidgetRounding);
TextPainter.TextIn(rect, EUi.Colors.Text, display, Align.Center, Align.Center);

return interaction.Clicked;
```
