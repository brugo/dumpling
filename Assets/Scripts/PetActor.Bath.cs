using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        const int SoapSize = 64;
        Texture2D soapMask;
        float[] soapValues;
        Color32[] soapPixels;
        float soapTotal, soapUploadAt;
        bool soapDirty;
        Transform soapBeadRoot;
        MimoBubbleSurface bubbleSurface;
        public int SoapBubbleCount => MimoBubbleSurface.BubbleCount;
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
            bodyMaterial.SetFloat("_SoapAmount", .72f);
            ConfigureFinishMasks();
            bodyMaterial.SetFloat("_SoapAmount", .42f);
        }

        public bool SoapAt(RaycastHit hit, float strength)
        {
            if (hit.collider != _bodyCollider || strength <= 0 || !soapMask) return false;
            PaintSoap(hit.textureCoord, .22f, strength * 8f);
            SpreadSoap(strength * .55f);
            SetExpression("giggle");
            return true;
        }

        public bool RinseAt(RaycastHit hit, float strength)
        {
            if (hit.collider != _bodyCollider || strength <= 0 || !soapMask) return false;
            PaintSoap(hit.textureCoord, .30f, -strength * 8f);
            // Abundant running water reaches around the body, including seams and the far side.
            SpreadSoap(-strength * .8f);
            WashWholeBody(strength);
            if (SoapCoverage < .012f) ClearSoap();
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

        void SpreadSoap(float amount)
        {
            soapTotal = 0;
            for (int i = 0; i < soapValues.Length; i++)
            {
                float value = Mathf.Clamp01(soapValues[i] + amount);
                soapValues[i] = value; soapTotal += value;
                byte mask = (byte)Mathf.RoundToInt(value * 255f); soapPixels[i] = new Color32(mask, mask, mask, 255);
            }
            soapDirty = true;
        }

        void WashWholeBody(float strength)
        {
            if (_dirt == null || Data == null) return;
            _dirtTotal = 0;
            Color pearl = Palette[Data.colorIndex], soil = new Color(.44f, .25f, .18f);
            for (int i = 0; i < _dirt.Length; i++)
            {
                _dirt[i] = Mathf.Max(0, _dirt[i] - _dirtPattern[i] * strength * .9f);
                _dirtTotal += _dirt[i]; _pixels[i] = SkinPixel(pearl, soil, _dirt[i]);
            }
            Data.cleanliness = Mathf.Clamp01(1f - _dirtTotal / Mathf.Max(1f, _dirtCapacity));
            _textureDirty = true;
            if (Time.time >= _nextWashEvent) { _nextWashEvent = Time.time + .3f; StateChanged?.Invoke(); }
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
            if (bubbleSurface) bubbleSurface.SetCoverage(SoapCoverage);
        }

        void BuildSoapBeads()
        {
            MeshFilter filter = _body.GetComponent<MeshFilter>();
            Mesh surface = _bakedColliderMesh ? _bakedColliderMesh : filter ? filter.sharedMesh : null;
            if (!surface) return;
            soapBeadRoot = new GameObject("Dense iridescent soap foam").transform;
            soapBeadRoot.SetParent(_body.transform, false);
            bubbleSurface = soapBeadRoot.gameObject.AddComponent<MimoBubbleSurface>();
            bubbleSurface.Initialize(surface, soapMask);
        }

        void ReleaseSoapLayer()
        {
            if (soapMask) Destroy(soapMask); if (soapBeadRoot) Destroy(soapBeadRoot.gameObject);
            soapMask = null; soapBeadRoot = null; bubbleSurface = null;
            soapValues = null; soapPixels = null;
            soapTotal = 0; soapDirty = false; soapUploadAt = 0;
        }
    }
}
