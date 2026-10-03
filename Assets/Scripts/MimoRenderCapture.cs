using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MimoGarden
{
    /// <summary>Scene evidence for opt-in checks, independent of whether Windows presents its swapchain.</summary>
    public static class MimoRenderCapture
    {
        // Hidden Windows players may have no presented backbuffer. Render the scene
        // and overlay explicitly so opt-in visual checks still capture real pixels.
        public static Texture2D Frame(Camera camera)
        {
            Texture2D scene = Scene(camera);
            if (!scene) return null;
            int width = Screen.width, height = Screen.height;
            Color[] pixels = new Color[width * height];
            Color[] scenePixels = scene.GetPixels();
            int left = Mathf.RoundToInt(camera.rect.x * width), bottom = Mathf.RoundToInt(camera.rect.y * height);
            for (int y = 0; y < scene.height; y++)
                for (int x = 0; x < scene.width; x++)
                    if (left + x < width && bottom + y < height) pixels[(bottom + y) * width + left + x] = scenePixels[y * scene.width + x];
            Object.Destroy(scene);
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var changed = new System.Collections.Generic.List<Canvas>();
            var layers = new System.Collections.Generic.Dictionary<GameObject, int>();
            var overlayObject = new GameObject("Temporary visual-check UI camera");
            Camera overlay = overlayObject.AddComponent<Camera>();
            overlay.enabled = false; overlay.clearFlags = CameraClearFlags.SolidColor; overlay.backgroundColor = Color.clear;
            overlay.cullingMask = 1 << 31; overlay.nearClipPlane = .01f; overlay.farClipPlane = 10f;
            overlay.transform.position = new Vector3(0, 0, -1000);
            overlay.allowHDR = false; overlay.allowMSAA = false;
            overlayObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            RenderTexture active = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D ui = null;
            try
            {
                target.Create(); overlay.targetTexture = target;
                foreach (Canvas canvas in canvases)
                {
                    if (!canvas.enabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    changed.Add(canvas);
                    foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                        if (!layers.ContainsKey(child.gameObject)) { layers.Add(child.gameObject, child.gameObject.layer); child.gameObject.layer = 31; }
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = overlay; canvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(overlay, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                ui = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                ui.ReadPixels(new Rect(0, 0, width, height), 0, 0); ui.Apply();
                Color[] front = ui.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    // The transparent render target contains premultiplied UI color.
                    pixels[i] = front[i] + pixels[i] * (1f - front[i].a); pixels[i].a = 1;
                }
                var output = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                output.SetPixels(pixels); output.Apply(); return output;
            }
            finally
            {
                foreach (Canvas canvas in changed) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
                foreach (var entry in layers) if (entry.Key) entry.Key.layer = entry.Value;
                Canvas.ForceUpdateCanvases(); RenderTexture.active = active;
                overlay.targetTexture = null; target.Release(); Object.Destroy(target);
                if (ui) Object.Destroy(ui); Object.Destroy(overlayObject);
            }
        }

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
