using System;
using System.Collections.Generic;
using UnityEngine;

namespace MimoGarden
{
    /// <summary>The same editable 3D character is used for recipes, care and the saved pet.</summary>
    public sealed partial class PetActor : MonoBehaviour
    {
        public PetSave Data { get; private set; }
        public Transform ModelRoot { get; private set; }
        public string CurrentExpression => Time.time <= _expressionUntil ? _expression : (Data != null && Data.hunger < .48f ? "hungry" : "happy");
        public float SquashAmount => _squash;
        public bool IsBeingCarried => _carried;
        public float LiftHeight => _jumpHeight;
        public event Action StateChanged;
        public static readonly Color[] Palette = PetCatalog.Colors;

        private const int TextureSize = 256;
        private Transform _motion, _leftEye, _rightEye, _mouth, _leftEar, _rightEar, _earGroup, _puff, _curl;
        private Renderer _body;
        private Collider _bodyCollider;
        private Material _pearl, _eyeMaterial, _blushMaterial, _whiteMaterial, _innerEarMaterial;
        private Texture2D _skin;
        private Mesh _bakedColliderMesh;
        private Color32[] _pixels;
        private float[] _dirt, _dirtPattern;
        private float _dirtCapacity, _dirtTotal;
        private Vector3 _leftEyeScale, _rightEyeScale, _mouthScale;
        private Vector3 _mouthPosition, _mouthGeometryCenter;
        private int _leftEyeVertical = 1, _rightEyeVertical = 1, _mouthVertical = 1;
        private Quaternion _mouthRotation;
        private Quaternion _leftEyeRotation, _rightEyeRotation, _leftEarRotation, _rightEarRotation;
        private string _expression = "happy";
        private float _expressionUntil, _blinkAt, _blinkStarted = -10f;
        private float _squash, _squashVelocity, _jumpHeight, _jumpVelocity, _pulse, _eatUntil;
        private float _nextNeedUpdate, _nextWashEvent, _nextTextureUpload;
        private bool _pressed, _textureDirty, _ready;
        private bool _carried, _dropBounce;
        private float _pressure = .34f;
        private Vector2 _squeezeLean, _carryLean, _smoothLean;
        private readonly List<Material> _ownedMaterials = new List<Material>();

        public void Initialize(PetSave data)
        {
            Data = data ?? new PetSave();
            PetStorage.Sanitize(Data);
            if (!_ready) BuildCharacter();
            ApplyRecipe(Data.colorIndex, Data.earsIndex, Data.tailIndex);
            ResetDirtFromState();
            InitializeSoapLayer();
            if (!Data.created) ClearSoap();
            _blinkAt = Time.time + UnityEngine.Random.Range(1.4f, 3.2f);
            _nextNeedUpdate = Time.time + 10f;
            SetExpression("happy");
        }

        public void SetData(PetSave data) => Initialize(data);

        private void BuildCharacter()
        {
            // A failed initialization must not leave an unconfigured, visible FBX behind.
            ReleaseCharacter();
            try
            {
                CreateCharacter();
            }
            catch
            {
                ReleaseCharacter();
                throw;
            }
        }

