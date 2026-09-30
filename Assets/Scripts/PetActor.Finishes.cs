using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MimoGarden
{
    public sealed partial class PetActor
    {
        Shader _finishShader;
        Cubemap _finishEnvironment;
        bool _sceneRefraction;
        Material _glitterMaterial, _auraMaterial;
        MeshRenderer _glitterRenderer, _auraRenderer;
        readonly List<Mesh> _finishMeshes = new List<Mesh>();

        public int FinishIndex => Data == null ? 0 : Mathf.Clamp(Data.finishIndex, 0, PetCatalog.FinishNames.Length - 1);
        public string FinishName => PetCatalog.FinishNames[FinishIndex];
        public bool IsTranslucentFinish => FinishIndex == 2 || FinishIndex == 3;
        public int InternalGlitterCount => _glitterRenderer && _glitterRenderer.enabled ? ReflectiveGlitterPieces : 0;
        public bool HasExternalGlow => _auraRenderer && _auraRenderer.enabled;
        public bool FinishMaterialsReady => _finishShader && _finishShader.isSupported && _finishEnvironment && _body && _body.sharedMaterial.shader == _finishShader;

        struct FinishStyle
        {
            public float opacity, smoothness, shine, rim, iridescence, grain, transmission, thickness, coat, microNormal, subsurface, environment;
            public FinishStyle(float opacity, float smoothness, float shine, float rim, float iridescence, float grain,
                float transmission, float thickness, float coat, float microNormal, float subsurface, float environment)
            {
                this.opacity = opacity; this.smoothness = smoothness; this.shine = shine; this.rim = rim; this.iridescence = iridescence; this.grain = grain;
                this.transmission = transmission; this.thickness = thickness; this.coat = coat; this.microNormal = microNormal; this.subsurface = subsurface; this.environment = environment;
            }
        }

        static readonly FinishStyle[] FinishStyles = {
            new FinishStyle(1f, .24f, .14f, .13f, 0f, 0f, 0f, 1f, .08f, .08f, .22f, .60f), // Powdered mochi with soft subsurface light.
            new FinishStyle(1f, .68f, .32f, .35f, .72f, 0f, 0f, 1f, .30f, .035f, .09f, .90f), // Layered nacre, satin reflections.
            new FinishStyle(.91f, .96f, .60f, .48f, .035f, 0f, .82f, 1.80f, .85f, .025f, .20f, .95f), // Thick transparent jelly with a wet polished skin.
            new FinishStyle(.93f, .94f, .58f, .48f, .06f, 0f, .78f, 1.50f, .82f, .025f, .17f, .98f), // Clear jelly containing reflective metallic flakes.
            new FinishStyle(1f, .50f, .38f, .22f, .10f, 1f, 0f, 1f, .15f, .24f, .14f, .85f), // Angular frosted sugar grains.
            new FinishStyle(1f, .82f, .35f, 1.15f, 1f, 0f, 0f, 1f, .50f, .055f, .15f, .90f) // Opalescent luminous glaze.
        };

        public void SetFinish(int finish)
        {
            if (Data == null) Data = new PetSave();
            PetCatalog.Set(Data, 7, finish);
            if (!_ready) return;
            ApplyFinish();
            StateChanged?.Invoke();
        }

        void BuildFinishes()
        {
            _finishShader = Resources.Load<Shader>("Shaders/MimoPastelFinish");
            _finishEnvironment = Resources.Load<Cubemap>("Materials/MimoFinishEnvironment");
            Shader sparkleShader = Resources.Load<Shader>("Shaders/MimoFinishSparkles");
            if (!_finishShader || !_finishShader.isSupported || !_finishEnvironment || !sparkleShader || !sparkleShader.isSupported)
                throw new InvalidOperationException("Os materiais pastel do Mimo não estão disponíveis neste renderizador.");

            _glitterMaterial = new Material(sparkleShader) { name = "Mimo · lâminas de glitter refletivo no gel", renderQueue = 3010 };
            _glitterMaterial.SetFloat("_Billboard", 0f);
            _glitterMaterial.SetFloat("_Intensity", 1.35f);
            _glitterMaterial.SetTexture("_FinishEnvironment", _finishEnvironment);
            _glitterMaterial.SetFloat("_EnvironmentStrength", 1f);
            _ownedMaterials.Add(_glitterMaterial);
            _auraMaterial = new Material(sparkleShader) { name = "Mimo · aura suave sem pós-processamento", renderQueue = 2990 };
            _auraMaterial.SetFloat("_Billboard", 1f);
            _auraMaterial.SetFloat("_Intensity", 1f);
            _ownedMaterials.Add(_auraMaterial);

            _glitterRenderer = FinishRenderer("Mimo internal glitter — one mesh", BuildReflectiveGlitter(), _glitterMaterial);
            _auraRenderer = FinishRenderer("Mimo soft outer glow", BuildAuraQuad(), _auraMaterial);
            _auraRenderer.transform.localPosition = _variantBodyBounds.center;
            // These two purely visual effects do not enlarge the physical drag footprint.
            // They start disabled while CacheVisibleGeometry records the solid character.
            _glitterRenderer.enabled = _auraRenderer.enabled = false;
        }

        MeshRenderer FinishRenderer(string name, Mesh mesh, Material material)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(_motion, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        void ApplyFinish()
        {
            if (!_finishShader || !_body || Data == null) return;
            Data.finishIndex = FinishIndex;
            FinishStyle style = FinishStyles[FinishIndex];
            ApplyFinishMaterial(_body.sharedMaterial, style, true);
            ApplyFinishMaterial(_pearl, style, false);
            Color pastel = Palette[Data.colorIndex];
            _glitterMaterial.SetColor("_BaseColor", Color.Lerp(pastel, Color.white, .85f));
            Color glow = Color.Lerp(pastel, Color.white, .50f); glow.a = .8f;
            _auraMaterial.SetColor("_BaseColor", glow);
            _glitterRenderer.enabled = FinishIndex == 3;
            _auraRenderer.enabled = FinishIndex == 5;
            ConfigureFinishMasks();
            // The opaque copy ends at queue 2500. Eyes then write their depth before
            // the shell: front eyes remain crisp, rear eyes are filtered through the gel.
            if (_eyeMaterial) _eyeMaterial.renderQueue = IsTranslucentFinish ? 2995 : -1;
            if (_whiteMaterial) _whiteMaterial.renderQueue = IsTranslucentFinish ? 2996 : -1;
            if (_blushMaterial) _blushMaterial.renderQueue = IsTranslucentFinish ? 2997 : 3000;
        }

        void ConfigureFinishMasks()
        {
            if (!_glitterMaterial) return;
            _glitterMaterial.SetTexture("_SkinMask", _skin ? _skin : Texture2D.whiteTexture);
            _glitterMaterial.SetTexture("_SoapMask", soapMask ? soapMask : Texture2D.blackTexture);
        }

        public void SetSceneRefraction(bool enabled)
        {
            if (_sceneRefraction == enabled) return;
            _sceneRefraction = enabled;
            if (!_finishShader || !_body) return;
            _body.sharedMaterial.SetFloat("_SceneRefraction", enabled ? 1f : 0f);
            _pearl.SetFloat("_SceneRefraction", enabled ? 1f : 0f);
        }

        void ApplyFinishMaterial(Material material, FinishStyle style, bool body)
        {
            if (!material) return;
            if (material.shader != _finishShader)
            {
                material.shader = _finishShader;
                material.shaderKeywords = Array.Empty<string>();
            }
            Color tint = body ? Color.white : Palette[Data.colorIndex];
            material.SetColor("_BaseColor", tint);
            material.SetColor("_Color", tint);
            material.SetTexture("_BaseMap", body ? _skin : Texture2D.whiteTexture);
            material.SetFloat("_Opacity", body ? style.opacity : Mathf.Max(style.opacity, .76f));
            material.SetFloat("_Smoothness", style.smoothness);
            material.SetFloat("_SpecularStrength", style.shine);
            material.SetFloat("_RimStrength", style.rim);
            material.SetFloat("_Iridescence", style.iridescence);
            material.SetFloat("_Grain", style.grain);
            material.SetTexture("_FinishEnvironment", _finishEnvironment);
            material.SetFloat("_EnvironmentMipCount", _finishEnvironment.mipmapCount);
            material.SetFloat("_EnvironmentStrength", style.environment);
            material.SetFloat("_FinishKind", FinishIndex);
            material.SetFloat("_Transmission", style.transmission);
            material.SetFloat("_Thickness", body ? style.thickness : style.thickness * .55f);
            material.SetFloat("_RefractionStrength", body ? (FinishIndex == 2 ? .06f : .05f) : .018f);
            material.SetFloat("_Subsurface", style.subsurface);
            material.SetFloat("_CoatStrength", style.coat);
            material.SetFloat("_MicroNormalStrength", style.microNormal);
            material.SetFloat("_DetailScale", FinishIndex == 4 ? 150f : 72f);
            material.SetFloat("_SceneRefraction", _sceneRefraction ? 1f : 0f);
            material.SetColor("_RimColor", Color.Lerp(Palette[Data.colorIndex], Color.white, .78f));
            // Soap owns its mask texture; recipe changes never replace or erase that texture.
            material.SetFloat("_SoapAmount", body ? 1f : 0f);
            bool translucent = style.opacity < .99f;
            material.SetFloat("_SrcBlend", (float)(translucent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(translucent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", translucent ? 0f : 1f);
            material.SetOverrideTag("RenderType", translucent ? "Transparent" : "Opaque");
            material.renderQueue = translucent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
        }

        Mesh BuildAuraQuad()
        {
            float x = _variantBodyBounds.extents.x * 1.38f;
            float y = _variantBodyBounds.extents.y * 1.40f;
            var mesh = new Mesh {
                name = "Mimo soft aura / 2 triangles",
                vertices = new[] { new Vector3(-x,-y,0), new Vector3(x,-y,0), new Vector3(x,y,0), new Vector3(-x,y,0) },
                normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0,2,1, 0,3,2 }
            };
            // The shader faces the camera, so allow its envelope in every local direction.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * (Mathf.Max(x, y) * 2.2f));
            _finishMeshes.Add(mesh); return mesh;
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        void ReleaseFinishes()
        {
            if (_glitterRenderer) _glitterRenderer.enabled = false;
            if (_auraRenderer) _auraRenderer.enabled = false;
            foreach (Mesh mesh in _finishMeshes) if (mesh) Destroy(mesh);
            _finishMeshes.Clear();
            _glitterRenderer = _auraRenderer = null;
            _glitterMaterial = _auraMaterial = null;
            _finishShader = null;
            _finishEnvironment = null;
            _sceneRefraction = false;
        }
    }
}
