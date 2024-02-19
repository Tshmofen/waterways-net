using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using Waterway;
using Waterways.Util;

namespace Waterways;

[Tool]
public partial class RiverManager : Node3D
{
    [Signal] public delegate void RiverChangedEventHandler();

    #region Const

    private const string CustomColorFirstProperty = "mat_albedo_color_first";
    private const string CustomColorSecondProperty = "mat_albedo_color_second";
    private const string CustomColorAlbedoProperty = "albedo_color";
    private const string MaterialParamPrefix = "mat_";
    private const string FoamNoisePath = $"{WaterwaysPlugin.PluginPath}/textures/foam_noise.png";

    private static readonly List<RiverShader> BuiltInShaders = [
        new RiverShader
        {
            Name = "Water",
            ShaderPath = $"{WaterwaysPlugin.PluginPath}/shaders/river.gdshader",
            TexturePaths = [
                ("normal_bump_texture", $"{WaterwaysPlugin.PluginPath}/textures/water1_normal_bump.png")
            ]
        },
        new RiverShader
        {
            Name = "Lava",
            ShaderPath = $"{WaterwaysPlugin.PluginPath}/shaders/lava.gdshader",
            TexturePaths = [
                ("normal_bump_texture", $"{WaterwaysPlugin.PluginPath}/textures/lava_normal_bump.png"),
                ("emission_texture", $"{WaterwaysPlugin.PluginPath}/textures/lava_emission.png")
            ]
        }
    ];

    private static readonly RiverShader DebugShader = new()
    {
        Name = "Debug",
        ShaderPath = $"{WaterwaysPlugin.PluginPath}/shaders/river_debug.gdshader",
        TexturePaths = [
            ("debug_pattern", $"{WaterwaysPlugin.PluginPath}/textures/debug_pattern.png"),
            ("debug_arrow", $"{WaterwaysPlugin.PluginPath}/textures/debug_arrow.svg")
        ]
    };

    private static readonly Godot.Collections.Dictionary<string, string> MaterialCategories = new()
    {
        { $"{nameof(MaterialCategory.Albedo).ToLower()}_", MaterialCategory.Albedo},
        { $"{nameof(MaterialCategory.Emission).ToLower()}_", MaterialCategory.Emission},
        { $"{nameof(MaterialCategory.Transparency).ToLower()}_", MaterialCategory.Transparency},
        { $"{nameof(MaterialCategory.Normal).ToLower()}_", MaterialCategory.Normal},
        { $"{nameof(MaterialCategory.Flow).ToLower()}_", MaterialCategory.Flow},
        { $"{nameof(MaterialCategory.Foam).ToLower()}_", MaterialCategory.Foam},
        { $"{nameof(MaterialCategory.Custom).ToLower()}_", MaterialCategory.Custom}
    };

    public static class DefaultValues
    {
        public const int ShapeStepLengthDivs = 1;
        public const int ShapeStepWidthDivs = 1;
        public const float ShapeSmoothness = 0.5f;
        public const ShaderType MatShaderType = ShaderType.Water;
        public const float LodLod0Distance = 50.0f;
    }

    #endregion

    #region Properties

    private Array<Dictionary> _cachedPropertyList;

    public Action CurrentGizmoRedraw { get; set; }

    // Shape Properties
    private int _shapeStepLengthDivs = DefaultValues.ShapeStepLengthDivs;
    public int ShapeStepLengthDivs
    {
        get => _shapeStepLengthDivs;
        set
        {
            _shapeStepLengthDivs = value;
            if (_firstEnterTree)
            {
                return;
            }

            GenerateRiver();
            EmitSignal(SignalName.RiverChanged);
        }
    }

    private int _shapeStepWidthDivs = DefaultValues.ShapeStepWidthDivs;
    public int ShapeStepWidthDivs
    {
        get => _shapeStepWidthDivs;
        set
        {
            _shapeStepWidthDivs = value;
            if (_firstEnterTree)
            {
                return;
            }

            GenerateRiver();
            EmitSignal(SignalName.RiverChanged);
        }
    }

