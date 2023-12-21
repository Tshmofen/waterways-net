using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Waterways;

[Tool]
public partial class SystemMapRenderer : SubViewport
{
    private const string HeightShaderPath = "res://addons/waterways/shaders/system_renders/system_height.gdshader";
    private const string FlowShaderPath = "res://addons/waterways/shaders/system_renders/system_flow.gdshader";

    private Camera3D _camera;
    private Node3D _container;

    public async Task<ImageTexture> GrabHeight(List<RiverManager> waterObjects, Aabb aabb)
    {
        _camera = GetNode<Camera3D>("$Camera3D");
        _container = GetNode<Node3D>("$Container");

        var heightMat = new ShaderMaterial
        {
            Shader = ResourceLoader.Load(HeightShaderPath) as Shader
        };

        heightMat.SetShaderParameter("lower_bounds", aabb.Position.Y);
        heightMat.SetShaderParameter("upper_bounds", aabb.End.Y);

        foreach (var obj in waterObjects)
        {
            // was used true??
            var waterMeshCopy = (MeshInstance3D)obj.MeshInstance.Duplicate((int)DuplicateFlags.Signals);
            _container.AddChild(waterMeshCopy);
            waterMeshCopy.Transform = obj.Transform; // TODO - This seems unneeded?
            waterMeshCopy.MaterialOverride = heightMat;
        }

        switch (aabb.GetLongestAxisIndex())
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
        var heightResult = ImageTexture.CreateFromImage(height);

        foreach (var child in _container.GetChildren())
        {
            _container.RemoveChild(child);
        }

        return heightResult;
    }

    public async Task<ImageTexture> GrabFlow(List<RiverManager> waterObjects, Aabb aabb)
    {
        _camera = GetNode<Camera3D>("$Camera3D");
        _container = GetNode<Node3D>("$Container");

        var flowMat = new ShaderMaterial
        {
            Shader = ResourceLoader.Load(FlowShaderPath) as Shader
        };

        foreach (var obj in waterObjects)
        {
            var waterMeshCopy = (MeshInstance3D) obj.MeshInstance.Duplicate((int)DuplicateFlags.Signals);
            _container.AddChild(waterMeshCopy);
            waterMeshCopy.Transform = obj.Transform;
            waterMeshCopy.MaterialOverride = flowMat;
            var shaderMaterial = (ShaderMaterial) waterMeshCopy.MaterialOverride;
            shaderMaterial.SetShaderParameter("flowmap", obj.FlowFoamNoise);
            shaderMaterial.SetShaderParameter("distmap", obj.DistPressure);
            shaderMaterial.SetShaderParameter("flow_base", obj.GetShaderParameter("flow_base"));
            shaderMaterial.SetShaderParameter("flow_steepness", obj.GetShaderParameter("flow_steepness"));
            shaderMaterial.SetShaderParameter("flow_distance", obj.GetShaderParameter("flow_distance"));
            shaderMaterial.SetShaderParameter("flow_pressure", obj.GetShaderParameter("flow_pressure"));
            shaderMaterial.SetShaderParameter("flow_max", obj.GetShaderParameter("flow_max"));
            shaderMaterial.SetShaderParameter("valid_flowmap", obj.GetShaderParameter("i_valid_flowmap"));
        }

        switch (aabb.GetLongestAxisIndex())
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
        var flowResult = ImageTexture.CreateFromImage(flow);

        foreach (var child in _container.GetChildren())
        {
            _container.RemoveChild(child);
        }

        return flowResult;
    }
}