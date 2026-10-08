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
| `Focused` | キーボードの焦点が当たっているか |
| `Committed` | 入力の編集が終わり、値が変わったか |
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
| `EUi.Muted(text, align, wrap)` | 控えめな色のラベル。`wrap: true` で折り返す |
| `EUi.MutedParagraph(text)` | 控えめな色で折り返す。`Muted(text, wrap: true)` と同じ |
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
| `EUi.Icon(icon, color)` | FontAwesome のアイコンを 1 つ |
| `EUi.IconText(icon, text, color, textColor)` | アイコンと文字を並べる |
| `EUi.RichLabel(parts...)` | 色の違う断片を 1 行に並べる。`SameLine` の置き換え |
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
| `EUi.SmallButton(label, style, width, disabled)` | 行の中へ小さく収める。`ImGui.SmallButton` の置き換え |
| `ButtonStyle.Prominent` | 最も押してほしい操作。大きめの文字と明るい枠 |
| `EUi.ButtonAt(id, rect, label, style, disabled)` | 矩形を指定して描く。高さも自由 |
| `EUi.ButtonWidth(label)` | ラベルに合わせた幅。行を自分で配るときに |
| `EUi.IconButton(icon, id, style, disabled)` | FontAwesome の文字を渡す正方形ボタン |
| `EUi.Checkbox(label, ref value, disabled, size)` | ラベル部分もクリックできる |
| `EUi.Checkbox(label, ref bool? value, …)` | 三状態。`null` は「一部だけ ON」で横棒になる |
| `EUi.Toggle(label, ref value, disabled)` | トグルスイッチ |
| `EUi.Radio(label, selected, disabled)` | 単体のラジオボタン |
| `EUi.RadioGroup(id, ref index, labels, horizontal)` | 選択肢から 1 つ選ぶ |

### ラベルを付けない

`Checkbox` / `Toggle` / `Radio` は、ラベルを `##` だけにすると**スイッチや四角だけを描きます**。
表の中や、別にラベル列を持つ場合はこの形になります。

```csharp
EUi.Toggle("##target_JobBars", ref enabled);   // スイッチだけ

using (EUi.Field("ジョブバー"))                 // ラベルは Field 側で出す
    EUi.Toggle("##jobBars", ref enabled);
```

`##` の後ろは ID にのみ使われるので、**同じ画面に複数置くときは別の文字にしてください**。

## 数値

| API | 説明 |
|---|---|
| `EUi.SliderInt(label, ref value, min, max, width, suffix, disabled)` | 整数スライダー |
| `EUi.SliderFloat(label, ref value, min, max, width, suffix, disabled, decimals)` | 小数スライダー。ドラッグ中に Shift で微調整 |
| `EUi.ProgressBar(fraction, overlay, width, height)` | 進捗バー。文字を省略すると百分率 |

**`max` より後ろの引数は名前付きで渡してください。**
`SliderFloat` は `decimals` が最後にあり、`SizeSpec` は数値から暗黙変換されるため、
`EUi.SliderFloat("x", ref v, 0f, 1f, 3)` と書くと **3 は桁数ではなく幅 3px** になります。
エラーにならないので気づけません。

```csharp
EUi.SliderFloat("明るさ", ref v, 0f, 0.2f, decimals: 3);   // 名前付きで渡す
```

