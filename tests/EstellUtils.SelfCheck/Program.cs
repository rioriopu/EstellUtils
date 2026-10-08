using System;
using System.Collections.Generic;
using System.Numerics;

using EstellUtils.UI;
using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;
using EstellUtils.UI.Theming;
using EstellUtils.UI.Theming.Presets;

namespace EstellUtils.SelfCheck;

/// <summary>
/// ゲームを起動せずに確かめられる部分の自己検証。
/// </summary>
/// <remarks>
/// <para>
/// 描画や入力は実機でしか確かめられないが、矩形の計算・ID の生成・色の変換・
/// 列幅の解決といった純粋な処理は、ここで機械的に検証できる。
/// </para>
/// <para>
/// テストフレームワークを持ち込むと NuGet の復元が要るため、
/// 依存なしで <c>dotnet run</c> できる形にしてある。失敗があれば終了コード 1 を返す。
/// </para>
/// </remarks>
internal static class Program
{
    private static readonly List<string> Failures = new();

    private static int Main()
    {
        CheckRectCuts();
        CheckRectBasics();
        CheckIds();
        CheckColors();
        CheckSizeSpecs();
        CheckEasingAndAnim();
        CheckLabelRoom();
        CheckEdgeSnap();
        CheckCrossAlign();
        CheckColumnLayout();
        CheckColumnMinimums();
        CheckThemeOverlay();
        CheckAutoColumnCarryOver();
        CheckAutoSizeMeasure();
        CheckTabWrapping();
        CheckDropdownPlacement();

        if (Failures.Count == 0)
        {
            Console.WriteLine("自己検証: すべて合格しました。");
            return 0;
        }

        Console.WriteLine($"自己検証: {Failures.Count} 件の失敗");

        foreach (var failure in Failures)
            Console.WriteLine("  - " + failure);

        return 1;
    }

    /// <summary>
    /// 切り出しメソッドの検証。
    /// </summary>
    /// <remarks>
    /// とくに「レシーバと out 引数へ同じ変数を渡す」書き方を確かめる。
    /// readonly struct の this は参照で渡されるため、実装の順序を誤ると
    /// 戻り値が潰れる (実際にタイトルバーのボタンが幅 0 になる不具合を起こした)。
    /// </remarks>
    private static void CheckRectCuts()
    {
        var area = Rect.FromSize(0f, 0f, 100f, 20f);

        // 自己代入しながら右から 3 回切り出す
        var a = area.CutRight(20f, out area);
        var b = area.CutRight(20f, out area);
        var c = area.CutRight(20f, out area);

        Expect(a.Width == 20f && a.Min.X == 80f, $"CutRight 1 回目が壊れている: {a}");
        Expect(b.Width == 20f && b.Min.X == 60f, $"CutRight 2 回目が壊れている: {b}");
        Expect(c.Width == 20f && c.Min.X == 40f, $"CutRight 3 回目が壊れている: {c}");
        Expect(area.Width == 40f, $"CutRight の残りが合わない: {area}");
        Expect(a.Height == 20f && b.Height == 20f, "CutRight で高さが変わっている");

        var left = Rect.FromSize(0f, 0f, 100f, 20f);
        var l1 = left.CutLeft(30f, out left);

        Expect(l1.Width == 30f && l1.Min.X == 0f, $"CutLeft が壊れている: {l1}");
        Expect(left.Min.X == 30f && left.Width == 70f, $"CutLeft の残りが合わない: {left}");
        Expect(left.Min.Y == 0f && left.Max.Y == 20f, $"CutLeft で縦がずれている: {left}");

        var top = Rect.FromSize(0f, 0f, 100f, 50f);
        var t1 = top.CutTop(10f, out top);

        Expect(t1.Height == 10f && t1.Min.Y == 0f, $"CutTop が壊れている: {t1}");
        Expect(top.Min.Y == 10f && top.Height == 40f, $"CutTop の残りが合わない: {top}");

        var bottom = Rect.FromSize(0f, 0f, 100f, 50f);
        var b1 = bottom.CutBottom(15f, out bottom);

        Expect(b1.Height == 15f && b1.Min.Y == 35f, $"CutBottom が壊れている: {b1}");
        Expect(bottom.Height == 35f, $"CutBottom の残りが合わない: {bottom}");

        // 幅より大きく切ろうとしても壊れないこと
        var small = Rect.FromSize(0f, 0f, 10f, 10f);
        var cut = small.CutRight(999f, out var rest);

        Expect(cut.Width == 10f, $"切りすぎたときに幅が壊れている: {cut}");
        Expect(rest.Width == 0f, $"切りすぎたときの残りが壊れている: {rest}");
    }

