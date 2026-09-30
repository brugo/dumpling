using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Opt-in rendered material comparisons and surface-state regression; never uses the ordinary save.</summary>
    public sealed class MimoMaterialCheck : MonoBehaviour
    {
        [Serializable] sealed class MaterialSample
        {
            public int finish, renderQueue, glitterPieces;
            public string name, shader, environment;
            public bool supported, cameraOpaqueCopy;
            public float opacity, smoothness, transmission, refraction, thickness, sceneRefraction, environmentStrength;
        }
        [Serializable] sealed class Report
        {
            public bool passed, ordinarySaveUnchanged, mobileProfile;
            public string version, gpu, isolatedSavePath, captureMethod;
            public int width, height;
            public float elapsedSeconds, soapBeforeRinse, soapAfterRinse, cleanlinessBeforeSoap, cleanlinessAfterRinse;
            public string[] checks, errors, screenshots;
            public MaterialSample[] materials;
        }

        static string output, ordinaryPath;
        static byte[] ordinaryBytes;
        static bool ordinaryExisted;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-material-test") >= 0;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), screenshots = new List<string>();
        readonly List<MaterialSample> samples = new List<MaterialSample>();
        MimoExperience experience;
        PetActor actor;
        Camera sceneCamera;
        Renderer body;
        Canvas[] canvases;
        bool[] canvasStates;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        Rect cameraRect;
        float cameraFov, started, soapBefore, soapAfter, cleanlinessBefore, cleanlinessAfter;
        bool finished, closeView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--mimo-test-output");
            output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1])
                : Path.Combine(Application.temporaryCachePath, "MimoMaterialValidation");
            Directory.CreateDirectory(output);
            ordinaryPath = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            ordinaryExisted = File.Exists(ordinaryPath);
            ordinaryBytes = ordinaryExisted ? File.ReadAllBytes(ordinaryPath) : null;
            PetStorage.SavePathOverride = Path.Combine(output, "isolated-materials-" + DateTime.UtcNow.Ticks + ".json");
            PetStorage.Save(new PetSave { created = true, petName = "Mimo", colorIndex = 0, finishIndex = 0,
                cleanliness = 1f, hunger = 1f, joy = 1f, recipeSeed = 78163 });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Mimo material acceptance").AddComponent<MimoMaterialCheck>(); }
        void Awake() { started = Time.realtimeSinceStartup; Application.runInBackground = true; Application.logMessageReceived += Log; }
        void OnDestroy() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message); }
        void Check(bool pass, string description) { if (pass) checks.Add(description); else errors.Add(description); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 140f) { errors.Add("Material check timed out."); Finish(); } }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            experience = FindAnyObjectByType<MimoExperience>(); sceneCamera = Camera.main;
            actor = experience ? experience.Actor : null;
            Check(experience && sceneCamera && actor && experience.CurrentScreen == "Home", "The saved test friend loads into the real home scene.");
            Check(PetStorage.SavePath.StartsWith(output, StringComparison.OrdinalIgnoreCase), "The save was isolated before the experience loaded.");
            if (!experience || !sceneCamera || !actor) { Finish(); yield break; }
            body = actor.ModelRoot.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "Body");
            Check(body, "The authored body renderer exists.");
            if (!body) { Finish(); yield break; }

            cameraPosition = sceneCamera.transform.position; cameraRotation = sceneCamera.transform.rotation;
            cameraRect = sceneCamera.rect; cameraFov = sceneCamera.fieldOfView;
            canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            canvasStates = canvases.Select(c => c.enabled).ToArray();
            Check(PetCatalog.FinishNames.Length == 6, "All six material families are available.");

            for (int finish = 0; finish < PetCatalog.FinishNames.Length; finish++)
            {
                actor.SetFinish(finish); actor.transform.rotation = Quaternion.identity;
                yield return new WaitForSecondsRealtime(.35f);
                ValidateMaterial(finish);
                yield return Capture("finish-" + finish + "-home.png");
                SetCloseView(true);
                yield return new WaitForSecondsRealtime(.15f);
                yield return Capture("finish-" + finish + "-close.png");
                yield return Capture("finish-" + finish + "-studio.png", true);
                SetCloseView(false);
            }

            SetCloseView(true);
            foreach (int finish in new[] { 2, 3 })
            {
                actor.SetFinish(finish);
                foreach (int angle in new[] { 0, 35, 90, 180 })
                {
                    actor.transform.rotation = Quaternion.Euler(0, angle, 0);
                    yield return new WaitForSecondsRealtime(.23f);
                    yield return Capture("finish-" + finish + "-angle-" + angle + ".png");
                }
                actor.transform.rotation = Quaternion.identity; actor.BeginSquish();
                yield return new WaitForSecondsRealtime(.4f);
                Check(actor.SquashAmount > .08f, "The " + actor.FinishName + " material remains on the deforming body.");
                yield return Capture("finish-" + finish + "-squished.png");
                actor.EndSquish(); yield return new WaitForSecondsRealtime(.65f);
            }

            actor.SetFinish(3);
            for (int frame = 0; frame < 24; frame++)
            {
                float angle = Mathf.Sin(frame * Mathf.PI * 2f / 24f) * 25f;
                actor.transform.rotation = Quaternion.Euler(0, angle, 0);
                yield return new WaitForSecondsRealtime(.10f);
                yield return Capture("turntable-" + frame.ToString("D2") + ".png", true);
            }
            actor.transform.rotation = Quaternion.identity;

            foreach (int color in new[] { 3, 7 })
            {
                PetSave recipe = actor.Data.Copy(); recipe.colorIndex = color; recipe.finishIndex = color == 3 ? 2 : 3;
                actor.Initialize(recipe); actor.ClearSoap();
                yield return new WaitForSecondsRealtime(.3f);
                yield return Capture("finish-" + recipe.finishIndex + "-color-" + color + ".png");
            }

            SetCloseView(false);
            Click("Banho"); yield return new WaitForSecondsRealtime(.4f);
            PetSave dirty = actor.Data.Copy(); dirty.colorIndex = 0; dirty.finishIndex = 3; dirty.cleanliness = .24f;
            actor.Initialize(dirty); actor.ClearSoap();
            cleanlinessBefore = actor.Data.cleanliness;
            yield return Capture("bath-01-dirty-glitter.png");
            yield return PaintBody(true, 2);
            soapBefore = actor.SoapCoverage;
            Check(soapBefore > .4f, "Localized sponge strokes cover the physical body.");
            Check(Mathf.Abs(actor.Data.cleanliness - cleanlinessBefore) < .015f, "Soap leaves the underlying dirt until water reaches it.");
            Texture2D soapMask = actor.SoapMaskTexture;
            for (int finish = 0; finish < PetCatalog.FinishNames.Length; finish++)
            {
                actor.SetFinish(finish); yield return new WaitForSecondsRealtime(.15f);
                Check(actor.SoapMaskTexture == soapMask && body.sharedMaterial.GetTexture("_SoapMask") == soapMask,
                    "Switching to " + actor.FinishName + " keeps the attached surface mask.");
                Check(Mathf.Abs(actor.SoapCoverage - soapBefore) < .001f && Mathf.Abs(actor.Data.cleanliness - cleanlinessBefore) < .015f,
                    "Switching to " + actor.FinishName + " preserves foam and dirt.");
                yield return Capture("bath-02-soap-finish-" + finish + ".png");
            }
            actor.SetFinish(3);
            yield return PaintBody(false, 3);
            soapAfter = actor.SoapCoverage; cleanlinessAfter = actor.Data.cleanliness;
            Check(soapAfter < soapBefore * .2f, "Rinsing removes the accumulated surface foam from glitter jelly.");
            Check(cleanlinessAfter > cleanlinessBefore + .2f, "Water removes the actual dirt beneath the new materials.");
            yield return Capture("bath-03-rinsed-glitter.png");

            PetSave persisted = actor.Data.Copy(); persisted.petName = "Material regression";
            Check(PetStorage.Save(persisted), "The finished material and care state can be saved.");
            PetSave reloaded = PetStorage.Load();
            Check(reloaded.petName == persisted.petName && reloaded.finishIndex == persisted.finishIndex
                && reloaded.colorIndex == persisted.colorIndex && Mathf.Abs(reloaded.cleanliness - persisted.cleanliness) < .001f,
                "Reload preserves the selected material, color, identity and cleanliness.");
            experience.EnterLaboratory(); yield return new WaitForSecondsRealtime(.15f);
            Check(!actor.gameObject.activeInHierarchy
                && sceneCamera.GetUniversalAdditionalCameraData().requiresColorOption == CameraOverrideOption.Off
                && Float(body.sharedMaterial, "_SceneRefraction") < .5f,
                "Hiding a transparent friend releases the camera's scene-color copy.");
            Finish();
        }

        void ValidateMaterial(int finish)
        {
            Material material = body.sharedMaterial;
            Check(actor.FinishIndex == finish && actor.FinishMaterialsReady, "The requested material is ready: " + actor.FinishName);
            Check(actor.GetComponentsInChildren<Renderer>().All(r => !r.enabled || (r.sharedMaterial && r.sharedMaterial.shader.isSupported)),
                "All active shaders are supported for " + actor.FinishName);
            foreach (string property in new[] { "_FinishEnvironment", "_EnvironmentStrength", "_Transmission", "_RefractionStrength",
                "_Thickness", "_Subsurface", "_CoatStrength", "_MicroNormalStrength", "_FinishKind", "_SceneRefraction", "_DetailScale" })
                Check(material.HasProperty(property), "Material input exists: " + actor.FinishName + " " + property);
            Texture environment = material.HasProperty("_FinishEnvironment") ? material.GetTexture("_FinishEnvironment") : null;
            Check(environment && environment.dimension == UnityEngine.Rendering.TextureDimension.Cube,
                "Reflections have a real environment cubemap: " + actor.FinishName);
            var cameraData = sceneCamera.GetUniversalAdditionalCameraData();
            bool copy = cameraData.requiresColorOption == CameraOverrideOption.On;
            bool translucent = finish == 2 || finish == 3;
            Check(copy == translucent, "The scene-color copy follows the visible transparent material: " + actor.FinishName);
            Check((Float(material, "_SceneRefraction") > .5f) == translucent, "The surface refraction gate matches the camera copy: " + actor.FinishName);
            if (translucent)
            {
                Check(Float(material, "_Transmission") > .5f && Float(material, "_RefractionStrength") > 0
                    && Float(material, "_Thickness") > 0 && material.renderQueue >= 3000,
                    "Jelly has transmission, thickness and scene refraction: " + actor.FinishName);
            }
            if (finish == 3) Check(actor.InternalGlitterCount >= 250, "Glitter uses a dense field of reflective flakes.");
            samples.Add(new MaterialSample { finish = finish, name = actor.FinishName, shader = material.shader.name,
                supported = material.shader.isSupported, environment = environment ? environment.name : "missing", renderQueue = material.renderQueue,
                glitterPieces = actor.InternalGlitterCount, cameraOpaqueCopy = copy, opacity = Float(material, "_Opacity"),
                smoothness = Float(material, "_Smoothness"), transmission = Float(material, "_Transmission"), refraction = Float(material, "_RefractionStrength"),
                thickness = Float(material, "_Thickness"), sceneRefraction = Float(material, "_SceneRefraction"), environmentStrength = Float(material, "_EnvironmentStrength") });
        }

        static float Float(Material material, string name) => material.HasProperty(name) ? material.GetFloat(name) : 0f;

        void SetCloseView(bool enabled)
        {
            closeView = enabled;
            for (int i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].enabled = enabled ? false : canvasStates[i];
            if (!enabled)
            {
                sceneCamera.rect = cameraRect; sceneCamera.fieldOfView = cameraFov;
                sceneCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); return;
            }
            sceneCamera.rect = new Rect(0, 0, 1, 1); sceneCamera.fieldOfView = 29f;
            Bounds bounds = actor.VisibleBounds;
            Vector3 direction = cameraRotation * Vector3.forward;
            float extent = Mathf.Max(bounds.extents.y, bounds.extents.x / sceneCamera.aspect);
            float distance = extent / (Mathf.Tan(sceneCamera.fieldOfView * Mathf.Deg2Rad * .5f) * .70f) + bounds.extents.z * .45f;
            sceneCamera.transform.position = bounds.center - direction * distance;
            sceneCamera.transform.rotation = cameraRotation;
        }

        IEnumerator PaintBody(bool soap, int passes)
        {
            for (int pass = 0; pass < passes; pass++) for (int side = 0; side < 4; side++)
            {
                actor.transform.rotation = Quaternion.Euler(0, side * 90, 0); Physics.SyncTransforms(); yield return null;
                Bounds bounds = actor.VisibleBounds;
                Vector3 min = sceneCamera.WorldToScreenPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z));
                Vector3 max = sceneCamera.WorldToScreenPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z));
                for (int row = 1; row < 9; row++) for (int col = 1; col < 9; col++)
                {
                    Vector2 point = new Vector2(Mathf.Lerp(min.x, max.x, col / 10f), Mathf.Lerp(min.y, max.y, row / 10f));
                    if (!Physics.Raycast(sceneCamera.ScreenPointToRay(point), out RaycastHit hit, 30f)
                        || hit.collider.GetComponentInParent<PetActor>() != actor) continue;
                    if (soap) actor.SoapAt(hit, .6f); else actor.RinseAt(hit, .9f);
                }
                yield return new WaitForSecondsRealtime(.12f);
            }
            actor.transform.rotation = Quaternion.identity; yield return new WaitForSecondsRealtime(.2f);
        }

        void Click(string label)
        {
            Button button = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.interactable && b.gameObject.activeInHierarchy
                && b.GetComponentsInChildren<Text>(true).Any(t => t.text.Trim() == label));
            Check(button, "The visible " + label + " action remains available."); if (button) button.onClick.Invoke();
        }

        IEnumerator Capture(string filename, bool studio = false)
        {
            yield return new WaitForEndOfFrame();
            CaptureOffscreen(filename, studio);
        }

        void CaptureOffscreen(string filename, bool studio)
        {
            // Hidden Windows players may have an unpresented, blank swapchain. Submit the
            // real camera through URP instead, including its opaque-color/refraction pass.
            // Evidence is explicitly scene-only; overlay UI is not part of this target.
            Rect previousRect = sceneCamera.rect;
            RenderTexture previousTarget = sceneCamera.targetTexture, previousActive = RenderTexture.active;
            Color previousBackground = sceneCamera.backgroundColor;
            CameraClearFlags previousClearFlags = sceneCamera.clearFlags;
            bool[] enabledCanvases = canvases.Select(c => c && c.enabled).ToArray();
            Renderer[] surroundings = studio ? FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && !r.transform.IsChildOf(actor.transform)).ToArray() : Array.Empty<Renderer>();
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * previousRect.width));
                int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * previousRect.height));
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "Mimo material evidence", antiAliasing = 1 };
                target.Create();
                for (int i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].enabled = false;
                if (studio)
                {
                    foreach (Renderer renderer in surroundings) renderer.enabled = false;
                    sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                    sceneCamera.backgroundColor = new Color(.86f, .89f, .92f, 1f);
                }
                sceneCamera.rect = new Rect(0, 0, 1, 1);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(sceneCamera, request);
                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false, false);

                Color32[] pixels = image.GetPixels32();
                int min = 765, max = 0, different = 0;
                Color32 first = pixels[0];
                int stride = Mathf.Max(1, pixels.Length / 12000);
                for (int index = 0; index < pixels.Length; index += stride)
                {
                    Color32 pixel = pixels[index];
                    int brightness = pixel.r + pixel.g + pixel.b;
                    min = Mathf.Min(min, brightness); max = Mathf.Max(max, brightness);
                    if (Mathf.Abs(pixel.r - first.r) + Mathf.Abs(pixel.g - first.g) + Mathf.Abs(pixel.b - first.b) > 30) different++;
                }
                Check(max - min > 75 && different > 500, "The offscreen render contains a nonblank scene: " + filename);
                File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
                screenshots.Add(filename);
            }
            catch (Exception exception) { errors.Add("Offscreen screenshot failed: " + filename + ": " + exception.Message); }
            finally
            {
                sceneCamera.targetTexture = previousTarget; sceneCamera.rect = previousRect;
                sceneCamera.backgroundColor = previousBackground; sceneCamera.clearFlags = previousClearFlags;
                RenderTexture.active = previousActive;
                foreach (Renderer renderer in surroundings) if (renderer) renderer.enabled = true;
                for (int i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].enabled = enabledCanvases[i];
                if (image) Destroy(image);
                if (target) { target.Release(); Destroy(target); }
            }
        }

        void Finish()
        {
            if (finished) return; finished = true;
            if (closeView && sceneCamera && canvases != null) SetCloseView(false);
            bool unchanged = ordinaryExisted ? File.Exists(ordinaryPath) && ordinaryBytes.SequenceEqual(File.ReadAllBytes(ordinaryPath)) : !File.Exists(ordinaryPath);
            Check(unchanged, "The user's ordinary save remains byte-for-byte untouched.");
            var report = new Report { passed = errors.Count == 0, ordinarySaveUnchanged = unchanged, mobileProfile = MimoPerformance.Lightweight,
                version = Application.version, gpu = SystemInfo.graphicsDeviceName, isolatedSavePath = PetStorage.SavePath,
                captureMethod = "URP SingleCameraRequest offscreen; actual game camera and lighting; scene only without overlay UI. Studio/turntable frames temporarily hide surroundings against a neutral background, preserving all character materials and lights.",
                width = Screen.width, height = Screen.height,
                elapsedSeconds = Time.realtimeSinceStartup - started, soapBeforeRinse = soapBefore, soapAfterRinse = soapAfter,
                cleanlinessBeforeSoap = cleanlinessBefore, cleanlinessAfterRinse = cleanlinessAfter, checks = checks.ToArray(), errors = errors.ToArray(),
                screenshots = screenshots.ToArray(), materials = samples.ToArray() };
            File.WriteAllText(Path.Combine(output, "material-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_MATERIAL_CHECK " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
