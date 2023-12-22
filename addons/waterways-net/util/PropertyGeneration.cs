using Godot;
using Godot.Collections;
using System;

namespace Waterways.Util;

public static class PropertyGeneration
{
    public const string Name = "name";
    public const string Type = "type";
    public const string Hint = "hintString";
    public const string HintString = "hint_string";
    public const string Usage = "usage";

    public static Dictionary CreateProperty(string name, Variant.Type type, PropertyHint hint, string hintString = null)
    {
        var propertyInfo = new Dictionary
        {
            { Name, name },
            { Type, (int) type },
            { Hint, (int) hint },
            { Usage, (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
        };

        if (hintString != null)
        {
            propertyInfo.Add(HintString, hintString);
        }

        return propertyInfo;
    }

    public static Dictionary CreateGroupingProperty(string groupName, string hintString)
    {
        return new Dictionary
        {
            { Name, groupName },
            { Type, (int) Variant.Type.Nil },
            { HintString, hintString },
            { Usage, (int)(PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable)}
        };
    }

    public static Dictionary CreateStorageProperty(string name, Variant.Type type, PropertyHint? hint = null, string hintString = null)
    {
        var property = new Dictionary
        {
            { Name, name },
            { Type, (int) type },
            { Usage, (int) PropertyUsageFlags.Storage}
        };

        if (hint != null)
        {
            property.Add(Hint, (int) hint);
        }

        if (hintString != null)
        {
            property.Add(HintString, hintString);
        }

        return property;
    }

    public static Dictionary CreatePropertyCopy(Dictionary target)
    {
        var copy = new Dictionary();

        foreach (var (key, value) in target)
        {
            copy[key] = value;
        }

        return copy;
    }

    public static string GetEnumHint<TEnum>() where TEnum : Enum
    {
        var enumValues = Enum.GetNames(typeof(TEnum));
        return string.Join(',', enumValues);
    }
}