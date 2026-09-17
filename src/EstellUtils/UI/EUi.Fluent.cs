using System;

using EstellUtils.UI.Binding;
using EstellUtils.UI.Fluent;

namespace EstellUtils.UI;

/// <summary>
/// 宣言的に画面を組み立てる API。
/// </summary>
public static partial class EUi
{
    /// <summary>
    /// ウィンドウを宣言的に組み立てる。プラグイン起動時に 1 度だけ呼び、
    /// 返ってきたウィンドウを保持して開閉する。
    /// <code>
    /// this.window = EUi.Window("設定")
    ///     .Size(460, 360)
    ///     .Tab("基本", this.DrawBasic)
    ///     .Register();
    /// </code>
    /// </summary>
    /// <param name="name">タイトル。</param>
    public static WindowBuilder Window(string name) => new(name);

    /// <summary>
    /// 設定クラスと画面を結びつける。
    /// </summary>
    /// <typeparam name="T">設定クラスの型。</typeparam>
    /// <param name="target">設定インスタンス。</param>
    /// <param name="save">保存処理。</param>
    /// <param name="saveMode">保存のタイミング。</param>
    /// <remarks>
    /// 返された <see cref="Binder{T}"/> は保存の保留状態を持つため、
    /// 毎フレーム作り直さずにプラグイン側で保持すること。
    /// </remarks>
    public static Binder<T> Bind<T>(T target, Action? save = null, SaveMode saveMode = SaveMode.OnRelease)
        where T : class
        => new(target, save, saveMode);
}
