using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        Transform workshopBackdrop;
        Mesh visualCylinder;

        Mesh SmoothVisualCylinder()
        {
            if (visualCylinder) return visualCylinder;
            const int sides = 64, ring = sides + 1;
            var vertices = new Vector3[ring * 4 + 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int top = 0; top < 2; top++)
                {
                    int side = top * ring + i, cap = (top + 2) * ring + i;
                    vertices[side] = vertices[cap] = radial * .5f + Vector3.up * (top * 2f - 1f);
                    normals[side] = radial; normals[cap] = top == 1 ? Vector3.up : Vector3.down;
                    uv[side] = new Vector2((float)i / sides, top); uv[cap] = new Vector2(radial.x, radial.z) * .5f + Vector2.one * .5f;
                }
                if (i == sides) continue;
                triangles.AddRange(new[] { i, ring + i, i + 1, i + 1, ring + i, ring + i + 1 });
                triangles.AddRange(new[] { ring * 4, ring * 2 + i, ring * 2 + i + 1 });
                triangles.AddRange(new[] { ring * 4 + 1, ring * 3 + i + 1, ring * 3 + i });
            }
            vertices[ring * 4] = Vector3.down; vertices[ring * 4 + 1] = Vector3.up;
            normals[ring * 4] = Vector3.down; normals[ring * 4 + 1] = Vector3.up;
            uv[ring * 4] = uv[ring * 4 + 1] = Vector2.one * .5f;
            visualCylinder = new Mesh { name = "Smooth shared tabletop cylinder", vertices = vertices, normals = normals, uv = uv, triangles = triangles.ToArray() };
            visualCylinder.RecalculateBounds(); mobileWorldMeshes.Add(visualCylinder);
            return visualCylinder;
        }

        void BuildVisualWorkshop()
        {
            // Preserve the scene roots used by navigation and the existing recipe state.
            foreach (Transform item in studioShell) item.gameObject.SetActive(false);
            foreach (Transform item in laboratory)
            {
                if (item == bowl || item == brew || item == brewParts) continue;
                if (item.name == "Pedestal shadow trim" || item.name == "Round creation table" || item.name == "Concentric inlay") continue;
                item.gameObject.SetActive(false);
            }
            Texture2D picture = Resources.Load<Texture2D>("Environment/MimoLanternWorkshop");
            if (!picture) { Debug.LogError("Workshop backdrop missing."); return; }
            Material backdrop = Mat("Lantern workshop panorama", Color.white);
            backdrop.shader = Shader.Find("Universal Render Pipeline/Unlit");
            backdrop.SetColor("_BaseColor", Color.white); backdrop.SetTexture("_BaseMap", picture);
            workshopBackdrop = Shape("Sunlit lantern street through the workshop", PrimitiveType.Quad, studioShell, Vector3.zero, Vector3.one, backdrop);
            Renderer r = workshopBackdrop.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            LayoutVisualWorkshop();
        }

        void LayoutVisualWorkshop()
        {
            if (!workshopBackdrop || !sceneCamera) return;
            const float distance = 30f;
            float height = 2f * Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * .5f) * distance;
            float width = Mathf.Max(height * 16f / 9f, height * sceneCamera.aspect);
            workshopBackdrop.SetPositionAndRotation(sceneCamera.transform.position + sceneCamera.transform.forward * distance, sceneCamera.transform.rotation);
            workshopBackdrop.localScale = new Vector3(width, height, 1);
        }
    }
}
