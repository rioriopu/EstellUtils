# ウィンドウ

`EuWindow` はタイトルバー・枠・リサイズグリップ・スクロールバーをすべて自前で描き、
位置と大きさもライブラリ側で持ちます。ImGui には透明な箱としてだけ使わせています。

## 表示の状態

ウィンドウは 3 つの状態を持てます。タイトルバーのボタンで切り替わります。

| 状態 | 説明 | 有効にする |
|---|---|---|
| 通常 | `Size` の大きさで `Draw()` を描く | 既定 |
| 小窓 | `CompactSize` の大きさで `DrawCompact()` を描く | `HasCompactMode = true` |
| 最小化 | タイトルバーだけに畳む | `Collapsible = true`（既定） |

切り替えは大きさを滑らかに変えながら行われます。
最小化はタイトルバーのダブルクリックでも切り替わります。

### 小窓モード

設定画面をしまいつつ、よく使う操作だけ手元に残したいときに使います。

```csharp
public sealed class ConfigWindow : EuWindow
{
    public ConfigWindow() : base("プラグイン設定")
    {
        this.HasCompactMode = true;
        this.CompactSize = new Vector2(280f, 150f);
    }

    public override void Draw()
    {
        // 通常の設定画面
    }

    public override void DrawCompact()
    {
        // よく使う操作だけ
        EUi.Toggle("有効", ref this.config.Enabled);

        if (EUi.Button("いま実行", ButtonStyle.Primary, SizeSpec.Fill))
            this.plugin.Run();
    }
}
```

`DrawCompact()` を書かなければ、小窓でも通常の中身がそのまま縮んで表示されます。

## 移動とリサイズ

| プロパティ | 既定 | 説明 |
|---|---|---|
| `Movable` | true | タイトルバーのドラッグで移動できる |
| `MoveFromAnywhere` | true | **ウィジェットの無い余白**をドラッグしても移動できる |
| `SnapToScreenEdges` | true | 画面の端に近づくと吸い付く |
| `Resizable` | true | 右下を掴んで大きさを変えられる |

`MoveFromAnywhere` は、その位置に反応するウィジェットが無いときだけ効きます。
ボタンやスライダーの上でドラッグしてもウィンドウは動きません。

ウィンドウは画面外へ行きすぎないように丸められます。タイトルバーが必ず画面内に残るので、
掴み直せなくなることはありません。

## 背景を落とす

```csharp
this.window.DimBackground = true;
this.window.DimAmount = 0.45f;
```

開いている間、すべてのウィンドウより奥に暗い覆いを描きます。
単発で使うなら `EUi.Scrim(0.5f)` を呼びます。

### ぼかしについて

**背後を実際にぼかすことはできません。** ImGui はゲーム画面の上へ重ねて描くだけで、
背後のピクセルを読み取る手段を持たないためです。本物のガウスぼかしを掛けるには
D3D のレンダーターゲットを取得してシェーダを通す必要があり、UI ライブラリの範囲を超えます。

代わりに用意しているのは次の 2 つです。目的（背景の情報量を落として UI を見やすくする）には
こちらで足ります。

1. **背景を暗く落とす** — 上記の `DimBackground`
2. **すりガラス風テーマ** — `FrostedGlassTheme`。透過を強めた地に上端の光沢と明るい細枠を
   合わせて、厚みのある曇りガラスの板に見せます。ゲーム画面が透けるので、
   動きのある背景の上ではガラス越しのように見えます

## その他

| プロパティ | 説明 |
|---|---|
| `AutoScroll` | 内容がはみ出したら自前のスクロール領域に載せる（既定 true） |
| `CloseOnEscape` | フォーカス中に Esc で閉じる。文字入力中は反応しない |
| `HasTitleBar` / `Closable` | タイトルバーと閉じるボタンの有無 |
| `Padding` | 内側の余白 |
| `Theme` | このウィンドウだけで使うテーマ |
| `DrawConditions()` | 描画するかの判定。戦闘中だけ出す、といった制御に使う |

## 位置とサイズの保存

ImGui の ini には保存されません。覚えておきたい場合はプラグインの設定へ保存します。

```csharp
public override void OnClose()
{
    this.config.WindowPos = this.Position;
    this.config.WindowSize = this.Size;
    this.config.Save();
}
```

読み込んだ位置を使うときは、初回の自動中央寄せが働かないよう `MarkPlaced()` を呼びます
（ビルダーの場合は `.At(x, y)`）。
