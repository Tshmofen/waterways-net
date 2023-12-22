using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using Waterways.Util;

namespace Waterways;

[Tool]
public partial class RiverManager : Node3D
{
    // river_changed used to update handles when values are changed on script side
    // progress_notified used to up progress bar when baking maps
    // albedo_set is needed since the gradient is a custom inspector that needs a signal to update from script side
    [Signal] public delegate void RiverChangedEventHandler();
    [Signal] public delegate void ProgressNotifiedEventHandler();

    private class RiverShader
    {
        public string Name { get; init; }
        public string ShaderPath { get; init; }
        public List<(string name, string path)> TexturePaths { get; init; }
    }

    private const string FilterRendererPath = $"{WaterwaysPlugin.PluginPath}/filter_renderer.tscn";
    private const string FlowOffsetNoiseTexturePath = $"{WaterwaysPlugin.PluginPath}/textures/flow_offset_noise.png";
    private const string FoamNoisePath = $"{WaterwaysPlugin.PluginPath}/textures/foam_noise.png";

    private static class MaterialCategories
    {
        public const string Albedo = "Albedo";
        public const string Emission = "Emission";
        public const string Transparency = "Transparency";
        public const string Flow = "Flow";
        public const string Foam = "Foam";
        public const string Custom = "Custom";
    }

    public enum ShaderTypes
    {
        Water,
        Lava,
        Custom
    }

    private static readonly List<RiverShader> BuiltinShaders = [
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

    private static class DefaultParameters
    {
        public const int ShapeStepLengthDivs = 1;
        public const int ShapeStepWidthDivs = 1;
        public const float ShapeSmoothness = 0.5f;
        public const int MatShaderType = 0;
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

    // Shape Properties
    private int _shapeStepLengthDivs = DefaultParameters.ShapeStepLengthDivs;
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
            EmitSignal("river_changed");
        }
    }

