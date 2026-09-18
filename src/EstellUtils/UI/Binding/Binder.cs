using System;
using System.Collections.Generic;

using EstellUtils.Diagnostics;
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

    /// <summary>巻き戻しの基準にする、保存済みの値。</summary>
    private readonly Dictionary<string, object?> saved = new(StringComparer.Ordinal);

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

    /// <summary>
    /// 絞り込みの文字列。空でなければ、ラベル・説明・グループ名に含む項目だけが描かれる。
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

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

    /// <summary>
    /// 絞り込み用の入力欄を描く。入力は <see cref="SearchText"/> へ反映され、
    /// 以降の <see cref="DrawAll"/> が自動的に絞り込まれる。
    /// </summary>
    /// <param name="hint">空のときに表示する案内文。</param>
    public bool DrawSearchBox(string hint = "設定を検索")
    {
        var text = this.SearchText;
        var result = EUi.TextInput("##euBinderSearch", ref text, hint, 64);

        if (!result.Changed)
            return false;

        this.SearchText = text;
        return true;
    }

    /// <summary>すべての項目を描く。</summary>
    /// <param name="grouped">
    /// <see cref="EuGroupAttribute"/> ごとにセクションへまとめるか。
    /// 絞り込み中は、探している項目がすぐ見えるようグループ分けを外して並べる。
    /// </param>
    public bool DrawAll(bool grouped = true)
    {
        var changed = false;

        if (this.IsSearching)
            return this.DrawFiltered();

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

    /// <summary>絞り込み中か。</summary>
    private bool IsSearching => !string.IsNullOrWhiteSpace(this.SearchText);

    /// <summary>絞り込み結果を、グループ分けせずに並べる。</summary>
    private bool DrawFiltered()
    {
        var changed = false;
        var hits = 0;

        foreach (var binding in ConfigModel<T>.Bindings)
        {
            if (!this.Matches(binding))
                continue;

            hits++;
            changed |= this.DrawBinding(binding, false);
        }

        if (hits == 0)
            EUi.Muted("一致する設定はありません。");

        this.MaybeFlush();
        return changed;
    }

    /// <summary>絞り込みの文字列に一致するか。</summary>
    private bool Matches(FieldBinding<T> binding)
    {
        var query = this.SearchText;

        return binding.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
            || binding.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (binding.Tip?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
            || (binding.Group?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
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

    /// <summary>
    /// 項目のラベルを、毎回求める関数へ差し替える。
    /// </summary>
    /// <param name="name">項目名。入れ子は <c>"MobHunt.Enabled"</c> のように書く。</param>
    /// <param name="label">ラベルを返す関数。</param>
    /// <remarks>
    /// <para>
    /// 属性に書けるのはコンパイル時定数だけなので、表示言語を実行時に切り替える場合はこれを使う。
    /// 起動時に一度呼べばよい。
    /// </para>
    /// <code>
    /// this.binder.SetLabel(nameof(Config.UpdateInterval), () => Language.Settings.UpdateInterval);
    /// </code>
    /// <para>
    /// 項目の解析結果は型ごとに共有されるので、この差し替えも同じ型のすべての Binder に効く。
    /// 表示言語はふつうアプリ全体で 1 つなので、そのほうが都合がよい。
    /// </para>
    /// </remarks>
    public void SetLabel(string name, Func<string> label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var binding = ConfigModel<T>.Find(name);

        if (binding is null)
        {
            UiLog.Warning($"設定項目「{name}」が見つかりません。ラベルを差し替えられませんでした。");
            return;
        }

        binding.LabelProvider = label;
    }

    /// <summary>項目のツールチップを、毎回求める関数へ差し替える。</summary>
    /// <param name="name">項目名。</param>
    /// <param name="tip">説明を返す関数。</param>
    public void SetTip(string name, Func<string?> tip)
    {
        ArgumentNullException.ThrowIfNull(tip);

        var binding = ConfigModel<T>.Find(name);

        if (binding is null)
        {
            UiLog.Warning($"設定項目「{name}」が見つかりません。説明を差し替えられませんでした。");
            return;
        }

        binding.TipProvider = tip;
    }

    /// <summary>
    /// 現在の値を「保存済み」として覚える。巻き戻しの基準になる。
    /// </summary>
    /// <remarks>
    /// ウィンドウを開いた時点で呼んでおくと、<see cref="Revert"/> でそこまで戻せる。
    /// <see cref="Flush"/> と保存の実行時にも自動で更新される。
    /// </remarks>
    public void MarkSaved()
    {
        this.saved.Clear();

        foreach (var binding in ConfigModel<T>.Bindings)
            this.saved[binding.Name] = binding.GetValue(this.Target);
    }

    /// <summary>
    /// 最後に保存した値へ巻き戻す。「破棄して閉じる」に当たる操作。
    /// </summary>
    /// <returns>巻き戻した項目があれば true。</returns>
    /// <remarks>
    /// <para>
    /// 既定値へ戻す <see cref="ResetAll"/> とは別物で、こちらは編集前の値へ戻す。
    /// </para>
    /// <para>
    /// 基準は <see cref="MarkSaved"/> を呼んだ時点、または最後に保存が走った時点。
    /// 一度も記録していない場合は何もしない。
    /// </para>
    /// </remarks>
    public bool Revert()
    {
        if (this.saved.Count == 0)
            return false;

        var reverted = false;

        foreach (var binding in ConfigModel<T>.Bindings)
        {
            if (this.saved.TryGetValue(binding.Name, out var value) &&
                binding.SetValue(this.Target, value))
            {
                reverted = true;
            }
        }

        this.pendingSave = false;
        return reverted;
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
        this.MarkSaved();
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