`InputInt` / `InputFloat` / `DragFloat` の 3 番目は `step` / `speed` です（ImGui と同じ並び）。
`EUi.InputInt("X", ref x, 0, 7680)` は `step=0` / `min=7680` になります。

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
| `EUi.TextInput(label, ref value, hint, maxLength, width, disabled)` | 1 行入力。IME 対応 |
| `EUi.TextArea(label, ref value, height, maxLength, disabled)` | 複数行入力 |
| `EUi.Combo(label, ref index, items, width, disabled)` | ドロップダウン |
| `EUi.ListBox(label, ref index, items, height, disabled)` | スクロールする一覧 |
| `EUi.ComboBody(label, preview, width, listHeight, disabled)` | 一覧の中身を自分で描くドロップダウン |
| `EUi.SelectableRow(id, selected, height, disabled)` | 中身を自分で描く選択行 |
| `EUi.SortableTableHeader(id, columns, ref sort, …)` | 押して並べ替えられる見出し |
| `EUi.SkipCell()` | セルを 1 つ飛ばす |
| `EUi.Place(width, height, horizontal, …)` | 場所を取ってから中へ並べる |
| `EUi.ColorEdit(id, ref color, showAlpha, width)` | 色見本 + 自前のカラーピッカー |
| `EUi.InputInt(label, ref value, step, min, max, width)` | 整数の直接入力。増減ボタン付き |
| `EUi.InputFloat(label, ref value, step, min, max, width)` | 小数の直接入力 |
| `EUi.DragFloat(label, ref value, speed, min, max, decimals, width)` | ドラッグで動かす。上限のはっきりしない値に |
| `EUi.InputVector2/3/4(id, ref value, labels, step, min, max)` | 数値の並び。色ではないベクトルに |
| `EUi.CheckboxFlags(label, ref value, mask)` | ビットマスクの 1 ビットを切り替える |
| `EUi.SearchBox(label, ref query, hint, width)` | 絞り込み欄。虫眼鏡と消しボタン付き |
| `EUi.SegmentedControl(id, ref index, options, width)` | 排他選択をひと続きで見せる |
| `EUi.KeyBind(label, ref binding, width)` | キー割り当て。修飾キーに対応 |

**第 1 引数はどのウィジェットでもラベル兼識別子です。**
`##` より前が画面に出て、全体が識別子になります（ImGui と同じ扱い）。
ラベルは本体の右へ添えられ、`width` はどれも「本体の幅」を指します。

```csharp
EUi.SliderFloat("音量", ref volume, 0f, 1f);
EUi.TextInput("保存先", ref path);           // 同じ並び。ラベルは右へ
EUi.TextInput("##path", ref path);           // ラベルを出さない
```

左にラベルを置きたい場合は `EUi.Field` で囲みます。

```csharp
using (EUi.Field("保存先"))
    EUi.TextInput("##path", ref path);
```

座標やピクセル数のように範囲の広い値は、スライダーでは合わせきれません。
そうした値は `InputInt` / `InputFloat` で直接打ち込みます。

### 一覧の中身を自分で描く

見出しを差し込む・項目ごとに色を変える・薄く見せるが押せる、といった一覧は
文字列の並びでは表せません。`ComboBody` を使います（`ImGui.BeginCombo` に当たるもの）。

```csharp
using (var list = EUi.ComboBody("監視する通貨##cur", current.Name, width: 320f))
{
    if (list.IsOpen)
    {
        foreach (var group in groups)
        {
            EUi.Muted(group.Kind);                      // 見出しを差し込む

            foreach (var item in group.Items)
            {
                if (EUi.Selectable(item.Name, item == current, color: item.Color))
                {
                    Pick(item);
                    list.Close();
                }
            }
        }
    }
}
```

**選んだら `Close()` を呼んでください。** 呼ばないと開いたままになります。

`list.Header` で閉じているときの欄の結果が取れるので、欄そのものに
ツールチップや右クリックのメニューを付けられます。
`list.JustOpened` は開いたフレームだけ true になるので、絞り込みの欄を空にする、
といった初期化に使えます。

行の中へ複数のものを並べたい場合は `SelectableRow` を使います。

```csharp
var row = EUi.SelectableRow("##item" + item.Id, item == current);

using (row)
{
    EUi.TextColored(item.Name, item.Color);
    EUi.Muted($"所持 {item.Count:N0}");
}

if (row.Clicked)
    Pick(item);
```

行全体が当たり判定になります。**クリックの判定は行を開いた時点で済んでいる**ので、
`using` を抜けたあとに読めます。

### 押せるが選べない項目

`disabled: true` にすると押せなくなり、「なぜ選べないのか」を知らせる機会が消えます。
薄く見せたいだけなら `color` を渡してください。

```csharp
if (EUi.Selectable(vendor.Name, i == index, color: locked ? EUi.Colors.TextDisabled : null))
{
    if (locked)
        ShowLockedMessage($"{vendor.Name}：達成度が足りません（{vendor.LockReason}）");
    else
        Pick(vendor);
}
```

