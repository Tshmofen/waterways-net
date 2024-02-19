using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace Waterways.Util;

public static class WaterHelperMethods
{
    public static void ResetAllColliders(Node node)
    {
        foreach (var n in node.GetChildren())
        {
            if (n.GetChildCount() > 0)
            {
                ResetAllColliders(n);
            }

            if (n is not CollisionShape3D { Disabled: false } shape)
            {
                continue;
            }

            shape.Disabled = true;
            shape.Disabled = false;
        }
    }

    public static int CalculateSide(int steps)
    {
        var sideFloat = Mathf.Sqrt(steps);

        if (Mathf.PosMod(sideFloat, 1.0) != 0.0)
        {
            sideFloat++;
        }

        return (int)sideFloat;
    }

    public static List<float> GenerateRiverWidthValues(Curve3D curve, int steps, int stepLengthDivs, Array<float> widths)
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

    public static Mesh GenerateRiverMesh(Curve3D curve, int steps, int stepLengthDivs, int stepWidthDivs, float smoothness, List<float> riverWidthValues)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var curveLength = curve.GetBakedLength();
        st.SetSmoothGroup(0);

        // Generating the verts
        for (var step = 0; step < (steps * stepLengthDivs) + 1; step++)
        {
            var position = curve.SampleBaked(step / (float)(steps * stepLengthDivs) * curveLength);
            var backwardPos = curve.SampleBaked((step - smoothness) / (steps * stepLengthDivs) * curveLength);
            var forwardPos = curve.SampleBaked((step + smoothness) / (steps * stepLengthDivs) * curveLength);
            var forwardVector = forwardPos - backwardPos;
            var rightVector = forwardVector.Cross(Vector3.Up).Normalized();
            var widthLerp = riverWidthValues[step];

            for (var wSub = 0; wSub < stepWidthDivs + 1; wSub++)
            {
                st.SetUV(new Vector2(wSub / (float)stepWidthDivs, step / (float)stepLengthDivs));
                st.AddVertex(position + (rightVector * widthLerp) - (2.0f * rightVector * widthLerp * wSub / stepWidthDivs));
            }
        }

        // Defining the tris
        for (var step = 0; step < steps * stepLengthDivs; step++)
        {
            for (var wSub = 0; wSub < stepWidthDivs; wSub++)
            {
                st.AddIndex((step * (stepWidthDivs + 1)) + wSub);
                st.AddIndex((step * (stepWidthDivs + 1)) + wSub + 1);
                st.AddIndex((step * (stepWidthDivs + 1)) + wSub + 2 + stepWidthDivs - 1);

                st.AddIndex((step * (stepWidthDivs + 1)) + wSub + 1);
                st.AddIndex((step * (stepWidthDivs + 1)) + wSub + 3 + stepWidthDivs - 1);
                st.AddIndex((step * (stepWidthDivs + 1)) + wSub + 2 + stepWidthDivs - 1);
            }
        }

        st.GenerateNormals();
        st.GenerateTangents();
        st.Deindex();

        var mesh = st.Commit();
        var mesh2 = new ArrayMesh();

        var mdt = new MeshDataTool();
        mdt.CreateFromSurface(mesh, 0);

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
                            mdt.SetVertexUV2(index, new Vector2(xCombOffset, yCombOffset));
                            mdt.SetVertexUV2(index + 1, new Vector2(xCombOffset + xGridSubLength, yCombOffset));
                            mdt.SetVertexUV2(index + 2, new Vector2(xCombOffset, yCombOffset + yGridSubLength));

                            mdt.SetVertexUV2(index + 3, new Vector2(xCombOffset + xGridSubLength, yCombOffset));
                            mdt.SetVertexUV2(index + 4, new Vector2(xCombOffset + xGridSubLength, yCombOffset + yGridSubLength));
                            mdt.SetVertexUV2(index + 5, new Vector2(xCombOffset, yCombOffset + yGridSubLength));
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

        mdt.CommitToSurface(mesh2);
        st.Clear();
        st.CreateFrom(mesh2, 0);
        st.Index();
        return st.Commit();
    }
}
