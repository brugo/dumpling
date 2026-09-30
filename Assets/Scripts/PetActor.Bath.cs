using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        const int SoapSize = 64, SoapBeads = 32;
        Texture2D soapMask;
        float[] soapValues;
        Color32[] soapPixels;
        float soapTotal, soapUploadAt;
        bool soapDirty;
        Mesh soapBeadMesh;
        Transform soapBeadRoot;
        MeshRenderer soapBeadRenderer;
        Vector3[] soapBeadCenters, soapBeadNormals, soapBeadVertices, soapUnitVertices;
        Vector2[] soapBeadUV;
        float soapBeadRadius;
        public float SoapCoverage => soapValues == null ? 0 : Mathf.Clamp01(soapTotal / soapValues.Length);
        public Texture2D SoapMaskTexture => soapMask;

        void InitializeSoapLayer()
        {
            if (!_body) return;
            if (!soapMask)
            {
                soapValues = new float[SoapSize * SoapSize]; soapPixels = new Color32[soapValues.Length];
                soapMask = new Texture2D(SoapSize, SoapSize, TextureFormat.RGBA32, false, true)
                { name = "Mimo persistent soap coverage", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                soapMask.SetPixels32(soapPixels); soapMask.Apply(false, false);
                BuildSoapBeads();
            }
            Material bodyMaterial = _body.sharedMaterial;
            bodyMaterial.SetTexture("_SoapMask", soapMask);
            bodyMaterial.SetColor("_SoapColor", new Color(.96f, .985f, 1f, 1));
            bodyMaterial.SetFloat("_SoapAmount", 1f);
            ConfigureFinishMasks();
        }

        public bool SoapAt(RaycastHit hit, float strength)
        {
            if (hit.collider != _bodyCollider || strength <= 0 || !soapMask) return false;
            PaintSoap(hit.textureCoord, .16f, strength * 5.5f);
            SetExpression("giggle");
            return true;
        }

        public bool RinseAt(RaycastHit hit, float strength)
        {
            if (hit.collider != _bodyCollider || strength <= 0 || !soapMask) return false;
            PaintSoap(hit.textureCoord, .135f, -strength * 3.3f);
            // Dirt remains under the foam until the water actually reaches this UV patch.
            WashAt(hit, strength * .7f);
            SetExpression("love");
            return true;
        }

        public float SoapAtUV(Vector2 uv)
        {
            if (soapValues == null) return 0;
            int x = Mathf.FloorToInt(Mathf.Repeat(uv.x, 1f) * SoapSize), y = Mathf.Clamp(Mathf.FloorToInt(uv.y * SoapSize), 0, SoapSize - 1);
            return soapValues[y * SoapSize + x];
        }

        public void ClearSoap()
        {
            if (soapValues == null) return;
            System.Array.Clear(soapValues, 0, soapValues.Length); System.Array.Clear(soapPixels, 0, soapPixels.Length);
            soapTotal = 0; soapDirty = true; soapUploadAt = 0;
        }

        void PaintSoap(Vector2 uv, float radius, float amount)
        {
            int y0 = Mathf.Max(0, Mathf.FloorToInt((uv.y - radius) * SoapSize)), y1 = Mathf.Min(SoapSize - 1, Mathf.CeilToInt((uv.y + radius) * SoapSize));
            int x0 = Mathf.FloorToInt((uv.x - radius) * SoapSize), x1 = Mathf.CeilToInt((uv.x + radius) * SoapSize);
            float radiusSquared = radius * radius;
            for (int y = y0; y <= y1; y++)
                for (int sampleX = x0; sampleX <= x1; sampleX++)
                {
                    int x = (sampleX + SoapSize) % SoapSize;
                    float dx = Mathf.Abs((x + .5f) / SoapSize - uv.x); dx = Mathf.Min(dx, 1f - dx);
                    float dy = (y + .5f) / SoapSize - uv.y, distance = dx * dx + dy * dy;
                    if (distance >= radiusSquared) continue;
                    int index = y * SoapSize + x; float old = soapValues[index];
                    float value = Mathf.Clamp01(old + amount * (1f - distance / radiusSquared));
                    soapValues[index] = value; soapTotal += value - old;
                    byte mask = (byte)Mathf.RoundToInt(value * 255f); soapPixels[index] = new Color32(mask, mask, mask, 255);
                }
            soapTotal = Mathf.Clamp(soapTotal, 0, soapValues.Length); soapDirty = true;
        }

        void UpdateSoapLayer()
        {
            if (!soapDirty || !soapMask || Time.unscaledTime < soapUploadAt) return;
            soapUploadAt = Time.unscaledTime + .1f; soapDirty = false;
            soapMask.SetPixels32(soapPixels); soapMask.Apply(false, false);
            if (!soapBeadMesh) return;
            soapBeadRenderer.enabled = SoapCoverage > .0005f;
            for (int bead = 0; bead < SoapBeads; bead++)
            {
                float amount = Mathf.SmoothStep(0, 1, Mathf.Clamp01((SoapAtUV(soapBeadUV[bead]) - .15f) / .65f));
                float radius = soapBeadRadius * (1f + (bead % 3) * .22f) * amount;
                Vector3 center = soapBeadCenters[bead] + soapBeadNormals[bead] * radius * .48f;
                for (int v = 0; v < soapUnitVertices.Length; v++) soapBeadVertices[bead * soapUnitVertices.Length + v] = center + soapUnitVertices[v] * radius;
            }
            soapBeadMesh.vertices = soapBeadVertices;
        }

        void BuildSoapBeads()
        {
            MeshFilter bodyFilter = _body.GetComponent<MeshFilter>(); Mesh source = _bakedColliderMesh ? _bakedColliderMesh : bodyFilter ? bodyFilter.sharedMesh : null;
            if (!source) return;
            Vector3[] bodyVertices = source.vertices, bodyNormals = source.normals; Vector2[] bodyUV = source.uv;
            if (bodyUV.Length != bodyVertices.Length) return;
            soapBeadCenters = new Vector3[SoapBeads]; soapBeadNormals = new Vector3[SoapBeads]; soapBeadUV = new Vector2[SoapBeads];
            var best = new float[SoapBeads]; for (int i = 0; i < best.Length; i++) best[i] = float.MaxValue;
            for (int i = 0; i < bodyVertices.Length; i++)
            {
                Vector2 uv = bodyUV[i]; int column = Mathf.Clamp((int)(uv.x * 8), 0, 7), row = Mathf.Clamp((int)((uv.y - .1f) / .8f * 4), 0, 3), bead = row * 8 + column;
                Vector2 target = new Vector2((column + .5f) / 8f, .1f + (row + .5f) * .2f); float distance = (uv - target).sqrMagnitude;
                if (distance >= best[bead]) continue;
                best[bead] = distance; soapBeadCenters[bead] = bodyVertices[i]; soapBeadNormals[bead] = bodyNormals.Length == bodyVertices.Length ? bodyNormals[i].normalized : (bodyVertices[i] - source.bounds.center).normalized; soapBeadUV[bead] = uv;
            }
            const int longitude = 8, latitude = 5, beadVertices = (longitude + 1) * (latitude + 1);
            soapUnitVertices = new Vector3[beadVertices]; soapBeadVertices = new Vector3[SoapBeads * beadVertices];
            var normals = new Vector3[soapBeadVertices.Length]; var triangles = new int[SoapBeads * longitude * (latitude - 1) * 6]; int cursor = 0;
            for (int y = 0; y <= latitude; y++)
                for (int x = 0; x <= longitude; x++)
                {
                    float phi = y * Mathf.PI / latitude, theta = x * Mathf.PI * 2f / longitude;
                    soapUnitVertices[y * (longitude + 1) + x] = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                }
            for (int bead = 0; bead < SoapBeads; bead++)
            {
                int offset = bead * beadVertices;
                for (int i = 0; i < beadVertices; i++) { soapBeadVertices[offset + i] = soapBeadCenters[bead]; normals[offset + i] = soapUnitVertices[i]; }
                for (int y = 0; y < latitude; y++)
                    for (int x = 0; x < longitude; x++)
                    {
                        int a = offset + y * (longitude + 1) + x, b = a + 1, c = a + longitude + 1, d = c + 1;
                        if (y > 0) { triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = c; }
                        if (y < latitude - 1) { triangles[cursor++] = b; triangles[cursor++] = d; triangles[cursor++] = c; }
                    }
            }
            soapBeadRadius = source.bounds.size.y * .035f;
            soapBeadMesh = new Mesh { name = "Mimo soap bubbles — one light mesh", vertices = soapBeadVertices, normals = normals, triangles = triangles };
            Bounds bounds = source.bounds; bounds.Expand(soapBeadRadius * 5); soapBeadMesh.bounds = bounds; soapBeadMesh.MarkDynamic();
            soapBeadRoot = new GameObject("Persistent soap beads").transform; soapBeadRoot.SetParent(_body.transform, false);
            soapBeadRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = soapBeadMesh;
            soapBeadRenderer = soapBeadRoot.gameObject.AddComponent<MeshRenderer>(); soapBeadRenderer.sharedMaterial = Material("Mimo soap cream bubbles", new Color(.91f, .98f, 1f), .75f);
            soapBeadRenderer.shadowCastingMode = ShadowCastingMode.Off; soapBeadRenderer.receiveShadows = false; soapBeadRenderer.enabled = false;
        }

        void ReleaseSoapLayer()
        {
            if (soapMask) Destroy(soapMask); if (soapBeadRoot) Destroy(soapBeadRoot.gameObject); if (soapBeadMesh) Destroy(soapBeadMesh);
            soapMask = null; soapBeadMesh = null; soapBeadRoot = null; soapBeadRenderer = null;
            soapValues = null; soapPixels = null; soapBeadCenters = soapBeadNormals = soapBeadVertices = soapUnitVertices = null; soapBeadUV = null;
            soapTotal = 0; soapDirty = false; soapUploadAt = 0;
        }
    }
}