### Enter だけを拾う

`Committed` は**焦点が外れたときにも立ちます**。「取り消し」ボタンを押して外れた場合も
確定として扱われるので、Enter だけを拾いたい場合は `Submitted` を見てください。

```csharp
var result = EUi.TextInput("名前", ref this.editing, autoFocus: true);

if (result.Submitted) Apply();     // Enter のときだけ
if (EUi.Button("やめる")) Cancel();
```

`autoFocus: true` にすると、初めて描かれたときに焦点が当たります（小窓を開いてすぐ打てます）。

### 打ち込んでいる最中かどうか

全角数字の正規化のように、入力中は表示用の控えを持ち、離れたら整えたい場合は
`Focused` を見ます。

```csharp
var result = EUi.TextInput("座標", ref this.editing);

if (!result.Focused)
    this.editing = Normalize(this.value);   // 打ち終わったら整える
```

### 入力が確定したとき

`Changed` は 1 文字打つたびに立ちます。ファイルパスや URL のように、
確定してから処理したい欄は `Committed` を見てください。

```csharp
if (EUi.TextInput("保存先", ref path).Committed)
    this.config.Save();
```

焦点が外れたとき、または Enter を押したときに、実際に書き換わっていれば立ちます
（ImGui の `IsItemDeactivatedAfterEdit()` と同じ意味）。

`Deactivated` は**マウスのボタンを離したこと**なので、入力欄の確定には使えません。
スライダーのドラッグ終了を拾うのはこちらです。

`ColorEdit` は `Vector4` と `uint`(0xAABBGGRR) の両方に対応します。

### キー割り当て

**`IKeyState` を `EUi.Initialize` へ渡してください。** ImGui 経由でキーを見ると、
FFXIV 本体が先に処理してしまうキーを拾えません。ゲームのキー状態を直接読むことで避けます。

```csharp
public Plugin(IDalamudPluginInterface pi, IPluginLog log, IKeyState keyState)
{
    EUi.Initialize(pi, log: log, keyState: keyState);
}
```

渡していない場合、割り当ての欄は「キー状態を取得できません」と表示して操作を受け付けません。

`KeyBinding` は単純なプロパティだけで構成してあるので、設定へそのまま保存できます。

```csharp
// 設定クラス。ref で渡すのでフィールドにする
public KeyBinding ToggleKey = new(VirtualKey.F9, Ctrl: true, Shift: false, Alt: false);

// 設定画面
if (EUi.KeyBind("切り替えキー", ref this.config.ToggleKey))
    this.config.Save();

// 判定したい場所（毎フレーム）
if (this.config.ToggleKey.IsPressed())
    this.Toggle();
```

欄を押すと待ち受け状態になり、次に押したキーを覚えます。
待ち受け中は **Esc で取り消し**、**右クリックで解除**です。
修飾キー単体は割り当てられず、他のキーと一緒に押すと組み合わせになります。

`IsPressed()` は修飾キーが指定どおりでなければ成立しません。
`Ctrl+F9` を割り当てた場合、`Ctrl+Shift+F9` では反応しません。

**`IsPressed()` は毎フレーム呼んでください。** 押し下がった瞬間は前回の呼び出しとの差で
見ているため、呼ばないフレームがあるとその間の押し下げを取りこぼします。

### 絞り込み

消しボタンで空にしたときも `Changed` が立つので、戻り値だけ見れば反映できます。

```csharp
EUi.SearchBox("##filter", ref this.filter);

foreach (var item in this.items)
{
    if (this.filter.Length > 0 &&
        !item.Name.Contains(this.filter, StringComparison.OrdinalIgnoreCase))
        continue;

    EUi.Selectable(item.Name, item == this.selected);
}
```

## 状態表示

| API | 説明 |
|---|---|
| `EUi.Badge(text, kind, filled)` | 小さな見出し札。状態や種別を 1 語で |
| `EUi.StatusDot(on, label, onColor, pulse)` | 点とラベル。動いているかを一目で |
| `EUi.Sparkline(id, values, height, min, max, color, label, format)` | 値の推移を折れ線で |