    private static void CheckRectBasics()
    {
        var rect = Rect.FromSize(10f, 10f, 100f, 50f);

        Expect(rect.Contains(new Vector2(10f, 10f)), "左上の点が含まれていない");
        Expect(!rect.Contains(new Vector2(110f, 60f)), "右下端が含まれてしまっている");
        Expect(rect.Center == new Vector2(60f, 35f), "中心がずれている");

        var other = Rect.FromSize(50f, 30f, 100f, 50f);
        Expect(rect.Overlaps(other), "重なりを検出できていない");
        Expect(!rect.Overlaps(Rect.FromSize(500f, 500f, 10f, 10f)), "離れた矩形が重なっている");

        var intersect = rect.Intersect(other);
        Expect(intersect.Min == new Vector2(50f, 30f), $"共通部分の左上がずれている: {intersect}");
        Expect(intersect.Max == new Vector2(110f, 60f), $"共通部分の右下がずれている: {intersect}");

        var far = rect.Intersect(Rect.FromSize(500f, 500f, 10f, 10f));
        Expect(far.IsEmpty, "重ならない矩形の共通部分が空でない");

        var shrunk = rect.Shrink(new EdgeInsets(5f, 4f, 3f, 2f));
        Expect(shrunk.Min == new Vector2(15f, 14f) && shrunk.Max == new Vector2(107f, 58f),
            $"余白の適用がずれている: {shrunk}");

        var placed = rect.Place(new Vector2(20f, 10f), Align.Center, Align.Center);
        Expect(placed.Center == rect.Center, $"中央寄せがずれている: {placed}");

        var stretched = rect.Place(new Vector2(20f, 10f), Align.Stretch, Align.Start);
        Expect(stretched.Width == rect.Width, "Stretch で幅が広がっていない");
    }

    private static void CheckIds()
    {
        // 表示文字列とは別に、ID はラベル全体から作られる
        var a = EuId.FromLabel("保存##left", 0UL, out var displayA);
        var b = EuId.FromLabel("保存##right", 0UL, out var displayB);

        Expect(displayA.SequenceEqual("保存"), "## の前だけが表示されていない");
        Expect(displayB.SequenceEqual("保存"), "## の前だけが表示されていない");
        Expect(a != b, "## で区別できていない");

        // ### は後半だけを ID にするので、表示が変わっても ID は変わらない
        var c = EuId.FromLabel("残り 10 秒###timer", 0UL, out var displayC);
        var d = EuId.FromLabel("残り 3 秒###timer", 0UL, out _);

        Expect(displayC.SequenceEqual("残り 10 秒"), "### の前だけが表示されていない");
        Expect(c == d, "### を使っても ID が変わってしまっている");

        // 親が違えば同じラベルでも別の ID になる
        var seeded = EuId.From("保存", EuId.Hash("windowA"));
        var seeded2 = EuId.From("保存", EuId.Hash("windowB"));

        Expect(seeded != seeded2, "親が違っても同じ ID になっている");
        Expect(!EuId.From("保存").IsNone, "ID が無効値になっている");
    }

    private static void CheckColors()
    {
        var color = EuColor.Rgba(0.2f, 0.4f, 0.6f, 0.8f);
        var vector = EuColor.ToVector(color);

        Expect(MathF.Abs(vector.X - 0.2f) < 0.01f, $"R が往復で変わっている: {vector.X}");
        Expect(MathF.Abs(vector.W - 0.8f) < 0.01f, $"A が往復で変わっている: {vector.W}");

        Expect(EuColor.AlphaOf(EuColor.WithAlpha(color, 0.5f)) is > 0.49f and < 0.51f,
            "アルファの差し替えがずれている");

        var lerped = EuColor.Lerp(EuColor.Black, EuColor.White, 0.5f);
        var mid = EuColor.ToVector(lerped);
        Expect(MathF.Abs(mid.X - 0.5f) < 0.02f, $"補間の中点がずれている: {mid.X}");

        var (h, s, v) = EuColor.ToHsv(EuColor.Rgb(0xFF0000));
        Expect(h < 0.01f && s > 0.99f && v > 0.99f, $"HSV 変換がずれている: {h}, {s}, {v}");

        var back = EuColor.FromHsv(h, s, v);
        Expect(back == EuColor.Rgb(0xFF0000), "HSV の往復で色が変わっている");

        Expect(EuColor.ReadableOn(EuColor.White) == EuColor.Black, "明るい地に白文字を選んでいる");
        Expect(EuColor.ReadableOn(EuColor.Black) == EuColor.White, "暗い地に黒文字を選んでいる");
    }

