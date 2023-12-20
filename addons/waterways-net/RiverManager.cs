using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Waterways;

[Tool]
public partial class RiverManager : Node3D
{
	private class RiverShader
	{ 
		public string name { get; set; }
		public string shader_path { get; set; }
		public List<(string name, string path)> texture_paths { get; set; }
	}

	private const string FILTER_RENDERER_PATH = "res://addons/waterways/filter_renderer.tscn";
    private const string FLOW_OFFSET_NOISE_TEXTURE_PATH = "res://addons/waterways/textures/flow_offset_noise.png";
    private const string FOAM_NOISE_PATH = "res://addons/waterways/textures/foam_noise.png";

    private static class MATERIAL_CATEGORIES 
	{
		public const string albedo_ = "Albedo";
		public const string emission_ = "Emission";
		public const string transparency_ = "Transparency";
		public const string flow_ = "Flow";
		public const string foam_ = "Foam";
		public const string custom_ = "Custom";
    };

    public enum SHADER_TYPES 
	{
		WATER,
		LAVA, 
		CUSTOM
	}

	private static readonly List<RiverShader> BUILTIN_SHADERS = [
		new RiverShader
        {
			name = "Water",
			shader_path = "res://addons/waterways/shaders/river.gdshader",
			texture_paths = [
				("normal_bump_texture", "res://addons/waterways/textures/water1_normal_bump.png")
			]
		},
		new RiverShader
        {
			name = "Lava",
			shader_path = "res://addons/waterways/shaders/lava.gdshader",
			texture_paths = [
				("normal_bump_texture", "res://addons/waterways/textures/lava_normal_bump.png"),
				("emission_texture", "res://addons/waterways/textures/lava_emission.png")
			]
		}
	];

	private static readonly RiverShader DEBUG_SHADER = new()
	{
		name = "Debug",
		shader_path = "res://addons/waterways/shaders/river_debug.gdshader",
		texture_paths = [
			("debug_pattern", "res://addons/waterways/textures/debug_pattern.png"),
			("debug_arrow", "res://addons/waterways/textures/debug_arrow.svg")
		]
	};

    private static class DEFAULT_PARAMETERS 
	{
		public const int shape_step_length_divs = 1;
		public const int shape_step_width_divs = 1;
		public const float shape_smoothness = 0.5f;
		public const int mat_shader_type = 0;
		public const RiverShader mat_custom_shader = null;
		public const int baking_resolution = 2;
		public const float baking_raycast_distance = 10.0f;
		public const int baking_raycast_layers = 1;
		public const float baking_dilate = 0.6f;
		public const float baking_flowmap_blur = 0.04f;
		public const float baking_foam_cutoff = 0.9f;
		public const float baking_foam_offset = 0.1f;
		public const float baking_foam_blur = 0.02f;
		public const float lod_lod0_distance = 50.0f;
    }

	// Shape Properties
	private int _shape_step_length_divs = 1;
    [Export] public int shape_step_length_divs
	{
		get => _shape_step_length_divs;
		set => set_step_length_divs(value);
	}

    private int _shape_step_width_divs = 1;
    [Export] public int shape_step_width_divs
    {
        get => _shape_step_width_divs;
        set => set_step_width_divs(value);
    }

    private float _shape_smoothness = 0.5f;
    [Export] public float shape_smoothness
    {
        get => _shape_smoothness;
        set => set_smoothness(value);
    }

	// Material Properties that not handled in shader
	private SHADER_TYPES _mat_shader_type;
    [Export] public SHADER_TYPES mat_shader_type
    {
        get => _mat_shader_type;
        set => set_shader_type(value);
    }

    private Shader _mat_custom_shader;
    [Export] public Shader mat_custom_shader
    {
        get => _mat_custom_shader;
        set => set_custom_shader(value);
    }

	// LOD Properties

	private float _lod_lod0_distance = 50.0f;
    [Export] public float lod_lod0_distance
    {
        get => _lod_lod0_distance;
        set => set_lod0_distance(value);
    }

