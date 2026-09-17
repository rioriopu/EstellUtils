using System.Collections.Generic;
using System.Numerics;

namespace EstellUtils.UI.Windowing;

/// <summary>
/// ウィンドウの位置や大きさなど、覚えておきたい状態。
/// </summary>
/// <remarks>
/// <para>
/// プラグインの設定クラスへそのまま持たせて保存する。
/// ImGui の ini には保存されないので、覚えておきたい場合はこれを使う。
/// </para>
/// <para>
/// 単純なプロパティだけで構成してあるので、Newtonsoft.Json でそのまま読み書きできる。
/// </para>
/// </remarks>
public sealed class EuWindowState
{
    /// <summary>左上の座標。</summary>
    public Vector2 Position { get; set; }

    /// <summary>大きさ。</summary>
    public Vector2 Size { get; set; }

    /// <summary>
    /// 位置が保存済みか。false のときは初回として画面中央に置く。
    /// </summary>
    public bool HasPosition { get; set; }

    /// <summary>タイトルバーだけに畳まれているか。</summary>
    public bool Collapsed { get; set; }

    /// <summary>位置と大きさを固定しているか。</summary>
    public bool Locked { get; set; }

    /// <summary>不透明度。</summary>
    public float Opacity { get; set; } = 1f;

    /// <summary>小窓が開いているか。</summary>
    public bool CompanionOpen { get; set; }

    /// <summary>小窓の左上の座標。</summary>
    public Vector2 CompanionPosition { get; set; }

    /// <summary>小窓の大きさ。</summary>
    public Vector2 CompanionSize { get; set; }

    /// <summary>小窓の位置が保存済みか。</summary>
    public bool HasCompanionPosition { get; set; }
}

/// <summary>
/// 複数のウィンドウの状態をまとめて持つ入れ物。
/// </summary>
/// <remarks>
/// プラグインの設定クラスへこれを 1 つ持たせ、
/// <see cref="EuWindowManager.BindLayout"/> で結びつければ、
/// 以降はすべてのウィンドウの位置と大きさが自動で保存・復元される。
/// <code>
/// // 設定クラス
/// public EuWindowLayout WindowLayout { get; set; } = new();
///
/// // 起動時
/// EUi.Windows.BindLayout(this.config.WindowLayout, this.config.Save);
/// </code>
/// </remarks>
public sealed class EuWindowLayout
{
    /// <summary>ウィンドウ名ごとの状態。</summary>
    public Dictionary<string, EuWindowState> Windows { get; set; } = new();

    /// <summary>名前に対応する状態を取り出す。無ければ作る。</summary>
    public EuWindowState GetOrCreate(string name)
    {
        if (this.Windows.TryGetValue(name, out var state))
            return state;

        state = new EuWindowState();
        this.Windows[name] = state;
        return state;
    }
}