    private static void CheckSizeSpecs()
    {
        Expect(SizeSpec.Px(120f).Resolve(500f) == 120f, "固定幅が解決できていない");
        Expect(SizeSpec.Fill.Resolve(500f) == 500f, "Fill が残り幅になっていない");
        Expect(SizeSpec.Ratio(0.25f).Resolve(400f) == 100f, "比率指定がずれている");
        Expect(SizeSpec.Auto.Resolve(400f, 33f) == 33f, "Auto が内容サイズを使っていない");

        // float からの暗黙変換
        SizeSpec implicitSpec = 42f;
        Expect(implicitSpec.Mode == SizeMode.Fixed && implicitSpec.Value == 42f,
            "float からの暗黙変換が固定幅になっていない");
    }

    private static void CheckEasingAndAnim()
    {
        foreach (EaseKind kind in Enum.GetValues<EaseKind>())
        {
            Expect(MathF.Abs(Easing.Apply(kind, 0f)) < 0.001f, $"{kind} が 0 から始まっていない");
            Expect(MathF.Abs(Easing.Apply(kind, 1f) - 1f) < 0.001f, $"{kind} が 1 で終わっていない");
        }

        // 追従はフレームレートに依存しないこと (刻み幅を変えても到達点がほぼ同じ)
        var coarse = 0f;
        for (var i = 0; i < 10; i++)
            coarse = Anim.Approach(coarse, 1f, 10f, 1f / 10f);

        var fine = 0f;
        for (var i = 0; i < 100; i++)
            fine = Anim.Approach(fine, 1f, 10f, 1f / 100f);

        Expect(MathF.Abs(coarse - fine) < 0.05f,
            $"追従がフレームレートに依存している: {coarse} と {fine}");

        Expect(Anim.InverseLerp(0f, 0f, 5f) == 0f, "ゼロ幅の正規化でゼロ除算している");
        Expect(Anim.PingPong(0.5f, 1f) is > 0.99f and <= 1f, "往復の頂点がずれている");
    }

    /// <summary>
    /// 「確保した幅」と「実際に配置する位置」が食い違っていないかの確認。
    /// </summary>
    /// <remarks>
    /// スライダーは バー / 値 / ラベル を横に並べる。行全体の幅を求めるときと、
    /// ラベルを置く位置を決めるときとで別の余白を使ってしまい、
    /// ラベルの末尾が省略される不具合を起こした。
    /// 数値としての取り違えなので、ここで押さえておく。
    /// </remarks>
    private static void CheckLabelRoom()
    {
        const float BarWidth = 220f;
        const float ValueWidth = 58f;
        const float GapToValue = 8f;    // SpacingMd
        const float GapToLabel = 12f;   // SpacingLg
        const float LabelWidth = 140f;

        // 行の確保に使う幅
        var valueSpace = ValueWidth + GapToValue;
        var labelSpace = LabelWidth + GapToLabel;
        var rowWidth = BarWidth + valueSpace + labelSpace;

        // 実際にラベルを置く位置
        var valueRight = BarWidth + GapToValue + ValueWidth;
        var labelLeft = valueRight + GapToLabel;
        var roomForLabel = rowWidth - labelLeft;

        Expect(roomForLabel >= LabelWidth,
            $"ラベルの幅が足りない (確保 {roomForLabel} / 必要 {LabelWidth})");

        // 列の宣言によって、希望より狭い幅しか確保できなかった場合。
        // 希望のままバーを描くと行からはみ出し、値がスクロールバーへ重なる
        const float Granted = 300f;
        var barInNarrowRow = MathF.Max(40f, MathF.Min(BarWidth, Granted - valueSpace - labelSpace));

        Expect(barInNarrowRow + valueSpace + labelSpace <= Granted + 0.01f,
            $"行からはみ出している (バー {barInNarrowRow} + 値 {valueSpace} + ラベル {labelSpace} > {Granted})");
    }

