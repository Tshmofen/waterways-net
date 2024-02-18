using Godot;
using System;
using System.Collections.Generic;

namespace Waterways;

[Tool]
public partial class RiverFloatSystem : Node3D
{
    private RiverManager _riverManager;
    private StaticBody3D _riverBody;
    private RayCast3D _rayCastUp;
    private RayCast3D _rayCastDown;

    [Export] public Vector3 RiverAreaPadding { get; set; } = new(0, 2, 0);
    [Export] public float CheckLength { get; set; } = 10f;
    [Export] public float DefaultHeight { get; set; } = 0;
    [Export(PropertyHint.Layers3DPhysics)] public uint FloatLayer { get; set; } = 256;

    #region Util

    private StaticBody3D GenerateCollisionBody(RiverManager riverManager)
    {
        var mesh = riverManager.GetMeshCopy();
        mesh.CreateTrimeshCollision();
        var body = mesh.GetChild<StaticBody3D>(0);
        body.CollisionMask = 0;
        body.CollisionLayer = FloatLayer;

        mesh.RemoveChild(body);
        mesh.QueueFree();

        return body;
    }

    private RayCast3D GenerateRayCast(Vector3 targetCheck)
    {
        return new RayCast3D
        {
            TargetPosition = targetCheck,
            CollisionMask = FloatLayer
        };
    }

    private bool TryGetRiverCollision(RayCast3D rayCast, Vector3 from, out float height)
    {
        rayCast.GlobalPosition = from;
        var collider = rayCast.GetCollider();

        if (collider == _riverBody)
        {
            height = rayCast.GetCollisionPoint().Y;
            return true;
        }

        height = DefaultHeight;
        return false;
    }

    private void GenerateFloatSystem()
    {
        if (_riverBody != null)
        {
            _riverBody.QueueFree();
            _rayCastUp.QueueFree();
            _rayCastDown.QueueFree();
        }

        AddChild(_riverBody = GenerateCollisionBody(_riverManager));
        AddChild(_rayCastUp = GenerateRayCast(Vector3.Up * CheckLength));
        AddChild(_rayCastDown = GenerateRayCast(Vector3.Down * CheckLength));
    }

    private static Vector3 GetGlobalFlowDirection(Node3D relativeNode, IReadOnlyList<Vector3> bakedPoints, int startIndex, int endIndex)
    {
        var startPoint = relativeNode.ToGlobal(bakedPoints[startIndex]);
        var endPoint = relativeNode.ToGlobal(bakedPoints[endIndex]);

        var direction = endPoint - startPoint;
        //direction.Y = 0;

        return direction.Normalized();
    }

    // .NET adaptation of these changes: https://github.com/godotengine/godot/pull/70977/commits/1319db2c9de7987821fbcdebda95e5bcc8ae4ae5
    // TODO: Replace with actual C++ function, once it is merged in Godot; or provide more optimized version later
    private static int GetClosestPointIndex(IReadOnlyList<Vector3> bakedPoints, Vector3 localPoint)
    {
        // brute force
        var nearestIndex = 0;
        var nearestDistance = -1.0f;

        for (var i = 0; i < bakedPoints.Count - 1; i++)
        {
            var interval = (bakedPoints[i + 1] - bakedPoints[i]).Length();
            var origin = bakedPoints[i];
            var direction = (bakedPoints[i + 1] - origin) / interval;

            var dot = Mathf.Clamp((localPoint - origin).Dot(direction), 0, interval);
            var projection = origin + (direction * dot);
            var dist = projection.DistanceSquaredTo(localPoint);

            if (nearestDistance >= 0.0 && dist >= nearestDistance)
            {
                continue;
            }

            nearestIndex = i;
            nearestDistance = dist;
        }

        return nearestIndex;
    }

    #endregion

    public override string[] _GetConfigurationWarnings()
    {
        if (GetParentOrNull<RiverManager>() == null)
        {
            return [$"{nameof(RiverFloatSystem)} must be a child of {nameof(RiverManager)}."];
        }

        return [];
    }

    public override void _EnterTree()
    {
        _riverManager = GetParentOrNull<RiverManager>();
        if (_riverManager != null && !Engine.IsEditorHint())
        {
            _riverManager.RiverChanged += GenerateFloatSystem;
        }
    }

    public override void _ExitTree()
    {
        if (_riverManager != null)
        {
            _riverManager.RiverChanged -= GenerateFloatSystem;
        }
    }

    public float GetWaterHeight(Vector3 globalPosition)
    {
        if (_riverBody == null)
        {
            return DefaultHeight;
        }

        if (TryGetRiverCollision(_rayCastDown, globalPosition, out var height))
        {
            return height;
        }

        if (TryGetRiverCollision(_rayCastUp, globalPosition, out height))
        {
            return height;
        }

        return DefaultHeight;
    }

    public Vector3 GetWaterFlow(Vector3 globalPosition)
    {
        // no river or not enough info for interpolation
        if (_riverManager == null || _riverManager.Curve.PointCount < 2)
        {
            return Vector3.Zero;
        }

        var curve = _riverManager.Curve;
        var bakedPoints = curve.GetBakedPoints();
        var closestIndex = GetClosestPointIndex(bakedPoints, _riverManager.ToLocal(globalPosition));

        return (closestIndex + 1 < bakedPoints.Length)
            ? GetGlobalFlowDirection(_riverManager, bakedPoints, closestIndex, closestIndex + 1)
            : GetGlobalFlowDirection(_riverManager, bakedPoints, closestIndex - 1, closestIndex);
    }
}