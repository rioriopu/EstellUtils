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

    public override void Draw()
    {
        // 中身がまだ生の ImGui なら、最上段でこれを開く
        using var raw = EUi.RawImGui();

        // ここから下は今までどおりの ImGui コード
    }
}
```

### 中身が生の ImGui のままの場合

**`Draw()` の先頭で `EUi.RawImGui()` を開いてください。**

`EuWindow.Draw()` の中は EstellUtils のレイアウトです。EstellUtils は独自のカーソルで
位置を決めるため、囲まずに生の `ImGui.*` を呼ぶと**画面の外へ描かれ、窓枠だけが残ります**。
何も出ていないように見えるので、原因に辿り着きにくい症状です。

```csharp
public override void Draw()
{
    using var raw = EUi.RawImGui();

    using var tabs = ImRaii.TabBar("##tabs");
    // ...
}
```

囲んでしまえば、中身は 1 関数ずつ置き換えていけます。
EstellUtils のウィジェットへ全部移り終えたら、この行を外します。

部分的に混ぜる場合も同じです。詳しくは下の「[生 ImGui との混在](#生-imgui-との混在)」を参照してください。

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

### 色付きテキスト

**見た目を変えたくない場合は、`EUi.Note` ではなく `TextColored` / `WrapColored` を使ってください。**
`Note` は枠と地とアイコンの付いた囲みで、色付きテキストとは別物です。

```csharp
// 移行前
ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "⚠ 試験機能です。");

// 移行後 — 1 行。見た目はそのまま
EUi.TextColored("⚠ 試験機能です。", new Vector4(1f, 0.4f, 0.4f, 1f));
```

```csharp
// 移行前
ImGui.PushStyleColor(ImGuiCol.Text, color);
ImGui.TextWrapped(text);
ImGui.PopStyleColor();

// 移行後 — 折り返し。見た目はそのまま
EUi.WrapColored(text, color);
```

色をテーマに任せる場合は `Vector4` の代わりに `NoteKind` を渡せます。
テーマを切り替えても、注意書きは注意書きの色のままになります。

```csharp
EUi.TextColored("⚠ 試験機能です。", NoteKind.Warning);
EUi.WrapColored(longText, NoteKind.Danger);
```

囲みが欲しいときだけ `Note` を使います。

```csharp
EUi.Note("この機能は試験中です。動作の保証はありません。", NoteKind.Warning);
EUi.Note(text, NoteKind.Warning, boxed: false);   // 囲みなし = WrapColored と同じ
```

| 移行前 | 移行後 |
|---|---|
| `ImGui.TextColored(color, text)` | `EUi.TextColored(text, color)` |
| `PushStyleColor` + `TextWrapped` + `PopStyleColor` | `EUi.WrapColored(text, color)` |
| `ImGui.TextDisabled(text)` | `EUi.Muted(text)` |
| （囲みは ImGui に無い） | `EUi.Note(text, kind)` |

### 見出しと区切り

```csharp
// 移行前
ImGui.TextColored(new Vector4(0.7f, 0.9f, 1f, 1f), "共通設定");
ImGui.Separator();
ImGui.Spacing();

// 移行後
EUi.Separator("共通設定");
```

見出しとして色付きテキストを使っていた箇所は `Separator(label)` か `Heading` に寄せられますが、
**本文としての色付きテキストは `TextColored` のままにしてください。**

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

**行に入り切らないタブは次の行へ折り返します。** タブが多い画面でも消えません。

コードから別のタブへ飛ばす場合は、選択状態を呼び出し側で持つか、`SelectTab` を使います。

```csharp
// 呼び出し側で持つ
if (EUi.Button("プリセットへ"))
    this.tabIndex = 2;

var tabs = EUi.TabBar("##tabs", ref this.tabIndex, "基本", "詳細", "プリセット");

// ライブラリ側に持たせたまま飛ばす
if (EUi.Button("プリセットへ"))
    EUi.SelectTab("##tabs", 2);
```

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
