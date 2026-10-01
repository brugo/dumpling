using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    // Thousands of shaded bubble billboards in one draw, sampled across the actual body surface.
    public sealed class MimoBubbleSurface : MonoBehaviour
    {
        public const int BubbleCount = 3072;
        Mesh mesh;
        Material material;
        MeshRenderer bubbles;
        public void Initialize(Mesh surface, Texture2D mask)
        {
            Vector3[] points = surface.vertices, normals = surface.normals;
            Vector2[] skin = surface.uv;
            int[] indices = surface.triangles;
            int faces = indices.Length / 3;
            var areas = new float[faces]; float total = 0;
            for (int i = 0; i < faces; i++)
            {
                int a = indices[i * 3], b = indices[i * 3 + 1], c = indices[i * 3 + 2];
                total += Vector3.Cross(points[b] - points[a], points[c] - points[a]).magnitude;
                areas[i] = total;
            }
            var vertices = new Vector3[BubbleCount * 4]; var vertexNormals = new Vector3[vertices.Length];
            var corners = new Vector2[vertices.Length]; var skinUV = new Vector2[vertices.Length];
            var sizes = new Vector2[vertices.Length]; var triangles = new int[BubbleCount * 6];
            Vector2[] quad = { new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1) };
            var random = new System.Random(553072);
            for (int bubble = 0; bubble < BubbleCount; bubble++)
            {
                int face = Array.BinarySearch(areas, (float)random.NextDouble() * total);
                if (face < 0) face = ~face; face = Mathf.Min(face, faces - 1);
                int a = indices[face * 3], b = indices[face * 3 + 1], c = indices[face * 3 + 2];
                float root = Mathf.Sqrt((float)random.NextDouble()), u = 1 - root, v = root * (float)random.NextDouble(), w = 1 - u - v;
                Vector3 normal = (normals[a] * u + normals[b] * v + normals[c] * w).normalized;
                Vector2 uvA = skin[a], uvB = skin[b], uvC = skin[c];
                if (Mathf.Abs(uvB.x - uvA.x) > .5f) uvB.x += uvB.x < uvA.x ? 1 : -1;
                if (Mathf.Abs(uvC.x - uvA.x) > .5f) uvC.x += uvC.x < uvA.x ? 1 : -1;
                Vector2 uv = uvA * u + uvB * v + uvC * w; uv.x = Mathf.Repeat(uv.x, 1);
                float seed = (float)random.NextDouble();
                // Larger bubbles sit among thousands of fine foam bubbles, so the soap film reads at tablet size.
                float radius = surface.bounds.size.y * (bubble < 280 ? Mathf.Lerp(.024f,.061f,seed) : Mathf.Lerp(.005f,.018f,seed*seed));
                Vector3 center = points[a] * u + points[b] * v + points[c] * w + normal * radius * .5f;
                int offset = bubble * 4;
                for (int q = 0; q < 4; q++)
                { vertices[offset + q] = center; vertexNormals[offset + q] = normal; corners[offset + q] = quad[q]; skinUV[offset + q] = uv; sizes[offset + q] = new Vector2(radius, seed); }
                int t = bubble * 6;
                triangles[t] = offset; triangles[t+1] = offset+1; triangles[t+2] = offset+2;
                triangles[t+3] = offset; triangles[t+4] = offset+2; triangles[t+5] = offset+3;
            }
            mesh = new Mesh { name = "3072 iridescent soap bubbles", vertices = vertices, normals = vertexNormals, uv = corners, uv2 = skinUV, triangles = triangles };
            mesh.SetUVs(2, new List<Vector2>(sizes));
            Bounds bounds = surface.bounds; bounds.Expand(surface.bounds.size.y * .09f); mesh.bounds = bounds;
            material = new Material(Shader.Find("MimoGarden/SoapBubbles")) { name = "Transparent rainbow soap film" };
            material.SetTexture("_SoapMask", mask);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            bubbles = gameObject.AddComponent<MeshRenderer>(); bubbles.sharedMaterial = material;
            bubbles.shadowCastingMode = ShadowCastingMode.Off; bubbles.receiveShadows = false; bubbles.enabled = false;
        }
        public void SetCoverage(float coverage) { if (bubbles) bubbles.enabled = coverage > .0005f; }
        void OnDestroy() { if (mesh) Destroy(mesh); if (material) Destroy(material); }
    }
}
