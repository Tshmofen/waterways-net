using Godot;
using Waterways.Data;
using Waterways.Utils;

namespace Waterways;

[Tool]
public partial class RiverManager : Path3D
{
    public const string PluginBaseAlias = nameof(Path3D);
    public const string PluginNodeAlias = nameof(RiverManager);
    public const string ScriptPath = $"{nameof(RiverManager)}.cs";
    public const string IconPath = "river.svg";

    private const string RiverManagerStamp = "RiverManager";
    private MeshInstance3D _meshInstance;
    private int _steps = 2;

    #region Export Properties

    private RiverShaderSettings _shaderSettings;
    [Export] public RiverShaderSettings ShaderSettings
    {
        get => _shaderSettings;
        set
        {
            _shaderSettings = value;

            if (_shaderSettings == null)
            {
                _shaderSettings = new RiverShaderSettings();
                _shaderSettings.CallDeferred(RiverShaderSettings.MethodName.SetDefaultProperties);
            }

            var material = GetShaderMaterial();
            if (material != null)
            {
                _shaderSettings.Material = _meshInstance?.Mesh?.SurfaceGetMaterial(0) as ShaderMaterial;
                EmitSignal(Path3D.SignalName.CurveChanged);
            }
        }
    }

    [ExportCategory("Shape Settings")]

    private float _riverWidth = 5f;
    [Export] public float RiverWidth
    {
        get => _riverWidth;
        set
        {
            _riverWidth = value;
            EmitSignal(Path3D.SignalName.CurveChanged);
        }
    }

    private int _shapeStepLengthDivs = 3;
    [Export(PropertyHint.Range, "1,8")] public int ShapeStepLengthDivs
    {
        get => _shapeStepLengthDivs;
        set
        {
            _shapeStepLengthDivs = value;
            EmitSignal(Path3D.SignalName.CurveChanged);
        }
    }

    private int _shapeStepWidthDivs = 5;
    [Export(PropertyHint.Range, "1,8")] public int ShapeStepWidthDivs
    {
        get => _shapeStepWidthDivs;
        set
        {
            _shapeStepWidthDivs = value;
            EmitSignal(Path3D.SignalName.CurveChanged);
        }
    }

    private float _shapeSmoothness = 0.5f;
    [Export(PropertyHint.Range, "0.1,5.0")] public float ShapeSmoothness
    {
        get => _shapeSmoothness;
        set
        {
            _shapeSmoothness = value;
            EmitSignal(Path3D.SignalName.CurveChanged);
        }
    }

    #endregion

    #region Util

    private ShaderMaterial GetShaderMaterial()
    {
        return _meshInstance?.Mesh?.SurfaceGetMaterial(0) as ShaderMaterial;
    }

    private void GenerateRiver()
    {
        EnsureWidthsCurveValidity();

        if (_meshInstance == null)
        {
            return;
        }

        _steps = (int) Mathf.Max(1.0f, Mathf.Round(Curve.GetBakedLength() / RiverWidth));
        _meshInstance.Mesh = RiverGenerator.GenerateRiverMesh(Curve, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, ShapeSmoothness, RiverWidth);
        _meshInstance.Mesh.SurfaceSetMaterial(0, ShaderSettings.Material);
    }

    private void EnsureWidthsCurveValidity()
    {
        if (Curve != null && Curve.PointCount >= 2)
        {
            return;
        }

        var curve = new Curve3D
        {
            BakeInterval = 0.05f
        };

        curve.AddPoint(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
        curve.AddPoint(new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
        
        Curve = curve;
    }

    #endregion

    public RiverManager()
    {
        if (!IsConnected(Path3D.SignalName.CurveChanged, Callable.From(GenerateRiver)))
        {
            Connect(Path3D.SignalName.CurveChanged, Callable.From(GenerateRiver));
        }
    }

    public override void _Ready()
    {
        EnsureWidthsCurveValidity();

        if (ShaderSettings == null)
        {
            ShaderSettings = new RiverShaderSettings();
            ShaderSettings.SetDefaultProperties();
        }

        var generateMesh = true;
        foreach (var child in GetChildren())
        {
            if (child is not MeshInstance3D mesh || !mesh.HasMeta(RiverManagerStamp))
            {
                continue;
            }

            generateMesh = false;
            _meshInstance = mesh;
            ShaderSettings.Material = _meshInstance.Mesh.SurfaceGetMaterial(0) as ShaderMaterial;
            break;
        }

        if (generateMesh)
        {
            var newMeshInstance = new MeshInstance3D
            {
                Name = "RiverMeshInstance"
            };

            newMeshInstance.SetMeta(RiverManagerStamp, true);
            AddChild(newMeshInstance);
            _meshInstance = newMeshInstance;
            GenerateRiver();
        }
    }

    #if TOOLS

    protected override void Dispose(bool disposing)
    {
        if (IsConnected(Path3D.SignalName.CurveChanged, Callable.From(GenerateRiver)))
        {
            Disconnect(Path3D.SignalName.CurveChanged, Callable.From(GenerateRiver));
        }
    }

    #endif

    #region Public Actions

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
        var newMesh = (MeshInstance3D)_meshInstance.Duplicate();
        newMesh.GlobalTransform = _meshInstance.GlobalTransform;
        newMesh.MaterialOverride = null;
        return newMesh;
    }

    #endregion
}