        private void CreateCharacter()
        {
            _motion = new GameObject("MimoMotion").transform;
            _motion.SetParent(transform, false);
            GameObject prefab = Resources.Load<GameObject>("MimoCharacter");
            if (prefab != null)
            {
                ModelRoot = Instantiate(prefab, _motion).transform;
                ModelRoot.name = "MimoCharacter";
                Transform bodyPart = FindPart("Body");
                if (bodyPart) _body = bodyPart.GetComponent<Renderer>();
            }
            else
            {
                Debug.LogError("Resources/MimoCharacter está ausente. Exibindo apenas uma forma temporária.");
                ModelRoot = new GameObject("MimoMissingAsset").transform;
                ModelRoot.SetParent(_motion, false);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Body";
                sphere.transform.SetParent(ModelRoot, false);
                sphere.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                sphere.transform.localScale = new Vector3(2.8f, 2.4f, 2.5f);
                Destroy(sphere.GetComponent<Collider>());
                _body = sphere.GetComponent<Renderer>();
            }

            if (_body == null)
            {
                _body = ModelRoot.GetComponentInChildren<Renderer>();
                Debug.LogWarning("Objeto Body não encontrado no Mimo; usando a primeira superfície disponível.");
            }
            OrientFace();
            NormalizeBody();
            _leftEye = FindPart("Eye.L");
            _rightEye = FindPart("Eye.R");
            _mouth = FindPart("Mouth");
            _leftEar = FindPart("Ear.L");
            _rightEar = FindPart("Ear.R");
            _earGroup = FindPart("RabbitEars");
            _puff = FindPart("Tail.Puff");
            _curl = FindPart("Tail.Curl");
            if (_leftEye) { _leftEyeScale = _leftEye.localScale; _leftEyeRotation = _leftEye.localRotation; _leftEyeVertical = VerticalAxis(_leftEye); }
            if (_rightEye) { _rightEyeScale = _rightEye.localScale; _rightEyeRotation = _rightEye.localRotation; _rightEyeVertical = VerticalAxis(_rightEye); }
            if (_mouth)
            {
                _mouthScale = _mouth.localScale;
                _mouthPosition = _mouth.localPosition;
                _mouthRotation = _mouth.localRotation;
                _mouthVertical = VerticalAxis(_mouth);
                Renderer mouthRenderer = _mouth.GetComponent<Renderer>();
                if (mouthRenderer) _mouthGeometryCenter = _mouth.InverseTransformPoint(mouthRenderer.bounds.center);
            }
            if (_leftEar) _leftEarRotation = _leftEar.localRotation;
            if (_rightEar) _rightEarRotation = _rightEar.localRotation;
            ConfigureMaterials();
            ConfigureCollider();
            BuildBodyVariants();
            BuildAccessories();
            BuildFinishes();
            CacheVisibleGeometry();
            _ready = true;
        }

        private Transform FindPart(string part)
        {
            if (!ModelRoot) return null;
            string expected = CanonicalName(part);
            foreach (Transform child in ModelRoot.GetComponentsInChildren<Transform>(true))
                if (CanonicalName(child.name) == expected) return child;
            return null;
        }