    private float _shapeSmoothness = DefaultValues.ShapeSmoothness;
    public float ShapeSmoothness
    {
        get => _shapeSmoothness;
        set
        {
            _shapeSmoothness = value;
            if (_firstEnterTree)
            {
                return;
            }

            GenerateRiver();
            EmitSignal(SignalName.RiverChanged);
        }
    }

    // Material Properties that not handled in shader
    private ShaderType _matShaderType = DefaultValues.MatShaderType;
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
                _material.Shader = MatCustomShader;
            }
            else
            {
                _material.Shader = ResourceLoader.Load(BuiltInShaders.First(s => s.Name == _matShaderType.ToString()).ShaderPath) as Shader;
                foreach (var (name, path) in BuiltInShaders.First(s => s.Name == _matShaderType.ToString()).TexturePaths)
                {
                    _material.SetShaderParameter(name, ResourceLoader.Load(path) as Texture);
                }
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
                _material.Shader = _matCustomShader;

                // Ability to fork default shader
                if (Engine.IsEditorHint() && string.IsNullOrEmpty(value.Code))
                {
                    var selectedShader = (Shader)ResourceLoader.Load(BuiltInShaders.First(s => s.Name == MatShaderType.ToString()).ShaderPath);
                    value.Code = selectedShader.Code;
                }
            }

            MatShaderType = (value != null ? ShaderType.Custom : ShaderType.Water);
        }
    }

    // LOD Properties
    private float _lodLod0Distance = DefaultValues.LodLod0Distance;
    public float LodLod0Distance
    {
        get => _lodLod0Distance;
        set
        {
            _lodLod0Distance = value;
            SetShaderParameter("i_lod0_distance", value);
        }
    }

    // Public variables
    public Curve3D Curve { get; set; }
    public MeshInstance3D MeshInstance { get; set; }
    public Texture2D FlowFoamNoise { get; set; }
    public Texture2D DistPressure { get; set; }

    private Array<float> _widths = [1.0f, 1.0f];
    public Array<float> Widths
    {
        get => _widths;
        set
        {
            _widths = value;
            if (_firstEnterTree)
            {
                return;
            }

            GenerateRiver();
        }
    }

    private int _debugView;
    public int DebugView
    {
        get => _debugView;
        set
        {
            _debugView = value;
            if (value == 0)
            {
                MeshInstance.MaterialOverride = null;
            }
            else
            {
                _debugMaterial.SetShaderParameter("mode", value);
                MeshInstance.MaterialOverride = _debugMaterial;
            }
        }
    }

    // Private variables
    private int _steps = 2;
    private SurfaceTool _surfaceTool;
    private MeshDataTool _meshDataTool;
    private ShaderMaterial _debugMaterial;
    private bool _firstEnterTree = true;

    // Serialised private variables
    private ShaderMaterial _material;
    private int _selectedShader = (int)ShaderType.Water;
    private int _uv2Sides;

    #endregion

    #region Properties Management

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
        var albedo = _material.GetShaderParameter(CustomColorAlbedoProperty).AsProjection();
        albedo[index] = new Vector4(color.R, color.G, color.B, color.A);
        _material.SetShaderParameter(CustomColorAlbedoProperty, albedo);
    }

    private static Dictionary CreateShaderParameter(Dictionary param, Rid shaderRid)
    {
        var paramName = GetPropertyName(param);
        var newProperty = PropertyGenerator.CreatePropertyCopy(param);
        newProperty[PropertyGenerator.Name] = MaterialParamPrefix + paramName;

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
        var firstColor = PropertyGenerator.CreateProperty(CustomColorFirstProperty, Variant.Type.Color, revert: new Color(0.6f, 0.7f, 0.65f));
        var secondColor = PropertyGenerator.CreateProperty(CustomColorSecondProperty, Variant.Type.Color, revert: new Color(0.25f, 0.35f, 0.35f));
        return [firstColor, secondColor];
    }

    private List<Dictionary> AccumulateShaderParameters()
    {
        var parameters = new List<Dictionary>();

        if (_material.Shader == null)
        {
            return parameters;
        }

        var shaderRid = _material.Shader.GetRid();
        var shaderParameters = RenderingServer
            .GetShaderParameterList(shaderRid)
            .Where(p => !GetPropertyName(p).StartsWith("i_"))
            .ToList();

        foreach (var (key, value) in MaterialCategories)
        {
            var group = shaderParameters.Where(p => GetPropertyName(p).StartsWith(key)).ToList();

            if (group.Count == 0)
            {
                continue;
            }

            parameters.Add(PropertyGenerator.CreateGroupingProperty($"Material/{value}", MaterialParamPrefix + key));

            foreach (var param in group)
            {
                if (GetPropertyName(param) == CustomColorAlbedoProperty)
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

        parameters.Add(PropertyGenerator.CreateGroupingProperty("Material", MaterialParamPrefix));
        parameters.AddRange(shaderParameters.Select(param => CreateShaderParameter(param, shaderRid)));

        return parameters;
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        var resultProperties = new Array<Dictionary>
        {
            PropertyGenerator.CreateGroupingProperty( "Shape", "Shape"),
            PropertyGenerator.CreateProperty(PropertyName.ShapeStepLengthDivs, Variant.Type.Int, PropertyHint.Range, "1,8", DefaultValues.ShapeStepLengthDivs),
            PropertyGenerator.CreateProperty(PropertyName.ShapeStepWidthDivs, Variant.Type.Int, PropertyHint.Range, "1,8", DefaultValues.ShapeStepWidthDivs),
            PropertyGenerator.CreateProperty(PropertyName.ShapeSmoothness, Variant.Type.Float, PropertyHint.Range, "0.1,5.0", DefaultValues.ShapeSmoothness),
            PropertyGenerator.CreateGroupingProperty( "Material", "Mat"),
            PropertyGenerator.CreateProperty(PropertyName.MatShaderType, Variant.Type.Int, PropertyHint.Enum, PropertyGenerator.GetEnumHint<ShaderType>(), (int) DefaultValues.MatShaderType),
            PropertyGenerator.CreateProperty(PropertyName.MatCustomShader, Variant.Type.Object, PropertyHint.ResourceType, nameof(Shader), Variant.From((GodotObject)null)),
            PropertyGenerator.CreateGroupingProperty( "Material", MaterialParamPrefix)
        };

        resultProperties.AddRange(AccumulateShaderParameters());
        resultProperties.Add(PropertyGenerator.CreateGroupingProperty("Lod", "Lod"));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.LodLod0Distance, Variant.Type.Float, PropertyHint.Range, "5.0,200.0", DefaultValues.LodLod0Distance));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.Curve, Variant.Type.Object));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.Widths, Variant.Type.Array));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.FlowFoamNoise, Variant.Type.Object));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.DistPressure, Variant.Type.Object));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName._material, Variant.Type.Object, PropertyHint.ResourceType, nameof(ShaderMaterial)));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName._selectedShader, Variant.Type.Int));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName._uv2Sides, Variant.Type.Int));

        return _cachedPropertyList = resultProperties;
    }

    public void SetShaderParameter(string param, Variant value)
    {
        _material.SetShaderParameter(param, value);
        _debugMaterial.SetShaderParameter(param, value);
    }

    public void PropertiesChanged()
    {
        EmitSignal(SignalName.RiverChanged);
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

        if (!propertyStr.StartsWith(MaterialParamPrefix))
        {
            return false;
        }

        if (propertyStr is CustomColorFirstProperty or CustomColorSecondProperty)
        {
            SetCustomColorParameter(value.AsColor(), propertyStr == CustomColorFirstProperty ? 0 : 1);
            return true;
        }

        var paramName = propertyStr.Replace(MaterialParamPrefix, string.Empty);
        _material.SetShaderParameter(paramName, value);
        return true;
    }

    public override Variant _Get(StringName property)
    {
        var propertyStr = property.ToString();

        if (!propertyStr.StartsWith(MaterialParamPrefix))
        {
            return Variant.From<GodotObject>(null);
        }

        if (propertyStr is CustomColorFirstProperty or CustomColorSecondProperty)
        {
            var albedo = _material.GetShaderParameter(CustomColorAlbedoProperty).AsProjection();
            var vectorColor = albedo[propertyStr == CustomColorFirstProperty ? 0 : 1];
            return new Color(vectorColor.X, vectorColor.Y, vectorColor.Z, vectorColor.W);
        }

        var paramName = propertyStr.Replace(MaterialParamPrefix, string.Empty);
        return _material.GetShaderParameter(paramName);
    }

    #endregion

    #region Event Handlers 

    private void ClearPropertyListCache()
    {
        _cachedPropertyList = null;
    }

    private void RedrawCurrentGizmo()
    {
        CurrentGizmoRedraw?.Invoke();
    }

    #endregion

    #region Util

    private void GenerateRiver()
    {
        var averageWidth = Widths.Sum() / (Widths.Count / 2f);
        _steps = (int)(Mathf.Max(1.0f, Mathf.Round(Curve.GetBakedLength() / averageWidth)));

        var riverWidthValues = WaterHelperMethods.GenerateRiverWidthValues(Curve, _steps, ShapeStepLengthDivs, Widths);
        MeshInstance.Mesh = WaterHelperMethods.GenerateRiverMesh(Curve, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, ShapeSmoothness, riverWidthValues);
        MeshInstance.Mesh.SurfaceSetMaterial(0, _material);
    }

    #endregion

    public RiverManager()
    {
        _surfaceTool = new SurfaceTool();
        _meshDataTool = new MeshDataTool();

        _debugMaterial = new ShaderMaterial
        {
            Shader = ResourceLoader.Load(DebugShader.ShaderPath) as Shader
        };

        foreach (var (name, path) in DebugShader.TexturePaths)
        {
            _debugMaterial.SetShaderParameter(name, ResourceLoader.Load(path) as Texture2D);
        }

        _material = new ShaderMaterial
        {
            Shader = ResourceLoader.Load(BuiltInShaders.First(s => s.Name == MatShaderType.ToString()).ShaderPath) as Shader
        };

        foreach (var (name, path) in BuiltInShaders.First(s => s.Name == MatShaderType.ToString()).TexturePaths)
        {
            _material.SetShaderParameter(name, ResourceLoader.Load(path) as Texture2D);
        }

        // Have to manually set the color, or it does not default right
        _material.SetShaderParameter("albedo_color", new Transform3D(new Vector3(0.0f, 0.8f, 1.0f), new Vector3(0.15f, 0.2f, 0.5f), Vector3.Zero, Vector3.Zero));
        PropertyListChanged += ClearPropertyListCache;
        RiverChanged += RedrawCurrentGizmo;
    }

    protected override void Dispose(bool disposing)
    {
        PropertyListChanged -= ClearPropertyListCache;
        RiverChanged -= RedrawCurrentGizmo;
    }

    public override void _Ready()
    {
        EmitSignal(SignalName.RiverChanged);
    }

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint() && _firstEnterTree)
        {
            _firstEnterTree = false;
        }

        if (Curve == null)
        {
            Curve = new Curve3D
            {
                BakeInterval = 0.05f
            };

            Curve.AddPoint(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
            Curve.AddPoint(new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
        }

        var generateMesh = true;
        foreach (var child in GetChildren())
        {
            if (child is not MeshInstance3D mesh || !mesh.HasMeta("RiverManager"))
            {
                continue;
            }

            generateMesh = false;
            MeshInstance = mesh;
            _material = MeshInstance.Mesh.SurfaceGetMaterial(0) as ShaderMaterial;
            break;
        }

        if (generateMesh)
        {
            var newMeshInstance = new MeshInstance3D
            {
                Name = "RiverMeshInstance"
            };

            newMeshInstance.SetMeta("RiverManager", true);
            AddChild(newMeshInstance);
            MeshInstance = newMeshInstance;
            GenerateRiver();
        }

        SetShaderParameter("i_uv2_sides", _uv2Sides);
        SetShaderParameter("i_distmap", DistPressure);
        SetShaderParameter("i_texture_foam_noise", ResourceLoader.Load(FoamNoisePath) as Texture2D);
    }

    public void CreateMeshDuplicate()
    {
        if (Owner == null)
        {
            GD.PushWarning("Cannot create MeshInstance3D sibling when River is root.");
            return;
        }

        var siblingMesh = GetMeshCopy();
        siblingMesh.Name = $"{Name}Mesh";
        GetParent().AddChild(siblingMesh);
        siblingMesh.Owner = GetTree().EditedSceneRoot;
    }

    public MeshInstance3D GetMeshCopy()
    {
        var newMesh = (MeshInstance3D)MeshInstance.Duplicate();
        newMesh.GlobalTransform = MeshInstance.GlobalTransform;
        newMesh.MaterialOverride = null;
        return newMesh;
    }

    #region Points Management

    public List<Vector3> GetCurvePoints()
    {
        var points = new List<Vector3>();

        for (var p = 0; p < Curve.PointCount; p++)
        {
            points.Add(Curve.GetPointPosition(p));
        }

        return points;
    }

    public int GetClosestPointTo(Vector3 point)
    {
        var closestDistance = 4096.0f;
        var closestIndex = -1;

        for (var p = 0; p < Curve.PointCount; p++)
        {
            var dist = point.DistanceTo(Curve.GetPointPosition(p));

            if (dist >= closestDistance)
            {
                continue;
            }

            closestDistance = dist;
            closestIndex = p;
        }

        return closestIndex;
    }

    public Aabb GetTransformedAabb()
    {
        return GlobalTransform * MeshInstance.GetAabb();
    }

    public void SetCurvePointPosition(int index, Vector3 position)
    {
        Curve.SetPointPosition(index, position);
        GenerateRiver();
    }

    public void SetCurvePointIn(int index, Vector3 position)
    {
        Curve.SetPointIn(index, position);
        GenerateRiver();
    }

    public void SetCurvePointOut(int index, Vector3 position)
    {
        Curve.SetPointOut(index, position);
        GenerateRiver();
    }

    public void AddPoint(Vector3 position, int index, Vector3 dir, float width)
    {
        if (index == -1)
        {
            var lastIndex = Curve.PointCount - 1;
            var dist = position.DistanceTo(Curve.GetPointPosition(lastIndex));
            var newDir = (dir != Vector3.Zero) ? dir : (position - Curve.GetPointPosition(lastIndex) - Curve.GetPointOut(lastIndex)).Normalized() * 0.25f * dist;
            Curve.AddPoint(position, -newDir, newDir);
            Widths.Add(Widths[^1]); // If this is a new point at the end, add a width that's the same as last
        }
        else
        {
            var dist = Curve.GetPointPosition(index).DistanceTo(Curve.GetPointPosition(index + 1));
            var newDir = (dir != Vector3.Zero) ? dir : (Curve.GetPointPosition(index + 1) - Curve.GetPointPosition(index)).Normalized() * 0.25f * dist;
            Curve.AddPoint(position, -newDir, newDir, index + 1);
            var newWidth = width != 0.0f ? width : (Widths[index] + Widths[index + 1]) / 2.0f;
            Widths.Insert(index + 1, newWidth); // We set the width to the average of the two surrounding widths
        }

        GenerateRiver();
        EmitSignal(SignalName.RiverChanged);
    }

    public void RemovePoint(int index)
    {
        // We don't allow rivers shorter than 2 points
        if (Curve.PointCount <= 2)
        {
            return;
        }

        Curve.RemovePoint(index);
        Widths.RemoveAt(index);
        GenerateRiver();
        EmitSignal(SignalName.RiverChanged);
    }

    #endregion
}