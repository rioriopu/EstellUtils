# 既存の設定画面からの移行

EstellUtils は生の `ImGui.*` 呼び出しと**同じフレーム内で混在できます**。
画面を一度に書き換える必要はありません。1 関数ずつ、様子を見ながら移せます。

## 移行の順序

1. `EUi.Initialize` / `EUi.Shutdown` を追加する
2. ウィンドウを `Window`(Dalamud) から `EuWindow` へ置き換える
3. 中身を関数単位で少しずつ置き換える
4. 設定項目が多い画面は、最後に属性バインディングへまとめる

## 1. 初期化

```csharp
// Plugin コンストラクタ
EUi.Initialize(pluginInterface, log: log);

// Dispose
EUi.Shutdown();
```

Dalamud の `WindowSystem` を使っている場合、`EUi.Windows` と併存できます。
どちらも `UiBuilder.Draw` に接続されるだけなので、同時に使って構いません。

## 2. ウィンドウの置き換え

### 移行前

```csharp
public class ConfigWindow : Window, IDisposable
{
    public ConfigWindow(Plugin plugin) : base("Masked Dalamud 設定")
    {
        Flags = ImGuiWindowFlags.NoCollapse;
        Size = new Vector2(460, 360);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 280),
            MaximumSize = new Vector2(900, 700),
        };
    }

    public override void Draw() { /* ... */ }
}
```

### 移行後

```csharp
public class ConfigWindow : EuWindow
{
    public ConfigWindow(Plugin plugin) : base("Masked Dalamud 設定")
    {
        this.Size = new Vector2(460f, 360f);
        this.MinSize = new Vector2(400f, 280f);
        this.MaxSize = new Vector2(900f, 700f);
    }

    public override void Draw() { /* 中身はそのままでも動く */ }
}
```

`Draw()` の中身が生の ImGui のままでも動作します。
違いは、ウィンドウの枠とタイトルバーが EstellUtils の描画になることだけです。

## 3. 中身の置き換え

よくある書き換えの対応表です。

### 定型の設定項目

```csharp
// 移行前
int interval = cfg.methodAUpdateIntervalFrames;
ImGui.SetNextItemWidth(160);
if (ImGui.SliderInt("オーバーレイ更新間隔 (フレーム)##scrubIv", ref interval, 1, 6))
{
    cfg.methodAUpdateIntervalFrames = Math.Clamp(interval, 1, 6);
    cfg.Save();
}
if (ImGui.IsItemHovered())
    ImGui.SetTooltip("GPU-GDI / CPU 方式に適用...");

// 移行後
if (EUi.SliderInt("オーバーレイ更新間隔##scrubIv", ref cfg.methodAUpdateIntervalFrames, 1, 6,
                  width: 160f, suffix: "フレーム").Tip("GPU-GDI / CPU 方式に適用..."))
    cfg.Save();
```

`ref` で直接フィールドを渡せるため、一時変数と `Clamp` が不要になります。

### 色付きの折り返しテキスト

```csharp
// 移行前
ImGui.PushStyleColor(ImGuiCol.Text, color);
ImGui.TextWrapped(text);
ImGui.PopStyleColor();

// 移行後
EUi.Paragraph(text, color);
EUi.Note(text, NoteKind.Warning);   // 状態色を使う場合
```

### 見出しと区切り

```csharp
// 移行前
ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1f), "共通設定");
ImGui.Separator();
ImGui.Spacing();

// 移行後
EUi.Separator("共通設定");
```

### タブ

```csharp
// 移行前
if (ImGui.BeginTabBar("##maskedTabs"))
{
    if (ImGui.BeginTabItem("基本")) { DrawBasic(); ImGui.EndTabItem(); }
    if (cfg.debugEnabled && ImGui.BeginTabItem("試験機能")) { DrawExp(); ImGui.EndTabItem(); }
    ImGui.EndTabBar();
}

// 移行後
var tabs = cfg.debugEnabled
    ? EUi.TabBar("##maskedTabs", "基本", "試験機能")
    : EUi.TabBar("##maskedTabs", "基本");

if (tabs.IsSelected("基本")) DrawBasic();
else if (tabs.IsSelected("試験機能")) DrawExp();
```

タブのラベルを先に宣言する形になります。条件付きのタブがある場合は、
`EUi.Window(...).Tab(label, body, visible)` のビルダーを使うと素直に書けます。

### 折りたたみ

```csharp
// 移行前
if (ImGui.CollapsingHeader("現在の状態##statusFold"))
    DrawStatus();

// 移行後
using (var s = EUi.Section("現在の状態##statusFold", defaultOpen: false))
{
    if (s.IsVisible)
        DrawStatus();
}
```

### 横並び

```csharp
// 移行前
ImGui.Button("A"); ImGui.SameLine();
ImGui.Button("B"); ImGui.SameLine();
ImGui.Button("C");

// 移行後
using (EUi.HStack())
{
    EUi.Button("A");
    EUi.Button("B");
    EUi.Button("C");
}
```

## 4. 属性バインディングへまとめる

設定項目が多い画面は、最後に属性へ移すと画面側のコードがほぼ消えます。
詳しくは [binding.md](binding.md) を参照してください。

## 生 ImGui との混在

EstellUtils のレイアウトは独自のカーソルで位置を決めます。
そのため、**生の `ImGui.*` は `EUi.RawImGui()` のスコープで囲んでください。**
囲まないと ImGui 側のカーソルが合わず、見えない場所へ描かれて何も出ていないように見えます。

```csharp
EUi.Heading("プレビュー");

using (EUi.RawImGui())
{
    ImGui.BeginChild("preview", new Vector2(0, 120), true);
    ImGui.Image(handle, size);
    ImGui.EndChild();
}

EUi.Muted("続きはここから");     // 正しい位置に出る
```

スコープを開くとき ImGui のカーソルが「次に置かれるはずの位置」へ合わせられ、
閉じるとき ImGui が進めた分だけレイアウトが進みます。
`BeginChild` や `Columns` のように ImGui の仕組みへ依存した部分を、
そのまま残したまま移行できます。

移行の途中では、置き換えていない部分をまるごとこのスコープへ入れておくのが楽です。

```csharp
public override void Draw()
{
    EUi.Separator("共通設定");
    this.binder.DrawGroup("共通設定");        // 置き換え済み

    using (EUi.RawImGui())
        this.DrawLegacyTabs();                // まだ生 ImGui のまま
}
```

- `ImGui.SameLine()` は EstellUtils のウィジェットには効きません。`EUi.HStack()` を使ってください
- ラベルの `##` / `###` の扱いは ImGui と同じです。既存のラベルをそのまま使えます
- `EUi.SyncFromImGui()` は、スコープを使わずカーソルだけ取り込みたい場合の低レベル版です
