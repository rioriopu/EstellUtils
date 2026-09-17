using System;
using System.Collections.Generic;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Binding;

/// <summary>保存を行うタイミング。</summary>
public enum SaveMode
{
    /// <summary>値が変わるたびに保存する。</summary>
    Immediate,

    /// <summary>
    /// 値が変わったら保留し、マウスを離したときにまとめて保存する。
    /// スライダーのドラッグ中に毎フレーム保存しないための既定値。
    /// </summary>
    OnRelease,

    /// <summary>自動保存しない。<see cref="Binder{T}.Flush"/> を呼んだときだけ保存する。</summary>
    Manual,
}

/// <summary>
/// 設定クラスと画面を結びつける。
/// </summary>
/// <typeparam name="T">設定クラスの型。</typeparam>
/// <remarks>
/// <code>
/// // 設定クラス側で見せ方まで宣言しておく
/// public class Config
/// {
///     [EuGroup("共通設定")]
///     [EuLabel("オーバーレイ更新間隔")]
///     [EuTip("値が大きいほど軽くなりますが、反応がカクつきます。")]
///     [EuRange(1, 6, Suffix = "フレーム")]
///     public int UpdateInterval = 2;
///
///     [EuGroup("共通設定")]
///     [EuLabel("デバッグ表示")]
///     public bool Debug;
/// }
///
/// // 画面側はこれだけ
/// binder.DrawAll();
/// </code>
/// </remarks>
public sealed class Binder<T>
    where T : class
{
    private readonly Action? save;

    private bool pendingSave;

    /// <summary>設定クラスと保存処理を結びつける。</summary>
    /// <param name="target">対象の設定インスタンス。</param>
    /// <param name="save">保存処理。省略すると保存は行われない。</param>
    /// <param name="saveMode">保存のタイミング。</param>
    public Binder(T target, Action? save = null, SaveMode saveMode = SaveMode.OnRelease)
    {
        this.Target = target ?? throw new ArgumentNullException(nameof(target));
        this.save = save;
        this.SaveMode = saveMode;
    }

    /// <summary>対象の設定インスタンス。</summary>
    public T Target { get; }

    /// <summary>保存のタイミング。</summary>
    public SaveMode SaveMode { get; set; }

    /// <summary>まとめて無効表示にするか。前提条件が満たされていないときに使う。</summary>
    public bool Disabled { get; set; }

    /// <summary>解析された項目。</summary>
    public IReadOnlyList<FieldBinding<T>> Bindings => ConfigModel<T>.Bindings;

    /// <summary>
    /// 名前を指定して 1 項目を描く。名前は <c>nameof</c> で渡すのが安全。
    /// </summary>
    /// <param name="name">メンバー名。</param>
    /// <param name="disabled">この項目だけ無効にするか。</param>
    public bool Draw(string name, bool disabled = false)
    {
        var binding = ConfigModel<T>.Find(name);

        if (binding is null)
        {
            EUi.Muted($"[未対応の設定項目: {name}]");
            return false;
        }

        return this.DrawBinding(binding, disabled);
    }

    /// <summary>すべての項目を描く。</summary>
    /// <param name="grouped">
    /// <see cref="EuGroupAttribute"/> ごとにセクションへまとめるか。
    /// </param>
    public bool DrawAll(bool grouped = true)
    {
        var changed = false;

        if (!grouped)
        {
            foreach (var binding in ConfigModel<T>.Bindings)
                changed |= this.DrawBinding(binding, false);

            this.MaybeFlush();
            return changed;
        }

        // グループ指定の無い項目を先に、まとめて出す
        foreach (var binding in ConfigModel<T>.Bindings)
        {
            if (string.IsNullOrEmpty(binding.Group))
                changed |= this.DrawBinding(binding, false);
        }

        foreach (var group in ConfigModel<T>.Groups)
        {
            using var section = EUi.Section(group);

            if (!section.IsVisible)
                continue;

            foreach (var binding in ConfigModel<T>.Bindings)
            {
                if (string.Equals(binding.Group, group, StringComparison.Ordinal))
                    changed |= this.DrawBinding(binding, false);
            }
        }

        this.MaybeFlush();
        return changed;
    }

    /// <summary>指定したグループの項目だけを描く (セクションの見出しは付けない)。</summary>
    public bool DrawGroup(string group)
    {
        var changed = false;

        foreach (var binding in ConfigModel<T>.Bindings)
        {
            if (string.Equals(binding.Group, group, StringComparison.Ordinal))
                changed |= this.DrawBinding(binding, false);
        }

        this.MaybeFlush();
        return changed;
    }

    /// <summary>すべての項目を既定値へ戻す。</summary>
    public void ResetAll()
    {
        foreach (var binding in ConfigModel<T>.Bindings)
            binding.ResetToDefault(this.Target);

        this.save?.Invoke();
        this.pendingSave = false;
    }

    /// <summary>指定した項目を既定値へ戻す。</summary>
    public void Reset(string name)
    {
        ConfigModel<T>.Find(name)?.ResetToDefault(this.Target);
        this.RequestSave();
    }

    /// <summary>既定値から変更されている項目があるか。</summary>
    public bool HasChanges()
    {
        foreach (var binding in ConfigModel<T>.Bindings)
        {
            if (binding.IsModified(this.Target))
                return true;
        }

        return false;
    }

    /// <summary>保留中の保存があれば今すぐ実行する。ウィンドウを閉じるときなどに呼ぶ。</summary>
    public void Flush()
    {
        if (!this.pendingSave)
            return;

        this.save?.Invoke();
        this.pendingSave = false;
    }

    /// <summary>1 項目を描き、変更があれば保存を要求する。</summary>
    private bool DrawBinding(FieldBinding<T> binding, bool disabled)
    {
        var changed = binding.Draw(this.Target, disabled || this.Disabled);

        if (changed)
            this.RequestSave();

        return changed;
    }

    /// <summary>保存を要求する。実際の保存は <see cref="SaveMode"/> に従う。</summary>
    private void RequestSave()
    {
        if (this.save is null)
            return;

        if (this.SaveMode == SaveMode.Immediate)
        {
            this.save();
            return;
        }

        this.pendingSave = true;
    }

    /// <summary>マウスが離されていれば保留中の保存を実行する。</summary>
    private void MaybeFlush()
    {
        if (!this.pendingSave || this.SaveMode != SaveMode.OnRelease)
            return;

        if (!UiContext.Current.Input.IsDown(MouseButton.Left))
            this.Flush();
    }
}