```csharp
using (EUi.HStack())
{
    EUi.StatusDot(this.running, this.running ? "動作中" : "停止中", pulse: true);
    EUi.Badge("試験", NoteKind.Warning);
}

EUi.Sparkline("fps", this.fpsHistory, 40f, label: $"{fps:0} fps");
```

`Sparkline` は古いものから順に並んだ配列を受け取ります。
上下の範囲は既定で配列の最小・最大に合わせるので、値の細かい動きが見えます。
`min` / `max` を渡すと固定できます。

マウスを乗せると、その位置に印と値が出ます。**値はグラフの中へ直接描きます** —
ツールチップにすると、呼び出し側が `Tip` で付けた説明と取り合いになるためです。
値の書式は `format` で変えられます（`"0"` で整数、既定は小数 2 桁まで）。

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

**開く場所と描く場所で ID の階層が違う場合は `EUi.RequestPopup` を使ってください。**
`OpenPopup` は呼んだ場所の階層で識別子を決めるので、一覧のループの中で開き、
ループの外で描くと噛み合いません。

```csharp
foreach (var item in items)
    if (EUi.Button("名前を変える")) EUi.RequestPopup("rename");   // ループの中

using (var p = EUi.Popup("rename", new Vector2(280f, 120f)))      // ループの外
    ...
```

項目の多い一覧を入れる場合は `scroll: true` を渡すと、中身が送り領域で包まれます。

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

**位置は開いた時点で決まり、以降は動きません。**
`MousePosition` でもマウスには追従しないので、メニューの項目を選びに行けます。

## 器

| API | 説明 |
|---|---|
| `EUi.Card(id, padding)` | 枠と地を持つ箱 |
| `EUi.Section(label, collapsible, defaultOpen, id)` | 折りたためる見出し付きの区画 |
| `EUi.Section(label, ref open, collapsible, id)` | 開閉を呼び出し側で持つ版 |
| `EUi.Section(label, body)` | コールバック版。閉じているときは中身が呼ばれない |
| `EUi.LabelColumn(id, minWidth, maxWidth)` | この中の `Field` のラベル幅を揃える |
| `EUi.Field(label, labelWidth)` | 「ラベル + ウィジェット」の 1 行 |
| `EUi.TabBar(id, labels...)` | タブ。選択中のタブを返す |
| `EUi.TabBar(id, ref index, labels...)` | 選択状態を呼び出し側で持つ版 |
| `EUi.SelectTab(id, index)` | コードから選択を変える |

タブは**行に入り切らない分を次の行へ折り返します**。枚数が増えても消えません。
コードから別のタブへ飛ばしたい場合は `ref` 版か `EUi.SelectTab` を使います。

```csharp
if (EUi.Button("プリセットへ"))
    EUi.SelectTab("##tabs", 2);

var tabs = EUi.TabBar("##tabs", "基本", "詳細", "プリセット");
```

同じ見出しを複数箇所で使う場合は `id` で分けます。
見出しへ `##` を埋め込む書き方も同じ意味ですが、引数のほうが意図がはっきりします。

```csharp
EUi.Section("詳細設定", id: "basicAdvanced");
EUi.Section("詳細設定", id: "displayAdvanced");
```

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

### ツールチップの折り返し

Tip は `Metrics.TooltipMaxWidth`（既定 360、拡大率に連動）で**常に自動折り返しします**。

`ImGui.SetTooltip` 向けに文の途中へ入れていた改行は外してください。
残したままだと「自動改行 → 数文字だけの行 → 手動改行」となって短い行が挟まります。
改行は段落の区切りにだけ残すのが読みやすくなります。

折り返し幅はテーマ単位です。変えるには `Derive` / `SetMetrics` で `TooltipMaxWidth` を書き換えます。

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
| `EUi.Scroll(id, SizeSpec, reserveBelow, spacing)` | 高さを配分で決める版 |

**`SizeSpec.Fill` は残り高さを全部使います。** そのあとに置いたものは場所が無くなって出ません。
即時モードでは「後ろに何が来るか」を先に知れないため、下に置くものがあるなら
`reserveBelow` でその分を伝えてください。

