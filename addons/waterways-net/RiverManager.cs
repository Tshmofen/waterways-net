using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using Waterway;
using Waterways.Util;

namespace Waterways;

[Tool]
public partial class RiverManager : Node3D
{
    private const string MaterialParamPrefix = "mat_";
    private const string FilterRendererPath = $"{WaterwaysPlugin.PluginPath}/filter_renderer.tscn";
    private const string FlowOffsetNoiseTexturePath = $"{WaterwaysPlugin.PluginPath}/textures/flow_offset_noise.png";
    private const string FoamNoisePath = $"{WaterwaysPlugin.PluginPath}/textures/foam_noise.png";

    // river_changed used to update handles when values are changed on script side
    // progress_notified used to up progress bar when baking maps
    // albedo_set is needed since the gradient is a custom inspector that needs a signal to update from script side
    [Signal] public delegate void RiverChangedEventHandler();
    [Signal] public delegate void ProgressNotifiedEventHandler();

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

    #region Vars

    public static class DefaultValues
    {
        public const int ShapeStepLengthDivs = 1;
        public const int ShapeStepWidthDivs = 1;
        public const float ShapeSmoothness = 0.5f;
        public const ShaderType MatShaderType = ShaderType.Water;
        public const int BakingResolution = 2;
        public const float BakingRaycastDistance = 10.0f;
        public const int BakingRaycastLayers = 1;
        public const float BakingDilate = 0.6f;
        public const float BakingFlowmapBlur = 0.04f;
        public const float BakingFoamCutoff = 0.9f;
        public const float BakingFoamOffset = 0.1f;
        public const float BakingFoamBlur = 0.02f;
        public const float LodLod0Distance = 50.0f;
    }

    private Array<Dictionary> _cachedPropertyList;

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

            ValidFlowmap = false;
            SetMaterials("i_valid_flowmap", ValidFlowmap);
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

            ValidFlowmap = false;
            SetMaterials("i_valid_flowmap", ValidFlowmap);
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

