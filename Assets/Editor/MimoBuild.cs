using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MimoGarden.EditorTools
{
    public static class MimoBuild
    {
        const string ScenePath = "Assets/Scenes/MimoGarden.unity";
        const string AndroidMenu = "Mimo/Gerar APK Android de teste";
        const string PendingAndroidBuild = "MimoGarden.PendingAndroidBuild";
        const string PendingAndroidDeadline = "MimoGarden.PendingAndroidDeadline";

        [MenuItem("Mimo/Preparar projeto")]
        public static void Prepare()
        {
            MimoFinishEnvironment.Ensure();
            foreach (string path in Directory.GetFiles("Assets/Resources/UI", "*.png"))
            {
                var texture = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!texture) continue;
                texture.textureType = TextureImporterType.Default;
                texture.alphaSource = TextureImporterAlphaSource.FromInput;
                texture.alphaIsTransparency = true; texture.sRGBTexture = true;
                texture.mipmapEnabled = false; texture.isReadable = false;
                texture.maxTextureSize = 1024; texture.textureCompression = TextureImporterCompression.Uncompressed;
                texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
                texture.SaveAndReimport();
            }
            var importer = AssetImporter.GetAtPath("Assets/Resources/MimoCharacter.fbx") as ModelImporter;
            if (importer == null) throw new Exception("O personagem MimoCharacter.fbx não foi encontrado.");
            importer.isReadable = true;
            importer.importBlendShapes = true;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (!pipeline) throw new Exception("O renderizador URP não foi encontrado.");
            bool mobile = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android || EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            pipeline.renderScale = mobile ? .67f : 1f;
            pipeline.msaaSampleCount = mobile ? 1 : 4;
            pipeline.shadowDistance = mobile ? 0f : 20f;
            var pipelineSettings = new SerializedObject(pipeline);
            pipelineSettings.FindProperty("m_SoftShadowsSupported").boolValue = !mobile;
            pipelineSettings.FindProperty("m_MainLightShadowsSupported").boolValue = !mobile;
            pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
            pipelineSettings.FindProperty("m_AnyShadowsSupported").boolValue = !mobile;
            pipelineSettings.FindProperty("m_AdditionalLightsRenderingMode").intValue = mobile ? 0 : 1;
            pipelineSettings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue = mobile ? 0 : 4;
            pipelineSettings.FindProperty("m_ReflectionProbeBlending").boolValue = !mobile;
            pipelineSettings.FindProperty("m_ReflectionProbeBoxProjection").boolValue = !mobile;
            pipelineSettings.FindProperty("m_SupportsLightCookies").boolValue = !mobile;
            pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            pipeline.mainLightShadowmapResolution = mobile ? 512 : 2048;
            pipeline.supportsHDR = !mobile;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(0, true);
            EditorUtility.SetDirty(pipeline);
            Directory.CreateDirectory("Assets/Resources/Materials");
            foreach (string name in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit" })
            {
                var shader = Shader.Find(name);
                if (!shader) throw new Exception("Shader ausente: " + name);
                string path = "Assets/Resources/Materials/" + name.Replace("Universal Render Pipeline/", "").Replace('/', '-') + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                if (name.Contains("Particles"))
                {
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    EditorUtility.SetDirty(material);
                }
            }

            // This runtime contact shadow must keep its transparent shader variant in the APK.
            const string contactPath = "Assets/Resources/Materials/Mobile-Contact-Shadow.mat";
            var contact = AssetDatabase.LoadAssetAtPath<Material>(contactPath);
            if (!contact) { contact = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(contact, contactPath); }
            contact.SetFloat("_Surface", 1f);
            contact.SetFloat("_Blend", 0f);
            contact.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            contact.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            contact.SetFloat("_ZWrite", 0f);
            contact.SetFloat("_Cull", (float)CullMode.Off);
            contact.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            contact.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(contact);

            PlayerSettings.companyName = "Mimo Garden";
            PlayerSettings.productName = "Mimo — Meu pequeno mundo";
            PlayerSettings.bundleVersion = "0.5.3";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.mimogarden.prototype");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.mimogarden.prototype");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Mimo Experience").AddComponent<MimoExperience>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("MIMO_PREPARE_OK " + Application.unityVersion);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/MimoCharacter.fbx");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                Debug.Log("MIMO_MODEL " + renderer.name + " center=" + renderer.bounds.center + " size=" + renderer.bounds.size);
        }

        [MenuItem("Mimo/Gerar versão Windows")]
        public static void BuildWindows()
        {
            Prepare();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../MimoGardenPlayable/MimoGarden.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Debug.Log("MIMO_BUILD_RESULT " + report.summary.result + " bytes=" + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build falhou: " + report.summary.result);
        }

        [MenuItem(AndroidMenu)]
        public static void BuildAndroid()
        {
            EnsureEditorCanBuildAndroid();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Instale Android Build Support, Android SDK & NDK Tools e OpenJDK para esta versão do Unity pelo Hub.");

            // Platform defines must be recompiled before building. Batch callers select Android at startup.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (Application.isBatchMode)
                    throw new InvalidOperationException("Inicie o Unity com -buildTarget Android antes de executar MimoGarden.EditorTools.MimoBuild.BuildAndroid.");
                SessionState.SetBool(PendingAndroidBuild, true);
                SessionState.SetFloat(PendingAndroidDeadline, (float)EditorApplication.timeSinceStartup + 900f);
                if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    SessionState.SetBool(PendingAndroidBuild, false);
                    throw new InvalidOperationException("Não foi possível ativar a plataforma Android.");
                }
                ResumeAndroidBuildAfterReload();
                Debug.Log("MIMO_ANDROID_WAIT: ativando Android e recompilando antes de gerar o APK.");
                return;
            }

            int originalInput = ReadActiveInputHandler();
            bool originalCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            bool originalAppBundle = EditorUserBuildSettings.buildAppBundle;
            bool originalExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            SceneSetup[] previousScenes = Application.isBatchMode ? null : EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Prepare();
                // Keep Android settings here: the Windows build keeps its existing Mono/windowed configuration.
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.mimogarden.prototype");
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, Il2CppCodeGeneration.OptimizeSize);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.Android.bundleVersionCode = Mathf.Max(8, PlayerSettings.Android.bundleVersionCode);
                PlayerSettings.Android.buildApkPerCpuArchitecture = false;
                PlayerSettings.Android.splitApplicationBinary = false;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.optimizedFramePacing = true;
                PlayerSettings.Android.startInFullscreen = true;
                PlayerSettings.Android.renderOutsideSafeArea = true;
                PlayerSettings.Android.forceInternetPermission = false;
                PlayerSettings.Android.forceSDCardPermission = false;
                PlayerSettings.Android.textureCompressionFormats = new[] { TextureCompressionFormat.ETC2 };
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                // Input.* and StandaloneInputModule are the actual touch path. Android does not support Both.
                SetActiveInputHandler(0);
                AssetDatabase.SaveAssets();
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../MimoGardenAndroid/MimoGarden.apk"));
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                Debug.Log("MIMO_ANDROID_CONFIG package=com.mimogarden.prototype backend=IL2CPP arch=ARM64,ARMv7 minSdk=26 targetSdk=Auto input=InputManager graphics=OpenGLES3 signing=UnityDebug output=" + output);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None
                });
                Debug.Log("MIMO_ANDROID_BUILD_RESULT " + report.summary.result + " bytes=" + report.summary.totalSize + " output=" + output);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new Exception("Não foi possível gerar o APK: " + report.summary.result);
            }
            finally
            {
                // Do not change the open Windows editor's input handling or the user's signing preferences.
                SetActiveInputHandler(originalInput);
                PlayerSettings.Android.useCustomKeystore = originalCustomKeystore;
                EditorUserBuildSettings.buildAppBundle = originalAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = originalExportProject;
                AssetDatabase.SaveAssets();
                if (previousScenes != null)
                {
                    SceneSetup[] savedScenes = previousScenes.Where(scene => !string.IsNullOrEmpty(scene.path)).ToArray();
                    if (savedScenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(savedScenes);
                    else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }

        [MenuItem(AndroidMenu, true)]
        static bool CanBuildAndroid()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !BuildPipeline.isBuildingPlayer;
        }

        static void EnsureEditorCanBuildAndroid()
        {
            if (!CanBuildAndroid()) throw new InvalidOperationException("Pare o Play e aguarde a compilação antes de gerar o APK.");
            if (Application.isBatchMode) return;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Salve as cenas alteradas antes de gerar o APK. As alterações abertas foram preservadas.");
        }

        [InitializeOnLoadMethod]
        static void ResumeAndroidBuildAfterReload()
        {
            if (!SessionState.GetBool(PendingAndroidBuild, false)) return;
            EditorApplication.update -= ContinueAndroidBuild;
            EditorApplication.update += ContinueAndroidBuild;
        }

        static void ContinueAndroidBuild()
        {
            if (!SessionState.GetBool(PendingAndroidBuild, false))
            {
                EditorApplication.update -= ContinueAndroidBuild;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup > SessionState.GetFloat(PendingAndroidDeadline, 0f))
            {
                SessionState.SetBool(PendingAndroidBuild, false);
                EditorApplication.update -= ContinueAndroidBuild;
                Debug.LogWarning("A geração pendente do APK foi cancelada; o Play e as cenas abertas foram preservados.");
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) return;
#if UNITY_ANDROID
            SessionState.SetBool(PendingAndroidBuild, false);
            EditorApplication.update -= ContinueAndroidBuild;
            EditorApplication.delayCall += BuildAndroid;
#endif
        }

        static SerializedObject SerializedPlayerSettings()
        {
            UnityEngine.Object settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (!settings) throw new InvalidOperationException("Não foi possível acessar PlayerSettings para configurar o toque Android.");
            return new SerializedObject(settings);
        }

        static int ReadActiveInputHandler()
        {
            SerializedObject settings = SerializedPlayerSettings();
            SerializedProperty property = settings.FindProperty("activeInputHandler");
            if (property == null) throw new InvalidOperationException("A versão do Unity não expõe activeInputHandler; confirme Input Manager nas configurações Android.");
            return property.intValue;
        }

        static void SetActiveInputHandler(int value)
        {
            SerializedObject settings = SerializedPlayerSettings();
            SerializedProperty property = settings.FindProperty("activeInputHandler");
            if (property == null) throw new InvalidOperationException("Não foi possível configurar activeInputHandler.");
            property.intValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void OpenPreview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.delayCall += () => { EditorApplication.isPlaying = true; };
        }
    }
}