```csharp
using (EUi.Scroll("list", SizeSpec.Fill, reserveBelow: EUi.LineHeight + EUi.Metrics.ItemSpacing.Y))
{
    foreach (var item in items)
        EUi.Selectable(item.Name, item == selected);
}

EUi.Muted($"{hidden} 件は表示していません");   // 送り領域の下に出る
```
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

### ImGui から移るときの注意

| ImGui | EstellUtils | 備考 |
|---|---|---|
| `ImGui.NewLine()` | `EUi.BlankLine()` | 空行を入れる |
| `ImGui.SameLine()` | `EUi.HStack()` で囲む | 続けたい要素をまとめて囲む |
| `ImGui.Indent()` / `Unindent()` | `using (EUi.Indent())` | スコープで戻る |

**`EUi.LineBreak()` は `ImGui.NewLine()` ではありません。**
こちらは `HStack` の中で折り返す位置を指定するものです。
`ImGui.NewLine()` のつもりで置き換えると、空行がすべて消えます。

**1 行の中で色を変えるだけなら `EUi.RichLabel` が最短です。**
`HStack` と違ってスコープを開かないので、既存の 1 行を 1 行へ置き換えられます。

```csharp
// 移行前
ImGui.TextColored(gray, "状態: ");
ImGui.SameLine();
ImGui.TextColored(green, "動作中");

// 移行後
EUi.RichLabel("状態: ", new TextRun("動作中", EUi.Colors.Success));
```

文字列をそのまま渡すと標準の文字色になります。幅に収まらない場合は断片の切れ目で折り返します。

ウィジェットを並べる場合は `HStack` で囲んでください。即時モードで
「直前の要素の右に続ける」にはレイアウトの状態を遡る必要があるため、
`SameLine` に直接あたるものはありません。

```csharp
// 移行前
ImGui.Text("状態:");
ImGui.SameLine();
ImGui.TextColored(color, "動作中");

// 移行後
using (EUi.HStack())
{
    EUi.Label("状態:");
    EUi.TextColored("動作中", color);
}
```

### ツールチップの付け先

`EUi.Tip` が見る「直前のウィジェット」には、**ボタンなどの操作系だけでなく文字も含まれます**。
`HStack` を閉じても塊としては記録されないので、最後に置いた要素に付きます。

```csharp
using (EUi.HStack())
{
    EUi.Button("ON にする");
    EUi.Label("停止中");
}

EUi.Tip("説明");          // ← 「停止中」の文字にだけ付く
```

塊全体に付けたい場合は `EUi.Group` で囲みます（`ImGui.BeginGroup` に当たるもの）。

```csharp
using (EUi.Group("state", horizontal: true))
{
    EUi.Button("ON にする");
    EUi.Label("停止中");
}

EUi.Tip("押すと有効になります");   // 塊のどこでも出る
```

または、戻り値へ直接つなぐ方法もあります（`EUi.Button("...").Tip("...")`）。

### 右へ寄せる

**列を宣言し**、余りを埋める位置で `EUi.Spacer()` を呼びます。

```csharp
using (EUi.Row(SizeSpec.Fill, 80f, 80f))
{
    EUi.Spacer();                                              // 余りを吸う
    EUi.Button("キャンセル", ButtonStyle.Normal, SizeSpec.Fill);
    EUi.Button("OK", ButtonStyle.Primary, SizeSpec.Fill);
}
```

**`Spacer` は列を宣言した行 (`Row`) の中でだけ使えます。**
`HStack` など列のない横並びでは何もしません（ログに一度だけ警告が出ます）。

列がない行でこれが残り幅を全部取ると、**後ろの要素へ配る幅が無くなって描かれなくなります**。
即時モードでは「後ろに何が来るか」を先に知れないため、行の幅を配るには列の宣言が要ります。

| やりたいこと | 書き方 |
|---|---|
| 要素の間に隙間を空ける | `EUi.Spacing(amount)` |
| 以降を右へ寄せる | `EUi.Row(SizeSpec.Fill, ...)` + `EUi.Spacer()` |
| 右端へ確実に揃える | 行を `Reserve` して `rect.CutRight(...)` で切り出す |

`EUi.Spacing()` とは別物です。**あちらは隙間を空けるだけで列を消費しない**ため、
列を宣言した行で使うと以降の要素が 1 つずつ前の列へずれます
（列を宣言した行では無視されるようにしてあります）。

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

