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
    /// <summary>Opt-in scenario regression. Desktop timings are observations, not Android performance claims.</summary>
    public sealed class MimoMobilePerformanceCheck : MonoBehaviour
    {
        [Serializable] sealed class RoomSample
        {
            public string environment;
            public int activeRenderers, frustumRenderers, activeSkinnedRenderers, actorRenderers;
            public long activeMeshTriangles;
            public int frameSamples;
            public float frameMillisecondsP50, frameMillisecondsP95, frameMillisecondsMax, renderScale;
        }

        [Serializable] sealed class Report
        {
            public bool passed, lightweightProfile, mobileDevice;
            public string timingScope, device, gpu, os, platform, unityVersion, isolatedSavePath;
            public int width, height, targetFrameRate, staticRenderersBefore, staticRenderersAfter, staticTriangles;
            public float elapsedSeconds;
            public string[] checks, errors, screenshots;
            public RoomSample[] rooms;
        }

        static string outputDirectory, originalSavePath;
        static byte[] originalSave;
        static bool originalSaveExisted, originalSaveSnapshotValid;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), screenshots = new List<string>();
        readonly List<RoomSample> rooms = new List<RoomSample>();
        MimoExperience experience;
        PetActor actor;
        Camera sceneCamera;
        float started;
        bool finished;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-performance-test") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateBeforeAwake()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--mimo-test-output");
            outputDirectory = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1])
                : Path.Combine(Application.temporaryCachePath, "MimoPerformanceValidation");
            // Isolate first, including when inspecting the ordinary save fails.
            PetStorage.SavePathOverride = Path.Combine(outputDirectory, "isolated-performance-pet-" + DateTime.UtcNow.Ticks + ".json");
            originalSavePath = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            try
            {
                originalSaveExisted = File.Exists(originalSavePath);
                if (originalSaveExisted) originalSave = File.ReadAllBytes(originalSavePath);
                originalSaveSnapshotValid = true;
            }
            catch (Exception) { originalSaveSnapshotValid = false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Enabled) new GameObject("Mimo opt-in mobile profile verification").AddComponent<MimoMobilePerformanceCheck>();
        }

        void Awake()
        {
            Application.runInBackground = true;
            started = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message);
        }
        void Check(bool condition, string message) { if (condition) checks.Add(message); else errors.Add(message); }
        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 90f)
            { errors.Add("Performance scenario verification timed out after 90 seconds."); Finish(); }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            yield return new WaitForSecondsRealtime(1.2f);
            experience = FindAnyObjectByType<MimoExperience>();
            sceneCamera = Camera.main;
            Check(experience && sceneCamera, "The experience and main camera start normally.");
            Check(Screen.width > Screen.height, "The player remains in tablet landscape orientation.");
            Check(Path.GetDirectoryName(Path.GetFullPath(PetStorage.SavePath)) == Path.GetFullPath(outputDirectory),
                "The test save was isolated before scene startup.");
            Check(MimoPerformance.Lightweight, "The lightweight rendering profile is active.");
            Check(Application.targetFrameRate >= 30, "The lightweight profile allows headroom above the 30 FPS floor.");
            if (!experience || !sceneCamera) { Finish(); yield break; }

            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            UniversalAdditionalCameraData cameraData = sceneCamera.GetComponent<UniversalAdditionalCameraData>();
            Check(pipeline && !pipeline.supportsHDR && !sceneCamera.allowHDR, "HDR is disabled in both pipeline and camera.");
            Check(pipeline && pipeline.msaaSampleCount == 1 && !sceneCamera.allowMSAA, "The scene does not request multisample antialiasing.");
            Check(cameraData && !cameraData.renderPostProcessing, "The camera renders without postprocessing passes.");
            Check(experience.MobileStaticRenderersBefore > experience.MobileStaticRenderersAfter && experience.MobileStaticRenderersAfter > 0,
                "Static environment batching reduces submitted room renderers without removing every surface.");

            experience.EnterLaboratory();
            yield return new WaitForSecondsRealtime(1.4f);
            actor = experience.Actor;
            Check(experience.CurrentScreen == "Laboratory" && actor && !actor.gameObject.activeInHierarchy && experience.CreationStation,
                "Creation displays the steamer and conceals the character until the reveal.");
            if (!actor) { Finish(); yield break; }
            yield return SampleRoom("Laboratory");
            yield return Capture("performance-laboratory.png");

            yield return experience.RunSmokeTest();
            yield return new WaitForSecondsRealtime(1.4f);
            Check(experience.CurrentScreen == "Home" && actor.Data.created && !string.IsNullOrWhiteSpace(actor.Data.petName),
                "Creation, birth and naming still lead to the care room.");
            Check(PetStorage.Load().created, "The newly created character reaches the isolated persistent save.");

            yield return SelectRoom("Comidinha", "Food");
            float hunger = actor.Data.hunger;
            actor.Feed();
            Check(actor.Data.hunger >= hunger, "The care character still accepts food.");
            yield return SelectRoom("Banho", "Bath");
            CheckSurfaceWashing();
            yield return SelectRoom("Brincar", "Play");
            actor.BeginSquish();
            yield return new WaitForSecondsRealtime(.45f);
            Check(actor.SquashAmount > .15f, "The neutral static body still responds to touch compression.");
            actor.EndSquish();
            yield return new WaitForSecondsRealtime(1.5f);
            Check(Mathf.Abs(actor.SquashAmount) < .08f, "The character returns to its resting shape after release.");
            yield return Capture("performance-garden.png");
            Finish();
        }

        IEnumerator SelectRoom(string label, string expected)
        {
            Button button = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(candidate =>
                candidate.gameObject.activeInHierarchy && candidate.interactable &&
                candidate.GetComponentsInChildren<Text>(true).Any(text => text.text.Trim() == label));
            Check(button, "The visible " + label + " care action exists.");
            if (!button) yield break;
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.8f);
            Check(experience.ActiveEnvironment == expected && experience.ActiveCareEnvironmentCount == 1,
                label + " opens exactly one matching care environment through its UI action.");
            yield return SampleRoom(expected);
        }

        IEnumerator SampleRoom(string expected)
        {
            bool secretRecipe = expected == "Laboratory";
            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(sceneCamera);
            if (!secretRecipe)
            {
                Bounds bounds = actor.VisibleBounds;
                Check(Finite(bounds.center) && Finite(bounds.extents) && bounds.size.y > .5f,
                    expected + ": the character has finite, nonempty bounds.");
                Check(GeometryUtility.TestPlanesAABB(frustum, bounds), expected + ": the character intersects the visible camera view.");
            }
            Renderer[] active = FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
            var sample = new RoomSample { environment = expected, activeRenderers = active.Length };
            int visibleRoomSurfaces = 0;
            foreach (Renderer renderer in active)
            {
                bool isActor = renderer.GetComponentInParent<PetActor>() == actor;
                if (isActor) sample.actorRenderers++;
                if (GeometryUtility.TestPlanesAABB(frustum, renderer.bounds))
                { sample.frustumRenderers++; if (!isActor) visibleRoomSurfaces++; }
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned) sample.activeSkinnedRenderers++;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter ? filter.sharedMesh : skinned ? skinned.sharedMesh : null;
                if (mesh) for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    if (mesh.GetTopology(sub) == MeshTopology.Triangles) sample.activeMeshTriangles += mesh.GetIndexCount(sub) / 3;
            }
            Check((secretRecipe ? sample.actorRenderers == 0 : sample.actorRenderers > 0) && visibleRoomSurfaces > 0,
                expected + ": visible surfaces match the secret recipe or revealed care character.");
            Check(!actor.GetComponentsInChildren<SkinnedMeshRenderer>(false).Any(renderer => renderer.enabled && renderer.name == "Body"),
                expected + ": the body no longer schedules unused skinning.");

            var times = new List<float>();
            float until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until) { yield return null; times.Add(Time.unscaledDeltaTime * 1000f); }
            times.Sort(); sample.frameSamples = times.Count;
            Check(times.Count > 0, expected + ": rendered frame timing observations were collected.");
            if (times.Count > 0)
            {
                sample.frameMillisecondsP50 = Percentile(times, .5f);
                sample.frameMillisecondsP95 = Percentile(times, .95f);
                sample.frameMillisecondsMax = times[times.Count - 1];
            }
            UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            sample.renderScale = pipeline ? pipeline.renderScale : 1f;
            rooms.Add(sample);
        }

        void CheckSurfaceWashing()
        {
            actor.AddDirt(.65f);
            float before = actor.Data.cleanliness;
            int hits = 0;
            var uvs = new HashSet<Vector2>();
            Physics.SyncTransforms();
            for (int y = 2; y <= 8; y++) for (int x = 2; x <= 8; x++)
            {
                Ray ray = sceneCamera.ViewportPointToRay(new Vector3(x / 10f, y / 10f, 0f));
                if (!Physics.Raycast(ray, out RaycastHit hit, 30f) || hit.collider.GetComponentInParent<PetActor>() != actor) continue;
                if (hit.collider is MeshCollider && Finite(new Vector3(hit.textureCoord.x, hit.textureCoord.y, 0f)) && actor.WashAt(hit, 1f))
                { hits++; uvs.Add(hit.textureCoord); }
            }
            Check(hits > 0 && uvs.Count > 1, "The rendered body retains precise surface hits with distinct wash UV coordinates.");
            Check(actor.Data.cleanliness > before, "Washing visible surface patches removes actual dirt from the character.");
        }

        static bool Finite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
        static float Percentile(List<float> sorted, float fraction) => sorted[Mathf.Clamp(Mathf.CeilToInt(sorted.Count * fraction) - 1, 0, sorted.Count - 1)];

        IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (!image) { errors.Add("Could not capture " + filename); yield break; }
            Color32[] pixels = image.GetPixels32();
            int low = 765, high = 0;
            for (int i = 0; i < pixels.Length; i += Mathf.Max(1, pixels.Length / 2000))
            { int brightness = pixels[i].r + pixels[i].g + pixels[i].b; low = Mathf.Min(low, brightness); high = Mathf.Max(high, brightness); }
            Check(high > 100 && high - low > 50, filename + ": the normal player screenshot contains rendered color variation.");
            File.WriteAllBytes(Path.Combine(outputDirectory, filename), image.EncodeToPNG());
            Destroy(image); screenshots.Add(filename);
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            bool saveUnchanged = false;
            try
            {
                saveUnchanged = originalSaveSnapshotValid && (originalSaveExisted
                    ? File.Exists(originalSavePath) && originalSave.SequenceEqual(File.ReadAllBytes(originalSavePath))
                    : !File.Exists(originalSavePath));
            }
            catch (Exception) { saveUnchanged = false; }
            Check(saveUnchanged, "The user's ordinary saved character remains byte-for-byte unchanged.");
            var report = new Report {
                passed = errors.Count == 0, lightweightProfile = MimoPerformance.Lightweight, mobileDevice = Application.isMobilePlatform,
                timingScope = Application.isMobilePlatform ? "Observed on this physical mobile runtime; includes frame pacing."
                    : "Desktop regression observations only; these timings do not establish Android FPS.",
                device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName, os = SystemInfo.operatingSystem,
                platform = Application.platform.ToString(), unityVersion = Application.unityVersion,
                isolatedSavePath = PetStorage.SavePath, width = Screen.width, height = Screen.height,
                targetFrameRate = Application.targetFrameRate, elapsedSeconds = Time.realtimeSinceStartup - started,
                staticRenderersBefore = experience ? experience.MobileStaticRenderersBefore : 0,
                staticRenderersAfter = experience ? experience.MobileStaticRenderersAfter : 0,
                staticTriangles = experience ? experience.MobileStaticTriangleCount : 0,
                checks = checks.ToArray(), errors = errors.ToArray(), screenshots = screenshots.ToArray(), rooms = rooms.ToArray()
            };
            try { Directory.CreateDirectory(outputDirectory); File.WriteAllText(Path.Combine(outputDirectory, "performance-checks.json"), JsonUtility.ToJson(report, true)); }
            catch (Exception error) { Debug.LogError("Performance report write failed: " + error.Message); Application.Quit(1); return; }
            Debug.Log("MIMO_PERFORMANCE " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