    /// <summary>
    /// 画面端への吸着が、ドラッグの邪魔をしないかの確認。
    /// </summary>
    /// <remarks>
    /// 吸着後の位置へマウスの移動量を積むと、吸着の範囲内で動かしても毎フレーム
    /// 端へ戻され、ウィンドウが貼り付いて動かせなくなる。
    /// 移動量は「吸着していない位置」へ積み、吸着は見た目にだけ効かせる。
    /// </remarks>
    private static void CheckEdgeSnap()
    {
        const float SnapDistance = 8f;
        const float ScreenLeft = 0f;

        // 端に吸い付いた状態から、少しずつ右へ動かしていく
        var logical = ScreenLeft;
        var shown = Snap(logical);

        Expect(shown == ScreenLeft, "端に置いたのに吸着していない");

        // 吸着の範囲内 (3px ずつ) でも、本来の位置は進み続ける
        for (var i = 0; i < 3; i++)
            logical += 3f;

        Expect(logical == 9f, $"移動量が失われている: {logical}");

        shown = Snap(logical);
        Expect(shown == 9f, $"吸着の範囲を出たのに端へ戻されている: {shown}");

        // 範囲内ならまだ吸着したまま
        Expect(Snap(ScreenLeft + 5f) == ScreenLeft, "近いのに吸着していない");

        static float Snap(float x)
            => MathF.Abs(x - ScreenLeft) < SnapDistance ? ScreenLeft : x;
    }

    /// <summary>
    /// 横並びでの縦方向の揃えの確認。
    /// </summary>
    /// <remarks>
    /// 高さの違う要素を上端で揃えると、背の低い文字だけが浮いて見える。
    /// 行の基準高さに対して、中央・下端へ寄せられること。
    /// </remarks>
    private static void CheckCrossAlign()
    {
        const float RowHeight = 24f;   // ボタンの高さ
        const float TextHeight = 16f;  // 文字の高さ

        Expect(Offset(Align.Start) == 0f, "上端揃えがずれている");
        Expect(Offset(Align.Center) == 4f, $"中央揃えがずれている: {Offset(Align.Center)}");
        Expect(Offset(Align.End) == 8f, $"下端揃えがずれている: {Offset(Align.End)}");

        // 行より背の高い要素が来たら、行のほうが広がる
        const float TallItem = 40f;
        var baseline = MathF.Max(RowHeight, TallItem);

        Expect(baseline == TallItem, "背の高い要素で行が広がっていない");

        static float Offset(Align align)
        {
            var baseline = MathF.Max(RowHeight, TextHeight);

            return align switch
            {
                Align.Center => (baseline - TextHeight) * 0.5f,
                Align.End => baseline - TextHeight,
                _ => 0f,
            };
        }
    }

    /// <summary>
    /// 列幅の配分の検証。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「列の間の隙間を引き忘れる」という同じ間違いを繰り返し起こしている
    /// (数値入力の増減ボタンと、確認ダイアログのボタン行がどちらもこれだった)。
    /// はみ出しは必ず右端の要素が枠を突き抜ける形で表に出るので、
    /// 合計が行幅を超えないことを機械的に確かめる。
    /// </para>
    /// </remarks>
    private static void CheckColumnLayout()
    {
        const float Spacing = 8f;

        // 隙間は列数 - 1 個
        Expect(ColumnLayout.SpacingTotal(1, Spacing) == 0f, "1 列に隙間が入っている");
        Expect(ColumnLayout.SpacingTotal(3, Spacing) == 16f, "3 列の隙間が 2 つ分になっていない");

        // 数値入力と同じ形。入力欄 + ボタン 2 つが、ちょうど収まること
        {
            const float Total = 300f;
            const float Button = 24f;
            var field = Total - (Button * 2f) - ColumnLayout.SpacingTotal(3, Spacing);

            Span<float> widths = stackalloc float[3];
            ColumnLayout.Resolve(
                [SizeSpec.Px(field), SizeSpec.Px(Button), SizeSpec.Px(Button)],
                Total, Spacing, widths);

            ExpectFits(widths, Total, Spacing, "数値入力の列が行に収まっていない");
            Expect(widths[2] == Button, $"増減ボタンが縮んでいる: {widths[2]}");
        }

        // 幅が足りない場合は、はみ出す代わりに比例で縮む
        {
            const float Total = 100f;

            Span<float> widths = stackalloc float[3];
            ColumnLayout.Resolve(
                [SizeSpec.Px(200f), SizeSpec.Px(24f), SizeSpec.Px(24f)],
                Total, Spacing, widths);

            ExpectFits(widths, Total, Spacing, "入りきらない列が縮められていない");
        }

        // Fill は隙間を除いた残りを受け取る
        {
            const float Total = 200f;

            Span<float> widths = stackalloc float[2];
            ColumnLayout.Resolve([SizeSpec.Px(60f), SizeSpec.Fill], Total, Spacing, widths);

            Expect(widths[1] == Total - 60f - Spacing, $"Fill が隙間を勘定していない: {widths[1]}");
            ExpectFits(widths, Total, Spacing, "Fill を含む列が行に収まっていない");
        }

        // Fill どうしは重みで分け合う
        {
            const float Total = 208f;

            Span<float> widths = stackalloc float[3];
            ColumnLayout.Resolve([SizeSpec.Fill, SizeSpec.Fill, SizeSpec.Fill], Total, Spacing, widths);

            Expect(
                MathF.Abs(widths[0] - 64f) < 0.01f,
                $"均等割りがずれている: {widths[0]}");

            ExpectFits(widths, Total, Spacing, "均等割りが行に収まっていない");
        }

        // 内容に合わせる列は、渡された実測幅を使う
        {
            const float Total = 300f;

            Span<float> widths = stackalloc float[2];
            ColumnLayout.Resolve(
                [SizeSpec.Auto, SizeSpec.Fill], Total, Spacing, widths, [50f]);

            Expect(widths[0] == 50f, $"Auto 列が実測幅になっていない: {widths[0]}");
            Expect(
                MathF.Abs(widths[1] - (Total - 50f - Spacing)) < 0.01f,
                $"Auto 列の残りが Fill へ渡っていない: {widths[1]}");

            ExpectFits(widths, Total, Spacing, "Auto 列を含む行が収まっていない");
        }

        // 実測幅を渡さなければ 0 幅のまま (従来どおり)
        {
            Span<float> widths = stackalloc float[2];
            ColumnLayout.Resolve([SizeSpec.Auto, SizeSpec.Fill], 300f, Spacing, widths);

            Expect(widths[0] == 0f, "実測が無いのに Auto 列へ幅が入っている");
        }

        // 比率の合計が 1 を超えても、行の外へは出ない
        {
            const float Total = 300f;

            Span<float> widths = stackalloc float[2];
            ColumnLayout.Resolve([SizeSpec.Ratio(0.8f), SizeSpec.Ratio(0.8f)], Total, Spacing, widths);

            ExpectFits(widths, Total, Spacing, "比率の合計が 1 を超えたときに収まっていない");
        }
    }