        private static string CanonicalName(string value)
        {
            return value.Replace(".", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
        }

        private int VerticalAxis(Transform part)
        {
            Vector3 direction = part.InverseTransformDirection(_motion.up);
            float x = Mathf.Abs(direction.x), y = Mathf.Abs(direction.y), z = Mathf.Abs(direction.z);
            return x > y && x > z ? 0 : y > z ? 1 : 2;
        }

        private static Vector3 ScaleVertical(Vector3 original, int axis, float amount)
        {
            original[axis] *= amount;
            return original;
        }

        private void NormalizeBody()
        {
            if (_body == null) return;
            Bounds bounds = BodyBoundsInMotion();
            if (bounds.size.y > 0.001f) ModelRoot.localScale *= 2.4f / bounds.size.y;
            bounds = BodyBoundsInMotion();
            ModelRoot.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private void OrientFace()
        {
            if (!_body) return;
            Transform eye = FindPart("Eye.L");
            if (!eye) return;
            Vector3 eyeFromCenter = _motion.InverseTransformPoint(eye.position) - _motion.InverseTransformPoint(_body.bounds.center);
            eyeFromCenter.y = 0f;
            // Imported DCC axes can vary. The camera and interactions consistently use front=-Z.
            if (eyeFromCenter.sqrMagnitude > .0000001f)
            {
                Transform otherEye = FindPart("Eye.R");
                if (otherEye)
                {
                    Vector3 otherFromCenter = _motion.InverseTransformPoint(otherEye.position) - _motion.InverseTransformPoint(_body.bounds.center);
                    otherFromCenter.y = 0f;
                    eyeFromCenter = (eyeFromCenter + otherFromCenter) * .5f;
                }
                if (eyeFromCenter.sqrMagnitude > .0000001f)
                    ModelRoot.localRotation = Quaternion.FromToRotation(eyeFromCenter.normalized, Vector3.back) * ModelRoot.localRotation;
            }
        }

        private Bounds BodyBoundsInMotion()
        {
            MeshFilter filter = _body.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = _body as SkinnedMeshRenderer;
            Mesh neutral = filter ? filter.sharedMesh : skinned ? skinned.sharedMesh : null;
            // Renderer bounds may include the reach of every blend shape. Size the neutral body itself.
            Bounds bounds = neutral ? neutral.bounds : _body.bounds;
            Bounds result = new Bounds(neutral
                ? _motion.InverseTransformPoint(_body.transform.TransformPoint(bounds.center))
                : _motion.InverseTransformPoint(bounds.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        result.Encapsulate(_motion.InverseTransformPoint(neutral ? _body.transform.TransformPoint(point) : point));
                    }
            return result;
        }

        private Material Material(string label, Color color, float smoothness, float metallic = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            if (!shader) throw new InvalidOperationException("Nenhum shader compatível encontrado para o Mimo.");
            var material = new Material(shader) { name = label };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_SpecColor")) material.SetColor("_SpecColor", new Color(0.7f, 0.62f, 0.66f));
            _ownedMaterials.Add(material);
            return material;
        }

        private void ConfigureMaterials()
        {
            _pearl = Material("Mimo · pérola macia", Color.white, 0.48f, 0.07f);
            _eyeMaterial = Material("Mimo · olhinhos", new Color(0.075f, 0.038f, 0.07f), 0.85f);
            _blushMaterial = Material("Mimo · bochechas", new Color(1f, 0.47f, 0.60f), 0.32f);
            Shader vertexBlush = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (vertexBlush != null)
            {
                _blushMaterial.shader = vertexBlush;
                // The authored vertex alpha fades the cheeks smoothly into the body.
                _blushMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, .68f));
                _blushMaterial.SetColor("_Color", new Color(1f, 1f, 1f, .68f));
                _blushMaterial.SetFloat("_Surface", 1f);
                _blushMaterial.SetFloat("_Blend", 0f);
                _blushMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _blushMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _blushMaterial.SetFloat("_ZWrite", 0f);
                _blushMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _blushMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            _whiteMaterial = Material("Mimo · brilho dos olhos", new Color(1f, 0.98f, 0.91f), 0.55f);
            _innerEarMaterial = Material("Mimo · interior das orelhas", new Color(1f, 0.54f, 0.67f), 0.40f);
            foreach (Renderer renderer in ModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                string label = CanonicalName(renderer.name);
                Material material = _pearl;
                if (label.StartsWith("eye") || label.StartsWith("mouth") || label.Contains("brow")) material = _eyeMaterial;
                if (label.Contains("blush")) material = _blushMaterial;
                if (label.Contains("highlight") || label.Contains("sparkle")) material = _whiteMaterial;
                if (label.Contains("innerear")) material = _innerEarMaterial;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (label.Contains("blush")) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            _skin = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, false);
            _skin.name = "Mimo pintura lavável";
            _skin.wrapMode = TextureWrapMode.Repeat;
            _skin.filterMode = FilterMode.Bilinear;
            _pixels = new Color32[TextureSize * TextureSize];
            _dirt = new float[_pixels.Length];
            _dirtPattern = new float[_pixels.Length];
            BuildDirtPattern();
            // Body gets a private texture so optional ears and tails remain pearl coloured.
            if (_body)
            {
                Material bodyMaterial = new Material(_pearl) { name = "Mimo · corpo lavável" };
                bodyMaterial.SetTexture("_BaseMap", _skin);
                bodyMaterial.SetTexture("_MainTex", _skin);
                _ownedMaterials.Add(bodyMaterial);
                _body.sharedMaterial = bodyMaterial;
            }
        }

        private void ConfigureCollider()
        {
            if (!_body) return;
            MeshFilter filter = _body.GetComponent<MeshFilter>();
            SkinnedMeshRenderer skinned = _body as SkinnedMeshRenderer;
            Mesh colliderMesh = filter ? filter.sharedMesh : null;
            if (skinned && skinned.sharedMesh)
            {
                // The authored FBX keeps blend shapes, so Unity imports Body as a skinned mesh.
                // Current motion deforms its common parent; bake the neutral mesh once for exact UV hits.
                _bakedColliderMesh = new Mesh { name = "Mimo neutral wash collider" };
                skinned.BakeMesh(_bakedColliderMesh);
                _bakedColliderMesh.RecalculateBounds();
                colliderMesh = _bakedColliderMesh;
                // Expressions animate transforms, never the imported blend shapes or bones.
                // Render this same neutral bake without scheduling skinning every frame.
                // Give the replacement its own identity transform: Unity need not host two
                // Renderer subclasses on one object while Destroy is deferred until frame end.
                GameObject surface = new GameObject("Body");
                surface.transform.SetParent(skinned.transform, false);
                filter = surface.AddComponent<MeshFilter>();
                filter.sharedMesh = _bakedColliderMesh;
                MeshRenderer neutralRenderer = surface.AddComponent<MeshRenderer>();
                neutralRenderer.sharedMaterials = skinned.sharedMaterials;
                neutralRenderer.shadowCastingMode = skinned.shadowCastingMode;
                neutralRenderer.receiveShadows = skinned.receiveShadows;
                neutralRenderer.lightProbeUsage = skinned.lightProbeUsage;
                neutralRenderer.reflectionProbeUsage = skinned.reflectionProbeUsage;
                neutralRenderer.renderingLayerMask = skinned.renderingLayerMask;
                skinned.enabled = false;
                _body = neutralRenderer;
                Destroy(skinned);
            }
            if (colliderMesh)
            {
                // Unity's missing-component placeholder is not a CLR null in the Editor.
                // Use Unity's object lifetime check, rather than ?? or ?., before accessing it.
                MeshCollider collider = _body.GetComponent<MeshCollider>();
                if (!collider) collider = _body.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = colliderMesh;
                collider.convex = false;
                _bodyCollider = collider;
            }
            else
            {
                _bodyCollider = _body.GetComponent<Collider>();
                if (!_bodyCollider) _bodyCollider = _body.gameObject.AddComponent<BoxCollider>();
            }
        }

        public void ApplyRecipe(int color, int ears, int tail)
        {
            if (Data == null) Data = new PetSave();
            Data.colorIndex = Mathf.Clamp(color, 0, Palette.Length - 1);
            Data.earsIndex = Mathf.Clamp(ears, 0, PetCatalog.EarNames.Length - 1);
            Data.tailIndex = Mathf.Clamp(tail, 0, PetCatalog.TailNames.Length - 1);
            Data.eyeIndex = Mathf.Clamp(Data.eyeIndex, 0, PetCatalog.EyeNames.Length - 1);
            if (!_ready) return;
            Color pearl = Palette[Data.colorIndex];
            _pearl.SetColor("_BaseColor", pearl);
            _pearl.SetColor("_Color", pearl);
            if (_earGroup) _earGroup.gameObject.SetActive(Data.earsIndex == 1);
            else
            {
                if (_leftEar) _leftEar.gameObject.SetActive(Data.earsIndex == 1);
                if (_rightEar) _rightEar.gameObject.SetActive(Data.earsIndex == 1);
            }
            if (_puff) _puff.gameObject.SetActive(Data.tailIndex == 1);
            if (_curl) _curl.gameObject.SetActive(Data.tailIndex == 2);
            ApplyBodyVariants();
            ApplyAccessories();
            ApplyFinish();
            ApplyCosmetic();
            RefreshVisibleGeometry();
            UploadSkin();
            _pulse = 0.3f;
            StateChanged?.Invoke();
        }

        public void SetExpression(string expression)
        {
            _expression = string.IsNullOrWhiteSpace(expression) ? "happy" : expression.ToLowerInvariant();
            _expressionUntil = Time.time + 4f;
        }

        public void Feed()
        {
            if (Data == null) return;
            Data.hunger = Mathf.Clamp01(Data.hunger + 0.24f);
            Data.joy = Mathf.Clamp01(Data.joy + 0.045f);
            _eatUntil = Time.time + 2.3f;
            _pulse = 0.6f;
            SetExpression("happy");
            StateChanged?.Invoke();
        }

        public void Pet()
        {
            if (Data == null) return;
            Data.joy = Mathf.Clamp01(Data.joy + 0.075f);
            _pulse = 0.55f;
            SetExpression("love");
            _expressionUntil = Time.time + 1.6f;
            StateChanged?.Invoke();
        }

        public void Jump()
        {
            if (!_ready) return;
            if (_jumpHeight < 0.06f) _jumpVelocity = 4.3f;
            _squashVelocity -= 0.8f;
            if (Data != null) Data.joy = Mathf.Clamp01(Data.joy + 0.055f);
            SetExpression("happy");
            StateChanged?.Invoke();
        }

        public void PlayHop()
        {
            if (!_ready || _carried || _jumpHeight > .04f) return;
            _jumpVelocity = 2.7f;
            _squashVelocity -= .65f;
            SetExpression("happy");
        }

        public void BeginSquish()
        {
            _pressure = .28f;
            _pressed = true;
            SetExpression("love");
        }

        public void SetSquishPressure(float pressure, Vector2 direction)
        {
            _pressed = true;
            _pressure = Mathf.Lerp(.12f, .43f, Mathf.Clamp01(pressure));
            _squeezeLean = Vector2.ClampMagnitude(direction, 1f) * 11f;
            SetExpression(pressure > .65f ? "giggle" : "love");
        }

        public void BeginCarry()
        {
            _pressed = false;
            _squeezeLean = Vector2.zero;
            _carried = true;
            _jumpVelocity = 0f;
            _dropBounce = false;
            SetExpression("surprised");
            _expressionUntil = Time.time + .65f;
        }

        public void SetCarryMotion(Vector3 worldMotion)
        {
            Vector3 local = transform.InverseTransformDirection(worldMotion);
            _carryLean = Vector2.ClampMagnitude(new Vector2(-local.x, local.z) * 10f, 15f);
        }

        public void EndCarry()
        {
            if (!_carried) return;
            _carried = false;
            _carryLean = Vector2.zero;
            _jumpVelocity = -.25f;
            _dropBounce = true;
            SetExpression("giggle");
            _expressionUntil = Time.time + 1.3f;
            if (Data != null) Data.joy = Mathf.Clamp01(Data.joy + .025f);
            StateChanged?.Invoke();
        }

        public void CancelTouchMotion()
        {
            _pressed = _carried = _dropBounce = false;
            _jumpHeight = _jumpVelocity = 0f;
            _squeezeLean = _carryLean = _smoothLean = Vector2.zero;
            _squash = _squashVelocity = 0f;
            if (_motion) { _motion.localScale = Vector3.one; _motion.localPosition = Vector3.zero; _motion.localRotation = Quaternion.identity; }
        }

        public void EndSquish()
        {
            if (!_pressed) return;
            _pressed = false;
            _squeezeLean = Vector2.zero;
            _squashVelocity -= 0.7f;
            Pet();
        }

        private void BuildDirtPattern()
        {
            _dirtCapacity = 0f;
            // Cylindrical UVs have the face around U=.5, and the seam behind the character.
            Vector2[] spots = { new Vector2(.33f, .36f), new Vector2(.66f, .39f), new Vector2(.51f, .72f),
                new Vector2(.44f, .2f), new Vector2(.16f, .53f), new Vector2(.84f, .61f) };
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = x / (float)(TextureSize - 1), v = y / (float)(TextureSize - 1);
                    float mark = 0f;
                    foreach (Vector2 spot in spots)
                    {
                        float dx = Mathf.Abs(u - spot.x);
                        dx = Mathf.Min(dx, 1f - dx);
                        float dy = v - spot.y;
                        mark += Mathf.Exp(-(dx * dx / .0035f + dy * dy / .008f));
                    }
                    float noise = 0.75f + 0.25f * Mathf.PerlinNoise(u * 35f, v * 37f);
                    float amount = Mathf.Clamp01(mark * noise * 1.4f);
                    int i = y * TextureSize + x;
                    _dirtPattern[i] = amount;
                    _dirtCapacity += amount;
                }
        }

        private void ResetDirtFromState()
        {
            if (_dirt == null || Data == null) return;
            _dirtTotal = 0f;
            for (int i = 0; i < _dirt.Length; i++)
            {
                _dirt[i] = _dirtPattern[i] * (1f - Data.cleanliness);
                _dirtTotal += _dirt[i];
            }
            UploadSkin();
        }

        public void AddDirt(float amount)
        {
            if (Data == null || _dirt == null || amount <= 0f) return;
            _dirtTotal = 0f;
            for (int i = 0; i < _dirt.Length; i++)
            {
                _dirt[i] = Mathf.Min(_dirtPattern[i], _dirt[i] + _dirtPattern[i] * amount);
                _dirtTotal += _dirt[i];
            }
            Data.cleanliness = Mathf.Clamp01(1f - _dirtTotal / Mathf.Max(1f, _dirtCapacity));
            UploadSkin();
            StateChanged?.Invoke();
        }

        public bool WashAt(RaycastHit hit, float strength)
        {
            if (_dirt == null || Data == null || hit.collider != _bodyCollider || strength <= 0f) return false;
            Vector2 uv = hit.textureCoord;
            float radius = .095f;
            int minY = Mathf.Max(0, Mathf.FloorToInt((uv.y - radius) * TextureSize));
            int maxY = Mathf.Min(TextureSize - 1, Mathf.CeilToInt((uv.y + radius) * TextureSize));
            int minX = Mathf.FloorToInt((uv.x - radius) * TextureSize);
            int maxX = Mathf.CeilToInt((uv.x + radius) * TextureSize);
            float radiusSquared = radius * radius;
            Color pearl = Palette[Data.colorIndex];
            Color soil = new Color(.44f, .25f, .18f);
            float removed = 0f;
            for (int y = minY; y <= maxY; y++)
                for (int sampleX = minX; sampleX <= maxX; sampleX++)
                {
                    // Visit only the brush rectangle, wrapping the seam behind the body.
                    int x = (sampleX + TextureSize) % TextureSize;
                    float dx = Mathf.Abs(x / (float)TextureSize - uv.x);
                    dx = Mathf.Min(dx, 1f - dx);
                    float dy = y / (float)TextureSize - uv.y;
                    float distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= radiusSquared) continue;
                    int i = y * TextureSize + x;
                    float old = _dirt[i];
                    if (old <= 0f) continue;
                    float distance = Mathf.Sqrt(distanceSquared) / radius;
                    _dirt[i] = Mathf.Max(0f, old - strength * 6f * (1f - distance));
                    removed += old - _dirt[i];
                    _pixels[i] = SkinPixel(pearl, soil, _dirt[i]);
                }
            _dirtTotal = Mathf.Max(0f, _dirtTotal - removed);
            Data.cleanliness = Mathf.Clamp01(1f - _dirtTotal / Mathf.Max(1f, _dirtCapacity));
            _textureDirty = removed > 0f || _textureDirty;
            SetExpression("love");
            if (Time.time >= _nextWashEvent)
            {
                _nextWashEvent = Time.time + .3f;
                Data.joy = Mathf.Clamp01(Data.joy + .007f);
                StateChanged?.Invoke();
            }
            return true;
        }

