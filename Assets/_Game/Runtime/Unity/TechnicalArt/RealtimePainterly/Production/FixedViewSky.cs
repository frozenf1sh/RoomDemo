// 文件：FixedViewSky.cs
// 职责：用原 Hero 的独立绘画摄像机生成固定画幅，交给主摄像机的深度合成。
// 边界：不修改游戏事实和镜头姿态；只临时占用专用层并拥有运行时相机、材质与 RT。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Production
{
    /// <summary>固定视角的天空画幅；先完成原版绘画和色调映射，再合成到房间后方。</summary>
    [RequireComponent(typeof(Camera)), DefaultExecutionOrder(1000)]
    public sealed class FixedViewSky : MonoBehaviour
    {
        public LivingSkyController Sky;
        public Shader FilmShader;
        public Mesh FilmMesh;
        public VolumeProfile FilmVolume;
        private const int FilmLayer = 31;
        private Camera view, filmCamera;
        private GameObject ownedRoot;
        private Material filmMaterial;
        private Renderer filmSurface;
        private RenderTexture picture;
        private RTHandle handle;
        private MaterialPropertyBlock properties;
        private int originalMask;
        private bool originalSurfaceEnabled;
        private int resetGeneration;

        /// <summary>已绘画化和色调映射的画幅，由本组件拥有。</summary>
        public RTHandle Picture => handle;
        /// <summary>画幅实际纹理，供导入颜色视图和视觉取样；调用方不得释放。</summary>
        public RenderTexture Texture => picture;
        /// <summary>窗口比例变化时只居中裁切画幅，不拉伸旋涡或改动主摄像机。</summary>
        public Vector4 FrameScaleBias
        {
            get
            {
                float aspect = (float)Mathf.Max(1, view.pixelWidth) / Mathf.Max(1, view.pixelHeight);
                Vector2 scale = aspect > 16f / 9 ? new Vector2(1, (16f / 9) / aspect) : new Vector2(aspect / (16f / 9), 1);
                return new Vector4(scale.x, scale.y, (1 - scale.x) * .5f, (1 - scale.y) * .5f);
            }
        }
        /// <summary>主摄像机覆盖率；房间几何仍通过真实深度遮挡天空。</summary>
        public float Opacity => Sky != null && Sky.isActiveAndEnabled ? Sky.Coverage * Sky.Opacity : 0;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying || Sky == null || FilmShader == null || FilmMesh == null) return;
            view = GetComponent<Camera>();
            originalMask = view.cullingMask;
            view.cullingMask &= ~(1 << FilmLayer);
            originalSurfaceEnabled = Sky.Surface.enabled;
            Sky.Surface.enabled = false;
            Sky.FixedView = true;
            properties = new MaterialPropertyBlock();
            ownedRoot = new GameObject("星夜固定画幅_运行时") { hideFlags = HideFlags.HideAndDontSave, layer = FilmLayer };
            var film = new GameObject("原版绘画矩形", typeof(MeshFilter), typeof(MeshRenderer)) { layer = FilmLayer };
            film.transform.SetParent(ownedRoot.transform, false);
            film.GetComponent<MeshFilter>().sharedMesh = FilmMesh;
            filmSurface = film.GetComponent<Renderer>();
            filmSurface.shadowCastingMode = ShadowCastingMode.Off;
            filmSurface.receiveShadows = false;
            filmMaterial = new Material(FilmShader) { hideFlags = HideFlags.HideAndDontSave };
            filmSurface.sharedMaterial = filmMaterial;
            var cameraNode = new GameObject("原版绘画摄像机") { layer = FilmLayer };
            cameraNode.transform.SetParent(ownedRoot.transform, false);
            cameraNode.transform.position = new Vector3(0, 0, -10);
            filmCamera = cameraNode.AddComponent<Camera>();
            filmCamera.orthographic = true;
            filmCamera.orthographicSize = 10;
            filmCamera.nearClipPlane = .05f;
            filmCamera.farClipPlane = 50;
            filmCamera.cullingMask = 1 << FilmLayer;
            filmCamera.clearFlags = CameraClearFlags.SolidColor;
            filmCamera.backgroundColor = new Color(.018f, .029f, .052f);
            filmCamera.allowHDR = true;
            filmCamera.allowMSAA = false;
            filmCamera.depth = view.depth - 10;
            var data = filmCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.requiresColorOption = CameraOverrideOption.On;
            data.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            data.taaSettings.quality = TemporalAAQuality.High;
            data.taaSettings.baseBlendFactor = .60f;
            data.taaSettings.jitterScale = .5f;
            data.volumeLayerMask = 1 << FilmLayer;
            var painter = cameraNode.AddComponent<PainterlyCamera>();
            painter.PainterlyIntensity = .90f;
            painter.PatchSize = .095f;
            painter.PatchBlend = .50f;
            painter.HighlightStrength = .12f;
            var volume = ownedRoot.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = FilmVolume;
            Resize();
            LateUpdate();
        }

        private void Resize()
        {
            int width = Mathf.Max(64, view.pixelWidth), height = Mathf.Max(36, Mathf.RoundToInt(width * 9f / 16));
            if (picture != null && picture.width == width && picture.height == height) return;
            if (picture != null) ReleasePicture();
            // 摄像机输出需要深度；合成阶段显式导入它的颜色视图，不把深度当成颜色资源。
            picture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "星夜_原版绘画画幅", filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            picture.Create();
            handle = RTHandles.Alloc(new RenderTargetIdentifier(picture));
            filmCamera.targetTexture = picture;
        }

        private void LateUpdate()
        {
            if (filmCamera == null) return;
            Resize();
            Sky.Surface.GetPropertyBlock(properties);
            filmSurface.SetPropertyBlock(properties);
            if (resetGeneration != Sky.ResetGeneration)
            {
                filmCamera.GetUniversalAdditionalCameraData().resetHistory = true;
                resetGeneration = Sky.ResetGeneration;
            }
            // 覆盖为零时不渲染第二个摄像机；主房间继续使用自己的普通黑夜。
            filmCamera.enabled = Opacity > .0001f;
        }

        private void ReleasePicture()
        {
            if (filmCamera != null) filmCamera.targetTexture = null;
            handle?.Release();
            handle = null;
            if (picture != null) { picture.Release(); Destroy(picture); }
            picture = null;
        }

        private void OnDisable()
        {
            if (ownedRoot != null) ownedRoot.SetActive(false);
            ReleasePicture();
            if (ownedRoot != null) Destroy(ownedRoot);
            if (filmMaterial != null) Destroy(filmMaterial);
            if (view != null) view.cullingMask = originalMask;
            if (Sky != null && Sky.Surface != null) Sky.Surface.enabled = originalSurfaceEnabled;
            filmCamera = null;
        }
    }
}