    /// <summary>列幅の合計が、隙間を含めて行幅に収まっているか。</summary>
    private static void ExpectFits(ReadOnlySpan<float> widths, float total, float spacing, string message)
    {
        var sum = ColumnLayout.SpacingTotal(widths.Length, spacing);

        foreach (var width in widths)
            sum += width;

        // 丸め差は許す。1px を超えてはみ出したら失敗
        Expect(sum <= total + 1f, $"{message}: 合計 {sum} > 行幅 {total}");
    }

    /// <summary>
    /// タブの折り返しの検証。
    /// </summary>
    /// <remarks>
    /// 以前は行に入り切らないタブを捨てていた。タブがそもそも無いように見えるため、
    /// 枚数が増えたときに気づけない。行数の数え方だけを取り出して確かめる。
    /// </remarks>
    private static void CheckTabWrapping()
    {
        const float Gap = 2f;

        // 幅 100 のタブを 9 枚、行幅 320 に並べる
        Span<float> widths = stackalloc float[9];
        widths.Fill(100f);

        // 実装をそのまま呼ぶ。計算を写すと、実装が変わっても検証が空振りする
        Expect(EUi.CountRows(widths, 320f, Gap) == 3, "9 枚が 3 行に折り返されていない");
        Expect(EUi.CountRows(widths, 1000f, Gap) == 1, "収まるのに折り返している");

        // 1 枚で行幅を超える場合でも、行数が増え続けないこと
        Span<float> wide = stackalloc float[3];
        wide.Fill(500f);

        Expect(EUi.CountRows(wide, 100f, Gap) == 3, "1 枚ずつ 3 行にならない");
    }

