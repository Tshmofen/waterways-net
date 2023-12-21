using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Waterways;

public partial class WaterSystemManager : Node3D
{
    private ImageTexture _systemMap;
    public ImageTexture SystemMap
    {
        get => _systemMap;
        set
        {
            _systemMap = value;
            if (_firstEnterTree)
            {
                return;
            }

            NotifyPropertyListChanged();
            UpdateConfigurationWarnings();
        }
    }

    public int SystemBakeResolution { get; set; } = 2;
    public string SystemGroupName { get; set; } = "waterways_system";
    public float MinimumWaterLevel { get; set; }

    // Auto assign
    public string WetGroupName { get; set; } = "waterways_wet";
    public int SurfaceIndex { get; set; } = -1;
    public bool MaterialOverride { get; set; }

    private Aabb _systemAabb;
    private Image _systemImg;
    private bool _firstEnterTree = true;

    public override void _EnterTree()
    {
        if (Engine.IsEditorHint() && _firstEnterTree)
        {
            _firstEnterTree = false;
        }
        AddToGroup(SystemGroupName);
    }

    public override void _Ready()
    {
        if (SystemMap != null)
        {
            _systemImg = SystemMap.GetImage();
        }
        else
        {
            GD.PushWarning("No WaterSystem map!");
        }
    }

    public override void _ExitTree()
    {
        RemoveFromGroup(SystemGroupName);
    }

    public override string[] _GetConfigurationWarnings()
    {
        if (SystemMap == null)
        {
            return ["No System Map is set. Select WaterSystem -> Generate System Map to generate and assign one."];
        }

        return [""];
    }

    public override Array<Dictionary> _GetPropertyList()
    {
        return [
            new Dictionary
            {
                { "name", "system_map" },
                { "type", (int) Variant.Type.Object },
                { "hint", (int) PropertyHint.ResourceType },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) },
                { "hint_string", "Texture2D" }
            },
            new Dictionary
            {
                { "name", "system_bake_resolution" },
                { "type", (int) Variant.Type.Int },
                { "hint", (int) PropertyHint.Enum },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) },
                { "hint_string", "128, 256, 512, 1024, 2048"}
            },
            new Dictionary
            {
                { "name", "system_group_name" },
                { "type", (int) Variant.Type.String },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) }
            },
            new Dictionary
            {
                { "name", "minimum_water_level" },
                { "type", (int) Variant.Type.Float },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) }
            },
            new Dictionary
            {
                { "name", "Auto assign texture & coordinates on generate" },
                { "type", (int) Variant.Type.Nil },
                { "usage", (int)(PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable) }
            },
            new Dictionary
            {
                { "name", "wet_group_name" },
                { "type", (int) Variant.Type.String },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) }
            },
            new Dictionary
            {
                { "name", "surface_index" },
                { "type", (int) Variant.Type.Int },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) }
            },
            new Dictionary
            {
                { "name", "material_override" },
                { "type", (int) Variant.Type.Bool },
                { "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable) }
            },
            // values that need to be serialized, but should not be exposed
            new Dictionary
            {
                { "name", "_system_aabb" },
                { "type", (int) Variant.Type.Aabb },
                { "usage", (int)(PropertyUsageFlags.Storage) }
            }
        ];
    }

    public async Task GenerateSystemMaps()
    {
        var rivers = new List<RiverManager>();

        foreach (var child in GetChildren())
        {
            if (child is RiverManager m)
            {
                rivers.Add(m);
            }
        }

        // We need to make the aabb out of the first river, so we don't include 0,0
        if (rivers.Count > 0)
        {
            _systemAabb = rivers[0].GetTransformedAabb();
        }

        foreach (var riverAabb in rivers.Select(river => river.GetTransformedAabb()))
        {
            _systemAabb = _systemAabb.Merge(riverAabb);
        }

        var renderer = new SystemMapRenderer();
        AddChild(renderer);
        var resolution = (int)Mathf.Pow(2, SystemBakeResolution + 7);
        var flowMap = await renderer.GrabFlow(rivers, _systemAabb);
        var heightMap = await renderer.GrabHeight(rivers, _systemAabb);
        RemoveChild(renderer);

        var filterRenderer = new FilterRenderer();
        AddChild(filterRenderer);
        SystemMap = await filterRenderer.ApplyCombine(flowMap, flowMap, heightMap) as ImageTexture;
        RemoveChild(filterRenderer);

        // give the map and coordinates to all nodes in the wet_group
        foreach (var node in GetTree().GetNodesInGroup(WetGroupName))
        {
            var mesh = (MeshInstance3D)node;
            ShaderMaterial material = null;
            if (SurfaceIndex != -1)
            {
                if (mesh.GetSurfaceOverrideMaterialCount() > SurfaceIndex)
                {
                    material = mesh.GetSurfaceOverrideMaterial(SurfaceIndex) as ShaderMaterial;
                }
            }

            if (MaterialOverride)
            {
                material = mesh.MaterialOverride as ShaderMaterial;
            }

            if (material != null)
            {
                material.SetShaderParameter("water_systemmap", SystemMap);
                material.SetShaderParameter("water_systemmap_coords", GetSystemMapCoordinates());
            }
        }
    }

    // Returns the vetical distance to the water, positive values above water level,
    // negative numbers below the water
    public float GetWaterAltitude(Vector3 queryPos)
    {
        if (_systemImg == null)
        {
            return queryPos.Y - MinimumWaterLevel;
        }

        var positionInAabb = queryPos - _systemAabb.Position;
        var pos2D = new Vector2(positionInAabb.X, positionInAabb.Z);
        pos2D /= _systemAabb.GetLongestAxisSize();

        if (pos2D.X > 1.0f || pos2D.X < 0.0f || pos2D.Y > 1.0f || pos2D.Y < 0.0f)
        {
            // We are outside the aabb of the Water System
            return queryPos.Y - MinimumWaterLevel;
        }

        pos2D *= _systemImg.GetWidth();
        var col = _systemImg.GetPixelv(new Vector2I((int)pos2D.X, (int)pos2D.Y));
        if (col == new Color(0, 0, 0))
        {
            // We hit the empty part of the System Map
            return queryPos.Y - MinimumWaterLevel;
        }

        // Throw a warning if the map is not baked
        var height = (col.B * _systemAabb.Size.Y) + _systemAabb.Position.Y;
        return queryPos.Y - height;
    }

    // Returns the flow vector from the system flowmap
    public Vector3 GetWaterFlow(Vector3 queryPos)
    {
        if (_systemImg == null)
        {
            return Vector3.Zero;
        }

        var positionInAabb = queryPos - _systemAabb.Position;
        var pos2D = new Vector2(positionInAabb.X, positionInAabb.Z);
        pos2D /= _systemAabb.GetLongestAxisSize();

        if (pos2D.X > 1.0f || pos2D.X < 0.0f || pos2D.Y > 1.0f || pos2D.Y < 0.0f)
        {
            return Vector3.Zero;
        }

        pos2D *= _systemImg.GetWidth();
        var col = _systemImg.GetPixelv(new Vector2I((int)pos2D.X, (int)pos2D.Y));

        if (col == new Color(0, 0, 0))
        {
            // We hit the empty part of the System Map
            return Vector3.Zero;
        }

        return (new Vector3(col.R, 0.5f, col.G) * 2.0f) - new Vector3(1.0f, 1.0f, 1.0f);
    }

    public Transform3D GetSystemMapCoordinates()
    {
        // storing the AABB info in a transform, seems dodgy
        return new Transform3D(_systemAabb.Position, _systemAabb.Size, _systemAabb.End, new Vector3());
    }
}