    private int _shapeStepWidthDivs = DefaultParameters.ShapeStepWidthDivs;
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
            EmitSignal("river_changed");
        }
    }

    private float _shapeSmoothness = DefaultParameters.ShapeSmoothness;
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
            EmitSignal("river_changed");
        }
    }

    // Material Properties that not handled in shader
    private ShaderTypes _matShaderType = DefaultParameters.MatShaderType;
    public ShaderTypes MatShaderType
    {
        get => _matShaderType;
        set
        {
            if (value == _matShaderType)
            {
                return;
            }

            _matShaderType = value;

            if (_matShaderType == ShaderTypes.Custom)
            {
                _material.Shader = MatCustomShader;
            }
            else
            {
                _material.Shader = ResourceLoader.Load(BuiltinShaders.First(s => s.Name == _matShaderType.ToString()).ShaderPath) as Shader;
                foreach (var (name, path) in BuiltinShaders.First(s => s.Name == _matShaderType.ToString()).TexturePaths)
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
                    var selectedShader = (Shader)ResourceLoader.Load(BuiltinShaders.First(s => s.Name == MatShaderType.ToString()).ShaderPath);
                    value.Code = selectedShader.Code;
                }
            }

            MatShaderType = (value != null ? ShaderTypes.Custom : ShaderTypes.Water);
        }
    }

    // LOD Properties

    private float _lodLod0Distance = DefaultParameters.LodLod0Distance;
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
    public int BakingResolution { get; set; } = DefaultParameters.BakingResolution;
    public float BakingRaycastDistance { get; set; } = DefaultParameters.BakingRaycastDistance;
    public int BakingRaycastLayers { get; set; } = DefaultParameters.BakingRaycastLayers;
    public float BakingDilate { get; set; } = DefaultParameters.BakingDilate;
    public float BakingFlowmapBlur { get; set; } = DefaultParameters.BakingFlowmapBlur;
    public float BakingFoamCutoff { get; set; } = DefaultParameters.BakingFoamCutoff;
    public float BakingFoamOffset { get; set; } = DefaultParameters.BakingFoamOffset;
    public float BakingFoamBlur { get; set; } = DefaultParameters.BakingFoamBlur;

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
    private SurfaceTool _st;
    private MeshDataTool _mdt;
    private ShaderMaterial _debugMaterial;
    private bool _firstEnterTree = true;
    private PackedScene _filterRenderer;

    // Serialised private variables
    private ShaderMaterial _material;
    private int _selectedShader = (int)ShaderTypes.Water;
    private int _uv2Sides;

    // Internal Methods
    public override Array<Dictionary> _GetPropertyList()
    {
        var resultProperties = new Array<Dictionary>
        {
            PropertyGeneration.CreateGroupingProperty( "Shape", "shape_"),
            PropertyGeneration.CreateProperty(PropertyName.ShapeStepLengthDivs, Variant.Type.Int, PropertyHint.Range, "1, 8"),
            PropertyGeneration.CreateProperty(PropertyName.ShapeStepWidthDivs, Variant.Type.Int, PropertyHint.Range, "1, 8"),
            PropertyGeneration.CreateProperty(PropertyName.ShapeSmoothness, Variant.Type.Float, PropertyHint.Range, "0.1, 5.0"),

            PropertyGeneration.CreateGroupingProperty( "Material", "mat_"),
            PropertyGeneration.CreateProperty(PropertyName.MatShaderType, Variant.Type.Int, PropertyHint.Enum, PropertyGeneration.GetEnumHint<ShaderTypes>()),
            PropertyGeneration.CreateProperty(PropertyName.MatCustomShader, Variant.Type.Object, PropertyHint.ResourceType, "Shader"),
        };

        var matCategories = new Godot.Collections.Dictionary<string, string>
        {
            { nameof(MaterialCategories.Albedo), MaterialCategories.Albedo},
            { nameof(MaterialCategories.Emission), MaterialCategories.Emission},
            { nameof(MaterialCategories.Transparency), MaterialCategories.Transparency},
            { nameof(MaterialCategories.Flow), MaterialCategories.Flow},
            { nameof(MaterialCategories.Foam), MaterialCategories.Foam},
            { nameof(MaterialCategories.Custom), MaterialCategories.Custom}
        };

        if (_material.Shader != null)
        {
            foreach (var parameter in RenderingServer.GetShaderParameterList(_material.Shader.GetRid()))
            {
                if (parameter[PropertyGeneration.Name].AsString().StartsWith("i_"))
                {
                    continue;
                }

                var hitCategory = (string) null;
                foreach (var (key, value) in matCategories)
                {
                    if (!parameter[PropertyGeneration.Name].AsString().StartsWith(key))
                    {
                        continue;
                    }

                    var property = PropertyGeneration.CreateGroupingProperty($"Material/{value}", $"mat_{key}");
                    resultProperties.Add(property);
                    hitCategory = key;
                    break;
                }

                if (hitCategory != null)
                {
                    matCategories.Remove(hitCategory);
                }

                var newProperty = PropertyGeneration.CreatePropertyCopy(parameter);
                var paramName = parameter[PropertyGeneration.Name].AsString();
                newProperty[PropertyGeneration.Name] = $"mat_{paramName}";
                if (paramName.Contains("curve"))
                {
                    newProperty[PropertyGeneration.Hint] = (int)PropertyHint.ExpEasing;
                    newProperty[PropertyGeneration.HintString] = "EASE";
                }

                resultProperties.Add(newProperty);
            }
        }

        resultProperties.Add(PropertyGeneration.CreateGroupingProperty("Lod", "lod_"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.LodLod0Distance, Variant.Type.Float, PropertyHint.Range, "5.0, 200.0"));

        resultProperties.Add(PropertyGeneration.CreateGroupingProperty("Baking", "baking_"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingResolution, Variant.Type.Int, PropertyHint.Enum, "64, 128, 256, 512, 1024"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingRaycastDistance, Variant.Type.Float, PropertyHint.Range, "0.0, 100.0"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingRaycastLayers, Variant.Type.Int, PropertyHint.Layers3DPhysics));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingDilate, Variant.Type.Float, PropertyHint.Range, "0.0, 1.0"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingFlowmapBlur, Variant.Type.Float, PropertyHint.Range, "0.0, 1.0"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingFoamCutoff, Variant.Type.Float, PropertyHint.Range, "0.0, 1.0"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingFoamOffset, Variant.Type.Float, PropertyHint.Range, "0.0, 1.0"));
        resultProperties.Add(PropertyGeneration.CreateProperty(PropertyName.BakingFoamBlur, Variant.Type.Float, PropertyHint.Range, "0.0, 1.0"));

        // Serialize these values without exposing it in the inspector
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName.Curve, Variant.Type.Object));
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName.Widths, Variant.Type.Array));
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName.ValidFlowmap, Variant.Type.Bool));
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName.FlowFoamNoise, Variant.Type.Object));
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName.DistPressure, Variant.Type.Object));
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName._material, Variant.Type.Object, PropertyHint.ResourceType, "ShaderMaterial"));
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName._selectedShader, Variant.Type.Int));
        resultProperties.Add(PropertyGeneration.CreateStorageProperty(PropertyName._uv2Sides, Variant.Type.Int));

        return resultProperties;
    }

    // todo maybe should be _ready
    public RiverManager()
    {
        _st = new SurfaceTool();
        _mdt = new MeshDataTool();
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
            Shader = ResourceLoader.Load(BuiltinShaders.First(s => s.Name == MatShaderType.ToString()).ShaderPath) as Shader
        };

        foreach (var (name, path) in BuiltinShaders.First(s => s.Name == MatShaderType.ToString()).TexturePaths)
        {
            _material.SetShaderParameter(name, ResourceLoader.Load(path) as Texture2D);
        }

        // Have to manually set the color, or it does not default right. Not sure how to work around this
        _material.SetShaderParameter("albedo_color", new Transform3D(new Vector3(0.0f, 0.8f, 1.0f), new Vector3(0.15f, 0.2f, 0.5f), Vector3.Zero, Vector3.Zero));
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
            return [""];
        }

        return ["No flowmap is set. Select River -> Generate Flow & Foam Map to generate and assign one."];
    }

    public Aabb GetTransformedAabb()
    {
        return GlobalTransform * MeshInstance.GetAabb();
    }

    // Public Methods - These should all be good to use as API from other scripts
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
        EmitSignal("river_changed");
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
        EmitSignal("river_changed");
        GenerateRiver();
    }

    public void BakeTexture()
    {
        GenerateRiver();
        // Need to await?
        GenerateFlowMap(Mathf.Pow(2, 6 + BakingResolution));
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

        // TODO: was using true (1) ?
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

    // Private Methods
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

        EmitSignal(SignalName.ProgressNotified, 0.0f, "Calculating Collisions (" + flowmapResolution + "x" + flowmapResolution + ")");
        await ToSignal(GetTree(), "process_frame");

        image = await WaterHelperMethods.GenerateCollisionMap(image, MeshInstance, BakingRaycastDistance, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, this);

        EmitSignal(SignalName.ProgressNotified, 0.95f, "Applying filters (" + flowmapResolution + "x" + flowmapResolution + ")");
        await ToSignal(GetTree(), "process_frame");

        // Calculate how many columns are in UV2
        _uv2Sides = WaterHelperMethods.CalculateSide(_steps);

        var margin = (int)(Mathf.Round(flowmapResolution / _uv2Sides));

        image = WaterHelperMethods.AddMargins(image, (int)flowmapResolution, margin);

        var collisionWithMargins = ImageTexture.CreateFromImage(image);

        // Create correctly tiling noise for A channel
        var noiseTexture = (Texture2D) ResourceLoader.Load(FlowOffsetNoiseTexturePath);
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

    // Signal Method
    public void PropertiesChanged()
    {
        EmitSignal(SignalName.RiverChanged);
    }
}