    /// <summary>
    /// 内容に合わせる列が、測った幅を次のフレームへ持ち越すことの検証。
    /// </summary>
    /// <remarks>
    /// 以前は頭打ちの後の幅を実測として記録していたため、
    /// 「実測 ≤ 列幅 ≤ 前の実測」となって値が増えることができず、
    /// 初回の 0 幅から何フレーム経っても広がらなかった。
    /// ColumnLayout へ値を直接渡す検証だけでは通ってしまうので、
    /// LayoutScope を通した持ち越しをそのまま回す。
    /// </remarks>
    private static void CheckAutoColumnCarryOver()
    {
        // ImGui へのカーソル同期だけを止める。配分と実測の記録はそのまま動かす
        LayoutScope.SuppressImGuiSync = true;

        try
        {
            var scope = new LayoutScope();
            var bounds = Rect.FromSize(0f, 0f, 400f, 24f);
            var carried = new float[8];

            // 1 フレーム分を回す。幅 width の部品を Auto 列へ、残りを Fill 列へ置く
            float Frame(float width)
            {
                scope.Reset(
                    LayoutKind.Horizontal, bounds, new Vector2(8f, 0f),
                    [SizeSpec.Auto, SizeSpec.Fill], false, default, Align.Center, 24f);

                scope.SetAutoWidths(carried);

                var autoRect = scope.Allocate(SizeSpec.Px(width), 16f);
                scope.Allocate(SizeSpec.Fill, 24f);

                scope.MeasuredColumnWidths.CopyTo(carried);

                return autoRect.Width;
            }

            // 初回は持ち越しが無いので 0 幅。ここは以前と同じ
            Expect(Frame(80f) == 0f, "初回の Auto 列が 0 幅になっていない");

            // 2 フレーム目には測った幅が届く
            Expect(
                MathF.Abs(Frame(80f) - 80f) < 0.01f,
                $"2 フレーム目の Auto 列が内容幅になっていない: {Frame(80f)}");

            // 内容が広がれば追従する
            Frame(120f);
            Expect(MathF.Abs(Frame(120f) - 120f) < 0.01f, "Auto 列が広がらない");

            // 内容が縮んでも追従する
            Frame(50f);
            Expect(MathF.Abs(Frame(50f) - 50f) < 0.01f, "Auto 列が縮まない");

            // 行に入りきらない内容は、はみ出す代わりに縮む
            Frame(900f);
            var huge = Frame(900f);

            Expect(huge <= 400f + 0.01f, $"Auto 列が行からはみ出している: {huge}");
            Expect(huge > 0f, "入りきらない Auto 列が消えている");

            // プールから使い回しても、前の行の実測を引き継がない
            scope.Reset(
                LayoutKind.Horizontal, bounds, new Vector2(8f, 0f),
                [SizeSpec.Auto, SizeSpec.Fill], false, default, Align.Center, 24f);

            var fresh = scope.Allocate(SizeSpec.Px(80f), 16f);

            Expect(fresh.Width == 0f, "使い回したスコープが前の行の幅を引き継いでいる");
        }
        finally
        {
            LayoutScope.SuppressImGuiSync = false;
        }
    }

    /// <summary>
    /// 中身に合わせる窓が使う計測値の検証。
    /// </summary>
    /// <remarks>
    /// 以前は余白を含む <c>ConsumedSize</c> を測った値として使い、
    /// そこへもう一度余白を足していた。余白 1 つ分だけ窓が大きくなり、
    /// 横幅いっぱいを取る部品があると毎フレーム広がって上限まで止まらなかった。
    /// </remarks>
    private static void CheckAutoSizeMeasure()
    {
        LayoutScope.SuppressImGuiSync = true;

        try
        {
            var scope = new LayoutScope();
            var padding = EdgeInsets.All(14f);
            var bounds = Rect.FromSize(0f, 0f, 400f, 300f);

            // 余白つきの縦積みへ、幅 60 の部品を 1 つ置く
            scope.Reset(LayoutKind.Vertical, bounds, new Vector2(0f, 4f), default, false, padding);
            scope.Allocate(SizeSpec.Px(60f), 16f);

            Expect(
                MathF.Abs(scope.ContentSize.X - 60f) < 0.01f,
                $"中身の幅に余白が入っている: {scope.ContentSize.X}");

            Expect(
                MathF.Abs(scope.ConsumedSize.X - (60f + padding.TotalHorizontal)) < 0.01f,
                "消費した幅に余白が入っていない");

            Expect(
                MathF.Abs(scope.RequestedContentWidth - 60f) < 0.01f,
                "希望した幅が内容幅になっていない");

            // 横幅いっぱいを取る部品は希望幅に数えない。
            // 数えると「窓が広がる → 希望も広がる」で止まらなくなる
            scope.Reset(LayoutKind.Vertical, bounds, new Vector2(0f, 4f), default, false, padding);
            scope.Allocate(SizeSpec.Fill, 2f);

            Expect(
                scope.RequestedContentWidth == 0f,
                $"残り幅の指定を希望幅に数えている: {scope.RequestedContentWidth}");

            // 窓の幅より長い部品は、描く幅は切られても希望幅には本来の幅が残る
            scope.Reset(LayoutKind.Vertical, bounds, new Vector2(0f, 4f), default, false, padding);

            var clipped = scope.Allocate(SizeSpec.Px(900f), 16f);

            Expect(clipped.Width <= bounds.Width + 0.01f, "描く幅が領域からはみ出している");
            Expect(
                MathF.Abs(scope.RequestedContentWidth - 900f) < 0.01f,
                $"切られた部品の本来の幅が残っていない: {scope.RequestedContentWidth}");
        }
        finally
        {
            LayoutScope.SuppressImGuiSync = false;
        }
    }

