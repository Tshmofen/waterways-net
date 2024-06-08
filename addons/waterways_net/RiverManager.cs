﻿using Godot;
using Godot.Collections;
using System.Linq;
using Waterways.Data;
using Waterways.Util;

namespace Waterways;

[Tool]
public partial class RiverManager : Node3D
{
    [Signal] public delegate void RiverChangedEventHandler();

    public const string PluginBaseAlias = nameof(Node3D);
    public const string PluginNodeAlias = nameof(RiverManager);
    public const string ScriptPath = $"{nameof(RiverManager)}.cs";
    public const string IconPath = "river.svg";

    private const string RiverManagerStamp = "RiverManager";
    private const string RiverCreationStamp = "RiverCreation";

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
                _shaderSettings.Material = material;
                CallDeferred(MethodName.UpdateRiver);
            }
        }
    }

    [ExportCategory("Shape Settings")]

    private int _shapeStepLengthDivs = 3;
    [Export(PropertyHint.Range, "1,8")] public int ShapeStepLengthDivs
    {
        get => _shapeStepLengthDivs;
        set
        {
            _shapeStepLengthDivs = value;
            CallDeferred(MethodName.UpdateRiver);
        }
    }

    private int _shapeStepWidthDivs = 5;
    [Export(PropertyHint.Range, "1,8")] public int ShapeStepWidthDivs
    {
        get => _shapeStepWidthDivs;
        set
        {
            _shapeStepWidthDivs = value;
            CallDeferred(MethodName.UpdateRiver);
        }
    }

    private float _shapeSmoothness = 0.5f;
    [Export(PropertyHint.Range, "0.1,5.0")] public float ShapeSmoothness
    {
        get => _shapeSmoothness;
        set
        {
            _shapeSmoothness = value;
            CallDeferred(MethodName.UpdateRiver);
        }
    }

    private Array<float> _pointWidths = [ 1, 1 ];
    [Export] public Array<float> PointWidths
    {
        get => _pointWidths;
        set
        {
            if (value == null || value.Count != _pointWidths.Count)
            {
                return;
            }

            _pointWidths = value;
            CallDeferred(MethodName.UpdateRiver);
        }
    }

    private Curve3D _curve = new();
    [Export] public Curve3D Curve
    { 
        get => _curve;
        set
        {
            if (value == null)
            {
                return;
            }

            _curve = value;
            CallDeferred(MethodName.UpdateRiver);
        }
    }

    #endregion

    #region Util

    private ShaderMaterial GetShaderMaterial()
    {
        return _meshInstance?.Mesh?.SurfaceGetMaterial(0) as ShaderMaterial;
    }

    private void MakeShaderMaterialUnique()
    {
        var shader = GetShaderMaterial();

        if (shader == null)
        {
            return;
        }

        _meshInstance.Mesh.SurfaceSetMaterial(0, shader.Duplicate() as ShaderMaterial);
    }

    private void GenerateRiver()
    {
        EnsureWidthsCurveValidity();

        if (_meshInstance == null)
        {
            return;
        }

        _steps = (int) Mathf.Max(1.0f, Mathf.Round(Curve.GetBakedLength() / PointWidths.Average()));
        _meshInstance.Mesh = RiverGenerator.GenerateRiverMesh(Curve, _steps, ShapeStepLengthDivs, ShapeStepWidthDivs, ShapeSmoothness, PointWidths);
        _meshInstance.Mesh.SurfaceSetMaterial(0, ShaderSettings.Material);
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

    #endregion

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
            ShaderSettings.Material = GetShaderMaterial();
            break;
        }

        if (generateMesh)
        {
            var newMeshInstance = new MeshInstance3D
            {
                Name = "RiverMeshInstance",
            };

            newMeshInstance.SetMeta(RiverManagerStamp, true);
            AddChild(newMeshInstance);
            _meshInstance = newMeshInstance;
            GenerateRiver();
        }

        if (Curve.GetMeta(RiverCreationStamp, 0ul).AsUInt64() != GetInstanceId())
        {
            ShaderSettings = ShaderSettings.Duplicate() as RiverShaderSettings;
            MakeShaderMaterialUnique();
            _shaderSettings.Material = GetShaderMaterial();
            Curve = Curve.Duplicate() as Curve3D;
        }
    }

    #region Public Actions

    public void AddPoint(Vector3 position, Vector3 direction, int index = -1, float width = -1)
    {
        if (index == -1)
        {
            var lastIndex = Curve.PointCount - 1;
            var distance = position.DistanceTo(Curve.GetPointPosition(lastIndex));
            var newDirection = (direction != Vector3.Zero) ? direction : (position - Curve.GetPointPosition(lastIndex) - Curve.GetPointOut(lastIndex)).Normalized() * 0.25f * distance;
            var newWidth = width > 0 ? width : PointWidths[^1]; // If this is a new point at the end, add a width that's the same as last

            Curve.AddPoint(position, -newDirection, newDirection);
            PointWidths.Add(newWidth);
        }
        else
        {
            var distance = Curve.GetPointPosition(index).DistanceTo(Curve.GetPointPosition(index + 1));
            var newDirection = (direction != Vector3.Zero) ? direction : (Curve.GetPointPosition(index + 1) - Curve.GetPointPosition(index)).Normalized() * 0.25f * distance;
            var newWidth = width > 0 ? width : (PointWidths[index] + PointWidths[index + 1]) / 2.0f; // We set the width to the average of the two surrounding widths

            Curve.AddPoint(position, -newDirection, newDirection, index + 1);
            PointWidths.Insert(index + 1, newWidth);
        }

        UpdateRiver();
    }

    public void RemovePoint(int index)
    {
        if (Curve.PointCount <= 2)
        {
            return;
        }

        Curve.RemovePoint(index);
        PointWidths.RemoveAt(index);
        UpdateRiver();
    }

    public MeshInstance3D GetMeshCopy()
    {
        var newMesh = (MeshInstance3D)_meshInstance.Duplicate();
        newMesh.GlobalTransform = _meshInstance.GlobalTransform;
        newMesh.MaterialOverride = null;
        return newMesh;
    }

    public void UpdateRiver()
    {
        GenerateRiver();
        EmitSignal(SignalName.RiverChanged);
    }

    #endregion
}
