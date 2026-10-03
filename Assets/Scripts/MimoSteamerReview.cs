using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MimoGarden
{
    // Opt-in asset inspection in the real player. Ordinary sessions never construct this component.
    public sealed class MimoSteamerReview : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string version, gpu;
            public bool passed, ordinarySaveUnchanged;
            public int openTriangles, closedTriangles, openRenderers, closedRenderers;
            public int textureWidth, textureHeight, textureMipCount;
            public string textureFormat;
            public string[] errors, screenshots;
        }
        static string output, ordinary;
        static byte[] saved;
        static bool existed;
        static bool Enabled => Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-steamer-review") >= 0;
        readonly List<string> errors = new List<string>(), screenshots = new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate()
        {
            if (!Enabled) return;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "--mimo-test-output");
            if (at < 0 || at + 1 >= args.Length) throw new ArgumentException("Steamer review requires --mimo-test-output.");
            output = Path.GetFullPath(args[at + 1]); Directory.CreateDirectory(output);
            ordinary = Path.Combine(Application.persistentDataPath, "mimo-pet.json");
            existed = File.Exists(ordinary); saved = existed ? File.ReadAllBytes(ordinary) : null;
            PetStorage.SavePathOverride = Path.Combine(output, "isolated-steamer-" + DateTime.UtcNow.Ticks + ".json");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (Enabled) new GameObject("Steamer visual inspection").AddComponent<MimoSteamerReview>(); }
        void Awake() { Application.runInBackground = true; Application.logMessageReceived += OnLog; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text); }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            var app = FindAnyObjectByType<MimoExperience>(); var cam = Camera.main;
            app.EnterLaboratory(); yield return new WaitForSecondsRealtime(1.5f);
            var station = app.CreationStation;
            var report = new Report { version = Application.version, gpu = SystemInfo.graphicsDeviceName };
            var grain = Resources.Load<Texture2D>("Textures/BambooGrain");
            if (!grain) errors.Add("Generated grain texture is missing.");
            else
            {
                report.textureWidth = grain.width; report.textureHeight = grain.height;
                report.textureMipCount = grain.mipmapCount; report.textureFormat = grain.format.ToString();
                if (grain.width > 512 || grain.height > 512 || grain.mipmapCount < 2) errors.Add("Texture exceeds mobile budget or has no mipmaps.");
            }
            if (station.LidVisible) errors.Add("The preparation view has a visible lid.");
            yield return new WaitForEndOfFrame(); Capture(cam, "01-game-open.png");
            Count(station, out report.openTriangles, out report.openRenderers);
            foreach (var f in station.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!f.GetComponent<Renderer>().enabled) continue;
                foreach (var p in f.sharedMesh.vertices)
                    if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) errors.Add("Invalid mesh: " + f.name);
                if ((f.GetComponent<Renderer>().sharedMaterial.shader.name == "MimoGarden/WovenBamboo" || f.GetComponent<Renderer>().sharedMaterial.shader.name == "MimoGarden/BoundBamboo") && f.sharedMesh.uv.Length != f.sharedMesh.vertexCount)
                    errors.Add("Woven surface is missing UVs: " + f.name);
            }
            station.BeginReveal(); station.SetRevealProgress(.20f);
            if (!station.LidVisible) errors.Add("Finalization did not show the lid.");
            Count(station, out report.closedTriangles, out report.closedRenderers);
            Capture(cam, "02-game-closed.png");
            var otherRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && !r.transform.IsChildOf(station.transform)).ToArray();
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled).ToArray();
            foreach (var r in otherRenderers) r.enabled = false;
            foreach (var c in canvases) c.enabled = false;
            app.enabled = false; station.enabled = false;
            cam.rect = new Rect(0, 0, 1, 1); cam.fieldOfView = 34; cam.backgroundColor = new Color(.16f,.20f,.19f);
            Vector3 center = station.transform.position + Vector3.up * .60f;
            Shoot(cam, center, new Vector3(0, 1.35f, -4.3f), "03-closed-front.png");
            Shoot(cam, center, new Vector3(3.1f, 2.6f, -3.1f), "04-closed-oblique.png");
            Shoot(cam, center, new Vector3(0, 4.7f, -.15f), "05-lid-top.png");
            station.SetRevealProgress(.38f);
            Shoot(cam, center + Vector3.up * .65f, new Vector3(3.5f, 2.3f, -4.2f), "06-lid-opening.png");
            station.RestoreForCreation();
            Shoot(cam, center, new Vector3(-3.1f, 2.7f, -3.1f), "07-open-interior.png");
            Shoot(cam, center, new Vector3(3.8f, .35f, 1.8f), "08-wall-back.png");
            report.ordinarySaveUnchanged = existed ? File.Exists(ordinary) && saved.SequenceEqual(File.ReadAllBytes(ordinary)) : !File.Exists(ordinary);
            if (!report.ordinarySaveUnchanged) errors.Add("Ordinary save changed.");
            report.errors = errors.ToArray(); report.screenshots = screenshots.ToArray(); report.passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(output, "steamer-review.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 1);
        }
        static void Count(MimoCreationStation station, out int triangles, out int renderers)
        {
            triangles = renderers = 0;
            foreach (var r in station.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled) { renderers++; triangles += r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3; }
        }
        void Shoot(Camera cam, Vector3 target, Vector3 offset, string name)
        { cam.transform.position = target + offset; cam.transform.LookAt(target); Capture(cam, name); }
        void Capture(Camera cam, string name)
        {
            var texture = MimoRenderCapture.Frame(cam);
            if (!texture) { errors.Add("Empty capture: " + name); return; }
            File.WriteAllBytes(Path.Combine(output, name), texture.EncodeToPNG()); Destroy(texture); screenshots.Add(name);
        }
    }
}
