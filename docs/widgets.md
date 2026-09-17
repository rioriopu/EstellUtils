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

## テキスト

| API | 説明 |
|---|---|
| `EUi.Label(text, color, align)` | 1 行。幅に収まらない場合は末尾を省略記号にする |
| `EUi.Muted(text)` | 控えめな色のラベル |
| `EUi.Paragraph(text, color)` | 幅に合わせて折り返す |
| `EUi.Note(text, kind)` | 状態色付きの折り返しテキスト（Info / Success / Warning / Danger） |
| `EUi.Heading(text)` | 大きめのフォントの見出し |
| `EUi.Bullet(text)` | 行頭に点を打つ箇条書き |
| `EUi.Separator(label)` | 区切り線。ラベルを渡すと線の中に文字を挟む |
| `EUi.Toast(message, kind, duration)` | 画面隅に出る通知。ウィンドウが閉じていても見える |

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

## 入力・選択

| API | 説明 |
|---|---|
| `EUi.TextInput(id, ref value, hint, maxLength, width, disabled)` | 1 行入力。IME 対応 |
| `EUi.TextArea(id, ref value, height, maxLength, disabled)` | 複数行入力 |
| `EUi.Combo(id, ref index, items, width, disabled)` | ドロップダウン |
| `EUi.ListBox(id, ref index, items, height, disabled)` | スクロールする一覧 |
| `EUi.ColorEdit(id, ref color, showAlpha, width)` | 色見本 + 自前のカラーピッカー |

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
    if (!section.IsOpen)
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

## レイアウト

| API | 説明 |
|---|---|
| `EUi.VStack(spacing)` | 縦に積む |
| `EUi.HStack(spacing, wrap)` | 横に並べる。`wrap` で折り返す |
| `EUi.Row(columns...)` | 列幅を先に宣言した横並び |
| `EUi.Grid(columnCount, spacing)` | 均等幅で折り返す |
| `EUi.Inset(padding, spacing)` | 内側に余白を取った縦積み |
| `EUi.Region(bounds, padding, spacing)` | 明示した矩形の中へ配置する |
| `EUi.Sized(width, height, padding)` | 大きさを固定した領域の中へ配置する |
| `EUi.Scroll(id, height, spacing)` | はみ出すとスクロールする領域 |
| `EUi.Spacing(amount)` / `EUi.NewLine()` | 空きを入れる / 次の行へ |
| `EUi.Reserve(size)` | 領域だけ確保して矩形を得る（独自描画用） |

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
