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
| `EUi.Paragraph(text, color)` | 幅に合わせて折り返す |
| `EUi.Note(text, kind)` | 状態色付きの折り返しテキスト（Info / Success / Warning / Danger） |
| `EUi.Heading(text)` | 大きめのフォントの見出し |
| `EUi.Bullet(text)` | 行頭に点を打つ箇条書き |
| `EUi.LabelClipped(text, maxWidth, color, align)` | 幅を決めて 1 行表示。溢れたら省略し、全文をツールチップで見せる |
| `EUi.Selectable(label, selected, width, height)` | 選択できる 1 行。一覧を自前で組むときに |
| `EUi.Image(texture, size, tint)` | 画像。アイテムアイコンなどの表示に |
| `EUi.ImageButton(texture, id, size)` | 押せる画像 |
| `EUi.Separator(label)` | 区切り線。ラベルを渡すと線の中に文字を挟む |
| `EUi.Toast(message, kind, duration)` | 画面隅に出る通知。ウィンドウが閉じていても見える |
| `EUi.Toast(title, message, kind, duration)` | 見出し付きの通知 |

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

ライブラリ内部と同じ API だけで書けます。

```csharp
public static bool MyWidget(ReadOnlySpan<char> label)
{
    var ctx = EUi.Context;
    ctx.EnsureFrame();

    var id = ctx.GetId(label, out var display);
    var rect = EUi.Reserve(SizeSpec.Fill, EUi.Metrics.WidgetHeight);

    var interaction = Interaction.Behavior(rect, id);

    // interaction.HoverAmount / PressAmount は遷移量 (0〜1)
    var color = EuColor.Lerp(EUi.Colors.Surface, EUi.Colors.Accent, interaction.HoverAmount);

    Painter.Rect(rect, color, EUi.Metrics.WidgetRounding);
    TextPainter.TextIn(rect, EUi.Colors.Text, display, Align.Center, Align.Center);

    return interaction.Clicked;
}
```
