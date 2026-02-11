using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static utility that converts a 2D boundary polygon into a Unity Mesh
/// suitable for floor rendering and collision.
/// Vertices are placed on the XZ plane (Y=0) with normals facing up,
/// matching Unity's floor convention.
/// </summary>
public static class FloorMeshGenerator
{
    /// <summary>
    /// Builds a floor mesh from a 2D boundary polygon.
    /// Input X maps to world X, input Y maps to world Z.
    /// Vertices are at (x, 0, z) with Vector3.up normals.
    /// </summary>
    public static Mesh GenerateFloorMesh(Vector2[] boundary)
    {
        if (boundary == null || boundary.Length < 3)
        {
            Debug.LogWarning("[FloorMeshGenerator] Boundary has fewer than 3 points, cannot generate mesh.");
            return null;
        }

        int vertCount = boundary.Length;
        var vertices = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];

        for (int i = 0; i < vertCount; i++)
        {
            vertices[i] = new Vector3(boundary[i].x, 0f, boundary[i].y);
            normals[i] = Vector3.up;
            uvs[i] = boundary[i];
        }

        int[] triangles = Triangulate(boundary);
        if (triangles == null || triangles.Length < 3)
        {
            Debug.LogWarning("[FloorMeshGenerator] Triangulation failed.");
            return null;
        }

        var mesh = new Mesh { name = "SpatialFloor" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Ear-clipping triangulation for simple (non-self-intersecting) polygons.
    /// Handles both convex and concave Guardian shapes.
    /// </summary>
    static int[] Triangulate(Vector2[] polygon)
    {
        int n = polygon.Length;
        if (n < 3) return null;

        var indices = new List<int>(n);
        if (SignedArea(polygon) < 0f)
        {
            for (int i = 0; i < n; i++)
                indices.Add(i);
        }
        else
        {
            for (int i = n - 1; i >= 0; i--)
                indices.Add(i);
        }

        var triangles = new List<int>((n - 2) * 3);
        int safety = n * n;

        while (indices.Count > 2 && safety-- > 0)
        {
            bool earFound = false;
            int count = indices.Count;

            for (int i = 0; i < count; i++)
            {
                int prev = (i + count - 1) % count;
                int next = (i + 1) % count;

                int iA = indices[prev];
                int iB = indices[i];
                int iC = indices[next];

                Vector2 a = polygon[iA];
                Vector2 b = polygon[iB];
                Vector2 c = polygon[iC];

                if (Cross2D(b - a, c - a) < 0f)
                    continue;

                bool containsPoint = false;
                for (int j = 0; j < count; j++)
                {
                    if (j == prev || j == i || j == next) continue;
                    if (PointInTriangle(polygon[indices[j]], a, b, c))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (containsPoint)
                    continue;

                triangles.Add(iA);
                triangles.Add(iB);
                triangles.Add(iC);
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                Debug.LogWarning("[FloorMeshGenerator] Ear-clipping could not find an ear; polygon may be degenerate.");
                break;
            }
        }

        return triangles.ToArray();
    }

    static float SignedArea(Vector2[] polygon)
    {
        float area = 0f;
        int n = polygon.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % n];
            area += (b.x - a.x) * (b.y + a.y);
        }
        return area;
    }

    static float Cross2D(Vector2 u, Vector2 v)
    {
        return u.x * v.y - u.y * v.x;
    }

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross2D(b - a, p - a);
        float d2 = Cross2D(c - b, p - b);
        float d3 = Cross2D(a - c, p - c);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }
}
