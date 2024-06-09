using Godot;
using System.Collections.Generic;
using System.Linq;
using Waterways.Util;
using Godot.Collections;

namespace Waterways.Data;

[Tool]
[GlobalClass]
public partial class RiverShaderSettings : Resource
{
    private int _selectedShader = (int)ShaderType.Water;
    private Array<Dictionary> _cachedPropertyList;

    public ShaderMaterial Material { get; set; } = new ShaderMaterial();

    #region Custom Properties

    // Material Properties that not handled in shader
    private ShaderType _matShaderType = ShaderType.Water;
    public ShaderType MatShaderType
    {
        get => _matShaderType;
        set
        {
            if (value == _matShaderType)
            {
                return;
            }

            _matShaderType = value;

            if (_matShaderType == ShaderType.Custom)
            {
                Material.Shader = MatCustomShader;
            }
            else
            {
                RiverShaderHelper.SetStandardMaterialShader(Material, _matShaderType);
            }

            NotifyPropertyListChanged();
        }
    }

    private Shader _matCustomShader;
    public Shader MatCustomShader
    {
        get => _matCustomShader;
        set
        {
            if (_matCustomShader == value)
            {
                return;
            }

            _matCustomShader = value;

            if (_matCustomShader != null)
            {
                Material.Shader = _matCustomShader;
            }

            MatShaderType = (value != null ? ShaderType.Custom : ShaderType.Water);
        }
    }

    private Texture _foamNoise;
    public Texture FoamNoise
    {
        get => _foamNoise;
        set
        {
            _foamNoise = value;
            Material.SetShaderParameter("i_texture_foam_noise", _foamNoise);
        }
    }

    private float _lodLod0Distance = 50;
    public float LodLod0Distance
    {
        get => _lodLod0Distance;
        set
        {
            _lodLod0Distance = value;
            Material.SetShaderParameter("i_lod0_distance", _lodLod0Distance);
        }
    }

    #endregion

    #region Property Util

    private Dictionary GetCachedProperty(StringName name)
    {
        _cachedPropertyList ??= _GetPropertyList();
        return _cachedPropertyList.FirstOrDefault(p => p[PropertyGenerator.Name].AsStringName() == name);
    }

    private static string GetPropertyName(Dictionary property)
    {
        return property[PropertyGenerator.Name].AsString();
    }

    private void SetCustomColorParameter(Color color, int index)
    {
        var albedo = Material.GetShaderParameter(MaterialConstant.CustomColorAlbedoProperty).AsProjection();
        albedo[index] = new Vector4(color.R, color.G, color.B, color.A);
        Material.SetShaderParameter(MaterialConstant.CustomColorAlbedoProperty, albedo);
    }

    private static Dictionary CreateShaderParameter(Dictionary param, Rid shaderRid)
    {
        var paramName = GetPropertyName(param);
        var newProperty = PropertyGenerator.CreatePropertyCopy(param);
        newProperty[PropertyGenerator.Name] = MaterialConstant.MaterialParamPrefix + paramName;

        if (paramName.Contains("curve"))
        {
            newProperty[PropertyGenerator.Hint] = (int)PropertyHint.ExpEasing;
            newProperty[PropertyGenerator.HintString] = "EASE";
        }

        var shaderDefault = RenderingServer.ShaderGetParameterDefault(shaderRid, paramName);
        if (shaderDefault.VariantType != Variant.Type.Nil)
        {
            newProperty[PropertyGenerator.Revert] = shaderDefault;
        }

        return newProperty;
    }

    private static Dictionary[] CreateCustomColorParameter()
    {
        var firstColor = PropertyGenerator.CreateProperty(MaterialConstant.CustomColorFirstProperty, Variant.Type.Color, revert: new Color(0.6f, 0.7f, 0.65f));
        var secondColor = PropertyGenerator.CreateProperty(MaterialConstant.CustomColorSecondProperty, Variant.Type.Color, revert: new Color(0.25f, 0.35f, 0.35f));
        return [firstColor, secondColor];
    }

