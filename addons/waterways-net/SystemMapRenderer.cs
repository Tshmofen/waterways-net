using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Waterways;

namespace Waterways;

[Tool]
public partial class SystemMapRenderer : SubViewport
{
	const string HEIGHT_SHADER_PATH = "res://addons/waterways/shaders/system_renders/system_height.gdshader";
	const string FLOW_SHADER_PATH = "res://addons/waterways/shaders/system_renders/system_flow.gdshader";
	const string ALPHA_SHADER_PATH = "res://addons/waterways/shaders/system_renders/alpha.gdshader";

	private Camera3D _camera;
	private Node3D _container;

	public async Task<ImageTexture> grab_height(List<RiverManager> water_objects, Aabb aabb, float resolution )
	{
		var size = new Vector2(resolution, resolution);
		_camera = GetNode<Camera3D>("$Camera3D");
		_container = GetNode<Node3D>("$Container");

		var height_mat = new ShaderMaterial();
		var height_shader = ResourceLoader.Load(HEIGHT_SHADER_PATH) as Shader;
		height_mat.Shader = height_shader;
		height_mat.SetShaderParameter("lower_bounds", aabb.Position.Y);
		height_mat.SetShaderParameter("upper_bounds", aabb.End.Y);
	
		foreach (var obj in water_objects)
		{
			// was used true??
			var water_mesh_copy = obj.mesh_instance.Duplicate((int) DuplicateFlags.Signals) as MeshInstance3D;
			_container.AddChild(water_mesh_copy);
			water_mesh_copy.Transform = obj.Transform; // TODO - This seems unneeded?
			water_mesh_copy.MaterialOverride = height_mat;
        }

		var longest_axis = aabb.GetLongestAxisIndex();
		switch (longest_axis) 
		{
			case Vector3.Axis.X:
				_camera.Position = aabb.Position + new Vector3(aabb.Size.X / 2.0f, aabb.Size.Y + 1.0f, aabb.Size.X / 2.0f);
				break;
			case Vector3.Axis.Y:
				// TODO
				// This shouldn't happen, we might need some code to handle if it does
				break;
			case Vector3.Axis.Z:
				_camera.Position = aabb.Position + new Vector3(aabb.Size.Z / 2.0f, aabb.Size.Y + 1.0f, aabb.Size.Z / 2.0f);
				break;
		}

		_camera.Size = aabb.GetLongestAxisSize();
		_camera.Far = aabb.Size.Y + 2.0f;

		RenderTargetClearMode = ClearMode.Always;
		RenderTargetUpdateMode = UpdateMode.Once;

		await ToSignal(GetTree(), "process_frame");
		await ToSignal(GetTree(), "process_frame");

		var height = GetTexture().GetImage();
		var height_result = ImageTexture.CreateFromImage(height);
	
		foreach (var child in _container.GetChildren())
		{
			_container.RemoveChild(child);
		}

		return height_result;
    }

	public async Task<ImageTexture> grab_alpha(List<RiverManager> water_objects, Aabb aabb, float resolution)
	{

		var size = new Vector2(resolution, resolution);
        _camera = GetNode<Camera3D>("$Camera3D");
        _container = GetNode<Node3D>("$Container");

		var alpha_mat = new ShaderMaterial();
		var alpha_shader = ResourceLoader.Load(ALPHA_SHADER_PATH) as Shader;
		alpha_mat.Shader = alpha_shader;
	
		foreach (var obj in water_objects)
		{
			var water_mesh_copy = obj.mesh_instance.Duplicate((int)DuplicateFlags.Signals) as MeshInstance3D;
			_container.AddChild(water_mesh_copy);
			water_mesh_copy.Transform = obj.Transform;
			water_mesh_copy.MaterialOverride = alpha_mat;
        }

		var longest_axis = aabb.GetLongestAxisIndex();
        switch (longest_axis)
        {
            case Vector3.Axis.X:
                _camera.Position = aabb.Position + new Vector3(aabb.Size.X / 2.0f, aabb.Size.Y + 1.0f, aabb.Size.X / 2.0f);
                break;
            case Vector3.Axis.Y:
                // TODO
                // This shouldn't happen, we might need some code to handle if it does
                break;
            case Vector3.Axis.Z:
                _camera.Position = aabb.Position + new Vector3(aabb.Size.Z / 2.0f, aabb.Size.Y + 1.0f, aabb.Size.Z / 2.0f);
                break;
        }

		_camera.Size = aabb.GetLongestAxisSize();
		_camera.Far = aabb.Size.Y + 2.0f;

        RenderTargetClearMode = ClearMode.Always;
        RenderTargetUpdateMode = UpdateMode.Once;

        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");

		var alpha = GetTexture().GetImage();
		var alpha_result = ImageTexture.CreateFromImage(alpha);

        foreach (var child in _container.GetChildren())
        {
            _container.RemoveChild(child);
        }

		return alpha_result;
    }

