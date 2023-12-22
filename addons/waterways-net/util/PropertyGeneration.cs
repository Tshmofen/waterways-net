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

    public static Dictionary CreateCustomProperty(PropertyUsageFlags usage, string name, Variant.Type type, PropertyHint? hint = null, string hintString = null)
    {
        var propertyInfo = new Dictionary
        {
            { Name, name },
            { Type, (int) type },
            { Usage, (int) usage }
        };

        if (hint != null)
        {
            propertyInfo.Add(Hint, (int)hint);
        }

        if (hintString != null)
        {
            propertyInfo.Add(HintString, hintString);
        }

        return propertyInfo;
    }

    public static Dictionary CreateProperty(string name, Variant.Type type, PropertyHint? hint = null, string hintString = null)
    {
        const PropertyUsageFlags usage = PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable;
        return CreateCustomProperty(usage, name, type, hint, hintString);
    }

    public static Dictionary CreateGroupingProperty(string groupName, string hintString = null)
    {
        const PropertyUsageFlags usage = PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable;
        return CreateCustomProperty(usage, groupName, Variant.Type.Nil, hintString: hintString);
    }

    public static Dictionary CreateStorageProperty(string name, Variant.Type type, PropertyHint? hint = null, string hintString = null)
    {
        return CreateCustomProperty(PropertyUsageFlags.Storage, name, type, hint, hintString);
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