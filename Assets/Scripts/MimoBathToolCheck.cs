using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    /// <summary>Opt-in acceptance check through the same tool pickup and drag APIs as touch.</summary>
    public sealed class MimoBathToolCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, ordinarySaveUnchanged;
            public string version, isolatedSavePath;
            public int width, height;
            public float elapsedSeconds, soapBeforeRinse, soapAfterRinse, cleanlinessBeforeSoap, cleanlinessAfterSoap, cleanlinessAfterRinse;
            public string[] checks, errors, screenshots;
        }
        static string output, ordinaryPath;
        static byte[] ordinaryBytes;
        static bool ordinaryExisted;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-bath-test") >= 0;
        readonly List<string> checks = new List<string>(), errors = new List<string>(), screenshots = new List<string>();
        MimoExperience experience;
        PetActor actor;
        Camera camera;
        Collider bodyCollider;
        bool finished;
        float started, covered, rinsed, initialClean, soapedClean, rinsedClean;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--mimo-test-output");
            output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : Path.Combine(Application.temporaryCachePath, "MimoBathToolValidation");
            Directory.CreateDirectory(output);
            ordinaryPath = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            ordinaryExisted = File.Exists(ordinaryPath); ordinaryBytes = ordinaryExisted ? File.ReadAllBytes(ordinaryPath) : null;
            PetStorage.SavePathOverride = Path.Combine(output, "isolated-bath-tools-" + DateTime.UtcNow.Ticks + ".json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Mimo bath tool acceptance").AddComponent<MimoBathToolCheck>(); }
        void Awake() { started = Time.realtimeSinceStartup; Application.runInBackground = true; Application.logMessageReceived += Log; }
        void OnDestroy() { Application.logMessageReceived -= Log; }
        void Log(string message, string stack, LogType type)
        { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message); }
        void Check(bool pass, string description) { if (pass) checks.Add(description); else errors.Add(description); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 115f) { errors.Add("Bath tool check timed out after 115 seconds."); Finish(); } }

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            experience = FindAnyObjectByType<MimoExperience>(); camera = Camera.main;
            Check(experience && camera, "The real experience and camera start.");
            Check(Screen.width > Screen.height, "The bath is presented in landscape.");
            Check(PetStorage.SavePath.StartsWith(output, StringComparison.OrdinalIgnoreCase), "An isolated save path was installed before scene Awake.");
            if (!experience || !camera) { Finish(); yield break; }
            yield return experience.RunSmokeTest();
            actor = experience.Actor;
            Check(actor && experience.CurrentScreen == "Home", "The complete ingredient, stirring and birth flow reaches home.");
            if (!actor) { Finish(); yield break; }
            actor.Data.cleanliness = .22f; actor.Initialize(actor.Data); actor.ClearSoap();
            bodyCollider = actor.GetComponentsInChildren<MeshCollider>().FirstOrDefault(c => c.enabled);
            Check(bodyCollider, "The physical body collider is available for real screen-ray hits.");
            Click("Banho"); yield return new WaitForSecondsRealtime(.7f);
            Check(experience.ActiveEnvironment == "Bath", "The Banho UI event opens the distinct bath room.");
            Click("Ensaboar"); yield return new WaitForSecondsRealtime(.25f);
            Check(experience.BathMode == "Soap", "The visible Ensaboar button selects the sponge.");
            Vector2 empty = new Vector2(camera.pixelRect.xMin + 18, Screen.height * .83f);
            Check(!experience.BeginBathToolGesture(empty), "Touching an unrelated background point cannot acquire a bath tool.");
            Check(!experience.BeginBathToolGesture(new Vector2(Screen.width - 25, Screen.height * .5f)), "Touching the sidebar cannot acquire a bath tool.");
            Vector2 source = camera.WorldToScreenPoint(experience.BathToolWorldPosition);
            Check(experience.BeginBathToolGesture(source), "The sponge is acquired by intersecting its actual scene bounds.");
            initialClean = actor.Data.cleanliness;
            for (float t = 0; t < 6f; t += Time.unscaledDeltaTime)
            {
                experience.DragBathToolGesture(BodyPoint(Mathf.Sin(t * 2.1f) * .6f, .52f + Mathf.Sin(t * 1.6f) * .27f), Time.unscaledDeltaTime);
                yield return null;
            }
            experience.DragBathToolGesture(BodyPoint(.15f, .56f), .025f);
            yield return new WaitForSecondsRealtime(.2f);
            covered = actor.SoapCoverage; soapedClean = actor.Data.cleanliness;
            Check(covered > .90f, "Simple sponge movements spread dense foam over essentially the whole body.");
            Check(actor.SoapBubbleCount >= 2000 && actor.GetComponentInChildren<MimoBubbleSurface>(), "Thousands of iridescent bubbles cover the actual body surface using one renderer.");
            Check(Mathf.Abs(soapedClean - initialClean) < .006f, "Applying soap does not wash the dirt away.");
            Check(experience.IsBathToolHeld && !experience.IsWaterRunning, "The sponge remains held without emitting rinse water.");
            yield return Capture("bath-tool-sponge-active.png");
            float soapTimingStart = Time.realtimeSinceStartup, soapFrameSum = 0; int soapFrames = 0;
            while (Time.realtimeSinceStartup-soapTimingStart < 2f) { yield return null; soapFrameSum += Time.unscaledDeltaTime; soapFrames++; }
            float soapFps = soapFrames/Mathf.Max(.001f,soapFrameSum);
            Check(!Application.isMobilePlatform || soapFps >= 30, "The dense fully soaped body renders above 30 FPS on the physical tablet (observed " + soapFps.ToString("F1") + " FPS).");
            experience.EndBathToolGesture(); yield return new WaitForSecondsRealtime(.35f);
            Check(!experience.IsBathToolHeld, "Releasing the sponge ends tool ownership.");
            Check(Mathf.Abs(actor.SoapCoverage - covered) < .006f, "Foam remains on the body after the sponge is released.");

            Click("Enxaguar"); yield return new WaitForSecondsRealtime(.25f);
            Check(experience.BathMode == "Rinse" && !experience.IsWaterRunning, "Enxaguar reveals the shower but does not wash automatically.");
            Check(Mathf.Abs(actor.SoapCoverage - covered) < .006f, "Selecting the shower preserves the existing foam.");
            source = camera.WorldToScreenPoint(experience.BathToolWorldPosition);
            Check(experience.BeginBathToolGesture(source), "The shower is picked up from its own scene geometry.");
            experience.DragBathToolGesture(BodyPoint(.18f, .55f), .035f); yield return null;
            Check(experience.IsWaterRunning, "Dragging the held shower emits animated, visible water.");
            Vector2 gripScreen = camera.WorldToScreenPoint(experience.BathToolWorldPosition);
            Vector2 sprayScreen = camera.WorldToScreenPoint(experience.BathWaterWorldStart);
            Check(Vector2.Distance(gripScreen,sprayScreen) > Screen.height * .08f, "The long handle keeps the spray head visibly away from the child's touch.");
            Check(actor.GetComponentsInChildren<MimoBubbleSurface>().Length == 1 && FindAnyObjectByType<MimoShowerWater>(), "Soap and clear shower water use their dedicated efficient renderers.");
            Check(actor.SoapCoverage > covered * .75f, "One water frame cannot instantly clear the whole body.");
            yield return Capture("bath-tool-shower-active.png");
            for (float t = 0; t < 3f; t += Time.unscaledDeltaTime)
            {
                experience.DragBathToolGesture(BodyPoint(Mathf.Sin(t * 2.1f) * .6f, .52f + Mathf.Sin(t * 1.6f) * .27f), Time.unscaledDeltaTime);
                yield return null;
            }
            rinsed = actor.SoapCoverage; rinsedClean = actor.Data.cleanliness;
            Check(rinsed == 0f, "A short generous rinse removes all foam, including the hidden side and tiny remaining patches.");
            Check(rinsedClean > .97f, "The broad shower cleans the complete body without aiming at individual dirt specks.");
            experience.EndBathToolGesture(); yield return null;
            Check(!experience.IsWaterRunning && !experience.IsBathToolHeld, "Releasing the shower stops its water immediately.");
            experience.SetInteractionMode("Amassar"); Physics.SyncTransforms();
            Vector2 touch = BodyPoint(0, .48f);
            Check(experience.BeginPetGesture(touch), "Touching Mimo itself still begins the normal squishy interaction.");
            for (float t = 0; t < .65f; t += Time.unscaledDeltaTime) { experience.MovePetGesture(touch + new Vector2(12, 35)); yield return null; }
            Check(actor.SquashAmount > .08f, "The character visibly squashes during direct touch in the bath room.");
            experience.EndPetGesture(); yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("bath-tool-after-rinse.png");
            Check(PetStorage.Save(actor.Data), "The resulting pet state saves to the isolated test file.");
            Finish();
        }

        Vector2 BodyPoint(float horizontal, float height)
        {
            Bounds bounds = bodyCollider ? bodyCollider.bounds : actor.VisibleBounds;
            Vector3 point = new Vector3(bounds.center.x + bounds.extents.x * horizontal, Mathf.Lerp(bounds.min.y, bounds.max.y, height), bounds.center.z);
            return camera.WorldToScreenPoint(point);
        }

        void Click(string label)
        {
            Button button = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.interactable && b.gameObject.activeInHierarchy && b.GetComponentsInChildren<Text>(true).Any(t => t.text.Trim() == label));
            Check(button, "The " + label + " button is present and interactable.");
            if (button) button.onClick.Invoke();
        }

        IEnumerator Capture(string filename)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = MimoRenderCapture.Scene(Camera.main);
            if (!texture) { errors.Add("Screenshot failed: " + filename); yield break; }
            File.WriteAllBytes(Path.Combine(output, filename), texture.EncodeToPNG()); Destroy(texture); screenshots.Add(filename);
            yield return new WaitForSecondsRealtime(.45f);
        }

        void Finish()
        {
            if (finished) return; finished = true;
            bool unchanged = ordinaryExisted ? File.Exists(ordinaryPath) && ordinaryBytes.SequenceEqual(File.ReadAllBytes(ordinaryPath)) : !File.Exists(ordinaryPath);
            Check(unchanged, "The ordinary pet save was not modified by this test.");
            var report = new Report { passed = errors.Count == 0, ordinarySaveUnchanged = unchanged, version = Application.version, isolatedSavePath = PetStorage.SavePath,
                width = Screen.width, height = Screen.height, elapsedSeconds = Time.realtimeSinceStartup - started, soapBeforeRinse = covered, soapAfterRinse = rinsed,
                cleanlinessBeforeSoap = initialClean, cleanlinessAfterSoap = soapedClean, cleanlinessAfterRinse = rinsedClean,
                checks = checks.ToArray(), errors = errors.ToArray(), screenshots = screenshots.ToArray() };
            File.WriteAllText(Path.Combine(output, "bath-tool-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_BATH_TOOL_CHECK " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