	public async Task<ImageTexture> grab_flow(List<RiverManager> water_objects, Aabb aabb, float resolution)
	{

        var size = new Vector2(resolution, resolution);
        _camera = GetNode<Camera3D>("$Camera3D");
        _container = GetNode<Node3D>("$Container");

		var flow_mat = new ShaderMaterial();
		var flow_shader = ResourceLoader.Load(FLOW_SHADER_PATH) as Shader;
		flow_mat.Shader = flow_shader;

		for (var i = 0; i < water_objects.Count; i++)
		{
			var water_mesh_copy = water_objects[i].mesh_instance.Duplicate((int)DuplicateFlags.Signals) as MeshInstance3D;
			_container.AddChild(water_mesh_copy);
			water_mesh_copy.Transform = water_objects[i].Transform;
			water_mesh_copy.MaterialOverride = flow_mat;
			var shaderMaterial = water_mesh_copy.MaterialOverride as ShaderMaterial;
			shaderMaterial.SetShaderParameter("flowmap", water_objects[i].flow_foam_noise);
			shaderMaterial.SetShaderParameter("distmap", water_objects[i].dist_pressure);
			shaderMaterial.SetShaderParameter("flow_base", water_objects[i].get_shader_parameter("flow_base"));
			shaderMaterial.SetShaderParameter("flow_steepness", water_objects[i].get_shader_parameter("flow_steepness"));
			shaderMaterial.SetShaderParameter("flow_distance", water_objects[i].get_shader_parameter("flow_distance"));
			shaderMaterial.SetShaderParameter("flow_pressure", water_objects[i].get_shader_parameter("flow_pressure"));
			shaderMaterial.SetShaderParameter("flow_max", water_objects[i].get_shader_parameter("flow_max"));
			shaderMaterial.SetShaderParameter("valid_flowmap", water_objects[i].get_shader_parameter("i_valid_flowmap"));
        }

        var longest_axis = aabb.GetLongestAxisIndex();

        switch (longest_axis)
        {
            case Vector3.Axis.X:
                _camera.Position = aabb.Position + new Vector3(aabb.Size.X / 2.0f, aabb.Size.Y + 1.0f, aabb.Size.X / 2.0f);
                break;
            case Vector3.Axis.Y:
                // TODO
                // This shouldn't happen, we might need some code to handle if it does
                break;
            case Vector3.Axis.Z:
                _camera.Position = aabb.Position + new Vector3(aabb.Size.Z / 2.0f, aabb.Size.Y + 1.0f, aabb.Size.Z / 2.0f);
                break;
        }

        _camera.Size = aabb.GetLongestAxisSize();
        _camera.Far = aabb.Size.Y + 2.0f;

        RenderTargetClearMode = ClearMode.Always;
        RenderTargetUpdateMode = UpdateMode.Once;

        await ToSignal(GetTree(), "process_frame");
        await ToSignal(GetTree(), "process_frame");

		var flow = GetTexture().GetImage();
		var flow_result = ImageTexture.CreateFromImage(flow);

        foreach (var child in _container.GetChildren())
        {
            _container.RemoveChild(child);
        }

		return flow_result;
    }

}