        private static Color32 SkinPixel(Color pearl, Color soil, float dirt)
        {
            float amount = Mathf.Clamp01(dirt * 1.4f);
            Color pixel = Color.Lerp(pearl, soil, amount);
            // Finishes use this clean-coverage channel to keep dirt opaque on translucent gel.
            pixel.a = 1f - amount;
            return pixel;
        }

        private void UploadSkin(bool repaint = true)
        {
            if (_skin == null || Data == null) return;
            if (repaint)
            {
                Color pearl = Palette[Data.colorIndex];
                Color soil = new Color(.44f, .25f, .18f);
                for (int i = 0; i < _pixels.Length; i++)
                    _pixels[i] = SkinPixel(pearl, soil, _dirt[i]);
            }
            _skin.SetPixels32(_pixels);
            _skin.Apply(false, false);
            _textureDirty = false;
        }

        private void Update()
        {
            if (!_ready || Data == null) return;
            float dt = Mathf.Min(Time.deltaTime, .04f);
            float t = Time.time;
            if (_textureDirty && t >= _nextTextureUpload)
            {
                _nextTextureUpload = t + 1f / 20f;
                // WashAt already recolors just the texels touched by the sponge.
                UploadSkin(false);
            }
            if (t >= _nextNeedUpdate && Data.created)
            {
                _nextNeedUpdate = t + 10f;
                Data.hunger = Mathf.Max(Mathf.Min(Data.hunger, .35f), Data.hunger - .0003f);
                Data.joy = Mathf.Max(Mathf.Min(Data.joy, .45f), Data.joy - .0002f);
                if (Data.cleanliness > .4f) AddDirt(.0002f);
                else StateChanged?.Invoke();
            }
            AnimateBody(t, dt);
            AnimateFace(t, dt);
            UpdateSoapLayer();
        }

