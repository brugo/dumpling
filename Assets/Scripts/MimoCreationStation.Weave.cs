using System;
using System.Collections.Generic;
using UnityEngine;

namespace MimoGarden
{
    public sealed partial class MimoCreationStation
    {
        Material wovenBamboo;

        Material WovenBamboo()
        {
            if (wovenBamboo) return wovenBamboo;
            wovenBamboo = new Material(Resources.Load<Shader>("Shaders/MimoWovenBamboo")) { name = "Continuous woven bamboo ribbons" };
            wovenBamboo.SetColor("_BaseColor", C("DCC39C"));
            materials.Add(wovenBamboo.name, wovenBamboo);
            return wovenBamboo;
        }

        // Each ribbon is a shallow rounded surface, not a row of disconnected blocks.
        // UVs follow its fibres, and vertex tints survive the existing material batching.
        void BambooRibbon(Transform parent, string name, int segments, float length,
            Func<float, float, Vector3> point, bool reverse, float tint)
        {
            const int cross = 5;
            var vertices = new Vector3[(segments + 1) * cross];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var indices = new List<int>(segments * (cross - 1) * 6);
            for (int i = 0; i <= segments; i++) for (int j = 0; j < cross; j++)
            {
                float t = (float)i / segments, v = j / (float)(cross - 1) * 2 - 1;
                int k = i * cross + j;
                vertices[k] = point(t, v); uv[k] = new Vector2(t * length, v);
                float shade = tint * Mathf.Lerp(.84f, 1f, Mathf.Sqrt(Mathf.Max(0, 1 - v * v)));
                colors[k] = new Color(shade, shade, shade, 1);
                if (i == segments || j == cross - 1) continue;
                int n = k + cross;
                if (reverse) indices.AddRange(new[] { k,n,k+1, k+1,n,n+1 });
                else indices.AddRange(new[] { k,k+1,n, k+1,n+1,n });
            }
            Mesh ribbon = Mesh(name, vertices, indices.ToArray());
            ribbon.uv = uv; ribbon.colors = colors;
            Part(name, parent, ribbon, Vector3.zero, Vector3.one, WovenBamboo());
        }

        static float WallRadius(float y) => Mathf.Lerp(1.24f, 1.28f, Mathf.InverseLerp(.17f, .78f, y));

        void BuildWovenWall(Transform parent)
        {
            const int uprights = 32, rows = 5;
            const float start = .235f, pitch = .105f;
            // Broad horizontal bamboo strips alternate in front of and behind the stakes.
            for (int row = 0; row < rows; row++)
            {
                int r = row;
                BambooRibbon(parent, "Horizontal bamboo ribbon " + r, uprights * 6, Mathf.PI * 2 * 1.27f, (t,v) =>
                {
                    float angle = t * Mathf.PI * 2;
                    float y = start + r * pitch + v * .043f;
                    float wave = Mathf.Cos(angle * (uprights / 2) + r * Mathf.PI);
                    float radius = WallRadius(y) + .024f + wave * .013f + .006f * (1 - v * v);
                    return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                }, false, .97f + (r % 3) * .025f);
            }
            // Paired fine uprights match the split reeds visible in the reference basket.
            for (int stake = 0; stake < uprights; stake++) for (int twin = 0; twin < 2; twin++)
            {
                int s = stake;
                float centerAngle = s * Mathf.PI * 2 / uprights + (twin == 0 ? -.014f : .014f);
                BambooRibbon(parent, "Paired upright bamboo reed", rows * 6, .60f, (t,v) =>
                {
                    float y = .185f + t * .60f;
                    float angle = centerAngle + v * .010f;
                    float wave = -Mathf.Cos((y - start) / pitch * Mathf.PI + s * Mathf.PI);
                    float radius = WallRadius(y) + .024f + wave * .013f + .004f * (1 - v * v);
                    return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                }, true, 1.015f - (s % 3) * .018f);
            }
        }

        void BuildWovenDisk(Transform parent, float height, float radius, bool dome)
        {
            const float pitch = .135f;
            int count = Mathf.FloorToInt((radius - .045f) / pitch);
            for (int axis = 0; axis < 2; axis++) for (int strip = -count; strip <= count; strip++)
            {
                int direction = axis, index = strip;
                float offset = strip * pitch, halfWidth = .057f;
                float extent = Mathf.Sqrt(radius * radius - Mathf.Pow(Mathf.Abs(offset) + halfWidth, 2));
                int steps = Mathf.CeilToInt(extent * 2 / pitch * 5);
                BambooRibbon(parent, "Continuous basketweave ribbon", steps, extent * 2, (t,v) =>
                {
                    float across = offset + v * halfWidth;
                    float edge = Mathf.Sqrt(Mathf.Max(0, radius * radius - across * across));
                    float along = Mathf.Lerp(-edge, edge, t);
                    float wave = Mathf.Cos(along / pitch * Mathf.PI + index * Mathf.PI) * (direction == 0 ? 1 : -1);
                    float x = direction == 0 ? along : across, z = direction == 0 ? across : along;
                    float crown = dome ? .055f * Mathf.Max(0, 1 - (x*x + z*z) / (radius*radius)) : 0;
                    float y = height + crown + .009f * wave + .004f * (1 - v*v);
                    return new Vector3(x, y, z);
                }, direction == 1, .97f + Mathf.Abs(strip % 3) * .018f);
            }
        }
    }
}
