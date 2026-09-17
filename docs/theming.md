# テーマと描画の差し替え

見た目を変える方法は 3 段階あります。下へ行くほど自由度が上がります。

1. **トークンを書き換える** — 色・余白・角丸・遷移速度を変える
2. **Painter を差し替える** — ウィジェットの描き方そのものを作り直す
3. **独自ウィジェットを書く** — 描画も入力も自分で決める（[widgets.md](widgets.md) 参照）

## 1. トークンを書き換える

プリセットを複製してから変更します。テーマは使い回すものなので、
プラグイン側で保持してください（毎フレーム作り直すとアロケーションが増えます）。

```csharp
using EstellUtils.UI.Theming;
using EstellUtils.UI.Theming.Presets;

private readonly Theme theme = XivNativeTheme.Create().Derive("MyPlugin", t =>
{
    t.Colors.Accent = EuColor.Rgb(0x6FA8DC);
    t.Colors.AccentHover = EuColor.Rgb(0x8FC0E8);
    t.Metrics.WidgetRounding = 5f;
    t.Motion.HoverSpeed = 24f;
});

// 起動時に既定テーマとして設定する
EUi.Initialize(pluginInterface, this.theme, log);
```

一部分だけ別テーマで描くこともできます。

```csharp
using (EUi.PushTheme(this.alertTheme))
{
    EUi.Card("warning");
    // ...
}
```

### 主な色トークン

| 分類 | トークン |
|---|---|
| ウィンドウ | `WindowTop` / `WindowBottom` / `WindowBorder` / `WindowBorderInner` |
| タイトルバー | `TitleTop` / `TitleBottom` / `TitleInactive` / `TitleText` / `TitleUnderline` |
| 面 | `Surface` / `SurfaceHover` / `SurfaceActive` / `SurfaceBorder` |
| ウィジェット | `WidgetTop` / `WidgetBottom` / `WidgetHoverTop` / `WidgetHoverBottom` / `WidgetActiveTop` / `WidgetActiveBottom` / `WidgetBorder` / `WidgetBorderHover` / `WidgetDisabled` |
| 文字 | `Text` / `TextMuted` / `TextDisabled` / `TextHeading` / `TextOnAccent` / `TextLink` |
| アクセント | `Accent` / `AccentHover` / `AccentActive` / `AccentMuted` |
| 状態 | `Success` / `Warning` / `Danger` / `Info` |
| 部品 | `Track` / `TrackFill` / `Knob` / `KnobBorder` / `Checkmark` / `Separator` / `Selection` / `FocusRing` / `Shadow` / `Overlay` |

グラデーションを使う箇所は「上/下」の 2 色で持っています。
単色にしたい場合は両方に同じ色を入れてください（モダンダークテーマがその例です）。

### 拡大率

`theme.Scale` を設定すると、寸法トークンが一括で拡大されます。

```csharp
theme.Scale = 1.25f;
```

### トークンを増やす

ライブラリが用意していない値は自由に追加できます。

```csharp
theme.CustomColors["myPlugin.highlight"] = EuColor.Rgb(0xFF8800);

var highlight = EUi.Theme.Color("myPlugin.highlight", EUi.Colors.Accent);
```

### 用意されているプリセット

| プリセット | 特徴 |
|---|---|
| `XivNativeTheme` | 既定。黒に近い紺の半透明地、金ベージュの細枠、金属質の縦グラデーション |
| `ModernDarkTheme` | 装飾を抑えた平面的なデザイン。情報量の多い画面向け |
| `FrostedGlassTheme` | すりガラス風。透過を強めた地に上端の光沢と明るい細枠 |

`FrostedGlassTheme` は背後を実際にぼかしているわけではありません
（ImGui は背後のピクセルを読めないため、描画の仕組み上できません）。
透過・光沢・細枠の組み合わせで、厚みのある曇りガラスの板に見せています。
背景の情報量を落としたい場合は `EUi.Scrim()` や `EuWindow.DimBackground` を併用してください。
詳しくは [windows.md](windows.md) を参照。

## 2. Painter を差し替える

`DefaultWidgetPainter` を継承し、必要なメソッドだけ差し替えます。
入力判定はライブラリ側が行うので、実装側は描画だけに集中できます。

```csharp
public sealed class MyPainter : DefaultWidgetPainter
{
    public override void DrawButton(in WidgetVisual v, ReadOnlySpan<char> label, ButtonStyle style)
    {
        // v.Hover / v.Press / v.Disabled は 0〜1 の遷移量
        var glow = EuColor.WithAlpha(Colors.Accent, v.Hover * 0.4f);

        Painter.Shadow(v.Rect, glow, 8f, 12f);
        Painter.RectGradientV(v.Rect, Colors.WidgetTop, Colors.WidgetBottom, 12f);
        TextPainter.TextIn(v.Rect, Colors.Text, label, Align.Center, Align.Center);
    }
}

theme.Painter = new MyPainter();
```

差し替えられるのは以下です。

`DrawButton` / `DrawCheckbox` / `DrawRadio` / `DrawToggle` / `DrawSlider` /
`DrawProgressBar` / `DrawCard` / `DrawSectionHeader` / `DrawSeparator` / `DrawTab` /
`DrawInputFrame` / `DrawWindowChrome` / `DrawWindowButton` / `DrawResizeGrip`

`DrawWindowChrome` にはタイトルバー全体の矩形と、ボタンを除いた「文字を置ける範囲」の
両方が渡されます。ボタンが増えてもタイトルが重なりません。

`WidgetVisual` が持つ情報：

| フィールド | 意味 |
|---|---|
| `Rect` | 描画先の矩形 |
| `Hover` / `Press` / `Disabled` | 各状態への遷移量 (0〜1) |
| `On` / `OnAmount` | ON 状態か / その遷移量 |
| `Value` | 0〜1 に正規化した値（スライダー・進捗） |
| `Focused` | キーボードフォーカスを持っているか |

## 描画プリミティブ

`Painter` と `TextPainter` が描画の基本部品です。

```csharp
Painter.Rect(rect, color, rounding, corners);
Painter.RectOutline(rect, color, thickness, rounding, corners);
Painter.RectGradientV(rect, top, bottom, rounding);   // 角丸にも対応
Painter.Shadow(rect, color, size, rounding, offset);
Painter.Circle(center, radius, color);
Painter.Ring(center, inner, outer, color, fromRad, toRad);
Painter.Chevron(area, Direction.Down, color, thickness);
Painter.Check(area, color, thickness, progress);      // progress で描き込みアニメ
Painter.NineSlice(texture, rect, border, textureSize, tint);

using (Painter.Clip(rect)) { /* はみ出しを切る */ }
using (Painter.UseForeground()) { /* 最前面レイヤーへ描く */ }
```

角丸のグラデーションは、`ImDrawList` の頂点バッファを直接書き換えて実現しています
（`AddRectFilledMultiColor` は角丸に対応していないため）。

## フォント

```csharp
using (EUi.PushFont(FontRole.Large))
    EUi.Label("大きい文字");
```

`Default` / `Small` / `Body` / `Large` / `Title` / `Mono` / `Icon` の 7 種類です。
`Body` 系はゲームフォント（Axis）を使います。Dalamud の既定フォントにしたい場合は
`EUi.Fonts.UseGameFont = false` を設定してください。
