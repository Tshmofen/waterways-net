using Godot;
using Waterways.Data;
using Waterways.Utils;

namespace Waterways;

[Tool]
public partial class RiverManager : Path3D
{
    public const string PluginBaseAlias = nameof(Path3D);
    public const string PluginNodeAlias = nameof(RiverManager);

    private const string RiverManagerStamp = "RiverManager";
    private MeshInstance3D _meshInstance;
    private int _steps = 2;

    [Export] public RiverShaderSettings ShaderSettings { get; set; }
    [Export] public float RiverWidth { get; set; } = 3f;
    [Export(PropertyHint.Range, "1,8")] public int ShapeStepLengthDivs { get; set; } = 1;
    [Export(PropertyHint.Range, "1,8")] public int ShapeStepWidthDivs { get; set; } = 1;
    [Export(PropertyHint.Range, "0.1,5.0")] public float ShapeSmoothness { get; set; } = 0.5f;

    #region Util

    private void GenerateRiver()
    {
        if (_meshInstance == null)
        {
            return;
        }

        _steps = (int)(Mathf.Max(1.0f, Mathf.Round(Curve.GetBakedLength() / RiverWidth)));
        _meshInstance.Mesh = RiverGenerator.GenerateRiverMesh(Curve, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, ShapeSmoothness, RiverWidth);
        _meshInstance.Mesh.SurfaceSetMaterial(0, ShaderSettings.Material);
    }

    private void EnsureWidthsCurveValidity()
    {
        Curve ??= new Curve3D
        {
            BakeInterval = 0.05f
        };

        if (Curve.PointCount < 2)
        {
            Curve.ClearPoints();
            Curve.AddPoint(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
            Curve.AddPoint(new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
        }
    }

    #endregion

    public override void _EnterTree()
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

        CurveChanged += GenerateRiver;
    }

    public override void _ExitTree()
    {
        CurveChanged -= GenerateRiver;
    }

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
