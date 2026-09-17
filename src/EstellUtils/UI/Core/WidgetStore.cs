using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace EstellUtils.UI.Core;

/// <summary>
/// 1 ウィジェット分の永続状態。immediate mode ではウィジェット自身がフレームをまたいで
/// 値を保持できないため、<see cref="EuId"/> を鍵にしてここへ預ける。
/// </summary>
/// <remarks>
/// 大量に作られるため struct とし、<see cref="WidgetStore.GetRef"/> で参照を直接書き換える。
/// 汎用のフィールドをいくつか用意してあるので、単純なウィジェットは専用の状態クラスを
/// 作らずに済む。複雑なもの (テキスト入力など) は <see cref="WidgetStore.GetOrCreate{T}"/> を使う。
/// </remarks>
public struct WidgetState
{
    /// <summary>最後にこの状態が参照されたフレーム番号。未使用状態の破棄判定に使う。</summary>
    public uint LastFrame;

    /// <summary>ホバー遷移量 (0=非ホバー, 1=ホバー)。</summary>
    public float Hover;

    /// <summary>押下遷移量 (0=通常, 1=押下)。</summary>
    public float Press;

    /// <summary>有効/無効やチェック状態などの遷移量。</summary>
    public float Toggle;

    /// <summary>折りたたみ・展開の状態。</summary>
    public bool Open;

    /// <summary>開閉アニメーションの進捗 (0=閉, 1=開)。</summary>
    public float OpenAmount;

    /// <summary>スクロール位置の現在値。</summary>
    public float Scroll;

    /// <summary>スクロール位置の目標値 (慣性スクロール用)。</summary>
    public float ScrollTarget;

    /// <summary>前フレームに計測した内容サイズ (幅)。レイアウトの先読みに使う。</summary>
    public float MeasuredWidth;

    /// <summary>前フレームに計測した内容サイズ (高さ)。</summary>
    public float MeasuredHeight;

    /// <summary>ウィジェット固有の汎用スロット。</summary>
    public float Custom0;

    /// <summary>ウィジェット固有の汎用スロット。</summary>
    public float Custom1;

    /// <summary>アクティブになった時刻 (<see cref="InputState.Time"/> 基準)。</summary>
    public float ActivatedAt;

    /// <summary>選択中の項目。タブや一覧で使う。</summary>
    public int SelectedIndex;

    /// <summary>初期化済みか。既定値の投入を 1 度だけ行うために使う。</summary>
    public bool Initialized;
}

/// <summary>
/// <see cref="EuId"/> をキーにウィジェット状態を保持する。フレームをまたいで参照されなく
/// なったエントリは定期的に破棄される。
/// </summary>
public sealed class WidgetStore
{
    /// <summary>未参照のまま何フレーム経過したら破棄するか。</summary>
    private const uint StaleFrameThreshold = 240;

    /// <summary>何フレームごとに掃除するか。毎フレーム走査すると無駄なので間引く。</summary>
    private const uint SweepInterval = 300;

    private readonly Dictionary<ulong, WidgetState> states = new();
    private readonly Dictionary<ulong, object> objects = new();
    private readonly List<ulong> removalBuffer = new();

    private uint currentFrame;

    /// <summary>保持しているウィジェット状態の数。デバッグ表示用。</summary>
    public int StateCount => this.states.Count;

    /// <summary>保持しているオブジェクト状態の数。デバッグ表示用。</summary>
    public int ObjectCount => this.objects.Count;

    /// <summary>
    /// 状態への参照を取得する。存在しなければ既定値で作る。
    /// 返された参照をその場で書き換えると、そのまま保存される。
    /// </summary>
    public ref WidgetState GetRef(EuId id)
    {
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(this.states, id.Value, out _);
        state.LastFrame = this.currentFrame;
        return ref state;
    }

    /// <summary>状態を読み取るだけ (存在しなければ既定値)。参照フレームは更新しない。</summary>
    public WidgetState Peek(EuId id)
        => this.states.TryGetValue(id.Value, out var state) ? state : default;

    /// <summary>その ID の状態が保持されているか。</summary>
    public bool Has(EuId id) => this.states.ContainsKey(id.Value);

    /// <summary>
    /// 参照型の状態を取得する。存在しなければ <paramref name="factory"/> で作る。
    /// テキスト入力バッファのように struct で表せない状態に使う。
    /// </summary>
    public T GetOrCreate<T>(EuId id, Func<T> factory)
        where T : class
    {
        if (this.objects.TryGetValue(id.Value, out var existing) && existing is T typed)
        {
            // 参照型側も同じフレーム管理に乗せる
            this.GetRef(id);
            return typed;
        }

        var created = factory();
        this.objects[id.Value] = created;
        this.GetRef(id);
        return created;
    }

    /// <summary>特定の ID の状態を明示的に破棄する。</summary>
    public void Remove(EuId id)
    {
        this.states.Remove(id.Value);

        if (this.objects.Remove(id.Value, out var obj) && obj is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>すべての状態を破棄する。テーマ切替などで作り直したいときに使う。</summary>
    public void Clear()
    {
        foreach (var obj in this.objects.Values)
        {
            if (obj is IDisposable disposable)
                disposable.Dispose();
        }

        this.states.Clear();
        this.objects.Clear();
    }

    /// <summary>フレーム開始時に呼ぶ。掃除もここで間引いて行う。</summary>
    internal void NewFrame(uint frame)
    {
        this.currentFrame = frame;

        if (frame % SweepInterval != 0)
            return;

        this.Sweep();
    }

    /// <summary>長く参照されていないエントリを破棄する。</summary>
    private void Sweep()
    {
        this.removalBuffer.Clear();

        foreach (var pair in this.states)
        {
            if (this.currentFrame - pair.Value.LastFrame > StaleFrameThreshold)
                this.removalBuffer.Add(pair.Key);
        }

        foreach (var key in this.removalBuffer)
        {
            this.states.Remove(key);

            if (this.objects.Remove(key, out var obj) && obj is IDisposable disposable)
                disposable.Dispose();
        }

        this.removalBuffer.Clear();
    }
}