	// Bake Properties
	[Export] public int baking_resolution { get; set; } = 2;
	[Export] public float baking_raycast_distance { get; set; } = 10.0f;
	[Export] public int baking_raycast_layers { get; set; } = 1;
	[Export] public float baking_dilate { get; set; } = 0.6f;
	[Export] public float baking_flowmap_blur { get; set; } = 0.04f;
	[Export] public float baking_foam_cutoff { get; set; } = 0.9f;
	[Export] public float baking_foam_offset { get; set; } = 0.1f;
	[Export] public float baking_foam_blur { get; set; } = 0.02f;

    // Public variables
    [Export] public Curve3D curve { get; set; }
    [Export] public bool valid_flowmap { get; set; }
    [Export] public MeshInstance3D mesh_instance { get; set; }
    [Export] public Texture2D flow_foam_noise { get; set; }
    [Export] public Texture2D dist_pressure { get; set; }

	private Array<float> _widths = [1.0f, 1.0f];
    [Export] public Array<float> widths
	{ 
		get => _widths; 
		set
		{
            _widths = value;
            if (_first_enter_tree)
                return;
            _generate_river();
        }
	}

	private int _debug_view;
    [Export] public int debug_view
    {
        get => _debug_view;
        set 
		{
            _debug_view = value;
            if (value == 0)
            {
                mesh_instance.MaterialOverride = null;
            }
            else
            {
                _debug_material.SetShaderParameter("mode", value);
                mesh_instance.MaterialOverride = _debug_material;
            }
        }
    }

	// Private variables
	private int _steps = 2;
	private SurfaceTool _st;
	private MeshDataTool _mdt;
	private ShaderMaterial _debug_material;
	private bool _first_enter_tree = true;
	private PackedScene _filter_renderer;

	// Serialised private variables
	private ShaderMaterial _material;
	private int _selected_shader = (int)SHADER_TYPES.WATER;
	private int _uv2_sides;

	// river_changed used to update handles when values are changed on script side
	// progress_notified used to up progress bar when baking maps
	// albedo_set is needed since the gradient is a custom inspector that needs a signal to update from script side
	[Signal] public delegate void river_changedEventHandler();
	[Signal] public delegate void progress_notifiedEventHandler();

