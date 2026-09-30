using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MimoGarden
{
    /// <summary>Scene evidence for opt-in checks, independent of whether Windows presents its swapchain.</summary>
    public static class MimoRenderCapture
    {
        public static Texture2D Scene(Camera camera)
        {
            if (!camera) return null;
            Rect rect = camera.rect;
            RenderTexture original = camera.targetTexture, active = RenderTexture.active;
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            bool[] visible = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++) { visible[i] = canvases[i].enabled; canvases[i].enabled = false; }
            int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * rect.height));
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                target.Create();
                camera.rect = new Rect(0, 0, 1, 1);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false); image.Apply(false, false);
                return image;
            }
            finally
            {
                camera.targetTexture = original; camera.rect = rect; RenderTexture.active = active;
                for (int i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].enabled = visible[i];
                target.Release(); Object.Destroy(target);
            }
        }
    }
}
