using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JevVision
{
    public sealed class CameraCapture : MonoBehaviour
    {
        public Camera frontCamera;
        public ExperimentConfig config;
        public RenderTexture Target { get; private set; }
        public Texture2D LastFrame { get; private set; }
        public byte[] LastJpeg { get; private set; }
        public double CapturedAt { get; private set; }
        private void EnsureTarget()
        {
            if (Target != null && Target.width == config.imageWidth && Target.height == config.imageHeight) return;
            Release();
            Target = new RenderTexture(config.imageWidth, config.imageHeight, 24, RenderTextureFormat.ARGB32) { name = "Actual front camera input", antiAliasing = 1 };
            Target.Create();
            LastFrame = new Texture2D(config.imageWidth, config.imageHeight, TextureFormat.RGB24, false);
            frontCamera.targetTexture = Target;
            frontCamera.enabled = false; // render only when capturing, including in batch players
            frontCamera.aspect = (float)config.imageWidth / config.imageHeight;
        }
        // Called after rendering, before any telemetry is drawn. JPEG is the only dynamic API input.
        public byte[] Capture()
        {
            EnsureTarget();
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderInto(frontCamera, Target);
                RenderTexture.active = Target;
                LastFrame.ReadPixels(new Rect(0, 0, Target.width, Target.height), 0, 0, false);
                LastFrame.Apply(false);
                LastJpeg = LastFrame.EncodeToJPG(config.jpegQuality);
                LastFrame.LoadImage(LastJpeg, false); // preview shows the decoded JPEG actually sent to JEV
                CapturedAt = Time.realtimeSinceStartupAsDouble;
                return LastJpeg;
            }
            finally { RenderTexture.active = previous; }
        }
        public static void RenderInto(Camera camera, RenderTexture destination)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = destination });
        }
        private void Start() => EnsureTarget();
        private void Release()
        {
            if (frontCamera != null) frontCamera.targetTexture = null;
            if (Target != null) { Target.Release(); Destroy(Target); }
            if (LastFrame != null) Destroy(LastFrame);
        }
        private void OnDestroy() => Release();
    }
}
