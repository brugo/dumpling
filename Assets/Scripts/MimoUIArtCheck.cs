using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MimoGarden
{
    public sealed class MimoUIArtCheck : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed, ordinarySaveUnchanged;
            public string version;
            public int width, height;
            public string[] checks, errors;
        }
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-ui-art-test") >= 0;
        static string output, ordinary;
        static byte[] previous;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        float started;
        bool finished;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--mimo-test-output");
            output = at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Path.Combine(Application.temporaryCachePath, "MimoUIArtCheck");
            Directory.CreateDirectory(output);
            ordinary = Path.Combine(Application.persistentDataPath, "mimo-pet.json"); previous = File.Exists(ordinary) ? File.ReadAllBytes(ordinary) : null;
            PetStorage.SavePathOverride = Path.Combine(output, "isolated-ui-" + DateTime.UtcNow.Ticks + ".json");
            var a = new PetSave { petName = "Mimo", created = true, colorIndex = 0, earsIndex = 1, tailIndex = 1, cleanliness = 1, hunger = .8f, joy = .9f };
            var b = new PetSave { petName = "Nuvem", created = true, colorIndex = 3, wingsIndex = 2, hatIndex = 4, cleanliness = 1 };
            PetStorage.SaveCollection(new PetCollection { pets = new List<PetSave> { a, b }, activePetId = a.petId });
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Mimo picture UI verification").AddComponent<MimoUIArtCheck>(); }
        void Awake() { started = Time.realtimeSinceStartup; Application.runInBackground = true; Application.logMessageReceived += Log; }
        void OnDestroy() { Application.logMessageReceived -= Log; }
        void Log(string m, string s, LogType k) { if (k == LogType.Error || k == LogType.Exception || k == LogType.Assert) errors.Add(m); }
        void Update() { if (!finished && Time.realtimeSinceStartup - started > 60) { errors.Add("UI verification timed out"); Finish(); } }
        void Check(bool value, string text) { if (value) checks.Add(text); else errors.Add(text); }
        IEnumerator Render() { yield return new WaitForEndOfFrame(); Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame(); }
        IEnumerator Capture(string name)
        {
            yield return Render(); Texture2D image = MimoRenderCapture.Frame(Camera.main);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); Destroy(image);
        }
        Button Find(string name) => FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.gameObject.activeInHierarchy && b.GetComponentsInChildren<Text>().Any(t => t.text == name));
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1);
            var app = FindAnyObjectByType<MimoExperience>();
            Check(app && app.CurrentScreen == "Home", "Isolated two-friend playground starts normally");
            if (!app) { Finish(); yield break; }
            Check(Resources.Load<Font>("UI/Nunito"), "The bundled rounded UI font loads");
            for (int c = 0; c < 8; c++)
            {
                Check(MimoUIArt.Category(c), "Generated category sprite loads: " + c);
                for (int i = 0; i < PetCatalog.Options(c).Length; i++)
                {
                    Sprite art = MimoUIArt.Ingredient(c, i);
                    Check(art && art.rect.width >= 200 && art.rect.height >= 160, "Generated ingredient is available above its display resolution: " + c + "." + i);
                }
            }
            foreach (string room in new[] { "Play", "Food", "Bath" })
            {
                app.VisitCareRoom(room); yield return Render();
                foreach (string destination in new[] { "Comidinha", "Banho", "Brincar", "Ateliê", "Surpresa" })
                {
                    Button b = Find(destination);
                    Check(b && !b.GetComponentInParent<ScrollRect>(), room + ": destination always visible: " + destination);
                    if (!b) continue;
                    Image art = b.GetComponentsInChildren<Image>().FirstOrDefault(i => i.sprite && i.sprite.name.StartsWith("MimoUIAtlas:"));
                    Check(art && !art.raycastTarget, room + ": generated destination picture does not intercept touches: " + destination);
                    RectTransform rect = (RectTransform)b.transform;
                    Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
                    Check((corners[2].x - corners[0].x) >= 44 && (corners[2].y - corners[0].y) >= 44 && Screen.safeArea.Contains((Vector2)corners[0] + Vector2.one) && Screen.safeArea.Contains((Vector2)corners[2] - Vector2.one), room + ": destination touch target fits screen: " + destination);
                    Vector2 center = rect.TransformPoint(rect.rect.center);
                    var e = new PointerEventData(EventSystem.current) { position = center };
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(e, hits);
                    Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == b.gameObject, room + ": destination button receives actual raycast: " + destination);
                }
                yield return Capture("ui-" + room.ToLowerInvariant());
            }
            app.EnterLaboratory(); yield return Render();
            for (int c = 0; c < 8; c++)
            {
                app.SelectRecipeCategory(c); yield return Render();
                var images = FindObjectsByType<MimoIngredientIcon>(FindObjectsSortMode.None).Where(i => i.gameObject.activeInHierarchy).ToArray();
                Check(images.Count(i => i.CategoryPreview) == 8, "All eight illustrated categories remain visible: " + c);
                Check(images.Count(i => !i.CategoryPreview) == PetCatalog.Options(c).Length && images.All(i => i.sprite && !i.raycastTarget), "Every ingredient card uses generated artwork: " + c);
                foreach (var i in images.Where(i => i.CategoryPreview))
                    Check(i.GetComponentInParent<Button>().GetComponentsInChildren<Text>().Any(t => t.fontSize >= 12 && !string.IsNullOrWhiteSpace(t.text)), "Picture category also has a readable short name: " + c + "." + i.Category);
                yield return Capture("ui-category-" + c);
            }
            Finish();
        }
        void Finish()
        {
            if (finished) return; finished = true;
            byte[] now = File.Exists(ordinary) ? File.ReadAllBytes(ordinary) : null;
            bool unchanged = previous == null ? now == null : now != null && previous.SequenceEqual(now);
            Check(unchanged, "The ordinary saved friends remain byte-for-byte unchanged");
            var report = new Report { passed = errors.Count == 0, ordinarySaveUnchanged = unchanged, version = Application.version, width = Screen.width, height = Screen.height, checks = checks.ToArray(), errors = errors.ToArray() };
            File.WriteAllText(Path.Combine(output, "ui-art-checks.json"), JsonUtility.ToJson(report, true));
            Debug.Log("MIMO_UI_ART " + (report.passed ? "PASS" : "FAIL") + " checks=" + checks.Count + " errors=" + errors.Count);
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
