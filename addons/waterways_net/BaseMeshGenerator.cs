using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using Waterways.Util;

namespace Waterways;

public abstract partial class BaseMeshGenerator : Node3D
{    
    /// <summary> Is called when river update was initiated. </summary>
    [Signal] public delegate void MeshUpdatedEventHandler();

    private const string GeneratorStamp = "BaseMeshGenerator";
    private const string CreationStamp = "MeshGeneratorCreation";

    #if TOOLS
    private EditorNode3DGizmo _associatedGizmo;
    #endif

    private MeshInstance3D _meshInstance;
    private int _steps = 2;

    /// <summary> Mesh used for editor selection. Generated only if associated gizmo is assigned. </summary>
    public TriangleMesh SelectMesh { get; private set; }

    #region Export

    private int _shapeStepLengthDivs = 3;
    [Export(PropertyHint.Range, "1,8")]
    public int ShapeStepLengthDivs
    {
        get => _shapeStepLengthDivs;
        set
        {
            _shapeStepLengthDivs = value;
            UpdateMesh();
        }
    }

    private int _shapeStepWidthDivs = 5;
    [Export(PropertyHint.Range, "1,8")]
    public int ShapeStepWidthDivs
    {
        get => _shapeStepWidthDivs;
        set
        {
            _shapeStepWidthDivs = value;
            UpdateMesh();
        }
    }

    private float _shapeSmoothness = 0.5f;
    [Export(PropertyHint.Range, "0.1,5.0")]
    public float ShapeSmoothness
    {
        get => _shapeSmoothness;
        set
        {
            _shapeSmoothness = value;
            UpdateMesh();
        }
    }

    private Array<float> _pointWidths = [1, 1];
    [Export]
    public Array<float> PointWidths
    {
        get => _pointWidths;
        set
        {
            if (value == null)
            {
                return;
            }

            _pointWidths = value;
            UpdateMesh();
        }
    }

    private Curve3D _curve = new();
    [Export]
    public Curve3D Curve
    {
        get => _curve;
        set
        {
            if (value == null)
            {
                return;
            }

            _curve = value;
            UpdateMesh();
        }
    }


    #endregion

    #region Protected

    protected virtual ShaderMaterial CurrentShaderMaterial
    {
        get => _meshInstance?.Mesh?.SurfaceGetMaterial(0) as ShaderMaterial;
        set => _meshInstance?.Mesh?.SurfaceSetMaterial(0, value);
    }

    protected virtual void HandleMeshDuplication() { }

    #endregion

    #region Util

    private void MakeShaderMaterialUnique()
    {
        if (CurrentShaderMaterial == null)
        {
            return;
        }

        CurrentShaderMaterial = CurrentShaderMaterial.Duplicate() as ShaderMaterial;
    }

    private void EnsureWidthsCurveValidity()
    {
        if (Curve == null || Curve.PointCount < 2)
        {
            var curve = new Curve3D
            {
                BakeInterval = 0.05f,
                ResourceLocalToScene = true
            };

            curve.AddPoint(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
            curve.AddPoint(new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));

            Curve = curve;
        }

        PointWidths ??= [1, 1];
        if (PointWidths.Count != Curve.PointCount)
        {
            while (PointWidths.Count < Curve.PointCount)
            {
                PointWidths.Add(1f);
            }

            while (PointWidths.Count > Curve.PointCount)
            {
                PointWidths.RemoveAt(PointWidths.Count - 1);
            }
        }
    }

    private void GenerateActualMesh()
    {
        EnsureWidthsCurveValidity();

        if (_meshInstance == null)
        {
            return;
        }

        _steps = (int)Mathf.Max(1.0f, Mathf.Round(Curve.GetBakedLength() / PointWidths.Average()));
        _meshInstance.Mesh = MeshGeneratorHelper.GenerateCurvePlaneMesh(Curve, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, ShapeSmoothness, PointWidths);
        _meshInstance.Mesh.SurfaceSetMaterial(0, CurrentShaderMaterial);
    }

    private void UpdateMeshImmediate()
    {
        GenerateActualMesh();
        UpdateGizmos();

        #if TOOLS
        if (_associatedGizmo != null && _meshInstance.Mesh != null)
        {
            SelectMesh = _meshInstance.Mesh.GenerateTriangleMesh();
        }
        #endif

        EmitSignal(SignalName.MeshUpdated);
    }

    #endregion

    public override void _Ready()
    {
        EnsureWidthsCurveValidity();

        var generateMesh = true;
        foreach (var child in GetChildren())
        {
            if (child is not MeshInstance3D mesh || !mesh.HasMeta(GeneratorStamp))
            {
                continue;
            }

            generateMesh = false;
            _meshInstance = mesh;
            break;
        }

        if (generateMesh)
        {
            var newMeshInstance = new MeshInstance3D
            {
                Name = "BaseGeneratorMeshInstance",
            };

            newMeshInstance.SetMeta(GeneratorStamp, true);
            AddChild(newMeshInstance);
            _meshInstance = newMeshInstance;
            GenerateActualMesh();
        }

        if (Curve.GetMeta(CreationStamp, 0ul).AsUInt64() != GetInstanceId())
        {
            MakeShaderMaterialUnique();
            Curve = Curve.Duplicate() as Curve3D;
            HandleMeshDuplication();
        }
    }

