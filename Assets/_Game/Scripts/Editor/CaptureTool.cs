#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using CircuitBreaker.UI;

namespace CircuitBreaker.EditorTools
{
    /// <summary>
    /// Renders the game (board + interface) offscreen to a PNG at any size, independent of the Game view.
    /// Used for store screenshots and visual checks. Play Mode only.
    /// </summary>
    public static class CaptureTool
    {
        /// <summary>
        /// Two passes composited on the CPU: the world with its bloom, then the interface alone (no bloom, as in the
        /// real overlay canvas) over a transparent background.
        /// </summary>
        public static string Capture(string path, int width = 1080, int height = 1920)
        {
            var cam = Camera.main;
            var ui = Object.FindFirstObjectByType<GameUI>();
            if (cam == null || ui == null) return "not in play mode";
            Tick(24);                                   // grids and fitters need a few passes to settle
            var canvas = ui.GetComponentInChildren<Canvas>(true);
            var camData = cam.GetUniversalAdditionalCameraData();
            var postFx = GameObject.Find("PostFX");

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var prevTarget = cam.targetTexture; var prevMode = canvas.renderMode; var prevCam = canvas.worldCamera;
            var prevMask = cam.cullingMask; var prevClear = cam.clearFlags; var prevBg = cam.backgroundColor;
            bool prevPp = camData.renderPostProcessing; bool prevCanvasEnabled = canvas.enabled;
            Texture2D world = null, overlay = null, result = null;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
                cam.targetTexture = rt;

                // pass 1: world only
                canvas.enabled = false;
                Tick();
                world = Render(cam, rt, width, height);
                if (world == null) return "render request not supported";

                // pass 2: interface only, no post-processing, transparent background
                canvas.enabled = true;
                var board = CircuitBreaker.Core.GameManager.Instance != null ? CircuitBreaker.Core.GameManager.Instance.Board.gameObject : null;
                if (board != null) board.SetActive(false);        // interface only (the UI shares the default layer)
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                camData.renderPostProcessing = false;
                if (postFx != null) postFx.SetActive(false);
                Tick();
                overlay = Render(cam, rt, width, height);

                var wp = world.GetPixels32();
                var op = overlay.GetPixels32();
                var outp = new Color32[wp.Length];
                for (int i = 0; i < wp.Length; i++)
                {
                    // The UI shader blends colour by alpha into a cleared target, so colour is premultiplied and alpha is squared.
                    float a = Mathf.Sqrt(op[i].a / 255f);
                    outp[i] = new Color32(
                        (byte)Mathf.Min(255f, op[i].r + wp[i].r * (1f - a)),
                        (byte)Mathf.Min(255f, op[i].g + wp[i].g * (1f - a)),
                        (byte)Mathf.Min(255f, op[i].b + wp[i].b * (1f - a)), 255);
                }
                result = new Texture2D(width, height, TextureFormat.RGB24, false);
                result.SetPixels32(outp);
                result.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, result.EncodeToPNG());
                return "saved " + path + " (" + width + "x" + height + ")";
            }
            finally
            {
                if (CircuitBreaker.Core.GameManager.Instance != null) CircuitBreaker.Core.GameManager.Instance.Board.gameObject.SetActive(true);
                if (postFx != null) postFx.SetActive(true);
                camData.renderPostProcessing = prevPp;
                cam.cullingMask = prevMask; cam.clearFlags = prevClear; cam.backgroundColor = prevBg;
                cam.targetTexture = prevTarget;
                canvas.enabled = prevCanvasEnabled;
                canvas.renderMode = prevMode; canvas.worldCamera = prevCam;
                rt.Release(); Object.DestroyImmediate(rt);
                if (world != null) Object.DestroyImmediate(world);
                if (overlay != null) Object.DestroyImmediate(overlay);
                if (result != null) Object.DestroyImmediate(result);
                Tick();
            }
        }

        /// <summary>World only (no interface), with a callback to arrange the camera and extra objects first.</summary>
        public static string CaptureWorld(string path, int width, int height, System.Action<Camera> arrange)
        {
            var cam = Camera.main;
            var ui = Object.FindFirstObjectByType<GameUI>();
            if (cam == null || ui == null) return "not in play mode";
            var canvas = ui.GetComponentInChildren<Canvas>(true);
            var controller = cam.GetComponent<CircuitBreaker.Gameplay.CameraController>();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var prevTarget = cam.targetTexture; bool prevCanvas = canvas.enabled; bool prevCtl = controller != null && controller.enabled;
            Texture2D tex = null;
            try
            {
                canvas.enabled = false;
                cam.targetTexture = rt;
                Tick();
                if (controller != null) controller.enabled = false;
                arrange?.Invoke(cam);
                tex = Render(cam, rt, width, height);
                if (tex == null) return "render request not supported";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return "saved " + path + " (" + width + "x" + height + ")";
            }
            finally
            {
                cam.targetTexture = prevTarget; canvas.enabled = prevCanvas;
                if (controller != null) controller.enabled = prevCtl;
                rt.Release(); Object.DestroyImmediate(rt);
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }

        private static Texture2D Render(Camera cam, RenderTexture rt, int width, int height)
        {
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (!RenderPipeline.SupportsRenderRequest(cam, request)) return null;
            RenderPipeline.SubmitRenderRequest(cam, request);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        /// <summary>Runs layout and per-frame updates once, since the Editor does not always tick frames.</summary>
        public static void Tick(int passes = 8)
        {
            for (int i = 0; i < passes; i++)
            {
                foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    mb.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                foreach (var lg in Object.FindObjectsByType<UnityEngine.UI.LayoutGroup>(FindObjectsSortMode.None))
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)lg.transform);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif
