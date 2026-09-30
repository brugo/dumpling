using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MimoGarden
{
    /// <summary>Mobile rendering budget. Canvas overlay stays at the display's native resolution.</summary>
    public sealed class MimoPerformance : MonoBehaviour
    {
        public static bool Lightweight => Application.isMobilePlatform ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "--mimo-mobile-profile") >= 0;

        UniversalRenderPipelineAsset original, runtime;
        float averageFrame, nextReview, nextIncrease, nextLog;
        float maximumScale, minimumScale;
        int samples;

        public static void Configure(GameObject owner)
        {
            Application.targetFrameRate = Lightweight ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            if (Lightweight) owner.AddComponent<MimoPerformance>();
        }

        void Awake()
        {
            original = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (!original) return;
            runtime = Instantiate(original);
            runtime.name = "Mimo lightweight runtime";
            float longestSide = Mathf.Max(1, Mathf.Max(Screen.width, Screen.height));
            maximumScale = Mathf.Clamp(1280f / longestSide, .5f, .85f);
            minimumScale = Mathf.Min(maximumScale, Mathf.Clamp(960f / longestSide, .45f, .7f));
            runtime.renderScale = maximumScale;
            runtime.upscalingFilter = UpscalingFilterSelection.Linear;
            runtime.msaaSampleCount = 1;
            runtime.supportsHDR = false;
            runtime.shadowDistance = 0f;
            runtime.maxAdditionalLightsCount = 0;
            runtime.supportsCameraDepthTexture = false;
            runtime.supportsCameraOpaqueTexture = false;
            runtime.useSRPBatcher = true;
            QualitySettings.renderPipeline = runtime;
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
            nextReview = Time.realtimeSinceStartup + 8f;
            nextIncrease = nextReview + 10f;
            nextLog = nextReview;
            Debug.Log("MIMO_MOBILE_PROFILE fps=30 scale=" + runtime.renderScale.ToString("F2") +
                " hdr=false msaa=1 shadows=none additionalLights=0 nativeUI=true");
        }

        void Update()
        {
            if (!runtime || Time.unscaledDeltaTime > .25f) return;
            float now = Time.realtimeSinceStartup;
            averageFrame = samples++ == 0 ? Time.unscaledDeltaTime : Mathf.Lerp(averageFrame, Time.unscaledDeltaTime, .04f);
            if (now < nextReview || samples < 30) return;
            nextReview = now + 2f;
            if (averageFrame > .040f && runtime.renderScale > minimumScale + .005f)
            {
                runtime.renderScale = Mathf.Max(minimumScale, runtime.renderScale - .05f);
                nextIncrease = now + 12f;
            }
            else if (averageFrame < .035f && now > nextIncrease && runtime.renderScale < maximumScale - .005f)
            {
                runtime.renderScale = Mathf.Min(maximumScale, runtime.renderScale + .05f);
                nextIncrease = now + 12f;
            }
            if (now >= nextLog)
            {
                Debug.Log("MIMO_MOBILE_FRAME averageMs=" + (averageFrame * 1000f).ToString("F1") +
                    " scale=" + runtime.renderScale.ToString("F2"));
                nextLog = now + 15f;
            }
        }

        void OnDestroy()
        {
            if (runtime && QualitySettings.renderPipeline == runtime) QualitySettings.renderPipeline = original;
            if (runtime) Destroy(runtime);
        }
    }
}