        private void AnimateBody(float t, float dt)
        {
            float target = _pressed ? _pressure : _carried ? -.07f : 0f;
            _squashVelocity += ((target - _squash) * 95f - _squashVelocity * 8.4f) * dt;
            _squash += _squashVelocity * dt;
            _squash = Mathf.Clamp(_squash, -.13f, .43f);
            if (_carried)
                _jumpHeight = Mathf.Lerp(_jumpHeight, .48f + Mathf.Sin(t * 5f) * .025f, 1f - Mathf.Exp(-12f * dt));
            else if (_jumpHeight > 0f || _jumpVelocity > 0f)
            {
                _jumpVelocity -= 15f * dt;
                _jumpHeight += _jumpVelocity * dt;
                if (_jumpHeight <= 0f)
                {
                    _jumpHeight = 0f;
                    _jumpVelocity = _dropBounce ? 1.7f : 0f;
                    _dropBounce = false;
                    _squashVelocity += 2.3f;
                }
            }
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * .5f);
            float breathing = Mathf.Sin(t * 2.1f) * .012f;
            float chew = t < _eatUntil ? Mathf.Sin(t * 15f) * .025f : 0f;
            float height = 1f - _squash + breathing + chew;
            float width = 1f / Mathf.Sqrt(Mathf.Max(.5f, height));
            _motion.localScale = new Vector3(width, height, width);
            _motion.localPosition = new Vector3(0f, _jumpHeight, 0f);
            float delight = _pulse * Mathf.Sin(t * 7f) * 3.4f;
            Vector2 targetLean = _pressed ? _squeezeLean : _carried ? _carryLean : Vector2.zero;
            _smoothLean = Vector2.Lerp(_smoothLean, targetLean, 1f - Mathf.Exp(-9f * dt));
            _motion.localRotation = Quaternion.Euler(_smoothLean.y, 0f, Mathf.Sin(t * 1.2f) * .8f + delight + _smoothLean.x);
            float earBounce = Mathf.Sin(t * 2.8f) * 2f - _squashVelocity * 9f;
            if (_leftEar) _leftEar.localRotation = _leftEarRotation * Quaternion.Euler(earBounce, 0f, -earBounce * .4f);
            if (_rightEar) _rightEar.localRotation = _rightEarRotation * Quaternion.Euler(earBounce, 0f, earBounce * .4f);
            AnimateBodyVariants(t, dt);
            AnimateAccessories(t, dt);
        }

