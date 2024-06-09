using Godot;
using Waterways.Data;

namespace Waterways;

[Tool]
public partial class RiverManager : BaseMeshGenerator
{
    public const string PluginBaseAlias = nameof(Node3D);
    public const string PluginNodeAlias = nameof(RiverManager);
    public const string ScriptPath = $"{nameof(RiverManager)}.cs";
    public const string IconPath = "river.svg";

    #region Export Propertie

    private RiverShaderSettings _shaderSettings;
    [Export] public RiverShaderSettings ShaderSettings
    {
        get => _shaderSettings;
        set
        {
            _shaderSettings = value;

            if (_shaderSettings == null)
            {
                _shaderSettings = new RiverShaderSettings(CurrentShaderMaterial);
                _shaderSettings.CallDeferred(RiverShaderSettings.MethodName.SetDefaultProperties);
            }

            CallDeferred(BaseMeshGenerator.MethodName.UpdateMesh);
        }
    }

    #endregion

    protected override ShaderMaterial CurrentShaderMaterial
    {
        get
        {
            return ShaderSettings?.Material ?? base.CurrentShaderMaterial;
        }
        set
        {
            if (ShaderSettings != null) 
            {
                ShaderSettings.Material = value;
            }

            base.CurrentShaderMaterial = value;
        }
    }

    protected override void HandleMeshDuplication()
    {
        ShaderSettings = ShaderSettings.Duplicate() as RiverShaderSettings;
        _shaderSettings.Material = CurrentShaderMaterial;
    }

    public override void _Ready()
    {
        if (ShaderSettings == null)
        {
            ShaderSettings = new RiverShaderSettings();
            ShaderSettings.SetDefaultProperties();
        }

        base._Ready();
        ShaderSettings.Material = CurrentShaderMaterial;
    }
}
