using System;
using System.Collections.Generic;
using System.Numerics;

using EstellUtils.UI.Core;
using EstellUtils.UI.Layout;
using EstellUtils.UI.Render;

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

    private static void Expect(bool condition, string message)
    {
        if (!condition)
            Failures.Add(message);
    }
}