        private void AnimateFace(float t, float dt)
        {
            if (t >= _blinkAt)
            {
                _blinkStarted = t;
                _blinkAt = t + UnityEngine.Random.Range(2.2f, 4.8f);
            }
            string expression = CurrentExpression;
            float openness = 1f, eyeTilt = 0f, mouthOpen = 1f;
            if (expression == "love" || expression == "carinho") { openness = .17f; eyeTilt = 7f; mouthOpen = .8f; }
            if (expression == "sleepy") { openness = .38f; mouthOpen = .8f; }
            if (expression == "annoyed" || expression == "angry" || expression == "raiva") { openness = .55f; eyeTilt = -17f; mouthOpen = .55f; }
            if (expression == "hungry" || expression == "fome") { openness = 1.06f; eyeTilt = 5f; mouthOpen = 1.8f; }
            if (expression == "surprised") { openness = 1.22f; eyeTilt = -3f; mouthOpen = 2.2f; }
            if (expression == "giggle") { openness = .3f; eyeTilt = 11f; mouthOpen = 1.55f + Mathf.Sin(t * 12f) * .15f; }
            float blinkAge = t - _blinkStarted;
            if (blinkAge < .18f) openness *= Mathf.Max(.045f, Mathf.Abs(blinkAge / .09f - 1f));
            if (t < _eatUntil) mouthOpen = .8f + .7f * (1f + Mathf.Sin(t * 15f));
            float easing = 1f - Mathf.Exp(-18f * dt);
            AnimateVariantFace(openness, eyeTilt, easing);
            if (_leftEye)
            {
                _leftEye.localScale = Vector3.Lerp(_leftEye.localScale, ScaleVertical(_leftEyeScale, _leftEyeVertical, openness), easing);
                _leftEye.localRotation = Quaternion.Slerp(_leftEye.localRotation, _leftEyeRotation * Quaternion.Euler(0f, 0f, eyeTilt), easing);
            }
            if (_rightEye)
            {
                _rightEye.localScale = Vector3.Lerp(_rightEye.localScale, ScaleVertical(_rightEyeScale, _rightEyeVertical, openness), easing);
                _rightEye.localRotation = Quaternion.Slerp(_rightEye.localRotation, _rightEyeRotation * Quaternion.Euler(0f, 0f, -eyeTilt), easing);
            }
            if (_mouth)
            {
                _mouth.localScale = Vector3.Lerp(_mouth.localScale, ScaleVertical(_mouthScale, _mouthVertical, mouthOpen), easing);
                // The source smile has its origin at the character's base, so preserve its face centre.
                _mouth.localPosition = _mouthPosition + _mouthRotation *
                    (Vector3.Scale(_mouthGeometryCenter, _mouthScale) - Vector3.Scale(_mouthGeometryCenter, _mouth.localScale));
            }
        }

