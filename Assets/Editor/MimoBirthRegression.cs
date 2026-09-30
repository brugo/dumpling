using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace MimoGarden.EditorTools
{
    // This regression must run in the Editor, whose missing-component wrappers differ from a player.
    [InitializeOnLoad]
    public static class MimoBirthRegression
    {
        const string Pending = "Mimo.BirthRegression.Pending";
        static int stage;
        static double next, deadline;
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static MimoExperience experience;
        static bool ingredientReady;
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../MimoGardenPreview"));
        [Serializable] class Report { public bool passed; public string[] checks; public string[] errors; public string environment = "Unity Editor Play Mode"; }

        static MimoBirthRegression()
        {
            if (SessionState.GetBool(Pending, false)) Attach();
        }

        [MenuItem("Mimo/Validar nascimento no Editor")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Pare o Play antes da verificação.");
            EditorSceneManager.OpenScene("Assets/Scenes/MimoGarden.unity");
            SessionState.SetBool(Pending, true);
            stage = 0; next = 0; ingredientReady = false; experience = null;
            checks.Clear(); errors.Clear();
            Attach();
            EditorApplication.isPlaying = true;
        }

        static void Attach()
        {
            deadline = EditorApplication.timeSinceStartup + 75;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks.Add(message);
        }
        static Button Control(string text)
        {
            return UnityEngine.Object.FindObjectsByType<Button>().FirstOrDefault(b => b.isActiveAndEnabled && b.GetComponentsInChildren<Text>().Any(t => t.text == text));
        }
        static void Click(string text)
        {
            var button = Control(text);
            if (!button || !button.interactable) throw new Exception("Controle indisponível: " + text);
            button.onClick.Invoke();
        }

        static IEnumerator PrepareRecipe()
        {
            yield return MimoRitualCheck.AddAndStir(experience, 0, 0);
            ingredientReady = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Pending, false)) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup > deadline) { errors.Add("Nascimento excedeu o prazo de verificação."); Finish(); return; }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            try
            {
                switch (stage)
                {
                    case 0:
                        experience = UnityEngine.Object.FindAnyObjectByType<MimoExperience>();
                        if (!experience) return;
                        PetStorage.SavePathOverride = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../work/editor-birth-regression-save.json"));
                        experience.EnterLaboratory();
                        break;
                    case 1:
                        ingredientReady = false;
                        experience.StartCoroutine(PrepareRecipe());
                        break;
                    case 2:
                        if (!ingredientReady) return;
                        Require(experience.CanFinalizeRecipe && !experience.PendingStir, "Cor arrastada e incorporada com movimento circular");
                        Click("Criar meu Mimo!");
                        Require(experience.CurrentScreen == "Birth", "A receita inicia o nascimento no Editor");
                        Require(!GameObject.Find("Touch tray"), "Faixa de controles oculta durante o nascimento");
                        next = EditorApplication.timeSinceStartup + 1.8;
                        stage++; return;
                    case 3:
                        Directory.CreateDirectory(Output);
                        ScreenCapture.CaptureScreenshot(Path.Combine(Output, "Nascimento-corrigido.png"));
                        // Reveal itself lasts 3.1 seconds; leave a margin beyond its last frame.
                        next = EditorApplication.timeSinceStartup + 1.6;
                        stage++; return;
                    case 4:
                        Require(experience.CurrentScreen == "Naming", "Nascimento termina e permite escolher o nome");
                        var actor = experience.Actor;
                        Require(actor.Data.colorIndex == 0 && actor.Data.earsIndex == 0 && actor.Data.tailIndex == 0, "Receita rosa sem orelhas e sem rabinho preservada");
                        var parts = actor.ModelRoot.GetComponentsInChildren<Transform>(true);
                        foreach (string partName in new[] { "RabbitEars", "Tail.Puff", "Tail.Curl" })
                            Require(!parts.First(t => t.name == partName).gameObject.activeInHierarchy, partName + " oculto");
                        var body = actor.ModelRoot.GetComponentsInChildren<MeshCollider>(true)
                            .FirstOrDefault(c => c.sharedMesh && c.GetComponent<Renderer>());
                        Require(body, "MeshCollider criado corretamente no Editor");
                        var skin = body.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap") as Texture2D;
                        Require(skin && skin.isReadable, "Textura lavável do corpo disponível no Editor");
                        Color color = skin.GetPixel(skin.width / 2, Mathf.Clamp(Mathf.RoundToInt(skin.height * .96f), 0, skin.height - 1));
                        Color chosenColor = PetCatalog.Colors[actor.Data.colorIndex];
                        Require(Mathf.Abs(color.r - chosenColor.r) < .025f && Mathf.Abs(color.g - chosenColor.g) < .025f
                            && Mathf.Abs(color.b - chosenColor.b) < .025f, "Material do corpo corresponde ao rosa pastel escolhido no catálogo");
                        Require(Control("Levar para casa") && UnityEngine.Object.FindAnyObjectByType<InputField>(), "Nome e botão de continuar disponíveis");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Output, "Rosa-sem-acessorios.png"));
                        break;
                    case 5: Click("Levar para casa"); break;
                    case 6:
                        Require(experience.CurrentScreen == "Home", "É possível continuar até a casinha");
                        foreach (string text in new[] { "Comidinha", "Banho", "Brincar", "Ateliê" }) Require(Control(text) && Control(text).interactable, "Controle disponível: " + text);
                        next = EditorApplication.timeSinceStartup + .5;
                        stage++; return;
                    default: Finish(); return;
                }
                stage++;
                next = EditorApplication.timeSinceStartup + .2;
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(); }
        }

        static void Finish()
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Directory.CreateDirectory(Output);
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "Regressao-nascimento-editor.json"), JsonUtility.ToJson(new Report { passed = passed, checks = checks.ToArray(), errors = errors.ToArray() }, true));
            Debug.Log("MIMO_EDITOR_BIRTH_REGRESSION " + (passed ? "PASS" : "FAIL"));
            EditorApplication.playModeStateChanged -= RestoreStorage;
            EditorApplication.playModeStateChanged += RestoreStorage;
            EditorApplication.isPlaying = false;
        }

        static void RestoreStorage(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            // Fast Enter Play Mode can retain static fields; restore only after the test's shutdown save.
            PetStorage.SavePathOverride = null;
            EditorApplication.playModeStateChanged -= RestoreStorage;
        }
    }
}
