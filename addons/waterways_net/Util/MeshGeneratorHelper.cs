using System.Collections.Generic;
using Godot;

namespace Waterways.Util;

public static class MeshGeneratorHelper
{
    #region Inner Util

    private static int CalculateSide(int steps)
    {
        var sideFloat = Mathf.Sqrt(steps);

        if (Mathf.PosMod(sideFloat, 1.0) != 0.0)
        {
            sideFloat++;
        }

        return (int)sideFloat;
    }

    private static List<float> GenerateWidthValues(Curve3D curve, int steps, int stepLengthDivs, IList<float> widths)
    {
        var riverWidthValues = new List<float>();

        for (var step = 0; step < (steps * stepLengthDivs) + 1; step++)
        {
            var targetPos = curve.SampleBaked(step / (float)((steps * stepLengthDivs) + 1) * curve.GetBakedLength());
            var closestDist = 4096.0f;
            var closestInterpolate = -1f;
            var closestPoint = -1;

            for (var cPoint = 0; cPoint < curve.PointCount - 1; cPoint++)
            {
                for (var innerStep = 0; innerStep < 100; innerStep++)
                {
                    var interpolate = innerStep / 100.0f;
                    var pos = curve.Sample(cPoint, interpolate);
                    var dist = pos.DistanceTo(targetPos);

                    if (dist >= closestDist)
                    {
                        continue;
                    }

                    closestDist = dist;
                    closestInterpolate = interpolate;
                    closestPoint = cPoint;
                }
            }

            riverWidthValues.Add(Mathf.Lerp(widths[closestPoint], widths[closestPoint + 1], closestInterpolate));
        }

        return riverWidthValues;
    }

    #endregion

    public static Mesh GenerateCurvePlaneMesh(Curve3D curve, int steps, int stepLengthDivs, int stepWidthDivs, float smoothness, IList<float> riverWidths)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        var curveLength = curve.GetBakedLength();
        surface.SetSmoothGroup(0);

        // Generating the verts
        var riverWidthsValues = GenerateWidthValues(curve, steps, stepLengthDivs, riverWidths);
        for (var step = 0; step < (steps * stepLengthDivs) + 1; step++)
        {
            var position = curve.SampleBaked(step / (float)(steps * stepLengthDivs) * curveLength);
            var backwardPos = curve.SampleBaked((step - smoothness) / (steps * stepLengthDivs) * curveLength);
            var forwardPos = curve.SampleBaked((step + smoothness) / (steps * stepLengthDivs) * curveLength);
            var forwardVector = forwardPos - backwardPos;
            var rightVector = forwardVector.Cross(Vector3.Up).Normalized();
            var widthLerp = riverWidthsValues[step];

            for (var wSub = 0; wSub < stepWidthDivs + 1; wSub++)
            {
                surface.SetUV(new Vector2(wSub / (float)stepWidthDivs, step / (float)stepLengthDivs));
                surface.AddVertex(position + (rightVector * widthLerp) - (2.0f * rightVector * widthLerp * wSub / stepWidthDivs));
            }
        }

        // Defining the tris
        for (var step = 0; step < steps * stepLengthDivs; step++)
        {
            for (var wSub = 0; wSub < stepWidthDivs; wSub++)
            {
                surface.AddIndex((step * (stepWidthDivs + 1)) + wSub);
                surface.AddIndex((step * (stepWidthDivs + 1)) + wSub + 1);
                surface.AddIndex((step * (stepWidthDivs + 1)) + wSub + 2 + stepWidthDivs - 1);

                surface.AddIndex((step * (stepWidthDivs + 1)) + wSub + 1);
                surface.AddIndex((step * (stepWidthDivs + 1)) + wSub + 3 + stepWidthDivs - 1);
                surface.AddIndex((step * (stepWidthDivs + 1)) + wSub + 2 + stepWidthDivs - 1);
            }
        }

        surface.GenerateNormals();
        surface.GenerateTangents();
        surface.Deindex();

        var mesh = surface.Commit();
        var arrayMesh = new ArrayMesh();

        var meshTool = new MeshDataTool();
        meshTool.CreateFromSurface(mesh, 0);

        // Generate UV2
        // Decide on grid size
        var gridSide = CalculateSide(steps);
        var gridSideLength = 1.0f / gridSide;
        var xGridSubLength = gridSideLength / stepWidthDivs;
        var yGridSubLength = gridSideLength / stepLengthDivs;

        var index = 0;
        var uVs = steps * stepWidthDivs * stepLengthDivs * 6;
        var xOffset = 0.0f;

        for (var x = 0; x < gridSide; x++)
        {
            var yOffset = 0.0f;
            for (var y = 0; y < gridSide; y++)
            {
                if (index < uVs)
                {
                    var subYOffset = 0.0f;
                    for (var subY = 0; subY < stepLengthDivs; subY++)
                    {
                        var subXOffset = 0.0f;
                        for (var subX = 0; subX < stepWidthDivs; subX++)
                        {
                            var xCombOffset = xOffset + subXOffset;
                            var yCombOffset = yOffset + subYOffset;
                            meshTool.SetVertexUV2(index, new Vector2(xCombOffset, yCombOffset));
                            meshTool.SetVertexUV2(index + 1, new Vector2(xCombOffset + xGridSubLength, yCombOffset));
                            meshTool.SetVertexUV2(index + 2, new Vector2(xCombOffset, yCombOffset + yGridSubLength));

                            meshTool.SetVertexUV2(index + 3, new Vector2(xCombOffset + xGridSubLength, yCombOffset));
                            meshTool.SetVertexUV2(index + 4, new Vector2(xCombOffset + xGridSubLength, yCombOffset + yGridSubLength));
                            meshTool.SetVertexUV2(index + 5, new Vector2(xCombOffset, yCombOffset + yGridSubLength));
                            index += 6;
                            subXOffset += gridSideLength / stepWidthDivs;
                        }

                        subYOffset += gridSideLength / stepLengthDivs;
                    }
                }

                yOffset += gridSideLength;
            }

            xOffset += gridSideLength;
        }

        meshTool.CommitToSurface(arrayMesh);
        surface.Clear();
        surface.CreateFrom(arrayMesh, 0);
        surface.Index();

        var finalMesh = surface.Commit();
        finalMesh.ResourceLocalToScene = true;
        return finalMesh;
    }
}