        private void OnEnable()
        {
            if (_ready) RefreshVisibleGeometry();
        }

        private void OnDisable()
        {
            CancelTouchMotion();
        }

        private void OnDestroy()
        {
            ReleaseCharacter();
        }

        private void ReleaseCharacter()
        {
            _ready = false;
            ReleaseSoapLayer();
            ReleaseFinishes();
            ReleaseCosmetic();
            ReleaseAccessories();
            ReleaseBodyVariants();
            // Destroy is deferred until the end of the frame; hide failed instances immediately.
            if (_motion)
            {
                _motion.gameObject.SetActive(false);
                Destroy(_motion.gameObject);
            }
            foreach (Material material in _ownedMaterials) if (material) Destroy(material);
            _ownedMaterials.Clear();
            if (_skin) Destroy(_skin);
            if (_bakedColliderMesh) Destroy(_bakedColliderMesh);
            ModelRoot = null;
            _motion = _leftEye = _rightEye = _mouth = _leftEar = _rightEar = _earGroup = _puff = _curl = null;
            _body = null;
            _bodyCollider = null;
            _pearl = _eyeMaterial = _blushMaterial = _whiteMaterial = _innerEarMaterial = null;
            _skin = null;
            _bakedColliderMesh = null;
            _pixels = null;
            _dirt = _dirtPattern = null;
            _dirtCapacity = _dirtTotal = 0f;
            _textureDirty = false;
            _pressed = false;
        }
    }
}
