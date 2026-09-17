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
