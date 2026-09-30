using System;
using System.Collections.Generic;
using UnityEngine;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        const int ReflectiveGlitterPieces = 552;

        // A flake is a thin piece of foil, not an opaque crystal. All foil and its
        // optical glints share one mesh/material, so the finish adds one draw call.
        Mesh BuildReflectiveGlitter()
        {
            var random = new System.Random(78163);
            var vertices = new List<Vector3>(ReflectiveGlitterPieces * 10);
            var normals = new List<Vector3>(ReflectiveGlitterPieces * 10);
            var colors = new List<Color>(ReflectiveGlitterPieces * 10);
            var uv = new List<Vector2>(ReflectiveGlitterPieces * 10);
            var centers = new List<Vector4>(ReflectiveGlitterPieces * 10);
            var skinUV = new List<Vector2>(ReflectiveGlitterPieces * 10);
            var indices = new List<int>(ReflectiveGlitterPieces * 18);
            Bounds bounds = _variantBodyBounds;
            Vector3 interior = bounds.center;
            interior.y = bounds.min.y + bounds.size.y * .43f;
            float bodyScale = bounds.size.y / 2.4f;
            _glitterMaterial.SetVector("_BodyCenter", bounds.center);
            _glitterMaterial.SetVector("_BodyExtents", bounds.extents);

            // Area weighting prevents the high-detail knot from collecting most
            // particles. Barycentric skin UVs retain the dirt/soap coverage mask.
            Mesh source = _bakedColliderMesh;
            if (!source && _body)
            {
                MeshFilter filter = _body.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh && filter.sharedMesh.isReadable) source = filter.sharedMesh;
            }
            Vector2[] bodyUV = source ? source.uv : null;
            int faceCount = _variantBodyTriangles == null ? 0 : _variantBodyTriangles.Length / 3;
            var areas = new float[faceCount];
            float totalArea = 0f;
            for (int face = 0; face < faceCount; face++)
            {
                int triangle = face * 3;
                Vector3 a = _variantBodyVertices[_variantBodyTriangles[triangle]];
                Vector3 b = _variantBodyVertices[_variantBodyTriangles[triangle + 1]];
                Vector3 c = _variantBodyVertices[_variantBodyTriangles[triangle + 2]];
                totalArea += Vector3.Cross(b - a, c - a).magnitude;
                areas[face] = totalArea;
            }
            Color[] foil = {
                new Color(1f,.69f,.22f), new Color(.97f,.99f,1f),
                new Color(1f,.86f,.48f), new Color(.94f,.98f,1f),
                new Color(1f,.73f,.27f), new Color(.93f,.83f,1f)
            };
            for (int piece = 0; piece < ReflectiveGlitterPieces; piece++)
            {
                Vector3 surface;
                Vector2 washableUV = Vector2.zero;
                if (faceCount > 0 && totalArea > .0001f)
                {
                    int face = Array.BinarySearch(areas, Range(random, 0f, totalArea));
                    if (face < 0) face = ~face;
                    int triangle = Mathf.Min(face, faceCount - 1) * 3;
                    int ia = _variantBodyTriangles[triangle], ib = _variantBodyTriangles[triangle + 1], ic = _variantBodyTriangles[triangle + 2];
                    float root = Mathf.Sqrt(Range(random, 0f, 1f));
                    float a = 1f - root, b = root * Range(random, 0f, 1f), c = 1f - a - b;
                    surface = _variantBodyVertices[ia] * a + _variantBodyVertices[ib] * b + _variantBodyVertices[ic] * c;
                    if (bodyUV != null && bodyUV.Length == _variantBodyVertices.Length)
                        washableUV = bodyUV[ia] * a + bodyUV[ib] * b + bodyUV[ic] * c;
                }
                else
                {
                    Vector3 direction = new Vector3(Range(random,-1f,1f), Range(random,-1f,1f), Range(random,-1f,1f)).normalized;
                    surface = bounds.center + Vector3.Scale(direction, bounds.extents * .88f);
                }
                Vector3 center = Vector3.Lerp(interior, surface, Range(random, .65f, .94f));
                // Mostly fine cosmetic glitter with occasional larger foil facets.
                float radius = bodyScale * Mathf.Lerp(.013f, .036f, Mathf.Pow(Range(random,0f,1f), 2.1f));
                float aspect = Range(random, .70f, 1.10f);
                Quaternion rotation = Quaternion.Euler(Range(random,0f,360f), Range(random,0f,360f), Range(random,0f,360f));
                Vector3 normal = rotation * Vector3.forward;
                Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
                Color color = foil[piece % foil.Length];
                color.a = Range(random, .84f, 1f);
                int first = vertices.Count;
                for (int corner = 0; corner < 6; corner++)
                {
                    float angle = corner * Mathf.PI / 3f;
                    Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    vertices.Add(center + radius * (right * point.x + up * point.y * aspect));
                    normals.Add(normal); colors.Add(color); uv.Add(point);
                    centers.Add(new Vector4(center.x, center.y, center.z, 0f));
                    skinUV.Add(washableUV);
                }
                for (int triangle = 1; triangle < 5; triangle++)
                {
                    indices.Add(first); indices.Add(first + triangle); indices.Add(first + triangle + 1);
                }

                // These cards are almost always invisible: the shader reveals a
                // fine diffraction cross only when this foil reflects a light.
                first = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 point = new Vector2(corner == 0 || corner == 3 ? -1f : 1f, corner < 2 ? -1f : 1f);
                    vertices.Add(center); normals.Add(normal); colors.Add(color); uv.Add(point);
                    centers.Add(new Vector4(center.x, center.y, center.z, radius * (piece % 6 == 0 ? 5.5f : 4.1f)));
                    skinUV.Add(washableUV);
                }
                indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                indices.Add(first); indices.Add(first + 2); indices.Add(first + 3);
            }
            var mesh = new Mesh { name = "Mimo 552 metallic foil flakes / 3312 triangles" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetUVs(0, uv); mesh.SetUVs(1, centers); mesh.SetUVs(2, skinUV); mesh.SetTriangles(indices, 0);
            // Glint cards expand in the vertex shader; include their reach in culling.
            mesh.bounds = new Bounds(bounds.center, bounds.size + Vector3.one * (.40f * bodyScale));
            _finishMeshes.Add(mesh);
            return mesh;
        }
    }
}