            ValidFlowmap = false;
            SetMaterials("i_valid_flowmap", ValidFlowmap);
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
            SetMaterials("i_lod0_distance", value);
        }
    }

    // Bake Properties
    public int BakingResolution { get; set; } = DefaultValues.BakingResolution;
    public float BakingRaycastDistance { get; set; } = DefaultValues.BakingRaycastDistance;
    public int BakingRaycastLayers { get; set; } = DefaultValues.BakingRaycastLayers;
    public float BakingDilate { get; set; } = DefaultValues.BakingDilate;
    public float BakingFlowmapBlur { get; set; } = DefaultValues.BakingFlowmapBlur;
    public float BakingFoamCutoff { get; set; } = DefaultValues.BakingFoamCutoff;
    public float BakingFoamOffset { get; set; } = DefaultValues.BakingFoamOffset;
    public float BakingFoamBlur { get; set; } = DefaultValues.BakingFoamBlur;

    // Public variables
    public Curve3D Curve { get; set; }
    public bool ValidFlowmap { get; set; }
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
    private PackedScene _filterRenderer;

    // Serialised private variables
    private ShaderMaterial _material;
    private int _selectedShader = (int)ShaderType.Water;
    private int _uv2Sides;

    #endregion

    #region Util

    private void GenerateRiver()
    {
        // TODO: count loss expected?
        var averageWidth = Widths.Sum() / (Widths.Count / 2f);
        _steps = (int)(Mathf.Max(1.0f, Mathf.Round(Curve.GetBakedLength() / averageWidth)));

        var riverWidthValues = WaterHelperMethods.GenerateRiverWidthValues(Curve, _steps, ShapeStepLengthDivs, Widths);
        MeshInstance.Mesh = WaterHelperMethods.GenerateRiverMesh(Curve, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, ShapeSmoothness, riverWidthValues);
        MeshInstance.Mesh.SurfaceSetMaterial(0, _material);
    }

    private async Task GenerateFlowMap(float flowmapResolution)
    {
        // WaterHelperMethods.ResetAllColliders(get_tree().root)
        var image = Image.Create((int)flowmapResolution, (int)flowmapResolution, true, Image.Format.Rgb8);
        image.Fill(new Color(0, 0, 0));

        EmitSignal(SignalName.ProgressNotified, 0.0f, $"Calculating Collisions ({flowmapResolution}x{flowmapResolution})");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        image = await WaterHelperMethods.GenerateCollisionMap(image, MeshInstance, BakingRaycastDistance, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, this);

        EmitSignal(SignalName.ProgressNotified, 0.95f, $"Applying filters ({flowmapResolution}x{flowmapResolution})");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        // Calculate how many columns are in UV2
        _uv2Sides = WaterHelperMethods.CalculateSide(_steps);

        var margin = (int)(Mathf.Round(flowmapResolution / _uv2Sides));
        image = WaterHelperMethods.AddMargins(image, (int)flowmapResolution, margin);
        var collisionWithMargins = ImageTexture.CreateFromImage(image);

        // Create correctly tiling noise for A channel
        var noiseTexture = (Texture2D)ResourceLoader.Load(FlowOffsetNoiseTexturePath);
        var noiseWithMarginSize = (_uv2Sides + 2) * (noiseTexture.GetWidth() / (float)(_uv2Sides));
        var noiseWithTiling = Image.Create((int)noiseWithMarginSize, (int)noiseWithMarginSize, false, Image.Format.Rgb8);
        var sliceWidth = noiseTexture.GetWidth() / (float)(_uv2Sides);

        for (var x = 0; x < _uv2Sides; x++)
        {
            noiseWithTiling.BlendRect(noiseTexture.GetImage(), new Rect2I(0, 0, (int)sliceWidth, noiseTexture.GetHeight()), new Vector2I((int)(sliceWidth + (x * sliceWidth)), (int)(sliceWidth - (noiseTexture.GetWidth() / 2.0f))));
            noiseWithTiling.BlendRect(noiseTexture.GetImage(), new Rect2I(0, 0, (int)sliceWidth, noiseTexture.GetHeight()), new Vector2I((int)(sliceWidth + (x * sliceWidth)), (int)(sliceWidth + (noiseTexture.GetWidth() / 2.0f))));
        }

        var tiledNoise = ImageTexture.CreateFromImage(noiseWithTiling);

        // Create renderer
        var rendererInstance = _filterRenderer.Instantiate<FilterRenderer>();

        AddChild(rendererInstance);

        var flowPressureBlurAmount = 0.04f / _uv2Sides * flowmapResolution;
        var dilateAmount = BakingDilate / _uv2Sides;
        var flowmapBlurAmount = BakingFlowmapBlur / _uv2Sides * flowmapResolution;
        var foamOffsetAmount = BakingFoamOffset / _uv2Sides;
        var foamBlurAmount = BakingFoamBlur / _uv2Sides * flowmapResolution;

        var flowPressureMap = await rendererInstance.ApplyFlowPressure(collisionWithMargins, flowmapResolution, _uv2Sides + 2.0f);
        var blurredFlowPressureMap = await rendererInstance.ApplyVerticalBlur(flowPressureMap, flowPressureBlurAmount, flowmapResolution + (margin * 2));
        var dilatedTexture = await rendererInstance.ApplyDilate(collisionWithMargins, dilateAmount, 0.0f, flowmapResolution + (margin * 2));
        var normalMap = await rendererInstance.ApplyNormal(dilatedTexture, flowmapResolution + (margin * 2));
        var flowMap = await rendererInstance.ApplyNormalToFlow(normalMap, flowmapResolution + (margin * 2));
        var blurredFlowMap = await rendererInstance.ApplyBlur(flowMap, flowmapBlurAmount, flowmapResolution + (margin * 2));
        var foamMap = await rendererInstance.ApplyFoam(dilatedTexture, foamOffsetAmount, BakingFoamCutoff, flowmapResolution + (margin * 2));
        var blurredFoamMap = await rendererInstance.ApplyBlur(foamMap, foamBlurAmount, flowmapResolution + (margin * 2));
        var flowFoamNoiseImg = await rendererInstance.ApplyCombine(blurredFlowMap, blurredFlowMap, blurredFoamMap, tiledNoise);
        var distPressureImg = await rendererInstance.ApplyCombine(dilatedTexture, blurredFlowPressureMap);

        RemoveChild(rendererInstance); // cleanup

        FlowFoamNoise = flowFoamNoiseImg;
        DistPressure = distPressureImg;

        SetMaterials("i_flowmap", FlowFoamNoise);
        SetMaterials("i_distmap", DistPressure);
        SetMaterials("i_valid_flowmap", true);
        SetMaterials("i_uv2_sides", _uv2Sides);

        ValidFlowmap = true;
        EmitSignal(SignalName.ProgressNotified, 100.0, "finished");
        UpdateConfigurationWarnings();
    }

    private Dictionary GetCachedProperty(StringName name)
    {
        _cachedPropertyList ??= _GetPropertyList();
        return _cachedPropertyList.FirstOrDefault(p => p[PropertyGenerator.Name].AsStringName() == name);
    }

    private static string GetPropertyName(Dictionary property)
    {
        return property[PropertyGenerator.Name].AsString();
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

    private void AccumulateShaderParameters(Array<Dictionary> resultProperties)
    {
        if (_material.Shader == null)
        {
            return;
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

            resultProperties.Add(PropertyGenerator.CreateGroupingProperty($"Material/{value}", MaterialParamPrefix + key));

            foreach (var param in group)
            {
                resultProperties.Add(CreateShaderParameter(param, shaderRid));
                shaderParameters.Remove(param);
            }
        }

        // add remaining parameters
        resultProperties.Add(PropertyGenerator.CreateGroupingProperty("Material", MaterialParamPrefix));
        foreach (var param in shaderParameters)
        {
            resultProperties.Add(CreateShaderParameter(param, shaderRid));
        }
    }

    #endregion

    public RiverManager()
    {
        _surfaceTool = new SurfaceTool();
        _meshDataTool = new MeshDataTool();
        _filterRenderer = ResourceLoader.Load(FilterRendererPath) as PackedScene;

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

        // Have to manually set the color, or it does not default right. Not sure how to work around this
        _material.SetShaderParameter("albedo_color", new Transform3D(new Vector3(0.0f, 0.8f, 1.0f), new Vector3(0.15f, 0.2f, 0.5f), Vector3.Zero, Vector3.Zero));

        PropertyListChanged += () => _cachedPropertyList = null;
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

        AccumulateShaderParameters(resultProperties);

        resultProperties.Add(PropertyGenerator.CreateGroupingProperty("Lod", "Lod"));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.LodLod0Distance, Variant.Type.Float, PropertyHint.Range, "5.0,200.0", DefaultValues.LodLod0Distance));
        resultProperties.Add(PropertyGenerator.CreateGroupingProperty("Baking", "Baking"));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingResolution, Variant.Type.Int, PropertyHint.Enum, "64,128,256,512,1024", DefaultValues.BakingResolution));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingRaycastDistance, Variant.Type.Float, PropertyHint.Range, "0.0,100.0", DefaultValues.BakingRaycastDistance));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingRaycastLayers, Variant.Type.Int, PropertyHint.Layers3DPhysics, revert: DefaultValues.BakingRaycastLayers));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingDilate, Variant.Type.Float, PropertyHint.Range, "0.0,1.0", DefaultValues.BakingDilate));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingFlowmapBlur, Variant.Type.Float, PropertyHint.Range, "0.0,1.0", DefaultValues.BakingFlowmapBlur));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingFoamCutoff, Variant.Type.Float, PropertyHint.Range, "0.0,1.0", DefaultValues.BakingFoamCutoff));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingFoamOffset, Variant.Type.Float, PropertyHint.Range, "0.0,1.0", DefaultValues.BakingFoamOffset));
        resultProperties.Add(PropertyGenerator.CreateProperty(PropertyName.BakingFoamBlur, Variant.Type.Float, PropertyHint.Range, "0.0,1.0", DefaultValues.BakingFoamBlur));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.Curve, Variant.Type.Object));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.Widths, Variant.Type.Array));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.ValidFlowmap, Variant.Type.Bool));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.FlowFoamNoise, Variant.Type.Object));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName.DistPressure, Variant.Type.Object));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName._material, Variant.Type.Object, PropertyHint.ResourceType,  nameof(ShaderMaterial)));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName._selectedShader, Variant.Type.Int));
        resultProperties.Add(PropertyGenerator.CreateStorageProperty(PropertyName._uv2Sides, Variant.Type.Int));

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

        if (GetChildCount() <= 0)
        {
            // This is what happens on creating a new river
            var newMeshInstance = new MeshInstance3D
            {
                Name = "RiverMeshInstance"
            };

            AddChild(newMeshInstance);
            MeshInstance = (MeshInstance3D)GetChild(0);
            GenerateRiver();
        }
        else
        {
            MeshInstance = (MeshInstance3D)GetChild(0);
            _material = MeshInstance.Mesh.SurfaceGetMaterial(0) as ShaderMaterial;
        }

        SetMaterials("i_valid_flowmap", ValidFlowmap);
        SetMaterials("i_uv2_sides", _uv2Sides);
        SetMaterials("i_distmap", DistPressure);
        SetMaterials("i_flowmap", FlowFoamNoise);
        SetMaterials("i_texture_foam_noise", ResourceLoader.Load(FoamNoisePath) as Texture2D);
    }

    public override string[] _GetConfigurationWarnings()
    {
        if (ValidFlowmap)
        {
            return [string.Empty];
        }

        return ["No flowmap is set. Select River -> Generate Flow & Foam Map to generate and assign one."];
    }

    public override bool _Set(StringName property, Variant value)
    {
        var propertyStr = property.ToString();

        if (!propertyStr.StartsWith(MaterialParamPrefix))
        {
            return false;
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

        var paramName = propertyStr.Replace(MaterialParamPrefix, string.Empty);
        return _material.GetShaderParameter(paramName);
    }

    public Aabb GetTransformedAabb()
    {
        return GlobalTransform * MeshInstance.GetAabb();
    }

    public void AddPoint(Vector3 position, int index, Vector3 dir, float width = 0.0f)
    {
        var newWidth = 0f;

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
            newWidth = width != 0.0f ? width : (Widths[index] + Widths[index + 1]) / 2.0f;
        }

        Widths.Insert(index + 1, newWidth); // We set the width to the average of the two surrounding widths
        EmitSignal(SignalName.RiverChanged);
        GenerateRiver();
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
        EmitSignal(SignalName.RiverChanged);
        GenerateRiver();
    }

    public void BakeTexture()
    {
        GenerateRiver();
        _ = GenerateFlowMap(Mathf.Pow(2, 6 + BakingResolution));
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

    public void SetMaterials(string param, Variant value)
    {
        _material.SetShaderParameter(param, value);
        _debugMaterial.SetShaderParameter(param, value);
    }

    public void SpawnMesh()
    {
        if (Owner == null)
        {
            GD.PushWarning("Cannot create MeshInstance3D sibling when River is root.");
            return;
        }

        var siblingMesh = (MeshInstance3D)MeshInstance.Duplicate((int)DuplicateFlags.Signals);
        GetParent().AddChild(siblingMesh);
        siblingMesh.Owner = GetTree().EditedSceneRoot;
        siblingMesh.Position = Position;
        siblingMesh.MaterialOverride = null;
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

    public Variant GetShaderParameter(string param)
    {
        return _material.GetShaderParameter(param);
    }

    public void PropertiesChanged()
    {
        EmitSignal(SignalName.RiverChanged);
    }

    public List<Vector3> GetCurvePoints()
    {
        var points = new List<Vector3>();

        for (var p = 0; p < Curve.PointCount; p++)
        {
            points.Add(Curve.GetPointPosition(p));
        }

        return points;
    }
}