    #region Public Action

    /// <summary> Forces deferred waterfall generation along with (in Editor) gizmos update and select mesh generation. </summary>
    public void UpdateMesh()
    {
        CallDeferred(MethodName.UpdateMeshImmediate);
    }

    #if TOOLS
    /// <summary> Only works in Editor, associates gizmo with the river to acknowledge that SelectMesh should be generated. Only one gizmo can be associated. </summary>
    public bool TryAssociateGizmo(EditorNode3DGizmo gizmo, bool forceAssociate = false)
    {
        if (!Engine.IsEditorHint())
        {
            return false;
        }

        _associatedGizmo ??= gizmo;

        // TODO: Workaround for gizmo duplication Godot bug
        if (forceAssociate && _associatedGizmo != gizmo)
        {
            _associatedGizmo = gizmo;
        }

        return _associatedGizmo == gizmo;
    }
    #endif

    /// <summary> Adds point to RiverManager Curve with width. Support adding for any index by inserting here, in case of PointCount insert (or -1) it will just be added to the end. </summary>
    public void AddPoint(Vector3 position, Vector3 direction, int index = -1, float width = -1)
    {
        if (index == -1 || index == Curve.PointCount)
        {
            var lastIndex = Curve.PointCount - 1;
            var distance = position.DistanceTo(Curve.GetPointPosition(lastIndex));
            var newDirection = (direction != Vector3.Zero) ? direction : (position - Curve.GetPointPosition(lastIndex) - Curve.GetPointOut(lastIndex)).Normalized() * 0.25f * distance;
            var newWidth = width > 0 ? width : PointWidths[^1]; // If this is a new point at the end, add a width that's the same as last

            Curve.AddPoint(position, -newDirection, newDirection);
            PointWidths.Add(newWidth);
        }
        else if (index == 0)
        {
            var distance = position.DistanceTo(Curve.GetPointPosition(0));
            var newDirection = (direction != Vector3.Zero) ? direction : (Curve.GetPointPosition(0) - position).Normalized() * 0.25f * distance;
            var newWidth = width > 0 ? width : PointWidths[0];

            Curve.AddPoint(position, -newDirection, newDirection, 0);
            PointWidths.Insert(0, newWidth);
        }
        else
        {
            var distance = Curve.GetPointPosition(index - 1).DistanceTo(Curve.GetPointPosition(index));
            var newDirection = (direction != Vector3.Zero) ? direction : (Curve.GetPointPosition(index) - Curve.GetPointPosition(index - 1)).Normalized() * 0.25f * distance;
            var newWidth = width > 0 ? width : (PointWidths[index - 1] + PointWidths[index]) / 2.0f; // We set the width to the average of the two surrounding widths

            Curve.AddPoint(position, -newDirection, newDirection, index);
            PointWidths.Insert(index, newWidth);
        }

        UpdateMesh();
    }

    /// <summary> Removes point and width at any given index, doesn't remove last 2 points. </summary>
    public void RemovePoint(int index)
    {
        if (Curve.PointCount <= 2)
        {
            return;
        }

        Curve.RemovePoint(index);
        PointWidths.RemoveAt(index);
        UpdateMesh();
    }

    /// <summary> Generates Mesh for collision generation or any other usages. Doesn't add it to the tree. </summary>
    public MeshInstance3D GetMeshCopy()
    {
        var newMesh = (MeshInstance3D)_meshInstance.Duplicate();
        newMesh.Name = $"{Name}Mesh";
        newMesh.GlobalTransform = _meshInstance.GlobalTransform;
        newMesh.MaterialOverride = null;
        return newMesh;
    }

    /// <summary> Moves node origin to the center of all Curve points, useful for better river manipulation. </summary>
    public void RecenterCurve()
    {
        var centerPoint = Vector3.Zero;
        var pointsGlobalPositions = new List<Vector3>();

        for (var point = 0; point < Curve.PointCount; point++)
        {
            var pointGlobalPosition = ToGlobal(Curve.GetPointPosition(point));
            centerPoint += pointGlobalPosition / Curve.PointCount;
            pointsGlobalPositions.Add(pointGlobalPosition);
        }

        GlobalPosition = centerPoint;

        for (var point = 0; point < Curve.PointCount; point++)
        {
            var oldPosition = ToLocal(pointsGlobalPositions[point]);
            Curve.SetPointPosition(point, oldPosition);
        }

        UpdateMesh();
    }

    #endregion
}
