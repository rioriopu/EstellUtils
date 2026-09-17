using System.Numerics;

using EstellUtils.UI.Binding;

namespace EstellUtils.Demo;

/// <summary>オーバーレイの表示方式。</summary>
public enum OverlayMode
{
    /// <summary>CPU で合成する。</summary>
    [EuEnumLabel("CPU 合成")]
    Cpu,

    /// <summary>GPU で合成する。</summary>
    [EuEnumLabel("GPU 合成")]
    Gpu,

    /// <summary>コンポジタに任せる。</summary>
    [EuEnumLabel("コンポジタ")]
    Composition,
}

/// <summary>
/// 設定バインディングのデモ用。属性だけでどこまで画面が組めるかを示す。
/// </summary>
/// <remarks>
/// この 1 クラスから、グループ分けされた設定画面がそのまま生成される。
/// 画面側のコードは <c>binder.DrawAll()</c> の 1 行だけ。
/// </remarks>
public sealed class DemoConfig
{
    [EuGroup("共通設定")]
    [EuOrder(0)]
    [EuLabel("オーバーレイ更新間隔")]
    [EuTip("値が大きいほど軽くなりますが、手元の UI の反応がカクつきます。\n1 = 毎フレーム、2 = 既定。")]
    [EuRange(1, 6, Suffix = "フレーム")]
    public int UpdateInterval = 2;

    [EuGroup("共通設定")]
    [EuOrder(1)]
    [EuLabel("表示方式")]
    [EuTip("合成をどこで行うかを選びます。")]
    public OverlayMode Mode = OverlayMode.Gpu;

    [EuGroup("共通設定")]
    [EuOrder(2)]
    [EuLabel("不透明度")]
    [EuRange(0f, 1f, Decimals = 2)]
    public float Opacity = 0.85f;

    [EuGroup("共通設定")]
    [EuOrder(3)]
    [EuLabel("DTR と自動連携する")]
    [EuTip("サーバー情報バーの表示状態に合わせてオーバーレイを切り替えます。")]
    public bool LinkWithDtr = true;

    [EuGroup("表示")]
    [EuOrder(0)]
    [EuLabel("ウィンドウを折りたためるようにする")]
    [EuToggle]
    public bool WindowCollapsible;

    [EuGroup("表示")]
    [EuOrder(1)]
    [EuLabel("強調色")]
    [EuTip("見出しやアクセントに使う色です。")]
    public Vector4 AccentColor = new(0.85f, 0.70f, 0.41f, 1f);

    [EuGroup("表示")]
    [EuOrder(2)]
    [EuLabel("表示名")]
    [EuTip("オーバーレイの左上に表示される名前です。")]
    public string DisplayName = string.Empty;

    [EuGroup("試験機能")]
    [EuLabel("デバッグ表示を有効にする")]
    [EuTip("内部状態を画面に出します。動作が重くなる場合があります。")]
    public bool DebugEnabled;

    [EuGroup("試験機能")]
    [EuLabel("ログ保持件数")]
    [EuRange(10, 500, Suffix = "件")]
    public int LogCapacity = 100;

    /// <summary>保存された回数。保存タイミングの確認用。</summary>
    [EuHidden]
    public int SaveCount;
}