    /// <summary>
    /// 1 色だけ差し替えて積んだときの、寸法と拡大率の検証。
    /// </summary>
    /// <remarks>
    /// 以前は拡大後の寸法を「拡大前の基準値」として渡していたため、
    /// スコープの中だけ寸法が二重に拡大されていた (拡大率 1.5 で高さが 36 ではなく 54)。
    /// 画面でしか気づけないので、ここで確かめる。
    /// </remarks>
    private static void CheckThemeOverlay()
    {
        var theme = XivNativeTheme.Create();
        theme.Scale = 1.5f;
        ThemeManager.SetDefault(theme);

        var outsideHeight = ThemeManager.Current.Metrics.WidgetHeight;
        var outsideScale = ThemeManager.Current.Scale;

        using (ThemeManager.PushColor(ThemeColor.TextHeading, 0xFF0000FFu))
        {
            Expect(
                MathF.Abs(ThemeManager.Current.Metrics.WidgetHeight - outsideHeight) < 0.01f,
                $"色を差し替えた中で寸法が変わっている: {ThemeManager.Current.Metrics.WidgetHeight} != {outsideHeight}");

            Expect(
                MathF.Abs(ThemeManager.Current.Scale - outsideScale) < 0.001f,
                "色を差し替えた中で拡大率が変わっている");

            Expect(
                ThemeManager.Current.Colors[ThemeColor.TextHeading] == 0xFF0000FFu,
                "差し替えた色が効いていない");
        }

        Expect(
            MathF.Abs(ThemeManager.Current.Metrics.WidgetHeight - outsideHeight) < 0.01f,
            "抜けたあとに寸法が戻っていない");

        // 拡大率を下げても、スコープの中が追従すること
        theme.Scale = 1f;

        var lowered = ThemeManager.Current.Metrics.WidgetHeight;

        using (ThemeManager.PushColor(ThemeColor.TextHeading, 0xFF00FF00u))
        {
            Expect(
                MathF.Abs(ThemeManager.Current.Metrics.WidgetHeight - lowered) < 0.01f,
                "拡大率を下げたのにスコープの中が追従していない");

            Expect(
                MathF.Abs(ThemeManager.Current.Scale - 1f) < 0.001f,
                "スコープの中の拡大率が古いまま残っている");
        }

        // 同じ深さを 2 回目以降に使うときは確保が起きないこと
        using (ThemeManager.PushColor(ThemeColor.Text, 0xFFFFFFFFu))
        {
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 16; i++)
        {
            using (ThemeManager.PushColor(ThemeColor.Text, 0xFFFFFFFFu))
            {
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Expect(allocated == 0L, $"色の差し替えで {allocated} バイト確保している");
    }

    /// <summary>
    /// 列の下限幅の検証。
    /// </summary>
    /// <remarks>
    /// 固定幅の列の合計が行の幅を超えると Fill の列が極端に細くなり、
    /// 中身が隣の列へはみ出す原因になる。下限を付けた列が確保されること、
    /// そのうえで合計が行の幅を超えないことを確かめる。
    /// </remarks>
    private static void CheckColumnMinimums()
    {
        const float Spacing = 8f;

        // 固定幅 3 列 + Fill 1 列。固定幅だけでほぼ埋まっている
        {
            Span<float> widths = stackalloc float[4];

            ColumnLayout.Resolve(
                [160f, 160f, 160f, SizeSpec.Fill.AtLeast(200f)], 600f, Spacing, widths);

            Expect(widths[3] >= 199.5f, "下限を付けた Fill の列が確保されていない");
            ExpectFits(widths, 600f, Spacing, "下限を確保したら行からはみ出した");

            // 代わりに固定幅の列が縮む
            Expect(widths[0] < 160f, "下限のために固定幅の列が縮んでいない");
        }

        // 余裕があるときは何も起きない
        {
            Span<float> widths = stackalloc float[2];
            ColumnLayout.Resolve([100f, SizeSpec.Fill.AtLeast(100f)], 600f, Spacing, widths);

            Expect(MathF.Abs(widths[0] - 100f) < 0.01f, "余裕があるのに固定幅が縮んでいる");
            Expect(widths[1] > 400f, "余った幅が Fill へ配られていない");
        }

        // 下限の合計が行の幅を超える場合も、はみ出さない
        {
            Span<float> widths = stackalloc float[3];

            ColumnLayout.Resolve(
                [SizeSpec.Fill.AtLeast(300f), SizeSpec.Fill.AtLeast(300f),
                 SizeSpec.Fill.AtLeast(300f)],
                400f, Spacing, widths);

            ExpectFits(widths, 400f, Spacing, "下限が入りきらないのにはみ出している");
        }

        // 固定幅の列にも下限が効く。下限の無い列から取る
        {
            Span<float> widths = stackalloc float[2];

            ColumnLayout.Resolve(
                [SizeSpec.Px(80f).AtLeast(120f), SizeSpec.Fill], 300f, Spacing, widths);

            Expect(widths[0] >= 119.5f, "固定幅の列の下限が効いていない");
            ExpectFits(widths, 300f, Spacing, "固定幅の下限で行からはみ出した");
        }
    }

    /// <summary>
    /// ドロップダウンの置き場所の検証。
    /// </summary>
    /// <remarks>
    /// 欄の真下に置くだけだと、画面の下のほうで高い一覧を開いたときに画面の外へ出る。
    /// 「読めない」形で表に出るうえ、実機でしか気づけないので機械的に確かめる。
    /// </remarks>
    private static void CheckDropdownPlacement()
    {
        var min = new Vector2(0f, 0f);
        var max = new Vector2(1920f, 1080f);

        // 画面の真ん中にある欄。下に入るのでそのまま下へ
        {
            var field = Rect.FromSize(100f, 400f, 200f, 24f);
            var size = new Vector2(200f, 300f);
            var pos = PopupPlacement.Dropdown(field, ref size, min, max);

            Expect(pos.Y > field.Max.Y, "下に入るのに下へ開いていない");
            Expect(size.Y == 300f, "下に入るのに高さが縮んでいる");
            Expect(pos.Y + size.Y <= max.Y + 0.01f, "下へ開いて画面の外へ出ている");
        }

        // 画面の下にある欄。下に入らないので上へ
        {
            var field = Rect.FromSize(100f, 1000f, 200f, 24f);
            var size = new Vector2(200f, 520f);
            var pos = PopupPlacement.Dropdown(field, ref size, min, max);

            Expect(size.Y == 520f, "上に入るのに高さが縮んでいる");
            Expect(pos.Y + size.Y <= field.Min.Y, "欄の上へ開いていない");
            Expect(pos.Y >= min.Y - 0.01f, "上へ開いて画面の外へ出ている");
        }

        // 上下どちらにも入らない。はみ出す代わりに縮める
        {
            var field = Rect.FromSize(100f, 500f, 200f, 24f);
            var size = new Vector2(200f, 900f);
            var pos = PopupPlacement.Dropdown(field, ref size, min, max);

            Expect(size.Y < 900f, "入りきらないのに縮んでいない");
            Expect(pos.Y >= min.Y - 0.01f, "縮めたのに上へはみ出している");
            Expect(pos.Y + size.Y <= max.Y + 0.01f, "縮めたのに下へはみ出している");
        }

        // 右端にある幅の広い一覧。左へずらして収める
        {
            var field = Rect.FromSize(1850f, 100f, 60f, 24f);
            var size = new Vector2(400f, 200f);
            var pos = PopupPlacement.Dropdown(field, ref size, min, max);

            Expect(pos.X + size.X <= max.X + 0.01f, "右へはみ出している");
            Expect(pos.X >= min.X - 0.01f, "左へはみ出している");
        }

        // 作業領域の原点が 0 でない場合も、その中へ収める
        {
            var offsetMin = new Vector2(200f, 150f);
            var offsetMax = new Vector2(1000f, 700f);
            var field = Rect.FromSize(900f, 650f, 80f, 24f);
            var size = new Vector2(300f, 400f);
            var pos = PopupPlacement.Dropdown(field, ref size, offsetMin, offsetMax);

            Expect(pos.X >= offsetMin.X - 0.01f, "作業領域の左より外に置いている");
            Expect(pos.X + size.X <= offsetMax.X + 0.01f, "作業領域の右より外に置いている");
            Expect(pos.Y >= offsetMin.Y - 0.01f, "作業領域の上より外に置いている");
            Expect(pos.Y + size.Y <= offsetMax.Y + 0.01f, "作業領域の下より外に置いている");
        }

        // 高さの下限は守る。室が無くても潰さない
        {
            var field = Rect.FromSize(100f, 1070f, 200f, 10f);
            var size = new Vector2(200f, 300f);
            PopupPlacement.Dropdown(field, ref size, min, max, minHeight: 28f);

            Expect(size.Y >= 28f, "下限を下回って潰れている");
        }
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            Failures.Add(message);
    }
}
