using System.IO;
using UnityEditor;
using UnityEngine;

namespace MimoGarden.EditorTools
{
    /// <summary>A small, linear HDR studio environment, baked once rather than rendered every frame.</summary>
    public static class MimoFinishEnvironment
    {
        const string AssetPath = "Assets/Resources/Materials/MimoFinishEnvironment.asset";
        const string Revision = "Mimo finish studio v2 - soft gel";

        public static void Ensure()
        {
            Cubemap existing = AssetDatabase.LoadAssetAtPath<Cubemap>(AssetPath);
            if (existing && existing.name == Revision) return;
            const int size = 256;
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, true)
            {
                name = Revision, filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp, anisoLevel = 1
            };
            for (int face = 0; face < 6; face++)
            {
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        Vector3 direction = Direction(face, 2f * (x + .5f) / size - 1f, 2f * (y + .5f) / size - 1f);
                        pixels[y * size + x] = Studio(direction.normalized);
                    }
                cube.SetPixels(pixels, (CubemapFace)face);
            }
            cube.Apply(true, false);
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            if (existing) { EditorUtility.CopySerialized(cube, existing); Object.DestroyImmediate(cube); EditorUtility.SetDirty(existing); }
            else
            {
                AssetDatabase.CreateAsset(cube, AssetPath);
                cube.name = Revision;
                EditorUtility.SetDirty(cube);
            }
            AssetDatabase.SaveAssets();
        }

        static Vector3 Direction(int face, float u, float v)
        {
            switch (face)
            {
                case 0: return new Vector3(1, -v, -u);
                case 1: return new Vector3(-1, -v, u);
                case 2: return new Vector3(u, 1, v);
                case 3: return new Vector3(u, -1, -v);
                case 4: return new Vector3(u, -v, 1);
                default: return new Vector3(-u, -v, -1);
            }
        }

        static Color Studio(Vector3 d)
        {
            Color floor = new Color(.19f, .14f, .125f, 1);
            Color sky = new Color(.34f, .39f, .48f, 1);
            Color value = Color.Lerp(floor, sky, Mathf.SmoothStep(0, 1, d.y * .5f + .5f));
            // Large diffused photographic windows produce recognisable reflections across a curved body.
            value += Panel(d, new Vector3(-.62f, .68f, -.65f), .24f, .42f, .10f) * new Color(3.5f, 3.35f, 3.1f, 0);
            value += Panel(d, new Vector3(.80f, .32f, -.45f), .08f, .46f, .065f) * new Color(2.3f, 2.9f, 3.3f, 0);
            value += Panel(d, new Vector3(.05f, .96f, .18f), .65f, .35f, .10f) * new Color(1.8f, 1.85f, 2.0f, 0);
            value += Panel(d, new Vector3(-.4f, .4f, .86f), .28f, .48f, .065f) * new Color(2.1f, 1.45f, 1.15f, 0);
            value.a = 1;
            return value;
        }

        static float Panel(Vector3 direction, Vector3 facing, float width, float height, float softness)
        {
            facing.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
            Vector3 up = Vector3.Cross(facing, right);
            float depth = Vector3.Dot(direction, facing);
            if (depth <= 0) return 0;
            float x = Mathf.Abs(Vector3.Dot(direction, right) / depth);
            float y = Mathf.Abs(Vector3.Dot(direction, up) / depth);
            return (1f - Mathf.SmoothStep(0, 1, (x - width + softness) / (2f * softness))) *
                   (1f - Mathf.SmoothStep(0, 1, (y - height + softness) / (2f * softness)));
        }
    }
}
