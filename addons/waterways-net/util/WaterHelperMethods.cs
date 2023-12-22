using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using static Godot.Image;

namespace Waterways.Util;

// Copyright © 2022 Kasper Arnklit Frandsen - MIT License
// See `LICENSE.md` included in the source distribution for details.

public static class WaterHelperMethods
{
    public static Vector3 CartToBary(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        var v0 = b - a;
        var v1 = c - a;
        var v2 = p - a;
        var d00 = v0.Dot(v0);
        var d01 = v0.Dot(v1);
        var d11 = v1.Dot(v1);
        var d20 = v2.Dot(v0);
        var d21 = v2.Dot(v1);
        var denominator = (d00 * d11) - (d01 * d01);
        var v = ((d11 * d20) - (d01 * d21)) / denominator;
        var w = ((d00 * d21) - (d01 * d20)) / denominator;
        var u = 1.0f - v - w;
        return new Vector3(u, v, w);
    }

    public static Vector3 BaryToCart(Vector3 a, Vector3 b, Vector3 c, Vector3 barycentric)
    {
        return (barycentric.X * a) + (barycentric.Y * b) + (barycentric.Z * c);
    }

    public static bool IsPointInBariatric(Vector3 v)
    {
        return v.X is >= 0 and <= 1 && v.Y is >= 0 and <= 1 && v.Z is >= 0 and <= 1;
    }

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

    // TODO: Might be problems with final values, recheck
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

    public static async Task<Image> GenerateCollisionMap(Image image, MeshInstance3D meshInstance, float raycastDist, int steps, int stepLengthDivs, int stepWidthDivs, RiverManager river)
    {
        var spaceState = meshInstance.GetWorld3D().DirectSpaceState;
        var uv2 = meshInstance.Mesh.SurfaceGetArrays(0)[5].AsVector2Array();
        var vertices = meshInstance.Mesh.SurfaceGetArrays(0)[0].AsVector3Array();

        // We need to move the verts into world space
        var worldVertices = vertices.Select(vertex => meshInstance.GlobalTransform * vertex).ToList();
        var trisInStepQuad = stepLengthDivs * stepWidthDivs * 2;
        var side = CalculateSide(steps);
        var percentage = 0.0f;

        river.EmitSignal(RiverManager.SignalName.ProgressNotified, percentage, "Calculating Collisions (" + image.GetWidth() + "x" + image.GetWidth() + ")");
        await river.ToSignal(river.GetTree(), "process_frame");

        for (var x = 0; x < image.GetWidth(); x++)
        {
            var curPercentage = x / (float)image.GetWidth();

            if (curPercentage > percentage + 0.1f)
            {
                percentage += 0.1f;
                river.EmitSignal(RiverManager.SignalName.ProgressNotified, percentage, "Calculating Collisions (" + image.GetWidth() + "x" + image.GetWidth() + ")");
                await river.ToSignal(river.GetTree(), "process_frame");
            }

            for (var y = 0; y < image.GetHeight(); y++)
            {
                var uvCoordinate = new Vector2((0.5f + x) / image.GetWidth(), (0.5f + y) / image.GetHeight());
                var baryatricCoords = Vector3.Zero;
                var correctTriangle = new List<int>();

                var pixel = (x * image.GetWidth()) + y;
                var column = pixel / image.GetWidth() / (image.GetWidth() / side);
                var row = pixel % image.GetWidth() / (image.GetWidth() / side);
                var stepQuad = (column * side) + row;

                if (stepQuad >= steps)
                {
                    break; // we are in the empty part of UV2, so we break to the next column
                }

                for (var tris = 0; tris < trisInStepQuad; tris++)
                {
                    var offsetTris = (trisInStepQuad * stepQuad) + tris;
                    var p = new Vector3(uvCoordinate.X, uvCoordinate.Y, 0.0f);
                    var a = new Vector3(uv2[offsetTris * 3].X, uv2[offsetTris * 3].Y, 0.0f);
                    var b = new Vector3(uv2[(offsetTris * 3) + 1].X, uv2[(offsetTris * 3) + 1].Y, 0.0f);
                    var c = new Vector3(uv2[(offsetTris * 3) + 2].X, uv2[(offsetTris * 3) + 2].Y, 0.0f);
                    baryatricCoords = CartToBary(p, a, b, c);

                    if (!IsPointInBariatric(baryatricCoords))
                    {
                        continue;
                    }

                    correctTriangle = [offsetTris * 3, (offsetTris * 3) + 1, (offsetTris * 3) + 2];
                    break; // we have the correct triangle, so we break out of loop
                }

                if (correctTriangle.Count == 0)
                {
                    continue;
                }

                var vert0 = worldVertices[correctTriangle[0]];
                var vert1 = worldVertices[correctTriangle[1]];
                var vert2 = worldVertices[correctTriangle[2]];

                var realPos = BaryToCart(vert0, vert1, vert2, baryatricCoords);
                var realPosUp = realPos + (Vector3.Up * raycastDist);

                var rayParamsUp = PhysicsRayQueryParameters3D.Create(realPos, realPosUp);
                var resultUp = spaceState.IntersectRay(rayParamsUp);

                var rayParamsDown = PhysicsRayQueryParameters3D.Create(realPosUp, realPos);
                var resultDown = spaceState.IntersectRay(rayParamsDown);

                static bool CheckResult(Dictionary dict)
                {
                    return dict is { Count: > 0 };
                }

                // TODO: REcheck that PARt
                var upHitFrontFace = resultUp?.ContainsKey("normal") == true && resultUp["normal"].AsVector3().Y < 0;

                if ((CheckResult(resultUp) || CheckResult(resultDown)) && !upHitFrontFace && CheckResult(resultDown))
                {
                    image.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f));
                }
            }
        }

        return image;
    }

    public static Image AddMargins(Image image, int resolution, int margin)
    {
        var withMarginsSize = resolution + (2 * margin);
        var imageWithMargins = Create(withMarginsSize, withMarginsSize, true, Format.Rgb8);
        imageWithMargins.BlendRect(image, new Rect2I(0, resolution - margin, resolution, margin), new Vector2I(margin + margin, 0));
        imageWithMargins.BlendRect(image, new Rect2I(0, 0, resolution, resolution), new Vector2I(margin, margin));
        imageWithMargins.BlendRect(image, new Rect2I(0, 0, resolution, margin), new Vector2I(0, resolution + margin));
        return imageWithMargins;
    }
}