### 見出しを固定して行だけ送る

固定見出しそのものはまだありません。見出しを送り領域の外に置けば固定できますが、
**列がずれます。**

```csharp
EUi.TableHeader(columns);                 // 送り領域の外

using (EUi.Scroll("rows", 240f))          // 行だけ送る
    foreach (var row in rows) { ... }
```

送り領域は、つまみが出ているとき内容の右端を `ScrollbarWidth + SpacingSm` だけ削ります。
見出しは全幅、行は削られた幅になるので、`Fill` の列がその差だけ縮み、
以降の固定幅列が左へずれます。**つまみは行数で出たり消えたりするので、
行が増えた瞬間に見出しだけズレる**という気づきにくい壊れ方をします。

見出し側で同じ幅を引いておけば揃います。

```csharp
EUi.TableHeader(columns, reserveScrollbar: true);
```

### 行全体を押せるようにする

`hoverable: true` にすると、行に乗せたとき薄く光り、`Result` から入力を受け取れます。
右クリックのメニューはこれをそのまま渡せます。

```csharp
using (var row = EUi.TableRow(Columns, i, hoverable: true))
{
    EUi.TableCell(item.Name);
    EUi.SkipCell();                      // 列を 1 つ飛ばす
}

switch (EUi.ContextMenu("rowMenu", row.Result, "コピー", "削除"))
{
    case 0: Copy(item); break;
    case 1: Delete(item); break;
}
```

縞を外したい行には `striped: false` を渡します（追加用の行や一括操作の行など）。

### 並べ替えられる見出し

```csharp
if (EUi.SortableTableHeader("items", Columns, ref this.sort))
    this.ApplySort();
```

押した列が昇順・降順で切り替わり、印（▲▼）が付きます。同じ列をもう一度押すと向きが反転します。
`TableSort` は単純なプロパティだけなので、設定へそのまま保存できます。

### セルに複数のものを置く

列を宣言した行では、ウィジェットを 1 つ置くごとに次の列へ進みます。
1 つのセルへ複数を置きたい場合は `EUi.Cell()` で囲みます。

```csharp
using (EUi.TableRow(columns, i))
{
    EUi.TableCell(item.Name);

    using (EUi.Cell())              // 列を 1 つ消費し、中で横に並べる
    {
        EUi.Label(item.State);

        if (EUi.SmallButton("再開"))
            Resume(item);
    }
}
```

**中で置くものの数が行ごとに変わっても、消費する列は 1 つのままです。**
条件によってボタンが出たり出なかったりする行でも、列がずれません。

名前の下に補足を添えるような 2 段のセルには `EUi.CellStack()` を使います。
高さを明示しなければ、中身に合わせて行が伸びます。

### 折り返す列

`TableColumn` の `Wrap` を true にすると、**行の高さが中身に合わせて伸びます**。

```csharp
private static readonly TableColumn[] Columns =
[
    new("名前", 120f),
    new("説明", SizeSpec.Fill, Wrap: true),
];
```

高さを自分で逆算する必要はありません。内容が変わった直後の 1 フレームだけ高さがずれ、
次のフレームで揃います（前フレームの実測を使うため）。

**`Cell` / `CellStack` の中身も一緒に数えます。** 2 段のセルを置いた行も、
高さを明示せずに任せられます。

`TableRow` へ `height` を明示した場合は、そちらが優先されます。

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

### 列の中での大きさの扱い

列を宣言した行の中では、**大きさの渡し方で振る舞いが変わります**。

| 渡し方 | 列の中での扱い | 使う部品 |
|---|---|---|
| `SizeSpec` + 高さ | 列の幅に引き伸ばされる | Button / Label / 入力欄 |
| `Vector2`（大きさそのもの） | 希望した大きさのまま置かれる | Badge / IconButton / Image |

`EUi.Custom(id, SizeSpec, height)` は前者、`EUi.Custom(id, Vector2)` は後者を通ります。
四角い部品を作るなら `Vector2` 版を使えば、列の中でも形が崩れません。

カーソルはどちらの場合も列ぶん進むので、列がずれることはありません。

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