    private List<Dictionary> AccumulateShaderParameters()
    {
        var parameters = new List<Dictionary>();

        if (Material.Shader == null)
        {
            return parameters;
        }

        var shaderRid = Material.Shader.GetRid();
        var shaderParameters = RenderingServer
            .GetShaderParameterList(shaderRid)
            .Where(p => !GetPropertyName(p).StartsWith("i_"))
            .ToList();

        foreach (var (key, value) in MaterialConstant.ShaderPrefixes)
        {
            var group = shaderParameters.Where(p => GetPropertyName(p).StartsWith(key)).ToList();

            if (group.Count == 0)
            {
                continue;
            }

            parameters.Add(PropertyGenerator.CreateGroupingProperty($"Material/{value}", MaterialConstant.MaterialParamPrefix + key));

            foreach (var param in group)
            {
                if (GetPropertyName(param) == MaterialConstant.CustomColorAlbedoProperty)
                {
                    parameters.AddRange(CreateCustomColorParameter());
                }
                else
                {
                    parameters.Add(CreateShaderParameter(param, shaderRid));
                }

                shaderParameters.Remove(param);
            }
        }

        parameters.Add(PropertyGenerator.CreateGroupingProperty("Material", MaterialConstant.MaterialParamPrefix));
        parameters.AddRange(shaderParameters.Select(param => CreateShaderParameter(param, shaderRid)));

        return parameters;
    }

    #endregion

    #region Properties Management

    public RiverShaderSettings()
    {
        Material ??= new ShaderMaterial();
        RiverShaderHelper.SetStandardMaterialShader(Material, _matShaderType);
    }

    public RiverShaderSettings(ShaderMaterial material) : base()
    {
        Material = material;
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        var resultProperties = new Array<Dictionary>
        {
            PropertyGenerator.CreateGroupingProperty( "Material", "Mat"),
            PropertyGenerator.CreateProperty(PropertyName.MatShaderType, Variant.Type.Int, PropertyHint.Enum, PropertyGenerator.GetEnumHint<ShaderType>(), (int) ShaderType.Water),
            PropertyGenerator.CreateProperty(PropertyName.MatCustomShader, Variant.Type.Object, PropertyHint.ResourceType, nameof(Shader), Variant.From((GodotObject)null)),
            PropertyGenerator.CreateProperty(PropertyName.FoamNoise, Variant.Type.Object, PropertyHint.ResourceType, nameof(Texture), Variant.From((GodotObject)null)),
            PropertyGenerator.CreateGroupingProperty( "Material", MaterialConstant.MaterialParamPrefix)
        };

        resultProperties.AddRange(AccumulateShaderParameters());
        resultProperties.Add(PropertyGenerator.CreateGroupingProperty("Lod", "Lod"));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.LodLod0Distance, Variant.Type.Float, PropertyHint.Range, "5.0,200.0", 50f));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName._selectedShader, Variant.Type.Int));

        return _cachedPropertyList = resultProperties;
    }

    public override bool _PropertyCanRevert(StringName property)
    {
        return GetCachedProperty(property)?.ContainsKey(PropertyGenerator.Revert) == true || base._PropertyCanRevert(property);
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return GetCachedProperty(property)?.TryGetValue(PropertyGenerator.Revert, out var value) == true ? value : base._PropertyGetRevert(property);
    }

    public override bool _Set(StringName property, Variant value)
    {
        var propertyStr = property.ToString();

        if (!propertyStr.StartsWith(MaterialConstant.MaterialParamPrefix))
        {
            return false;
        }

        if (propertyStr is MaterialConstant.CustomColorFirstProperty or MaterialConstant.CustomColorSecondProperty)
        {
            SetCustomColorParameter(value.AsColor(), propertyStr == MaterialConstant.CustomColorFirstProperty ? 0 : 1);
            return true;
        }

        var paramName = propertyStr.Replace(MaterialConstant.MaterialParamPrefix, string.Empty);
        Material.SetShaderParameter(paramName, value);
        return true;
    }

    public override Variant _Get(StringName property)
    {
        var propertyStr = property.ToString();

        if (!propertyStr.StartsWith(MaterialConstant.MaterialParamPrefix))
        {
            return Variant.From<GodotObject>(null);
        }

        if (propertyStr is MaterialConstant.CustomColorFirstProperty or MaterialConstant.CustomColorSecondProperty)
        {
            var albedo = Material.GetShaderParameter(MaterialConstant.CustomColorAlbedoProperty).AsProjection();
            var vectorColor = albedo[propertyStr == MaterialConstant.CustomColorFirstProperty ? 0 : 1];
            return new Color(vectorColor.X, vectorColor.Y, vectorColor.Z, vectorColor.W);
        }

        var paramName = propertyStr.Replace(MaterialConstant.MaterialParamPrefix, string.Empty);
        return Material.GetShaderParameter(paramName);
    }

    public void SetDefaultProperties()
    {
        foreach (var property in GetPropertyList())
        {
            var propertyName = property[PropertyGenerator.Name].AsStringName();
            if (PropertyCanRevert(propertyName))
            {
                Set(propertyName, PropertyGetRevert(propertyName));
            }
        }

        EmitSignal(GodotObject.SignalName.PropertyListChanged);
    }

    #endregion
}
