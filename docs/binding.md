# 設定バインディング

設定クラスに属性を付けるだけで、設定画面が生成されます。

## 使い方

```csharp
using EstellUtils.UI.Binding;

public sealed class Configuration : IPluginConfiguration
{
    [EuGroup("共通設定")]
    [EuOrder(0)]
    [EuLabel("オーバーレイ更新間隔")]
    [EuTip("値が大きいほど軽くなりますが、手元の UI の反応がカクつきます。")]
    [EuRange(1, 6, Suffix = "フレーム")]
    public int UpdateInterval = 2;

    [EuGroup("共通設定")]
    [EuLabel("DTR と自動連携する")]
    public bool LinkWithDtr = true;

    [EuGroup("表示")]
    [EuLabel("強調色")]
    public Vector4 AccentColor = new(0.85f, 0.70f, 0.41f, 1f);

    [EuHidden]
    public int Version;
}
```

画面側はこれだけです。

```csharp
// Binder は保存の保留状態を持つので、毎フレーム作り直さず保持する
private readonly Binder<Configuration> binder;

public ConfigWindow(Configuration config) : base("設定")
{
    this.binder = EUi.Bind(config, config.Save);
}

public override void Draw() => this.binder.DrawAll();

public override void OnClose() => this.binder.Flush();
```

`DrawAll()` は `EuGroup` ごとに折りたためるセクションを作り、
`EuOrder` の順に項目を並べます。

## 属性

| 属性 | 説明 |
|---|---|
| `[EuLabel("...")]` | 表示するラベル。省略するとメンバー名から自動生成 |
| `[EuTip("...")]` | ホバー時のツールチップ |
| `[EuRange(min, max)]` | 数値の範囲。`Decimals` と `Suffix` も指定できる |
| `[EuGroup("...")]` | 項目をまとめるセクション名 |
| `[EuOrder(n)]` | 並び順。小さいほど上 |
| `[EuToggle]` | 真偽値をトグルスイッチで表示（既定はチェックボックス） |
| `[EuColor]` | `uint` を色として編集する。`ShowAlpha` で不透明度の編集可否 |
| `[EuEnumLabel("...")]` | 列挙値の表示名（列挙型のメンバーに付ける） |
| `[EuHidden]` | 自動生成の対象から外す |

## 対応する型

| 型 | ウィジェット |
|---|---|
| `bool` | チェックボックス（`[EuToggle]` でトグル） |
| `int` | 整数スライダー（`[EuRange]` が無ければ 0〜100） |
| `float` | 小数スライダー（`[EuRange]` が無ければ 0〜1） |
| `string` | テキスト入力 |
| 列挙型 | ドロップダウン |
| `Vector4` | カラーピッカー |
| `uint` | カラーピッカー（`[EuColor]` が必要） |

対応しない型は無視されます。手書きの画面と併用してください。

## 保存のタイミング

既定は `SaveMode.OnRelease` です。値が変わったら保存を保留し、
**マウスを離したときにまとめて保存**します。スライダーのドラッグ中に
毎フレーム設定ファイルを書かないための既定値です。

| モード | 挙動 |
|---|---|
| `Immediate` | 値が変わるたびに保存 |
| `OnRelease` | マウスを離したときにまとめて保存（既定） |
| `Manual` | `Flush()` を呼んだときだけ保存 |

ウィンドウを閉じるときは `Flush()` を呼んで、保留分を確定させてください。

## 部分的に使う

全自動でなく、項目を選んで描くこともできます。

```csharp
EUi.Heading("基本");
this.binder.Draw(nameof(Configuration.UpdateInterval));
this.binder.Draw(nameof(Configuration.LinkWithDtr));

EUi.Separator();
this.binder.DrawGroup("表示");     // グループ内の項目だけ（見出しは付かない）
```

手書きのウィジェットと自由に混ぜられます。

## 絞り込み

項目が増えてきたら、検索欄を置くと目的の設定へすぐ辿り着けます。

