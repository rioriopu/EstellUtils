using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;

using EstellUtils.UI.Core;

namespace EstellUtils.UI.Layout;

/// <summary>
/// レイアウトスコープのスタックを管理する。
/// </summary>
/// <remarks>
/// <para>
/// スコープを閉じると、そのスコープが実際に消費した大きさを外側へ申告する。
/// 外側がレイアウトスコープならそこへ、無ければ <c>ImGui.Dummy</c> で ImGui のカーソルを進める。
/// これにより、生の <c>ImGui.*</c> 呼び出しと同じ流れの中へ自然に収まる。
/// </para>
/// <para>
/// スコープはプールして使い回すため、毎フレームのアロケーションは発生しない。
/// </para>
/// </remarks>
public sealed class LayoutEngine
{
    private readonly List<LayoutScope> active = new(8);
    private readonly Stack<LayoutScope> pool = new(8);

    /// <summary>現在のスコープ。無ければ null。</summary>
    public LayoutScope? Current => this.active.Count > 0 ? this.active[^1] : null;

    /// <summary>入れ子の深さ。</summary>
    public int Depth => this.active.Count;

    /// <summary>
    /// 次の要素を配置できる領域。スコープが無ければ ImGui のカーソル位置から求める。
    /// </summary>
    public Rect AvailableRect
    {
        get
        {
            var current = this.Current;
            if (current is not null)
                return current.PeekAvailable();

            return Rect.FromSize(ImGui.GetCursorScreenPos(), ImGui.GetContentRegionAvail());
        }
    }

    /// <summary>スコープを開く。</summary>
    public LayoutScope Push(
        LayoutKind kind, Rect bounds, Vector2 spacing,
        ReadOnlySpan<SizeSpec> columns = default, bool wrap = false, EdgeInsets padding = default,
        Align crossAlign = Align.Start, float rowHeight = 0f)
    {
        var scope = this.pool.Count > 0 ? this.pool.Pop() : new LayoutScope();
        scope.Reset(kind, bounds, spacing, columns, wrap, padding, crossAlign, rowHeight);
        this.active.Add(scope);
        return scope;
    }

    /// <summary>
    /// スコープを閉じ、消費した大きさを外側へ申告する。
    /// </summary>
    /// <param name="commitToParent">
    /// 外側へ大きさを申告するか。領域を先に確保しているスコープ (スクロール領域など) では
    /// false にして二重に領域を消費しないようにする。
    /// </param>
    public Vector2 Pop(bool commitToParent = true)
    {
        if (this.active.Count == 0)
            return Vector2.Zero;

        var scope = this.active[^1];
        this.active.RemoveAt(this.active.Count - 1);

        var size = scope.ConsumedSize;
        this.pool.Push(scope);

        if (!commitToParent)
            return size;

        var parent = this.Current;
        if (parent is not null)
        {
            if (size != Vector2.Zero)
                parent.Allocate(size);
        }
        else if (size != Vector2.Zero)
        {
            // 最も外側のスコープ。ImGui 側のカーソルを進めて後続の描画と整合させる
            ImGui.Dummy(size);
        }

        return size;
    }

    /// <summary>
    /// 積まれているスコープをすべて捨てる。例外でスコープが閉じられなかった場合の保険として
    /// フレーム開始時に呼ばれる。
    /// </summary>
    public void Reset()
    {
        for (var i = 0; i < this.active.Count; i++)
            this.pool.Push(this.active[i]);

        this.active.Clear();
    }
}

/// <summary><c>using</c> でレイアウトスコープを閉じるハンドル。</summary>
public readonly struct LayoutHandle : IDisposable
{
    private readonly LayoutEngine? engine;
    private readonly bool commitToParent;

    /// <summary>スコープを閉じるハンドルを作る。</summary>
    /// <param name="engine">対象のレイアウトエンジン。</param>
    /// <param name="commitToParent">
    /// 閉じるときに消費した大きさを外側へ申告するか。
    /// 領域を先に確保してから開いたスコープ (<c>Region</c> / <c>Sized</c> など) では
    /// false にしないと、同じ領域を二重に消費してしまう。
    /// </param>
    internal LayoutHandle(LayoutEngine engine, bool commitToParent = true)
    {
        this.engine = engine;
        this.commitToParent = commitToParent;
    }

    /// <inheritdoc/>
    public void Dispose() => this.engine?.Pop(this.commitToParent);
}
