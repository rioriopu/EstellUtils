# ウィンドウ

`EuWindow` はタイトルバー・枠・リサイズグリップ・スクロールバーをすべて自前で描き、
位置と大きさもライブラリ側で持ちます。ImGui には透明な箱としてだけ使わせています。

## 表示の状態

ウィンドウは 3 つの状態を持てます。タイトルバーのボタンで切り替わります。

| 状態 | 説明 | 有効にする |
|---|---|---|
| 通常 | `Size` の大きさで `Draw()` を描く | 既定 |
| 最小化 | タイトルバーだけの帯に畳む（幅はそのまま） | `Collapsible = true`（既定） |
| 小窓 | **別ウィンドウ**として `DrawCompact()` を描く | `HasCompanion = true` |

切り替えは大きさを滑らかに変えながら行われます。
最小化はタイトルバーのダブルクリックでも切り替わります。

### 小窓

小窓は**本体とは別の独立したウィンドウ**です。位置も大きさも別に持つので、
設定画面を閉じたまま、よく使う操作だけを画面の隅へ置いておけます。

```csharp
public sealed class ConfigWindow : EuWindow
{
    public ConfigWindow() : base("プラグイン設定")
    {
        this.HasCompanion = true;
        this.CompanionSize = new Vector2(280f, 150f);

        // 本体と小窓を同時に出したい場合
        // this.CompanionReplacesMain = false;
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

`DrawCompact()` を書かなければ、小窓にも通常の中身がそのまま出ます。
小窓のタイトルバーには「元へ戻す」ボタンが付き、本体へ戻れます。

| プロパティ | 説明 |
|---|---|
| `HasCompanion` | 小窓を持つ。タイトルバーに切り替えボタンが出る |
| `CompanionSize` | 小窓の大きさ |
| `CompanionReplacesMain` | 小窓を開くとき本体を閉じるか（既定 true） |
| `Companion` | 小窓のウィンドウ本体。位置の保存などに使う |
| `IsCompanionOpen` | 小窓が開いているか |

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

## 背景のぼかしについて

**背後を実際にぼかすことはできません。** ImGui はゲーム画面の上へ重ねて描くだけで、
背後のピクセルを読み取る手段を持たないためです。本物のガウスぼかしを掛けるには
D3D のレンダーターゲットを取得してシェーダを通す必要があり、UI ライブラリの範囲を超えます。

ガラス越しのような見え方に近づけたい場合は `FrostedGlassTheme` を使ってください。
透過を強めた地に上端の光沢と明るい細枠を合わせて、厚みのある曇りガラスの板に見せます。
ゲーム画面が透けるので、動きのある背景の上ではそれらしく見えます。

## タイトルバーへボタンを足す

標準のボタン（閉じる・畳む・小窓・鍵）の左側に、好きなボタンを並べられます。

```csharp
this.TitleBarButtons.Add(new TitleBarButton
{
    Id = "settings",
    Icon = FontAwesomeIcon.Cog.ToIconString(),
    Tooltip = "設定を開く",
    IsActive = () => this.plugin.ConfigOpen,   // ON 状態を強調したい場合
    IsVisible = () => this.plugin.Ready,       // 条件付きで出したい場合
    OnClick = () => this.plugin.OpenConfig(),
});
```

## 固定と透過

| プロパティ | 説明 |
|---|---|
| `Locked` | 位置と大きさを固定する。移動もリサイズもできなくなる |
| `ShowLockButton` | タイトルバーに鍵ボタンを出す |
| `ClickThrough` | マウス操作を透過させ、背後のゲーム画面を直接操作できるようにする |
| `Opacity` | ウィンドウ全体の不透明度（0〜1） |

`ClickThrough` 中はウィンドウ自身のボタンも押せません。コマンドなど別の解除手段を
用意しておいてください。

`Opacity` は描画全体に倍率を掛けます。独自ウィジェットを書いている場合も
`Painter` / `TextPainter` を使っていれば自動的に従います
（自分で一部だけ薄くしたいときは `Painter.UseAlpha(0.5f)`）。

## 動的なタイトル

状態をタイトルへ出したい場合は `GetTitle()` をオーバーライドします。
ウィンドウの識別子は生成時に固定されるので、毎フレーム変えても位置や状態は失われません。

```csharp
public override string GetTitle()
    => $"AutoRetainer {this.version} | 残り {this.remaining}";
```

## その他

| プロパティ | 説明 |
|---|---|
| `AutoScroll` | 内容がはみ出したら自前のスクロール領域に載せる（既定 true） |
| `CloseOnEscape` | フォーカス中に Esc で閉じる。文字入力中は反応しない |
| `HasTitleBar` / `Closable` | タイトルバーと閉じるボタンの有無 |
| `Padding` | 内側の余白 |
| `Theme` | このウィンドウだけで使うテーマ |
| `DrawConditions()` | 描画するかの判定。戦闘中だけ出す、といった制御に使う |

## 独自に描くときの注意

ImGui はウィンドウの内側 (余白の半分ほど) でクリップをかけます。
そのため、ウィンドウの縁ぎりぎりに何かを描くと切り取られます。
枠やタイトルバーのように縁まで描きたいものは、クリップを広げてください。

```csharp
using (Painter.ClipFullScreen())
{
    // ウィンドウの縁や、その外側まで描ける
}
```

## 位置とサイズの保存

ImGui の ini には保存されません。設定クラスへ `EuWindowLayout` を 1 つ持たせ、
起動時に結びつければ、以降は自動で復元・保存されます。

```csharp
// 設定クラス
public EuWindowLayout WindowLayout { get; set; } = new();

// 起動時
EUi.Initialize(pluginInterface, log: log);
EUi.Windows.Add(this.configWindow);
EUi.Windows.BindLayout(this.config.WindowLayout, this.config.Save);
```

覚えられるのは、位置・大きさ・畳んでいるか・固定しているか・不透明度、
そして小窓の開閉と位置・大きさです。

保存処理は**動かし終えた・大きさを変え終えた時点で 1 度だけ**呼ばれます。
ドラッグ中に毎フレーム書き出すことはありません。

ウィンドウごとに別の入れ物を使いたい場合は、直接割り当てられます。

```csharp
this.configWindow.State = this.config.ConfigWindowState;
this.configWindow.StateChanged = this.config.Save;
```

### 手動で扱う

自分で読み書きしたい場合は `Position` / `Size` をそのまま使えます。

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
