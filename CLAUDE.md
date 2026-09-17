# EstellUtils 開発ガイド

FFXIV / Dalamud プラグイン向けの独自 UI ライブラリ。

## 中核の設計方針

**ImGui をウィジェットライブラリとして使わない。** `ImGui.Button` / `ImGui.SliderInt` などの
標準ウィジェットは一切呼ばない。ImGui は以下の用途にのみ使う:

- `ImDrawList` — 描画面
- `ImGui.GetIO()` — マウス・キー入力の取得元
- `ImGui.Begin` — クリッピング領域・Z 順・フォーカス管理の箱（装飾はすべて無効化する）

ウィンドウは `NoTitleBar | NoBackground | NoScrollbar | NoResize | NoCollapse` で
透明な箱として確保し、タイトルバー・枠・リサイズグリップ・スクロールバー・ウィジェットは
すべて `ImDrawList` への直接描画で構築する。ヒットテストも `Rect.Contains(io.MousePos)` で自前に行う。

ただし生 ImGui との**同一フレーム内での混在は維持する**（カーソル位置を `ImGui.Dummy` で同期）。
既存プラグインの設定画面を 1 関数ずつ移植できるようにするため。

## API の二層構造

- **下層** — 即時モードの静的ヘルパー。`EUi.Slider(...)` 等。戻り値は `WidgetResult`。
- **上層** — 宣言的な Fluent ビルダー。`EUi.Window(...).Tab(...).Section(...)` 等。

ライブラリ内のウィジェットも、外部利用者に公開しているのと**同じ低層 API のみ**で実装する
（内部特権を作らない）。これにより利用者が同じ土俵で独自ウィジェットを書ける。

## コーディング規約

- コード内のコメント・XML ドキュメントは**日本語**で書く
- `Nullable=enable` / `LangVersion=preview`
- ファイルスコープ名前空間を使う（`namespace EstellUtils.UI.Core;`）
- 毎フレーム呼ばれるコードでは**アロケーションを避ける**（LINQ・ラムダキャプチャ・boxing に注意）

## ビルド

```
dotnet build EstellUtils.sln -c Release
```

Dalamud の参照パスは `Directory.Build.props` の `DalamudLibPath` で解決する。
既定は `%APPDATA%\XIVLauncher\addon\Hooks\dev`。環境変数 `DALAMUD_HOME` で上書き可能。

## git 運用

- author は `rioriopu <rioriopuu@gmail.com>`
- コミットメッセージは**日本語**
- `Co-Authored-By: Claude` / `Generated with Claude Code` などの署名行は**付けない**
- フェーズ単位でコミットする。各コミットは必ずビルドが通る状態にすること
