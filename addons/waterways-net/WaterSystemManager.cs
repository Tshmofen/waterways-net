using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Waterways;

public partial class WaterSystemManager : Node3D
{
	private ImageTexture _system_map;
    [Export] public ImageTexture system_map 
	{ 
		get => _system_map;
		set 
		{
            _system_map = value;
            if (_first_enter_tree)
                return;
            NotifyPropertyListChanged();
            UpdateConfigurationWarnings();
        } 
	}

	[Export] public int system_bake_resolution { get; set; } = 2;
	[Export] public string system_group_name { get; set; } = "waterways_system";
	[Export] public float minimum_water_level { get; set; }

	// Auto assign
	[Export] public string wet_group_name { get; set; } = "waterways_wet";
	[Export] public int surface_index { get; set; } = -1;
	[Export] public bool material_override { get; set; }

	private Aabb _system_aabb;
	private Image _system_img;
	private bool _first_enter_tree = true;

	public override void _EnterTree()
	{
		if (Engine.IsEditorHint() && _first_enter_tree)
		{
			_first_enter_tree = false;
		}
		AddToGroup(system_group_name);
	}

	public override void _Ready()
	{
		if (system_map != null)
			_system_img = system_map.GetImage();
		else
			GD.PushWarning("No WaterSystem map!");
	}


	public override void _ExitTree()
	{
		RemoveFromGroup(system_group_name);
    }

	public override string[] _GetConfigurationWarnings()
	{
		if (system_map == null)
			return ["No System Map is set. Select WaterSystem -> Generate System Map to generate and assign one."];
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

    public async Task generate_system_maps()
	{
		var rivers = new List<RiverManager>();
	
		foreach (var child in GetChildren())
        {
			if (child is RiverManager m)
				rivers.Add(m);
        }

		// We need to make the aabb out of the first river, so we don't include 0,0
		if (rivers.Count > 0)
			_system_aabb = rivers[0].get_transformed_aabb();
	
		foreach (var river in rivers)
		{
			var river_aabb = river.get_transformed_aabb();
			_system_aabb = _system_aabb.Merge(river_aabb);
        }

		var renderer = new SystemMapRenderer();
		AddChild(renderer);
		var resolution = (int) Mathf.Pow(2, system_bake_resolution + 7);
		var flow_map = await renderer.grab_flow(rivers, _system_aabb, resolution);
		var height_map = await renderer.grab_height(rivers, _system_aabb, resolution);
		var alpha_map = await renderer.grab_alpha(rivers, _system_aabb, resolution);
		RemoveChild(renderer);

		var filter_renderer = new FilterRenderer();
		AddChild(filter_renderer);
		system_map = await filter_renderer.apply_combine(flow_map, flow_map, height_map) as ImageTexture;
		RemoveChild(filter_renderer);

		// give the map and coordinates to all nodes in the wet_group
		var wet_nodes = GetTree().GetNodesInGroup(wet_group_name);
		foreach (var node in wet_nodes)
		{
			var mesh = (MeshInstance3D)node;
			ShaderMaterial material = null;
			if (surface_index != -1)
				if (mesh.GetSurfaceOverrideMaterialCount() > surface_index)
					material = mesh.GetSurfaceOverrideMaterial(surface_index) as ShaderMaterial;

			if (material_override)
				material = mesh.MaterialOverride as ShaderMaterial;
		
			if (material != null)
			{
				material.SetShaderParameter("water_systemmap", system_map);
				material.SetShaderParameter("water_systemmap_coords", get_system_map_coordinates());
            }
        }
    }

	// Returns the vetical distance to the water, positive values above water level,
	// negative numbers below the water
	public float get_water_altitude(Vector3 query_pos)
	{
		if (_system_img == null)
			return query_pos.Y - minimum_water_level;

		var position_in_aabb = query_pos - _system_aabb.Position;
		var pos_2d = new Vector2(position_in_aabb.X, position_in_aabb.Z);
		pos_2d = pos_2d / _system_aabb.GetLongestAxisSize();
		if (pos_2d.X > 1.0f || pos_2d.X < 0.0f || pos_2d.Y > 1.0f || pos_2d.Y < 0.0f)
		{
			// We are outside the aabb of the Water System
			return query_pos.Y - minimum_water_level;
		}

		pos_2d = pos_2d * _system_img.GetWidth();
		var col = _system_img.GetPixelv(new Vector2I((int) pos_2d.X, (int) pos_2d.Y));
		if (col == new Color(0, 0, 0))
		{
			// We hit the empty part of the System Map
			return query_pos.Y - minimum_water_level;
		}

		// Throw a warning if the map is not baked
		var height = col.B * _system_aabb.Size.Y + _system_aabb.Position.Y;
		return query_pos.Y - height;
	}

	// Returns the flow vector from the system flowmap
	public Vector3 get_water_flow(Vector3 query_pos)
	{

		if (_system_img == null)
			return Vector3.Zero;

		var position_in_aabb = query_pos - _system_aabb.Position;
		var pos_2d = new Vector2(position_in_aabb.X, position_in_aabb.Z);
		pos_2d = pos_2d / _system_aabb.GetLongestAxisSize();
		if (pos_2d.X > 1.0f || pos_2d.X < 0.0f || pos_2d.Y > 1.0f || pos_2d.Y < 0.0f)
			return Vector3.Zero;

		pos_2d = pos_2d * _system_img.GetWidth();
		var col = _system_img.GetPixelv(new Vector2I((int)pos_2d.X, (int)pos_2d.Y));
	
		if (col == new Color(0, 0, 0))
		{

			// We hit the empty part of the System Map
			return Vector3.Zero;
		}

		var flow = new Vector3(col.R, 0.5f, col.G) * 2.0f - new Vector3(1.0f, 1.0f, 1.0f);
		return flow;
    }

	public Transform3D get_system_map_coordinates()
	{
		// storing the AABB info in a transform, seems dodgy
		var offset = new Transform3D(_system_aabb.Position, _system_aabb.Size, _system_aabb.End, new Vector3());
		return offset;
	}
}