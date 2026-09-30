using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden
{
    // Opt-in development check. Never runs during an ordinary child's play session.
    public sealed class MimoSmokeCheck : MonoBehaviour
    {
        [Serializable] class Report { public bool passed; public string[] checks; public string[] errors; public string[] controls; public string version; }
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void IsolateSave()
        {
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--mimo-test") && !args.Contains("--mimo-test-restore")) return;
            int index = Array.IndexOf(args, "--mimo-test-output");
            if (index >= 0 && index + 1 < args.Length) PetStorage.SavePathOverride = Path.Combine(args[index + 1], "isolated-pet-save.json");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--mimo-test") || args.Contains("--mimo-test-restore")) new GameObject("Mimo automated verification").AddComponent<MimoSmokeCheck>();
        }

        void Awake() { Application.runInBackground = true; Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string stack, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message);
        }
        void Check(bool condition, string message) { if (condition) checks.Add(message); else errors.Add(message); }

        IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--mimo-test-output");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Application.persistentDataPath;
            Directory.CreateDirectory(output);
            PetStorage.SavePathOverride = Path.Combine(output, "isolated-pet-save.json");
            yield return new WaitForSeconds(2f);
            if (args.Contains("--mimo-test-restore"))
            {
                var restored = FindAnyObjectByType<MimoExperience>();
                Check(restored && restored.CurrentScreen == "Home", "Application restart returns directly to home");
                Check(restored && restored.Actor.Data.created && restored.Actor.Data.petName == "Mimo" && restored.Actor.Data.colorIndex == 0 && restored.Actor.Data.earsIndex == 1 && restored.Actor.Data.tailIndex == 1, "Same named creation restored after restarting process");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "restored-home.png"));
                yield return new WaitForSeconds(.7f);
                File.WriteAllText(Path.Combine(output, "restart-checks.json"), JsonUtility.ToJson(new Report { passed = errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray(), version = Application.unityVersion }, true));
                Application.Quit(errors.Count == 0 ? 0 : 1);
                yield break;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "laboratory-landscape.png"));
            var experience = FindAnyObjectByType<MimoExperience>();
            if (experience != null)
            {
                yield return experience.RunSmokeTest();
                Check(experience.CurrentScreen == "Home" && experience.Actor.Data.created, "Creation, birth, naming and home transition");
            }
            yield return new WaitForSeconds(.6f);
            var actor = FindAnyObjectByType<PetActor>();
            Check(actor != null, "Character instantiated");
            Check(Camera.main != null, "Main camera exists");
            Check(Screen.width > Screen.height, "Landscape viewport");
            if (actor != null)
            {
                var transforms = actor.ModelRoot.GetComponentsInChildren<Transform>(true);
                Check(transforms.Length >= 18, "Full character hierarchy present");
                Transform ears = transforms.FirstOrDefault(t => t.name == "RabbitEars");
                Transform puff = transforms.FirstOrDefault(t => t.name == "Tail.Puff");
                Transform curl = transforms.FirstOrDefault(t => t.name == "Tail.Curl");
                for (int color = 0; color < 3; color++)
                    for (int ear = 0; ear < 2; ear++)
                        for (int tail = 0; tail < 3; tail++)
                        {
                            actor.ApplyRecipe(color, ear, tail);
                            Check(actor.Data.colorIndex == color && actor.Data.earsIndex == ear && actor.Data.tailIndex == tail &&
                                ears != null && ears.gameObject.activeSelf == (ear == 1) &&
                                puff != null && puff.gameObject.activeSelf == (tail == 1) &&
                                curl != null && curl.gameObject.activeSelf == (tail == 2), "Recipe " + color + "/" + ear + "/" + tail);
                        }
                actor.ApplyRecipe(1, 1, 2);
                actor.Data.hunger = .4f;
                actor.Feed();
                Check(actor.Data.hunger > .4f, "Feeding increases nourishment");
                actor.Data.joy = .4f;
                actor.Pet();
                Check(actor.Data.joy > .4f, "Affection increases joy");
                actor.AddDirt(.6f);
                float before = actor.Data.cleanliness;
                int washed = 0;
                var camera = Camera.main;
                for (int y = 3; y < 8; y++) for (int x = 3; x < 8; x++)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3(x / 10f, y / 10f, 0f));
                    if (Physics.Raycast(ray, out RaycastHit hit, 30f) && actor.WashAt(hit, 1f)) washed++;
                }
                Check(washed > 0 && actor.Data.cleanliness > before, "Surface raycast and localized washing work");
                actor.BeginSquish();
                yield return new WaitForSeconds(.3f);
                actor.EndSquish();
                yield return new WaitForSeconds(.6f);
                Check(float.IsFinite(actor.ModelRoot.lossyScale.x), "Squish remains finite");
                foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>())
                    Check(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.isSupported, "Supported material: " + renderer.name);

                var saved = new PetSave { created = true, petName = "Teste Mimo", colorIndex = 2, earsIndex = 1, tailIndex = 2, hunger = .73f, joy = .82f, cleanliness = .63f, recipeSeed = 118 }; 
                Check(PetStorage.Save(saved), "Atomic local save succeeds");
                var loaded = PetStorage.Load();
                Check(loaded.created && loaded.petName == saved.petName && loaded.colorIndex == 2 && loaded.earsIndex == 1 && loaded.tailIndex == 2 && loaded.recipeSeed == 118, "Saved identity and ingredients restored");
                var offline = saved.Copy();
                offline.savedUtcTicks = DateTime.UtcNow.AddDays(-30).Ticks;
                PetStorage.ApplyOfflineTime(offline, DateTime.UtcNow.Ticks);
                Check(offline.hunger >= .35f && offline.joy >= .45f && offline.cleanliness >= .4f, "Offline care decay remains gentle");
                actor.Initialize(new PetSave { created = true, petName = "Mimo", colorIndex = 0, earsIndex = 1, tailIndex = 1, cleanliness = 1f, hunger = .9f, joy = 1f });
                actor.SetExpression("happy");
            }
            yield return new WaitForSeconds(4.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "automated-landscape.png"));
            var controls = FindObjectsByType<Button>().Select(b => b.name + " | " + string.Join(" ", b.GetComponentsInChildren<Text>().Select(t => t.text))).ToArray();
            yield return new WaitForSeconds(1f);
            var report = new Report { passed = errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray(), controls = controls, version = Application.unityVersion };
            File.WriteAllText(Path.Combine(output, "checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_SMOKE " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
