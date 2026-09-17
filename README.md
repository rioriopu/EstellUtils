# EstellUtils

FFXIV / Dalamud プラグイン向けの、**独自 UI ライブラリ**です。

ImGui の標準ウィジェットを使わず、`ImDrawList` への直接描画でウィジェットを自前に構築します。
ImGui は「描画面・入力・クリッピング・Z 順」の下請けとしてのみ利用するため、
見た目・当たり判定・アニメーションを完全に自由に設計できます。

```csharp
// 下層: 即時モード。生 ImGui と同じフレーム内で混在できる
if (EUi.SliderInt("更新間隔", ref interval, 1, 6, suffix: "フレーム").Tip("大きいほど軽くなります"))
    config.Save();

// 上層: 宣言的。設定クラスの属性から画面がそのまま生成される
binder.DrawAll();
```

## 特徴

- **完全に独自の描画** — タイトルバー・枠・スクロールバーからウィジェットまで、すべて自前描画
- **二層 API** — 即時モードの静的ヘルパーと、宣言的な Fluent ビルダーの両方を提供
- **生 ImGui と混在可能** — 既存の設定画面を 1 関数ずつ移行できる
- **差し替え可能なテーマ** — 色・寸法・モーションをトークン化。既定は FFXIV ネイティブ UI 風
- **描画そのものも差し替え可能** — `IWidgetPainter` を実装すれば、ボタン 1 種類だけ別物にできる
- **設定バインディング** — 属性を付けるだけで、グループ分けされた設定画面が生成される
- **ウィンドウの表示状態** — 通常 / 小窓 / 最小化を切り替えられる。余白を掴んでの移動、
  画面端への吸着、背景を暗く落とす表示にも対応
- **アロケーションに配慮** — レイアウトスコープはプール、値の書式化は `stackalloc`、
  列宣言は `params ReadOnlySpan<T>`

## 動作環境

| 項目 | 値 |
|---|---|
| Dalamud | API 15 (15.0.3.4 で検証) |
| ターゲット | `net10.0-windows7.0` / x64 |
| 依存 | `Dalamud.Bindings.ImGui`（Dalamud が実行時に供給） |

## 導入

プラグインの csproj からプロジェクト参照を追加します。`EstellUtils.dll` は
出力ディレクトリへコピーされるので、そのままプラグイン ZIP に同梱してください。

```xml
<ItemGroup>
  <ProjectReference Include="..\EstellUtils\src\EstellUtils\EstellUtils.csproj" />
</ItemGroup>
```

プラグインの起動時と終了時に初期化・解放を呼びます。

```csharp
public Plugin(IDalamudPluginInterface pi, IPluginLog log)
{
    EUi.Initialize(pi, log: log);          // UiBuilder.Draw への接続もここで行われる

    this.window = new ConfigWindow();
    EUi.Windows.Add(this.window);
}

public void Dispose() => EUi.Shutdown();
```

ウィンドウは `EuWindow` を継承するか、ビルダーで宣言します。

```csharp
this.window = EUi.Window("Masked Dalamud 設定")
    .Size(460, 360)
    .MinSize(400, 280)
    .Tab("基本", this.DrawBasicTab)
    .Tab("設定", this.DrawSettingsTab)
    .Register();
```

## ドキュメント

| 文書 | 内容 |
|---|---|
| [docs/architecture.md](docs/architecture.md) | 設計思想と層構成。なぜ ImGui のウィジェットを使わないのか |
| [docs/getting-started.md](docs/getting-started.md) | 導入手順と最小のサンプル |
| [docs/widgets.md](docs/widgets.md) | ウィジェットとレイアウトの一覧 |
| [docs/windows.md](docs/windows.md) | ウィンドウの機能（小窓・最小化・移動・背景の扱い） |
| [docs/theming.md](docs/theming.md) | テーマの調整と、描画そのものの差し替え |
| [docs/binding.md](docs/binding.md) | 属性による設定画面の自動生成 |
| [docs/migration.md](docs/migration.md) | 既存の ImGui 設定画面からの移行手順 |

## デモ

`samples/EstellUtils.Demo` は全ウィジェットを並べたギャラリープラグインです。
ビルドすると `C:\DevPlugins\EstellUtilsDemo\` へ出力されるので、Dalamud の devPlugins から
読み込んで `/eudemo` で開けます。

```
dotnet build EstellUtils.sln -c Release
```

## 自己検証

矩形の切り出し・ID の生成・色の変換・列幅の解決といった、ゲームを起動せずに
確かめられる部分には自己検証を用意しています。外部パッケージには依存しません。

```
dotnet run --project tests/EstellUtils.SelfCheck
```

## 開発状況

API は開発初期のため、予告なく変更されます。

| フェーズ | 内容 | 状態 |
|---|---|---|
| 0 | リポジトリ基盤・ビルド構成 | 完了 |
| 1 | Core（ID / 矩形 / 入力 / 状態保持 / アニメーション） | 完了 |
| 2 | Render（描画プリミティブ・テキスト） | 完了 |
| 3 | Theme（デザイントークン・既定テーマ） | 完了 |
| 4 | Layout（レイアウトエンジン・コンテナ） | 完了 |
| 5 | Widgets 第1陣（基本ウィジェット） | 完了 |
| 6 | Windowing（独自ウィンドウ基盤） | 完了 |
| 7 | Widgets 第2陣（入力系・一覧系） | 完了 |
| 8 | Fluent / Binding（宣言的 API・設定バインディング） | 完了 |
| 9 | デモプラグイン（ウィジェットギャラリー） | 完了 |
| 10 | ドキュメント | 完了 |

### 今後の予定

- ゲーム本体の uld テクスチャを使った 9 スライス描画（より忠実な FFXIV 風テーマ）
- ウィンドウのドッキング（端に寄せたときの整列）
- テーマの JSON 保存・読み込み
- 一覧の仮想化（数千行でも軽い表示）
- スプリッター（ドラッグで分割位置を変えるレイアウト）

## ライセンス

未定