```csharp
this.binder.DrawSearchBox();
this.binder.DrawAll();
```

入力は `binder.SearchText` に入り、`DrawAll()` が自動で絞り込みます。
絞り込み中はグループ分けを外してフラットに並ぶので、探している項目がすぐ見えます。
ラベル・メンバー名・説明・グループ名のいずれかに含まれていれば一致とみなします。

## 既定値への復帰

引数なしコンストラクタを持つ設定クラスなら、既定値を自動で採取します。

```csharp
if (EUi.Button("既定値へ戻す", ButtonStyle.Danger))
    this.binder.ResetAll();

if (this.binder.HasChanges())
    EUi.Muted("既定値から変更されています");
```

特定の項目だけ戻すこともできます。

```csharp
this.binder.Reset(nameof(Configuration.UpdateInterval));
```

## 性能について

型の解析は最初の 1 回だけで、結果は静的にキャッシュされます。
値の読み書きは式木からコンパイルしたデリゲートを使うため、
毎フレームのリフレクション呼び出しやボクシングは発生しません。


## 表示言語を実行時に切り替える

属性に書けるのはコンパイル時定数だけなので、ラベルを実行時に差し替えるには
関数を渡します。方法は 2 つあります。

### 静的メンバーから引く

```csharp
[EuLabelFrom(typeof(Language.Settings), nameof(Language.Settings.UpdateInterval))]
public int UpdateInterval = 2;
```

指定先は `static` で、`string` を返すプロパティ・フィールド・引数なしメソッドのいずれか。
描画のたびに読むので、言語を切り替えればそのまま追従します。

### コードから差し替える

```csharp
this.binder.SetLabel(nameof(Config.UpdateInterval), () => Language.Settings.UpdateInterval);
this.binder.SetTip(nameof(Config.UpdateInterval), () => Language.Tips.UpdateInterval);
```

起動時に一度呼べば足ります。項目の解析結果は型ごとに共有されるので、
この差し替えも同じ型のすべての `Binder` に効きます。

## 入れ子になった設定クラス

設定をいくつかのクラスへ分けている場合は `[EuNested]` を付けます。

```csharp
public class PluginConfig
{
    [EuNested(Group = "モブハント")]
    public MobHuntConfig MobHunt { get; set; } = new();

    [EuNested(Group = "宝の地図")]
    public TreasureConfig Treasure { get; set; } = new();
}
```

中身の項目が、あたかも直下にあるかのように並びます。
項目名は `MobHunt.Enabled` のように親を辿った形になるので、
`Binder.SetLabel` などで指すときもこの名前を使います。

**入れ子のクラスは初期化しておいてください** (`= new()`)。
値の読み書きは `target.MobHunt.Enabled` という式を組み立てて行うため、
途中が null だとそこで失敗します。入れ子は 4 段まで辿ります。

## ベクトルと色の区別

`Vector4` を**色として扱うのは `[EuColor]` を付けたときだけ**です。
座標や余白の 4 つ組は数値の入力欄になります。

```csharp
[EuColor]
public Vector4 MarkerColor = new(1f, 0.5f, 0f, 1f);       // 色ピッカー

[EuVector("L", "D", "R", "U")]
public Vector4 ScreenMargin = new(10f, 10f, 10f, 10f);    // 数値 4 つ
```

`[EuVector]` は成分のラベルを与えるためのもので、無くても数値として扱われます。
`Step` / `Min` / `Max` も指定できます。

## 破棄して閉じる

既定値へ戻す `ResetAll()` とは別に、**編集前の値へ巻き戻す** `Revert()` があります。

```csharp
public override void OnOpen() => this.binder.MarkSaved();   // 基準を記録

// 「破棄して閉じる」ボタン
if (EUi.Button("破棄"))
{
    this.binder.Revert();
    this.IsOpen = false;
}
```

基準は `MarkSaved()` を呼んだ時点、または最後に保存が走った時点です。
一度も記録していなければ何もしません。
