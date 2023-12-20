using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Waterways;
using static Godot.Image;

namespace Waterways;

// Copyright © 2022 Kasper Arnklit Frandsen - MIT License
// See `LICENSE.md` included in the source distribution for details.

public static class WaterHelperMethods
{
	public static Vector3 cart2bary(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
	{
		var v0 = b - a;
		var v1 = c - a;
		var v2 = p - a;
		var d00 = v0.Dot(v0);
		var d01 = v0.Dot(v1);
		var d11 = v1.Dot(v1);
		var d20 = v2.Dot(v0);
		var d21 = v2.Dot(v1);
		var denom = d00 * d11 - d01 * d01;
		var v = (d11 * d20 - d01 * d21) / denom;
		var w = (d00 * d21 - d01 * d20) / denom;
		var u = 1.0f - v - w;
		return new Vector3(u, v, w);
    }

	public static Vector3 bary2cart(Vector3 a, Vector3 b, Vector3 c, Vector3 barycentric)
    {
		return barycentric.X * a + barycentric.Y * b + barycentric.Z * c;
    }

    public static bool point_in_bariatric(Vector3 v)
	{
        return v.X is >= 0 and <= 1 && v.Y is >= 0 and <= 1 && v.Z is >= 0 and <= 1;
    }

	public static void reset_all_colliders(Node node)
	{
        foreach (var n in node.GetChildren())
		{
            if (n.GetChildCount() > 0)
			{
				reset_all_colliders(n);
            }

            if (n is CollisionShape3D {Disabled: false} shape)
			{
                shape.Disabled = true;
                shape.Disabled = false;
            }
		}
    }

	public static float sum_array(ICollection<float> array) => array.Sum();

	public static int calculate_side(int steps)
	{
		var side_float = Mathf.Sqrt(steps);

        if (Mathf.PosMod(side_float, 1.0) != 0.0)
        {
			side_float += 1.0f;
        }

		return (int)(side_float);

    }

	// TODO: Might be problems with final values, recheck
	public static List<float> generate_river_width_values(Curve3D curve, int steps, int step_length_divs, int step_width_divs, Array<float> widths)
	{
		var river_width_values = new List<float>();
		var length = curve.GetBakedLength();

		for (var step = 0; step < steps * step_length_divs + 1; step++)
		{
			var target_pos = curve.SampleBaked((step / (float)(steps * step_length_divs + 1)) * curve.GetBakedLength());
			var closest_dist = 4096.0f;
			var closest_interpolate = -1f;
			var closest_point = -1;

			for (var c_point = 0; c_point < curve.PointCount - 1; c_point++)
			{
				for (var innerStep = 0; innerStep < 100; innerStep++)
				{

					var interpolate = innerStep / 100.0f;
					var pos = curve.Sample(c_point, interpolate);
					var dist = pos.DistanceTo(target_pos);
					if (dist < closest_dist)
					{
						closest_dist = dist;
						closest_interpolate = interpolate;
						closest_point = c_point;
                    }
                }
            }

			river_width_values.Add(Mathf.Lerp(widths[closest_point], widths[closest_point + 1], closest_interpolate));
		}

		return river_width_values;
    }

	public static Mesh generate_river_mesh(Curve3D curve, int steps, int step_length_divs, int step_width_divs, float smoothness, List<float> river_width_values)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		var curve_length = curve.GetBakedLength();
		st.SetSmoothGroup(0);

		// Generating the verts
		for (var step = 0; step < steps * step_length_divs + 1; step++)
		{
			var position = curve.SampleBaked(step / (float)(steps * step_length_divs) * curve_length, false);
			var backward_pos = curve.SampleBaked(((float)(step) - smoothness) / (float)(steps * step_length_divs) * curve_length, false);
			var forward_pos = curve.SampleBaked(((float)(step) + smoothness) / (float)(steps * step_length_divs) * curve_length, false);
			var forward_vector = forward_pos - backward_pos;
			var right_vector = forward_vector.Cross(Vector3.Up).Normalized();
			var width_lerp = river_width_values[step];
		
			for (var w_sub = 0; w_sub < step_width_divs + 1; w_sub++)
			{
				st.SetUV(new Vector2((float)(w_sub) / ((float)(step_width_divs)), (float)(step) / (float)(step_length_divs)));
				st.AddVertex(position + right_vector * width_lerp - 2.0f * right_vector * width_lerp * (float)(w_sub) / ((float)(step_width_divs)));
            }
        }

		// Defining the tris
		for (var step = 0; step < steps * step_length_divs; step++)
		{
			for (var w_sub = 0; w_sub < step_width_divs; w_sub++)
			{
				st.AddIndex((step * (step_width_divs + 1)) + w_sub);
				st.AddIndex((step * (step_width_divs + 1)) + w_sub + 1);
				st.AddIndex((step * (step_width_divs + 1)) + w_sub + 2 + step_width_divs - 1);

				st.AddIndex((step * (step_width_divs + 1)) + w_sub + 1);
				st.AddIndex((step * (step_width_divs + 1)) + w_sub + 3 + step_width_divs - 1);
				st.AddIndex((step * (step_width_divs + 1)) + w_sub + 2 + step_width_divs - 1);
            }
        }


		st.GenerateNormals();
		st.GenerateTangents();
		st.Deindex();

		var mesh = new ArrayMesh();
		var mesh2 = new ArrayMesh();
		var mesh3 = new ArrayMesh();
		mesh = st.Commit();

		var mdt = new MeshDataTool();
		mdt.CreateFromSurface(mesh, 0);

		// Generate UV2
		// Decide on grid size
		var grid_side = calculate_side(steps);
		var grid_side_length = 1.0f / (float)(grid_side);
		var x_grid_sub_length = grid_side_length / (float)(step_width_divs);
		var y_grid_sub_length = grid_side_length / (float)(step_length_divs);
		var grid_size = Mathf.Pow(grid_side, 2);
		var index = 0;
		var UVs = steps * step_width_divs * step_length_divs * 6;
		var x_offset = 0.0f;

		for (var x = 0; x < grid_side; x++) 
		{
			var y_offset = 0.0f;
			for (var y = 0; y < grid_side; y++)
			{
				if (index < UVs)
				{
					var sub_y_offset = 0.0f;
					for (var sub_y = 0; sub_y < step_length_divs; sub_y++)
					{
						var sub_x_offset = 0.0f;
						for (var sub_x = 0; sub_x < step_width_divs; sub_x++)
						{
							var x_comb_offset = x_offset + sub_x_offset;
							var y_comb_offset = y_offset + sub_y_offset;
							mdt.SetVertexUV2(index, new Vector2(x_comb_offset, y_comb_offset));
							mdt.SetVertexUV2(index + 1, new Vector2(x_comb_offset + x_grid_sub_length, y_comb_offset));
							mdt.SetVertexUV2(index + 2, new Vector2(x_comb_offset, y_comb_offset + y_grid_sub_length));

							mdt.SetVertexUV2(index + 3, new Vector2(x_comb_offset + x_grid_sub_length, y_comb_offset));
							mdt.SetVertexUV2(index + 4, new Vector2(x_comb_offset + x_grid_sub_length, y_comb_offset + y_grid_sub_length));
							mdt.SetVertexUV2(index + 5, new Vector2(x_comb_offset, y_comb_offset + y_grid_sub_length));
							index += 6;
							sub_x_offset += grid_side_length / (float)(step_width_divs);
                        }

						sub_y_offset += grid_side_length / (float)(step_length_divs);
                    }
                }

                y_offset += grid_side_length;
            }

			x_offset += grid_side_length;

        }

		mdt.CommitToSurface(mesh2);
		st.Clear();
		st.CreateFrom(mesh2, 0);
		st.Index();
		mesh3 = st.Commit();
		return mesh3;
	}

