# 導入

## 1. プロジェクト参照を追加する

```xml
<ItemGroup>
  <ProjectReference Include="..\EstellUtils\src\EstellUtils\EstellUtils.csproj" />
</ItemGroup>
```

`EstellUtils.dll` はプラグインの出力ディレクトリへコピーされます。
プラグイン ZIP へ同梱してください（Dalamud が実行時に供給するものではありません）。

Dalamud の参照パスは `Directory.Build.props` の `DalamudLibPath` が解決します。
既定は `%APPDATA%\XIVLauncher\addon\Hooks\dev` で、環境変数 `DALAMUD_HOME` で上書きできます。

## 2. 初期化する

```csharp
using EstellUtils.UI;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ConfigWindow window;

    public Plugin(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        // フォントの準備と UiBuilder.Draw への接続をまとめて行う
        EUi.Initialize(pluginInterface, log: log);

        this.window = new ConfigWindow();
        EUi.Windows.Add(this.window);
    }

    public void Dispose() => EUi.Shutdown();
}
```

`EUi.Initialize` の引数：

| 引数 | 説明 |
|---|---|
| `pluginInterface` | 必須。フォント生成と描画接続に使う |
| `theme` | 既定テーマ。省略すると FFXIV ネイティブ風 |
| `log` | ライブラリ内部のログ出力先。省略するとログは捨てられる |
| `hookDraw` | `UiBuilder.Draw` へ自動接続するか。false のときは毎フレーム `EUi.Windows.Draw()` を呼ぶ |

## 3. ウィンドウを作る

### 継承して作る

```csharp
using EstellUtils.UI;
using EstellUtils.UI.Windowing;

public sealed class ConfigWindow : EuWindow
{
    public ConfigWindow() : base("プラグイン設定")
    {
        this.Size = new Vector2(460f, 360f);
        this.MinSize = new Vector2(400f, 280f);
    }

    public override void Draw()
    {
        EUi.Heading("ようこそ");
        EUi.Paragraph("ここに説明文を書きます。幅に合わせて折り返されます。");

        if (EUi.Button("保存", ButtonStyle.Primary))
            this.config.Save();
    }
}
```

### ビルダーで作る

継承するほどでもない画面は、その場で宣言できます。

```csharp
this.window = EUi.Window("プラグイン設定")
    .Size(460, 360)
    .MinSize(400, 280)
    .Tab("基本", this.DrawBasicTab)
    .Tab("詳細", this.DrawAdvancedTab)
    .Tab("試験機能", this.DrawExperimentalTab, () => this.config.DebugEnabled)  // 表示条件付き
    .OnClose(() => this.binder.Flush())
    .Register();
```

`Register()` が返すウィンドウを保持し、`IsOpen` で開閉します。

```csharp
this.window.IsOpen = true;
this.window.Toggle();
```

## 4. 位置とサイズを保存する

ウィンドウの位置は ImGui の ini には保存されません。覚えておきたい場合は
プラグインの設定へ保存してください。

```csharp
public override void OnClose()
{
    this.config.WindowPos = this.Position;
    this.config.WindowSize = this.Size;
    this.config.Save();
}
```

読み込み側では、位置を設定したあとに自動中央寄せが働かないよう注意してください
（`EuWindow` を継承している場合は `MarkPlaced()`、ビルダーの場合は `.At(x, y)`）。

## 起動直後の描画について

ゲーム起動直後の数フレームは、**ウィンドウが描かれません。**

フォントのアトラスは非同期に構築されます。それが済むまでに描いてしまうと、
ASCII しか持たない代替フォントが使われ、日本語がすべて `?` になります。
`EuWindowManager` は準備が整うまで描画を遅らせます（上限 5 秒。
万一いつまでも整わない場合に何も出なくなるのを防ぐため）。

自前で `UiBuilder.Draw` へ描くものがある場合は、同じ判定を使えます。

```csharp
if (!EUi.Fonts.IsTextReady)
    return;
```

## 最小の完成例

`samples/EstellUtils.Demo/Plugin.cs` が、そのまま最小の使用例になっています。
