using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed partial class MimoExperience
    {
        readonly List<Mesh> mobileWorldMeshes = new List<Mesh>();
        readonly List<GameObject> mobileWorldBatches = new List<GameObject>();
        Mesh mobileSphereMesh;
        readonly MobileContact[] mobileContacts = new MobileContact[PetStorage.MaxPets + 1];
        MimoMobileContactShadowUpdater mobileContactUpdater;
        Material mobileContactMaterial;
        Texture2D mobileContactTexture;
        bool mobileWorldOptimized;
        static readonly int MobileContactColor = Shader.PropertyToID("_BaseColor");
        public int MobileStaticRenderersBefore { get; private set; }
        public int MobileStaticRenderersAfter { get; private set; }
        public int MobileStaticTriangleCount { get; private set; }
        public int MobileVisibleContactShadows { get; private set; }

        sealed class MobileContact
        {
            public Transform transform;
            public MeshRenderer renderer;
            public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        }

        Mesh GetMobileSphereMesh()
        {
            if (mobileSphereMesh) return mobileSphereMesh;
            const int longitude = 16, latitude = 10;
            var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
            var normals = new Vector3[vertices.Length]; var uv = new Vector2[vertices.Length];
            var indices = new List<int>(longitude * (latitude - 1) * 6);
            for (int y = 0; y <= latitude; y++)
            {
                float phi = y * Mathf.PI / latitude;
                for (int x = 0; x <= longitude; x++)
                {
                    float theta = x * Mathf.PI * 2f / longitude; int i = y * (longitude + 1) + x;
                    normals[i] = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    vertices[i] = normals[i] * .5f; uv[i] = new Vector2((float)x / longitude, 1f - (float)y / latitude);
                    if (x == longitude || y == latitude) continue;
                    int next = i + 1, below = i + longitude + 1, belowNext = below + 1;
                    if (y != 0) { indices.Add(i); indices.Add(next); indices.Add(below); }
                    if (y != latitude - 1) { indices.Add(next); indices.Add(belowNext); indices.Add(below); }
                }
            }
            mobileSphereMesh = new Mesh { name = "Mimo mobile shared sphere 16x10", vertices = vertices, normals = normals, uv = uv, triangles = indices.ToArray() };
            mobileSphereMesh.RecalculateBounds(); mobileWorldMeshes.Add(mobileSphereMesh);
            return mobileSphereMesh;
        }

        void OptimizeMobileWorld()
        {
            if (!MimoPerformance.Lightweight || mobileWorldOptimized) return;
            mobileWorldOptimized = true;
            CombineMobileRoom(studioShell); CombineMobileRoom(laboratory);
            CombineMobileRoom(kitchenRoom); CombineMobileRoom(bathRoom); CombineMobileRoom(gardenRoom);
            BuildMobileContactShadow(); UpdateMobileContactShadow();
            Debug.Log("MIMO_MOBILE_WORLD staticRenderers=" + MobileStaticRenderersBefore + "->" + MobileStaticRenderersAfter
                + " staticTriangles=" + MobileStaticTriangleCount + " sphereTriangles=" + (GetMobileSphereMesh().triangles.Length / 3));
        }

        bool MobileStaticCandidate(Transform item, Transform root)
        {
            // Parent activity changes with the room, but hidden children and animated items remain independent.
            for (Transform p = item; p && p != root; p = p.parent)
            {
                if (!p.gameObject.activeSelf || p == bowl || p == brew || p == brewParts || p == home || p == tool || p == actorRoot) return false;
                if (p.GetComponent<MimoPlaygroundMovingPart>()) return false;
                foreach (Floaty floating in floaties) if (p == floating.transform) return false;
            }
            return true;
        }

        void CombineMobileRoom(Transform root)
        {
            if (!root) return;
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var renderers = new Dictionary<Material, List<MeshRenderer>>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.sharedMesh.subMeshCount != 1 || !MobileStaticCandidate(filter.transform, root)) continue;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || renderer.HasPropertyBlock()) continue;
                Material material = renderer.sharedMaterial;
                if (!material || material.renderQueue > (int)RenderQueue.GeometryLast || (material.HasProperty("_Surface") && material.GetFloat("_Surface") > .5f)) continue;
                if (!groups.ContainsKey(material)) { groups.Add(material, new List<CombineInstance>()); renderers.Add(material, new List<MeshRenderer>()); }
                groups[material].Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = 0, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                renderers[material].Add(renderer);
            }
            int before = 0, after = 0, triangles = 0;
            foreach (KeyValuePair<Material, List<CombineInstance>> group in groups)
            {
                List<MeshRenderer> originals = renderers[group.Key]; before += originals.Count; after++;
                long vertexCount = 0;
                foreach (CombineInstance item in group.Value)
                {
                    vertexCount += item.mesh.vertexCount;
                    triangles += (int)item.mesh.GetIndexCount(0) / 3;
                }
                if (originals.Count == 1)
                {
                    originals[0].shadowCastingMode = ShadowCastingMode.Off; originals[0].receiveShadows = false;
                    continue;
                }
                var combined = new Mesh { name = root.name + " / mobile / " + group.Key.name };
                if (vertexCount > 65535) combined.indexFormat = IndexFormat.UInt32;
                combined.CombineMeshes(group.Value.ToArray(), true, true, false); combined.RecalculateBounds();
                mobileWorldMeshes.Add(combined);
                Transform batch = Group("Mobile static batch — " + group.Key.name, root);
                batch.gameObject.AddComponent<MeshFilter>().sharedMesh = combined;
                MeshRenderer output = batch.gameObject.AddComponent<MeshRenderer>(); output.sharedMaterial = group.Key;
                output.shadowCastingMode = ShadowCastingMode.Off; output.receiveShadows = false;
                output.lightProbeUsage = LightProbeUsage.Off; output.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mobileWorldBatches.Add(batch.gameObject);
                foreach (MeshRenderer original in originals) original.enabled = false;
            }
            MobileStaticRenderersBefore += before; MobileStaticRenderersAfter += after; MobileStaticTriangleCount += triangles;
            Debug.Log("MIMO_MOBILE_ROOM " + root.name + " staticRenderers=" + before + "->" + after + " triangles=" + triangles);
        }

        void BuildMobileContactShadow()
        {
            if (mobileContactMaterial) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return;
            mobileContactTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = "Mimo soft contact gradient", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = (x + .5f - 32f) / 32f, dy = (y + .5f - 32f) / 32f;
                    float r2 = dx * dx + dy * dy;
                    float alpha = (.72f * Mathf.Exp(-r2*3.2f) + .28f*Mathf.Exp(-r2*12f)) * Mathf.Clamp01((1f-r2)*5f);
                    pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            mobileContactTexture.SetPixels32(pixels); mobileContactTexture.Apply(false, true);
            mobileContactMaterial = new Material(shader) { name = "Mimo mobile contact shadow", renderQueue = (int)RenderQueue.Transparent };
            mobileContactMaterial.SetOverrideTag("RenderType", "Transparent");
            mobileContactMaterial.SetFloat("_Surface", 1); mobileContactMaterial.SetFloat("_Blend", 0);
            mobileContactMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mobileContactMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mobileContactMaterial.SetFloat("_ZWrite", 0); mobileContactMaterial.SetFloat("_Cull", (float)CullMode.Off);
            mobileContactMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mobileContactMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON"); mobileContactMaterial.DisableKeyword("_ALPHATEST_ON");
            mobileContactMaterial.SetTexture("_BaseMap", mobileContactTexture);
            mobileContactMaterial.SetColor("_BaseColor", new Color(.12f,.15f,.13f,.50f));
            var mesh = new Mesh
            {
                name = "Mimo contact shadow quad",
                vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateBounds(); mobileWorldMeshes.Add(mesh);
            // These pooled quads live outside room hierarchies and their static batches.
            // Slot zero belongs to the recipe preview; the other five follow the live roster.
            for (int i = 0; i < mobileContacts.Length; i++)
            {
                Transform contact = Group("Mimo soft ground contact " + i);
                contact.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = contact.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mobileContactMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mobileContacts[i] = new MobileContact { transform = contact, renderer = renderer };
                contact.gameObject.SetActive(false);
            }
            mobileContactUpdater = gameObject.AddComponent<MimoMobileContactShadowUpdater>();
            mobileContactUpdater.Owner = this;
        }

        internal void UpdateMobileContactShadow()
        {
            MobileVisibleContactShadows = 0;
            UpdateMobileContact(mobileContacts[0], creationActor);
            for (int i = 1; i < mobileContacts.Length; i++)
                UpdateMobileContact(mobileContacts[i], i <= playgroundPets.Count ? playgroundPets[i - 1] : null);
        }

        void UpdateMobileContact(MobileContact contact, PetActor pet)
        {
            if (contact == null || !contact.transform) return;
            bool visible = pet && pet.gameObject.activeInHierarchy && place != Place.Birth;
            if (contact.transform.gameObject.activeSelf != visible) contact.transform.gameObject.SetActive(visible);
            if (!visible) return;
            MobileVisibleContactShadows++;
            Transform petTransform = pet.transform;
            Vector3 scale = petTransform.localScale;
            float floor = place == Place.Home ? .083f : .303f;
            Vector3 position = petTransform.position;
            // Root height includes seats and the swing; LiftHeight contains the body's hop/carry motion.
            float rootRestHeight = place == Place.Home ? PetGround : .29f;
            float lift = Mathf.Max(0, position.y - rootRestHeight) + Mathf.Max(0, pet.LiftHeight) * Mathf.Abs(scale.y);
            position.y = floor;
            contact.transform.position = position;
            contact.transform.rotation = Quaternion.Euler(0, petTransform.eulerAngles.y, 0);
            float expansion = 1f + Mathf.Min(lift, 1.4f) * .17f;
            // The soft edge must extend beyond the body silhouette to remain visible from the high park camera.
            contact.transform.localScale = new Vector3(4.6f*Mathf.Abs(scale.x)*expansion,1,3.5f*Mathf.Abs(scale.z)*expansion);
            contact.properties.SetColor(MobileContactColor, new Color(.12f,.15f,.13f,.50f/(1f+lift*1.35f)));
            contact.renderer.SetPropertyBlock(contact.properties);
        }

        void ReleaseMobileWorld()
        {
            if (mobileContactUpdater)
            {
                mobileContactUpdater.Owner = null;
                mobileContactUpdater.enabled = false;
                Destroy(mobileContactUpdater);
                mobileContactUpdater = null;
            }
            for (int i = 0; i < mobileContacts.Length; i++)
            {
                if (mobileContacts[i] != null && mobileContacts[i].transform) Destroy(mobileContacts[i].transform.gameObject);
                mobileContacts[i] = null;
            }
            foreach (GameObject batch in mobileWorldBatches) if (batch) Destroy(batch);
            foreach (Mesh mesh in mobileWorldMeshes) if (mesh) Destroy(mesh);
            if (mobileContactMaterial) Destroy(mobileContactMaterial);
            if (mobileContactTexture) Destroy(mobileContactTexture);
            mobileWorldBatches.Clear(); mobileWorldMeshes.Clear();
            mobileSphereMesh = null; mobileContactMaterial = null; mobileContactTexture = null;
            MobileVisibleContactShadows = 0;
            mobileWorldOptimized = false;
        }
    }

    /// <summary>Runs after the swing/seat LateUpdate so every contact follows this frame's final pose.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class MimoMobileContactShadowUpdater : MonoBehaviour
    {
        internal MimoExperience Owner;
        void LateUpdate()
        {
            if (Owner && Owner.isActiveAndEnabled) Owner.UpdateMobileContactShadow();
        }
    }
}