    // Internal Methods
    public override Array<Dictionary> _GetPropertyList()
	{
		var props = new Array<Dictionary> 
		{
			new Dictionary
			{
				{ "name", "Shape" },
				{ "type", (int) Variant.Type.Nil },
				{ "hint_string", "shape_" },
				{ "usage", (int)(PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "shape_step_length_divs" },
				{ "type", (int) Variant.Type.Int },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "1, 8" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "shape_step_width_divs" },
				{ "type", (int) Variant.Type.Int },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "1, 8" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "shape_smoothness" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "0.1, 5.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "Material" },
				{ "type", (int) Variant.Type.Nil },
				{ "hint_string", "mat_" },
				{ "usage", (int)(PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "mat_shader_type" },
				{ "type", (int) Variant.Type.Int },
				{ "hint", (int) PropertyHint.Enum },
				{ "hint_string", "Water, Lava, Custom" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "mat_custom_shader" },
				{ "type", (int) Variant.Type.Object },
				{ "hint", (int) PropertyHint.ResourceType },
				{ "hint_string", "Shader" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			}
		};

		var mat_categories = new Godot.Collections.Dictionary<string, string>
        {
			{ nameof(MATERIAL_CATEGORIES.albedo_), MATERIAL_CATEGORIES.albedo_},
			{ nameof(MATERIAL_CATEGORIES.emission_), MATERIAL_CATEGORIES.emission_},
			{ nameof(MATERIAL_CATEGORIES.transparency_), MATERIAL_CATEGORIES.transparency_},
			{ nameof(MATERIAL_CATEGORIES.flow_), MATERIAL_CATEGORIES.flow_},
			{ nameof(MATERIAL_CATEGORIES.foam_), MATERIAL_CATEGORIES.foam_},
			{ nameof(MATERIAL_CATEGORIES.custom_), MATERIAL_CATEGORIES.custom_}
		};

		var props2 = new Array<Dictionary>();
	
		if (_material.Shader != null)
		{
			var shader_params = RenderingServer.GetShaderParameterList(_material.Shader.GetRid());
		
			foreach (var p in shader_params)
			{
				if (p["name"].AsString().StartsWith("i_"))
					continue;

				string hit_category = null;
				foreach (var category in mat_categories)
				{
					if (p["name"].AsString().StartsWith(category.Key))
					{
                        

                        props2.Add(new Dictionary
                        {
                            { "name", "Material/" + category.Value},
                            { "type", (int)Variant.Type.Nil },
                            { "hint_string", "mat_" + category.Key },
                            { "usage", (int)(PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable)}
                        });

						hit_category = category.Key;
						break;
                    }
                }

				if (hit_category != null)
					mat_categories.Remove(hit_category);

				var cp = new Dictionary();
				foreach (var k in p)
				{
					cp[k.Key] = p[k.Key];
				}

				cp["name"] = "mat_" + p["name"];
				if (cp["name"].AsString().Contains("curve"))
				{
					cp["hint"] = (int) PropertyHint.ExpEasing;
					cp["hint_string"] = "EASE";
				}

				props2.Add(cp);
            }
        }

		var props3 = new Array<Dictionary>
		{
			new Dictionary
			{
				{ "name", "Lod" },
				{ "type", (int) Variant.Type.Nil },
				{ "hint_string", "lod_" },
				{ "usage", (int)(PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "lod_lod0_distance" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "5.0, 200.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "Baking" },
				{ "type", (int) Variant.Type.Nil },
				{ "hint_string", "baking_" },
				{ "usage", (int)(PropertyUsageFlags.Group | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_resolution" },
				{ "type", (int) Variant.Type.Int },
				{ "hint", (int) PropertyHint.Enum },
				{ "hint_string", "64, 128, 256, 512, 1024" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_raycast_distance" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "0.0, 100.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_raycast_layers" },
				{ "type", (int) Variant.Type.Int },
				{ "hint", (int) PropertyHint.Layers3DPhysics },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_dilate" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "0.0, 1.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_flowmap_blur" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "0.0, 1.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_foam_cutoff" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "0.0, 1.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_foam_offset" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "0.0, 1.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			new Dictionary
			{
				{ "name", "baking_foam_blur" },
				{ "type", (int) Variant.Type.Float },
				{ "hint", (int) PropertyHint.Range },
				{ "hint_string", "0.0, 1.0" },
				{ "usage", (int)(PropertyUsageFlags.Default | PropertyUsageFlags.ScriptVariable)}
			},
			// Serialize these values without exposing it in the inspector
            new Dictionary
			{
				{ "name", "curve" },
				{ "type", (int) Variant.Type.Object },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			},
			new Dictionary
			{
				{ "name", "widths" },
				{ "type", (int) Variant.Type.Array },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			},
			new Dictionary
			{
				{ "name", "valid_flowmap" },
				{ "type", (int) Variant.Type.Bool },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			},
			new Dictionary
			{
				{ "name", "flow_foam_noise" },
				{ "type", (int) Variant.Type.Object },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			},
			new Dictionary
			{
				{ "name", "dist_pressure" },
				{ "type", (int) Variant.Type.Object },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			},
			new Dictionary
			{
				{ "name", "_material" },
				{ "type", (int) Variant.Type.Object },
				{ "hint", (int) PropertyHint.ResourceType },
				{ "hint_string", "ShaderMaterial" },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			},
			new Dictionary
			{
				{ "name", "_selected_shader" },
				{ "type", (int) Variant.Type.Int },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			},
			new Dictionary
			{
				{ "name", "_uv2_sides" },
				{ "type", (int) Variant.Type.Int },
				{ "usage", (int)(PropertyUsageFlags.Storage)}
			}
		};

		var combined_props = props + props2 + props3;
		return combined_props;
	}

	// todo maybe should be _ready
    public RiverManager()
	{
		_st = new SurfaceTool();
		_mdt = new MeshDataTool();
		_filter_renderer = ResourceLoader.Load(FILTER_RENDERER_PATH) as PackedScene;

		_debug_material = new ShaderMaterial();
		_debug_material.Shader = ResourceLoader.Load(DEBUG_SHADER.shader_path) as Shader;
		foreach (var texture in DEBUG_SHADER.texture_paths)
		{
			_debug_material.SetShaderParameter(texture.name, ResourceLoader.Load(texture.path) as Texture2D);
        }

		_material = new ShaderMaterial();
		_material.Shader = ResourceLoader.Load(BUILTIN_SHADERS.First(s => s.name == mat_shader_type.ToString()).shader_path) as Shader;
		foreach (var texture in BUILTIN_SHADERS.First(s => s.name == mat_shader_type.ToString()).texture_paths)
		{
			_material.SetShaderParameter(texture.name, ResourceLoader.Load(texture.path) as Texture2D);
		}

		// Have to manually set the color or it does not default right. Not sure how to work around this
		_material.SetShaderParameter("albedo_color", new Transform3D(new Vector3(0.0f, 0.8f, 1.0f), new Vector3(0.15f, 0.2f, 0.5f), Vector3.Zero, Vector3.Zero));
    }

	public override void _EnterTree()
	{
		if (Engine.IsEditorHint() && _first_enter_tree)
			_first_enter_tree = false;
	
		if (curve == null)
		{
			curve = new Curve3D();
			curve.BakeInterval = 0.05f;
			curve.AddPoint(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
			curve.AddPoint(new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, 0.0f, -0.25f), new Vector3(0.0f, 0.0f, 0.25f));
		}
	
		if (GetChildCount() <= 0)
		{
			// This is what happens on creating a new river
			var new_mesh_instance = new MeshInstance3D();
			new_mesh_instance.Name = "RiverMeshInstance";
			AddChild(new_mesh_instance);
			mesh_instance = GetChild(0) as MeshInstance3D;
			_generate_river();
		}
		else
		{
			mesh_instance = GetChild(0) as MeshInstance3D;
			_material = mesh_instance.Mesh.SurfaceGetMaterial(0) as ShaderMaterial;
		}

		set_materials("i_valid_flowmap", valid_flowmap);
		set_materials("i_uv2_sides", _uv2_sides);
		set_materials("i_distmap", dist_pressure);
		set_materials("i_flowmap", flow_foam_noise);
		set_materials("i_texture_foam_noise", ResourceLoader.Load(FOAM_NOISE_PATH) as Texture2D);
    }

    public override string[] _GetConfigurationWarnings()
	{
		if (valid_flowmap)
			return [""];

		return ["No flowmap is set. Select River -> Generate Flow & Foam Map to generate and assign one."];
    }

	// TODO: should overwrite?
	public Aabb  get_transformed_aabb()
	{
		return GlobalTransform * mesh_instance.GetAabb();
    }

	// Public Methods - These should all be good to use as API from other scripts
	public void add_point(Vector3 position, int index, Vector3 dir, float width = 0.0f)
	{
		var new_width = 0f;

		if (index == -1)
		{
			var last_index = curve.PointCount - 1;
			var dist = position.DistanceTo(curve.GetPointPosition(last_index));
			var new_dir = (dir != Vector3.Zero) ? dir : (position - curve.GetPointPosition(last_index) - curve.GetPointOut(last_index)).Normalized() * 0.25f * dist;
			curve.AddPoint(position, -new_dir, new_dir, -1);
			widths.Add(widths[widths.Count - 1]); // If this is a new point at the end, add a width that's the same as last
        }
        else
		{
			var dist = curve.GetPointPosition(index).DistanceTo(curve.GetPointPosition(index + 1));
			var new_dir = (dir != Vector3.Zero) ? dir : (curve.GetPointPosition(index + 1) - curve.GetPointPosition(index)).Normalized() * 0.25f * dist;
			curve.AddPoint(position, -new_dir, new_dir, index + 1);
			new_width = width != 0.0f ? width : (widths[index] + widths[index + 1]) / 2.0f;
        }

		widths.Insert(index + 1, new_width); // We set the width to the average of the two surrounding widths
		EmitSignal("river_changed");
		_generate_river();
    }

	public void remove_point(int index)
	{
		// We don't allow rivers shorter than 2 points
		if (curve.PointCount <= 2)
			return;
		curve.RemovePoint(index);
		widths.RemoveAt(index);
		EmitSignal("river_changed");
		_generate_river();
    }

    public void bake_texture()
	{
		_generate_river();
		_generate_flowmap(Mathf.Pow(2, 6 + baking_resolution));
    }

    public void set_curve_point_position(int index, Vector3 position)
	{
		curve.SetPointPosition(index, position);
		_generate_river();
	}

    public void set_curve_point_in(int index, Vector3 position)
	{
		curve.SetPointIn(index, position);
		_generate_river();
	}

    public void set_curve_point_out(int index, Vector3 position)
	{
		curve.SetPointOut(index, position);
		_generate_river();
	}

    public void set_materials(string param, Variant value)
	{
		_material.SetShaderParameter(param, value);
		_debug_material.SetShaderParameter(param, value);
    }

	public void spawn_mesh()
	{
		if (Owner == null)
		{
			GD.PushWarning("Cannot create MeshInstance3D sibling when River is root.");
			return;
        }

		// TODO: was using true (1) ?
		var sibling_mesh = (MeshInstance3D) mesh_instance.Duplicate((int) DuplicateFlags.Signals);
		GetParent().AddChild(sibling_mesh);
		sibling_mesh.Owner = GetTree().EditedSceneRoot;
		sibling_mesh.Position = Position;
		sibling_mesh.MaterialOverride = null;
    }

    public List<Vector3> get_curve_points()
	{
		var points = new List<Vector3>();

		for (var p = 0; p < curve.PointCount; p++)
        {
			points.Add(curve.GetPointPosition(p));
        }

		return points;
	}

	public int get_closest_point_to(Vector3 point)
	{
		var points = new List<Vector3>();
		var closest_distance = 4096.0f;
		var closest_index = -1;

		for (var p = 0; p < curve.PointCount; p++)
		{
			var dist = point.DistanceTo(curve.GetPointPosition(p));
			if (dist < closest_distance)
			{
				closest_distance = dist;
				closest_index = p;
            }
		}

		return closest_index;
    }

    public Variant get_shader_parameter(string param)
	{
		return _material.GetShaderParameter(param);
    }

	// Parameter Setters
	public void set_step_length_divs(int value)
	{
		shape_step_length_divs = value;
		if (_first_enter_tree)
			return;
		valid_flowmap = false;
		set_materials("i_valid_flowmap", valid_flowmap);
		_generate_river();
		EmitSignal("river_changed");
	}

    public void set_step_width_divs(int value)
	{
		shape_step_width_divs = value;
		if (_first_enter_tree)
			return;
		valid_flowmap = false;
		set_materials("i_valid_flowmap", valid_flowmap);
		_generate_river();
		EmitSignal("river_changed");
	}

    public void set_smoothness(float value)
	{
		shape_smoothness = value;
		if (_first_enter_tree)
			return;
		valid_flowmap = false;
		set_materials("i_valid_flowmap", valid_flowmap);
		_generate_river();
		EmitSignal("river_changed");
	}

    public void set_shader_type(SHADER_TYPES type)
	{
		if (type == mat_shader_type)
			return;

		mat_shader_type = type;
	
		if (mat_shader_type == SHADER_TYPES.CUSTOM)
		{
			_material.Shader = mat_custom_shader;
		}
		else
		{
			_material.Shader = ResourceLoader.Load(BUILTIN_SHADERS.First(s => s.name == mat_shader_type.ToString()).shader_path) as Shader;
			foreach (var texture in BUILTIN_SHADERS.First(s => s.name == mat_shader_type.ToString()).texture_paths)
			{
				_material.SetShaderParameter(texture.name,  ResourceLoader.Load(texture.path) as Texture);
            }
		}

		NotifyPropertyListChanged();
    }

    public void set_custom_shader(Shader shader)
	{
		if (mat_custom_shader == shader)
			return;

		mat_custom_shader = shader;

		if (mat_custom_shader != null)
		{
			_material.Shader = mat_custom_shader;
		
			if (Engine.IsEditorHint())
			{
				// Ability to fork default shader
				if (shader.Code == "")
				{
					var selected_shader = ResourceLoader.Load(BUILTIN_SHADERS.First(s => s.name == mat_shader_type.ToString()).shader_path) as Shader;
					shader.Code = selected_shader.Code;
				}
			}
		}

		if (shader != null)
		{
			//print("shader != null - set shader type to custom");
			//print(shader)
			set_shader_type(SHADER_TYPES.CUSTOM);
		}
		else
		{
			set_shader_type(SHADER_TYPES.WATER);
		}
    }

    public void set_lod0_distance(float value)
	{
		lod_lod0_distance = value;
		set_materials("i_lod0_distance", value);
    }

	// Private Methods
	private void _generate_river()
	{
		var average_width = WaterHelperMethods.sum_array(widths) / (float)(widths.Count / 2);
		_steps = (int)(Mathf.Max(1.0f, Mathf.Round(curve.GetBakedLength() / average_width)));

		var river_width_values = WaterHelperMethods.generate_river_width_values(curve, _steps, shape_step_length_divs, shape_step_width_divs, widths);
		mesh_instance.Mesh = WaterHelperMethods.generate_river_mesh(curve, _steps, shape_step_length_divs, shape_step_width_divs, shape_smoothness, river_width_values);
		mesh_instance.Mesh.SurfaceSetMaterial(0, _material);
	}

	private async Task _generate_flowmap(float flowmap_resolution)
	{
		// WaterHelperMethods.reset_all_colliders(get_tree().root)
		var image = Image.Create( (int)flowmap_resolution, (int)flowmap_resolution, true, Image.Format.Rgb8);
		image.Fill(new Color(0, 0, 0));

		EmitSignal("progress_notified", 0.0f, "Calculating Collisions (" + flowmap_resolution + "x" + flowmap_resolution + ")");
		await ToSignal(GetTree(), "process_frame");

		image = await WaterHelperMethods.generate_collisionmap(image, mesh_instance, baking_raycast_distance, baking_raycast_layers, _steps, shape_step_length_divs, shape_step_width_divs, this);

		EmitSignal("progress_notified", 0.95f, "Applying filters (" + flowmap_resolution + "x" + flowmap_resolution + ")");
		await ToSignal(GetTree(), "process_frame");

		// Calculate how many colums are in UV2
		_uv2_sides = WaterHelperMethods.calculate_side(_steps);

		var margin = (int)(Mathf.Round(flowmap_resolution / (float)(_uv2_sides)));

		image = WaterHelperMethods.add_margins(image, (int)flowmap_resolution, margin);

		var collision_with_margins = ImageTexture.CreateFromImage(image);

		// Create correctly tiling noise for A channel
		var noise_texture = ResourceLoader.Load(FLOW_OFFSET_NOISE_TEXTURE_PATH) as Texture2D;
		var noise_with_margin_size = (float)(_uv2_sides + 2) * ((float)(noise_texture.GetWidth()) / (float)(_uv2_sides));
		var noise_with_tiling = Image.Create((int)noise_with_margin_size, (int)noise_with_margin_size, false, Image.Format.Rgb8);
		var slice_width = (float)(noise_texture.GetWidth()) / (float)(_uv2_sides);

		for (var x = 0; x <_uv2_sides; x++)
		{
			noise_with_tiling.BlendRect(noise_texture.GetImage(), new Rect2I(0, 0, (int) slice_width, noise_texture.GetHeight()), new Vector2I((int)(slice_width + (float)(x) * slice_width), (int)(slice_width - (noise_texture.GetWidth() / 2.0f))));
			noise_with_tiling.BlendRect(noise_texture.GetImage(), new Rect2I(0, 0, (int) slice_width, noise_texture.GetHeight()), new Vector2I((int)(slice_width + (float)(x) * slice_width), (int)(slice_width + (noise_texture.GetWidth() / 2.0f))));
        }

		var tiled_noise = ImageTexture.CreateFromImage(noise_with_tiling);

		// Create renderer
		var renderer_instance = _filter_renderer.Instantiate<FilterRenderer>();

		AddChild(renderer_instance);

		var flow_pressure_blur_amount = 0.04f / (float)(_uv2_sides) * flowmap_resolution;
		var dilate_amount = baking_dilate / (float)(_uv2_sides);
		var flowmap_blur_amount = baking_flowmap_blur / (float)(_uv2_sides) * flowmap_resolution;
		var foam_offset_amount = baking_foam_offset / (float)(_uv2_sides);
		var foam_blur_amount = baking_foam_blur / (float)(_uv2_sides) * flowmap_resolution;

		var flow_pressure_map = await renderer_instance.apply_flow_pressure(collision_with_margins, flowmap_resolution, _uv2_sides + 2.0f);
		var blurred_flow_pressure_map = await renderer_instance.apply_vertical_blur(flow_pressure_map, flow_pressure_blur_amount, flowmap_resolution + margin * 2);
		var dilated_texture = await renderer_instance.apply_dilate(collision_with_margins, dilate_amount, 0.0f, flowmap_resolution + margin * 2);
		var normal_map = await renderer_instance.apply_normal(dilated_texture, flowmap_resolution + margin * 2);
		var flow_map = await renderer_instance.apply_normal_to_flow(normal_map, flowmap_resolution + margin * 2);
		var blurred_flow_map = await renderer_instance.apply_blur(flow_map, flowmap_blur_amount, flowmap_resolution + margin * 2);
		var foam_map = await renderer_instance.apply_foam(dilated_texture, foam_offset_amount, baking_foam_cutoff, flowmap_resolution + margin * 2);
		var blurred_foam_map = await renderer_instance.apply_blur(foam_map, foam_blur_amount, flowmap_resolution + margin * 2);
		var flow_foam_noise_img = await renderer_instance.apply_combine(blurred_flow_map, blurred_flow_map, blurred_foam_map, tiled_noise);
		var dist_pressure_img = await renderer_instance.apply_combine(dilated_texture, blurred_flow_pressure_map);

		// Debug texture gen
		//	flow_pressure_map.get_image().save_png("res://test_assets/baked_pressure_map.png")
		//	blurred_flow_pressure_map.get_image().save_png("res://test_assets/baked_pressure_map_blurred.png")
		//	dilated_texture.get_image().save_png("res://test_assets/dilated_texture.png")
		//	normal_map.get_image().save_png("res://test_assets/normal_map.png")
		//	flow_map.get_image().save_png("res://test_assets/flow_map.png")
		//	blurred_flow_map.get_image().save_png("res://test_assets/blurred_flow_map.png")

		RemoveChild(renderer_instance); // cleanup

		var flow_foam_noise_result = flow_foam_noise_img.GetImage().GetRegion(new Rect2I(margin, margin, (int)flowmap_resolution, (int)flowmap_resolution));
		var dist_pressure_result = dist_pressure_img.GetImage().GetRegion(new Rect2I(margin, margin, (int)flowmap_resolution, (int)flowmap_resolution));

		flow_foam_noise = flow_foam_noise_img;
		dist_pressure = dist_pressure_img;

		set_materials("i_flowmap", flow_foam_noise);
		set_materials("i_distmap", dist_pressure);
		set_materials("i_valid_flowmap", true);
		set_materials("i_uv2_sides", _uv2_sides);
		valid_flowmap = true;
		EmitSignal("progress_notified", 100.0, "finished");
		UpdateConfigurationWarnings();
	}
}