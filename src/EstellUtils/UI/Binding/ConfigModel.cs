using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

using EstellUtils.Diagnostics;

namespace EstellUtils.UI.Binding;

/// <summary>
/// 設定クラスを解析して、項目ごとの表示方法を組み立てる。
/// </summary>
/// <typeparam name="T">設定クラスの型。</typeparam>
/// <remarks>
/// <para>
/// 解析は型ごとに 1 度だけ行われ、結果は静的にキャッシュされる。
/// 値の読み書きは式木からコンパイルしたデリゲートを使うので、
/// 毎フレームのリフレクション呼び出しやボクシングは起きない。
/// </para>
/// <para>
/// 対応する型は <c>bool</c> / <c>int</c> / <c>float</c> / <c>string</c> /
/// 列挙型 / <c>uint</c> (色) / <see cref="Vector4"/> (色)。
/// それ以外の型は無視される。
/// </para>
/// </remarks>
public static class ConfigModel<T>
    where T : class
{
    private static readonly Dictionary<string, FieldBinding<T>> ByName = new(StringComparer.Ordinal);

    static ConfigModel()
    {
        var bindings = new List<FieldBinding<T>>();
        var defaults = TryCreateDefault();

        Collect(typeof(T), new List<MemberInfo>(4), null, defaults, bindings, 0);

        Bindings = bindings
            .OrderBy(b => b.Order)
            .ThenBy(b => b.Name, StringComparer.Ordinal)
            .ToArray();

        foreach (var binding in Bindings)
            ByName[binding.Name] = binding;

        Groups = Bindings
            .Select(b => b.Group)
            .Where(g => !string.IsNullOrEmpty(g))
            .Distinct(StringComparer.Ordinal)
            .ToArray()!;
    }

    /// <summary>解析された項目。</summary>
    public static IReadOnlyList<FieldBinding<T>> Bindings { get; }

    /// <summary>項目に付けられていたグループ名の一覧 (出現順)。</summary>
    public static IReadOnlyList<string> Groups { get; }

    /// <summary>名前で項目を探す。</summary>
    public static FieldBinding<T>? Find(string name)
        => ByName.TryGetValue(name, out var binding) ? binding : null;

    /// <summary>入れ子を辿る深さの上限。循環していてもここで止まる。</summary>
    private const int MaxNestingDepth = 4;

    /// <summary>
    /// 型の項目を集める。<see cref="EuNestedAttribute"/> の付いたメンバーは中身まで辿る。
    /// </summary>
    private static void Collect(
        Type type, List<MemberInfo> path, string? group, T? defaults,
        List<FieldBinding<T>> bindings, int depth)
    {
        foreach (var member in EnumerateMembers(type))
        {
            if (member.GetCustomAttribute<EuHiddenAttribute>() is not null)
                continue;

            var nested = member.GetCustomAttribute<EuNestedAttribute>();

            if (nested is not null)
            {
                if (depth >= MaxNestingDepth)
                {
                    UiLog.Warning(
                        $"設定項目「{member.Name}」の入れ子が深すぎます ({MaxNestingDepth} 段まで)。");
                    continue;
                }

                var memberType = MemberTypeOf(member);

                path.Add(member);
                Collect(memberType, path, nested.Group ?? group, defaults, bindings, depth + 1);
                path.RemoveAt(path.Count - 1);

                continue;
            }

            try
            {
                path.Add(member);

                var binding = Build(path, group, defaults);
                if (binding is not null)
                    bindings.Add(binding);
            }
            catch (Exception ex)
            {
                UiLog.Error($"設定項目「{member.Name}」の解析に失敗しました。", ex);
            }
            finally
            {
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    /// <summary>メンバーの型を返す。</summary>
    private static Type MemberTypeOf(MemberInfo member)
        => member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;

    /// <summary>公開フィールドと読み書きできる公開プロパティを列挙する。</summary>
    private static IEnumerable<MemberInfo> EnumerateMembers(Type type)
    {

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!field.IsInitOnly && !field.IsLiteral)
                yield return field;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                yield return property;
        }
    }

    /// <summary>既定値を採るためのインスタンスを作る。作れなければ null。</summary>
    private static T? TryCreateDefault()
    {
        try
        {
            return Activator.CreateInstance<T>();
        }
        catch
        {
            // 引数なしコンストラクタが無い場合は既定値の比較・復帰を諦める
            return null;
        }
    }

    /// <summary>メンバーの型に応じた項目を作る。</summary>
    /// <param name="path">設定クラスの根から辿るメンバーの並び。入れ子の場合は 2 段以上になる。</param>
    /// <param name="inheritedGroup">入れ子の親から引き継いだグループ名。</param>
    /// <param name="defaults">既定値を採るためのインスタンス。</param>
    private static FieldBinding<T>? Build(
        IReadOnlyList<MemberInfo> path, string? inheritedGroup, T? defaults)
    {
        var member = path[^1];
        var valueType = MemberTypeOf(member);

        // 入れ子の項目は、親をたどった名前で区別する
        var name = BuildPathName(path);
        var widgetId = "##" + name;

        var label = member.GetCustomAttribute<EuLabelAttribute>()?.Label ?? Humanize(member.Name);
        var tip = member.GetCustomAttribute<EuTipAttribute>()?.Tip;
        var group = member.GetCustomAttribute<EuGroupAttribute>()?.Group ?? inheritedGroup;
        var order = member.GetCustomAttribute<EuOrderAttribute>()?.Order ?? 0;
        var range = member.GetCustomAttribute<EuRangeAttribute>();
        var colorAttribute = member.GetCustomAttribute<EuColorAttribute>();
        var vectorAttribute = member.GetCustomAttribute<EuVectorAttribute>();

        // 表示言語を実行時に切り替える場合に備えて、静的メンバーから引く経路も用意する
        var labelProvider = BuildTextProvider(member.GetCustomAttribute<EuLabelFromAttribute>());
        var tipProvider = BuildTextProvider(member.GetCustomAttribute<EuTipFromAttribute>());

        var defaultValue = defaults is null ? null : GetPathValue(path, defaults);

        if (valueType == typeof(bool))
        {
            var (getter, setter) = BuildAccessors<bool>(path);
            return new BoolBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                LabelProvider = labelProvider, TipProvider = tipProvider,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                UseToggle = member.GetCustomAttribute<EuToggleAttribute>() is not null,
            };
        }

        if (valueType == typeof(int))
        {
            var (getter, setter) = BuildAccessors<int>(path);
            return new IntBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                LabelProvider = labelProvider, TipProvider = tipProvider,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                Min = range is null ? 0 : (int)range.Min,
                Max = range is null ? 100 : (int)range.Max,
                Suffix = range?.Suffix,
            };
        }

        if (valueType == typeof(float))
        {
            var (getter, setter) = BuildAccessors<float>(path);
            return new FloatBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                LabelProvider = labelProvider, TipProvider = tipProvider,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                Min = range?.Min ?? 0f,
                Max = range?.Max ?? 1f,
                Decimals = range?.Decimals ?? 2,
                Suffix = range?.Suffix,
            };
        }

        if (valueType == typeof(string))
        {
            var (getter, setter) = BuildAccessors<string>(path);
            return new StringBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                LabelProvider = labelProvider, TipProvider = tipProvider,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
            };
        }

        if (valueType == typeof(uint))
        {
            if (colorAttribute is null)
                return null;

            var (getter, setter) = BuildAccessors<uint>(path);
            return new ColorPackedBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                LabelProvider = labelProvider, TipProvider = tipProvider,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                ShowAlpha = colorAttribute.ShowAlpha,
            };
        }

        if (valueType == typeof(Vector4))
        {
            var (getter, setter) = BuildAccessors<Vector4>(path);

            // 色として扱うのは [EuColor] を付けたときだけ。
            // 座標や余白の 4 つ組まで色ピッカーになってしまうため
            if (colorAttribute is not null)
            {
                return new ColorVectorBinding<T>
                {
                    Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                    LabelProvider = labelProvider, TipProvider = tipProvider,
                    DefaultValue = defaultValue, Getter = getter, Setter = setter,
                    ShowAlpha = colorAttribute.ShowAlpha,
                };
            }

            return new VectorBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                LabelProvider = labelProvider, TipProvider = tipProvider,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                Labels = vectorAttribute?.Labels ?? [],
                Step = vectorAttribute?.Step ?? 0f,
                Min = vectorAttribute is null || vectorAttribute.Min <= float.MinValue ? null : vectorAttribute.Min,
                Max = vectorAttribute is null || vectorAttribute.Max >= float.MaxValue ? null : vectorAttribute.Max,
            };
        }

        if (valueType.IsEnum)
        {
            var (getter, setter) = BuildEnumAccessors(path, valueType);
            var values = Enum.GetValues(valueType).Cast<object>().Select(Convert.ToInt32).ToArray();
            var names = Enum.GetNames(valueType)
                .Select(n => valueType.GetField(n)?.GetCustomAttribute<EuEnumLabelAttribute>()?.Label ?? n)
                .ToArray();

            return new EnumBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                LabelProvider = labelProvider, TipProvider = tipProvider,
                DefaultValue = defaultValue is null ? null : Convert.ToInt32(defaultValue),
                Getter = getter, Setter = setter, Names = names, Values = values,
            };
        }

        return null;
    }

    /// <summary>入れ子を辿った名前を作る。<c>MobHunt.Enabled</c> のようになる。</summary>
    private static string BuildPathName(IReadOnlyList<MemberInfo> path)
    {
        if (path.Count == 1)
            return path[0].Name;

        var parts = new string[path.Count];

        for (var i = 0; i < path.Count; i++)
            parts[i] = path[i].Name;

        return string.Join('.', parts);
    }

    /// <summary>パスを辿って現在値を取り出す (既定値の採取にのみ使う)。</summary>
    private static object? GetPathValue(IReadOnlyList<MemberInfo> path, object instance)
    {
        object? current = instance;

        foreach (var member in path)
        {
            if (current is null)
                return null;

            current = member is FieldInfo field
                ? field.GetValue(current)
                : ((PropertyInfo)member).GetValue(current);
        }

        return current;
    }

    /// <summary>
    /// 静的メンバーから文字列を引く関数を作る。属性が無ければ null。
    /// </summary>
    private static Func<string>? BuildTextProvider(Attribute? attribute)
    {
        var (type, memberName) = attribute switch
        {
            EuLabelFromAttribute label => (label.Type, label.Member),
            EuTipFromAttribute tip => (tip.Type, tip.Member),
            _ => (null, null),
        };

        if (type is null || memberName is null)
            return null;

        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        if (type.GetProperty(memberName, Flags) is { CanRead: true } property)
            return () => property.GetValue(null) as string ?? string.Empty;

        if (type.GetField(memberName, Flags) is { } field)
            return () => field.GetValue(null) as string ?? string.Empty;

        if (type.GetMethod(memberName, Flags, Type.EmptyTypes) is { } method)
            return () => method.Invoke(null, null) as string ?? string.Empty;

        UiLog.Warning($"型「{type.Name}」に静的メンバー「{memberName}」が見つかりません。");
        return null;
    }

    /// <summary>
    /// 型付きの読み書きデリゲートを式木から作る。
    /// </summary>
    /// <remarks>
    /// 入れ子の場合は <c>target.MobHunt.Enabled</c> のように辿る式になる。
    /// 途中のクラスは初期化されている前提なので、設定クラスでは <c>= new()</c> を付けておくこと。
    /// </remarks>
    private static (Func<T, TValue> Getter, Action<T, TValue> Setter) BuildAccessors<TValue>(
        IReadOnlyList<MemberInfo> path)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var access = BuildAccessExpression(target, path);

        var getter = Expression.Lambda<Func<T, TValue>>(access, target).Compile();

        var value = Expression.Parameter(typeof(TValue), "value");
        var setter = Expression.Lambda<Action<T, TValue>>(
            Expression.Assign(access, value), target, value).Compile();

        return (getter, setter);
    }

    /// <summary>パスを辿るメンバーアクセスの式を組み立てる。</summary>
    private static Expression BuildAccessExpression(Expression root, IReadOnlyList<MemberInfo> path)
    {
        var access = root;

        foreach (var member in path)
            access = Expression.MakeMemberAccess(access, member);

        return access;
    }

    /// <summary>列挙型を <c>int</c> として読み書きするデリゲートを作る。</summary>
    private static (Func<T, int> Getter, Action<T, int> Setter) BuildEnumAccessors(
        IReadOnlyList<MemberInfo> path, Type enumType)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var access = BuildAccessExpression(target, path);

        var getter = Expression.Lambda<Func<T, int>>(
            Expression.Convert(access, typeof(int)), target).Compile();

        var value = Expression.Parameter(typeof(int), "value");
        var setter = Expression.Lambda<Action<T, int>>(
            Expression.Assign(access, Expression.Convert(value, enumType)), target, value).Compile();

        return (getter, setter);
    }

    /// <summary>
    /// ラベル属性が無いときに、メンバー名から読みやすい見出しを作る。
    /// <c>updateInterval</c> → <c>Update Interval</c>。
    /// </summary>
    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        Span<char> buffer = stackalloc char[name.Length * 2];
        var written = 0;

        buffer[written++] = char.ToUpperInvariant(name[0]);

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                buffer[written++] = ' ';

            buffer[written++] = c;
        }

        return new string(buffer[..written]);
    }
}
