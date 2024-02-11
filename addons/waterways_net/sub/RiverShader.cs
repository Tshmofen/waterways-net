using System.Collections.Generic;

namespace Waterways;

public class RiverShader
{
    public string Name { get; init; }
    public string ShaderPath { get; init; }
    public List<(string name, string path)> TexturePaths { get; init; }
}