	public static async Task<Image> generate_collisionmap(Image image, MeshInstance3D mesh_instance, float raycast_dist, int raycast_layers, int steps, int step_length_divs, int step_width_divs, RiverManager river)
	{
		var space_state = mesh_instance.GetWorld3D().DirectSpaceState;
		//print("is the space state what we expect?");
		//print(space_state);

		var uv2 = mesh_instance.Mesh.SurfaceGetArrays(0)[5].AsVector2Array();
		var verts = mesh_instance.Mesh.SurfaceGetArrays(0)[0].AsVector3Array();
		// We need to move the verts into world space
		var world_verts = new List<Vector3>();
		for (var v = 0; v < verts.Length; v++)
        {
			world_verts.Add(mesh_instance.GlobalTransform * (verts[v]));
        }

		var tris_in_step_quad = step_length_divs * step_width_divs * 2;
		var side = calculate_side(steps);
		var percentage = 0.0f;

		river.EmitSignal("progress_notified", percentage, "Calculating Collisions (" + image.GetWidth() + "x" + image.GetWidth() + ")");
		await river.ToSignal(river.GetTree(), "process_frame");

		var ray_params = PhysicsRayQueryParameters3D.Create(new Vector3(0.0f, 5.0f, 0.0f), new Vector3(0.0f, 0.0f, 0.0f));
		// ray_params_up.collision_mask = raycast_layers
		var result = space_state.IntersectRay(ray_params);

		//print("Single cast test!")
		//print(result)
		//print("done")

		for (var x = 0; x < image.GetWidth(); x++)
		{
			var cur_percentage = (float)(x) / (float)(image.GetWidth());

			if (cur_percentage > percentage + 0.1f)
			{
				percentage += 0.1f;
				river.EmitSignal("progress_notified", percentage, "Calculating Collisions (" + image.GetWidth() + "x" + image.GetWidth() + ")");
				await river.ToSignal(river.GetTree(), "process_frame");
            }

            for (var y = 0; y < image.GetHeight(); y++)
			{
				var uv_coordinate = new Vector2((0.5f + (float)(x)) / (float)(image.GetWidth()), (0.5f + (float)(y)) / (float)(image.GetHeight()));
				var baryatric_coords = Vector3.Zero;
				var correct_triangle = new List<int>();

				var pixel = (int)(x * image.GetWidth() + y);
				var column = (pixel / image.GetWidth()) / (image.GetWidth() / side);
				var row = (pixel % image.GetWidth()) / (image.GetWidth() / side);
				var step_quad = column * side + row;
				
				if (step_quad >= steps)
				{
					break; // we are in the empty part of UV2 so we break to the next column
				}

				for (var tris = 0; tris < tris_in_step_quad; tris++)
				{

					var offset_tris = (tris_in_step_quad * step_quad) + tris;
					var triangle = new List<Vector2>();
					triangle.Add(uv2[offset_tris * 3]);
					triangle.Add(uv2[offset_tris * 3 + 1]);
					triangle.Add(uv2[offset_tris * 3 + 2]);
					var p = new Vector3(uv_coordinate.X, uv_coordinate.Y, 0.0f);
					var a = new Vector3(uv2[offset_tris * 3].X, uv2[offset_tris * 3].Y, 0.0f);
					var b = new Vector3(uv2[offset_tris * 3 + 1].X, uv2[offset_tris * 3 + 1].Y, 0.0f);
					var c = new Vector3(uv2[offset_tris * 3 + 2].X, uv2[offset_tris * 3 + 2].Y, 0.0f);
					baryatric_coords = cart2bary(p, a, b, c);
				
					if (point_in_bariatric(baryatric_coords))
					{
						correct_triangle = [offset_tris * 3, offset_tris * 3 + 1, offset_tris * 3 + 2];
						break; // we have the correct triangle so we break out of loop
					}
				}

				if (correct_triangle.Count != 0)
                {
                    var vert0 = world_verts[correct_triangle[0]];
                    var vert1 = world_verts[correct_triangle[1]];
                    var vert2 = world_verts[correct_triangle[2]];

                    var real_pos = bary2cart(vert0, vert1, vert2, baryatric_coords);
                    var real_pos_up = real_pos + Vector3.Up * raycast_dist;

                    var ray_params_up = PhysicsRayQueryParameters3D.Create(real_pos, real_pos_up);
                    // ray_params_up.collision_mask = raycast_layers
                    var result_up = space_state.IntersectRay(ray_params_up);

                    var ray_params_down = PhysicsRayQueryParameters3D.Create(real_pos_up, real_pos);
                    // ray_params_down.collision_mask = raycast_layers
                    var result_down = space_state.IntersectRay(ray_params_down);

                    if ((x == 32 && y == 32) || (x == 50 && y == 50))
                    {
                        //print("real pos at x: ", x, ", y: ", y)
                        //print(real_pos)
                        //print(real_pos_up)
                        //print("ray_params_down")
                        //print(var_to_str(ray_params_down))
                        //print(result_up)
                        //print(result_down)
                    }

                    bool CheckResult(Dictionary dict)
                    {
                        return dict is {Count: > 0};
                    }

                    // TODO: REcheck that PARt
                    var up_hit_frontface = result_up?.ContainsKey("normal") == true && result_up["normal"].AsVector3().Y < 0;
				
                    if (CheckResult(result_up) || CheckResult(result_down))
                    {
                        if (!up_hit_frontface && CheckResult(result_down))
                        {
                            // print("Does this ever happen") - Nope
                            image.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f));
                        }
                    }
                }
            }
        }

		return image;
    }

	// Adds offset margins so filters will correctly extend across UV edges
	public static Image add_margins(Image image, int resolution, int margin)
	{
		var with_margins_size = resolution + 2 * margin;

		var image_with_margins = Image.Create(with_margins_size, with_margins_size, true, Format.Rgb8);
		image_with_margins.BlendRect(image, new Rect2I(0, resolution - margin, resolution, margin), new Vector2I(margin + margin, 0));
		image_with_margins.BlendRect(image, new Rect2I(0, 0, resolution, resolution), new Vector2I(margin, margin));
		image_with_margins.BlendRect(image, new Rect2I(0, 0, resolution, margin), new Vector2I(0, resolution + margin));

		return image_with_margins;

    }
}
