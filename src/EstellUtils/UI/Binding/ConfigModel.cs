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

        foreach (var member in EnumerateMembers())
        {
            try
            {
                var binding = Build(member, defaults);
                if (binding is not null)
                    bindings.Add(binding);
            }
            catch (Exception ex)
            {
                UiLog.Error($"設定項目「{member.Name}」の解析に失敗しました。", ex);
            }
        }

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

    /// <summary>公開フィールドと読み書きできる公開プロパティを列挙する。</summary>
    private static IEnumerable<MemberInfo> EnumerateMembers()
    {
        var type = typeof(T);

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
    private static FieldBinding<T>? Build(MemberInfo member, T? defaults)
    {
        if (member.GetCustomAttribute<EuHiddenAttribute>() is not null)
            return null;

        var valueType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;

        var name = member.Name;
        var widgetId = "##" + name;
        var label = member.GetCustomAttribute<EuLabelAttribute>()?.Label ?? Humanize(name);
        var tip = member.GetCustomAttribute<EuTipAttribute>()?.Tip;
        var group = member.GetCustomAttribute<EuGroupAttribute>()?.Group;
        var order = member.GetCustomAttribute<EuOrderAttribute>()?.Order ?? 0;
        var range = member.GetCustomAttribute<EuRangeAttribute>();
        var colorAttribute = member.GetCustomAttribute<EuColorAttribute>();

        var defaultValue = defaults is null ? null : GetMemberValue(member, defaults);

        if (valueType == typeof(bool))
        {
            var (getter, setter) = BuildAccessors<bool>(member);
            return new BoolBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                UseToggle = member.GetCustomAttribute<EuToggleAttribute>() is not null,
            };
        }

        if (valueType == typeof(int))
        {
            var (getter, setter) = BuildAccessors<int>(member);
            return new IntBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                Min = range is null ? 0 : (int)range.Min,
                Max = range is null ? 100 : (int)range.Max,
                Suffix = range?.Suffix,
            };
        }

        if (valueType == typeof(float))
        {
            var (getter, setter) = BuildAccessors<float>(member);
            return new FloatBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                Min = range?.Min ?? 0f,
                Max = range?.Max ?? 1f,
                Decimals = range?.Decimals ?? 2,
                Suffix = range?.Suffix,
            };
        }

        if (valueType == typeof(string))
        {
            var (getter, setter) = BuildAccessors<string>(member);
            return new StringBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
            };
        }

        if (valueType == typeof(uint))
        {
            if (colorAttribute is null)
                return null;

            var (getter, setter) = BuildAccessors<uint>(member);
            return new ColorPackedBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                ShowAlpha = colorAttribute.ShowAlpha,
            };
        }

        if (valueType == typeof(Vector4))
        {
            var (getter, setter) = BuildAccessors<Vector4>(member);
            return new ColorVectorBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                DefaultValue = defaultValue, Getter = getter, Setter = setter,
                ShowAlpha = colorAttribute?.ShowAlpha ?? true,
            };
        }

        if (valueType.IsEnum)
        {
            var (getter, setter) = BuildEnumAccessors(member, valueType);
            var values = Enum.GetValues(valueType).Cast<object>().Select(Convert.ToInt32).ToArray();
            var names = Enum.GetNames(valueType)
                .Select(n => valueType.GetField(n)?.GetCustomAttribute<EuEnumLabelAttribute>()?.Label ?? n)
                .ToArray();

            return new EnumBinding<T>
            {
                Name = name, WidgetId = widgetId, Label = label, Tip = tip, Group = group, Order = order,
                DefaultValue = defaultValue is null ? null : Convert.ToInt32(defaultValue),
                Getter = getter, Setter = setter, Names = names, Values = values,
            };
        }

        return null;
    }

    /// <summary>メンバーの現在値を取り出す (既定値の採取にのみ使う)。</summary>
    private static object? GetMemberValue(MemberInfo member, T instance)
        => member is FieldInfo field ? field.GetValue(instance) : ((PropertyInfo)member).GetValue(instance);

    /// <summary>型付きの読み書きデリゲートを式木から作る。</summary>
    private static (Func<T, TValue> Getter, Action<T, TValue> Setter) BuildAccessors<TValue>(MemberInfo member)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var access = Expression.MakeMemberAccess(target, member);

        var getter = Expression.Lambda<Func<T, TValue>>(access, target).Compile();

        var value = Expression.Parameter(typeof(TValue), "value");
        var setter = Expression.Lambda<Action<T, TValue>>(
            Expression.Assign(access, value), target, value).Compile();

        return (getter, setter);
    }

    /// <summary>列挙型を <c>int</c> として読み書きするデリゲートを作る。</summary>
    private static (Func<T, int> Getter, Action<T, int> Setter) BuildEnumAccessors(MemberInfo member, Type enumType)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var access = Expression.MakeMemberAccess(target, member);

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
