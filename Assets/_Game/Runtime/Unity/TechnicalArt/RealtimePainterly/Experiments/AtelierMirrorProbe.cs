// 文件：AtelierMirrorProbe.cs
// 职责：给独立手镜验证提供真实平面反射、斜置比较和可控更新频率。
// 边界：只拥有镜像镜头、RT及运行时材质和网格，不读取或修改GameState。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Experiments
{
    /// <summary>实时反射只渲染当前三维场景，镜面层被排除以避免递归。</summary>
    public sealed class AtelierMirrorProbe : MonoBehaviour
    {
        [Tooltip("实际观察镜头")] public Camera Source;
        [Tooltip("完整镜面，不包含镜框")] public Renderer Surface;
        [Range(128, 1024)] public int Resolution = 512;
        [Range(5, 30)] public int FramesPerSecond = 15;
        public bool CompositeAfterPost;
        public Shader CompositeShader;
        /// <summary>有效镜面注册表，停用时立即移除，Feature按真实Source筛选。</summary>
        public static readonly HashSet<AtelierMirrorProbe> Active = new HashSet<AtelierMirrorProbe>();
        internal RTHandle TextureHandle { get; private set; }
        internal Material CompositeMaterial => mirrorMaterial;
        internal Mesh CompositeMesh => mirrorMesh;
        private bool surfaceWasEnabled;
        private Production.AtelierDraftCamera reflectedDraft;
        private Camera reflected;
        private RenderTexture texture;
        private Material originalMaterial, mirrorMaterial;
        private MeshFilter filter;
        private Mesh originalMesh, mirrorMesh;
        private Vector3[] vertices;
        private Vector2[] projectedUV;
        private UniversalRenderPipeline.SingleCameraRequest request;
        private float nextFrame;
        /// <summary>仅供构建取证读取现有RT；资源仍归镜面组件释放。</summary>
        internal RenderTexture ReviewTexture => texture;
        /// <summary>只读反射纹理，运行取证读取后不能释放或替换。</summary>
        public RenderTexture ReflectionTexture => texture;

        /// <summary>供主流程验收读取真实摄像机设置，不改变后处理开关。</summary>
        public bool SourceUsesPostProcessing => Source != null && Source.GetUniversalAdditionalCameraData().renderPostProcessing;

        private void OnEnable()
        {
            if (!Application.isPlaying || Source == null || Surface == null) return;
            var node = new GameObject("TA验证_手镜镜像摄像机") { hideFlags = HideFlags.HideAndDontSave };
            reflected = node.AddComponent<Camera>();
            reflected.enabled = false;
            if (CompositeAfterPost) reflectedDraft = node.AddComponent<Production.AtelierDraftCamera>();
            texture = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32)
            {
                name = "TA验证_手镜实时画幅", hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            TextureHandle = RTHandles.Alloc(texture);
            surfaceWasEnabled = Surface.enabled;
            originalMaterial = Surface.sharedMaterial;
            mirrorMaterial = new Material(CompositeAfterPost ? CompositeShader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "TA验证_自有镜面材质" };
            if (!CompositeAfterPost) mirrorMaterial.SetColor("_BaseColor", new Color(.86f,.86f,.86f,1));
            filter = Surface.GetComponent<MeshFilter>();
            originalMesh = filter.sharedMesh;
            mirrorMesh = Instantiate(originalMesh);
            filter.sharedMesh = mirrorMesh;
            vertices = mirrorMesh.vertices;
            projectedUV = new Vector2[vertices.Length];
            Surface.sharedMaterial = mirrorMaterial;
            Surface.SetPropertyBlock(null);
            request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            if (CompositeAfterPost) Surface.enabled = false;
            Active.Add(this);
        }

        private void LateUpdate()
        {
            if (reflected == null || Source == null || Surface == null || Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + 1f / FramesPerSecond;
            Vector3 point = Surface.transform.position;
            Vector3 normal = Surface.transform.forward;
            if (Vector3.Dot(Source.transform.position - point, normal) < 0) normal = -normal;
            float distance = -Vector3.Dot(normal, point);
            var matrix = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    matrix[row, column] -= 2 * normal[row] * normal[column];
                matrix[row, 3] = -2 * distance * normal[row];
            }
            reflected.CopyFrom(Source);
            reflected.enabled = false;
            // 镜面使用确定的LDR背景，避免Player中的HDR清屏值经主镜头Bloom扩散。
            reflected.allowHDR = false;
            reflected.allowMSAA = false;
            reflected.useOcclusionCulling = false;
            reflected.clearFlags = CameraClearFlags.SolidColor;
            reflected.backgroundColor = new Color(.012f, .018f, .025f, 1);
            reflected.cullingMask = Source.cullingMask & ~(1 << 30) & ~(1 << 31);
            reflected.targetTexture = texture;
            reflected.worldToCameraMatrix = Source.worldToCameraMatrix * matrix;
            reflected.transform.position = matrix.MultiplyPoint(Source.transform.position);
            Vector3 clipPoint = reflected.worldToCameraMatrix.MultiplyPoint(point + normal * .003f);
            Vector3 clipNormal = reflected.worldToCameraMatrix.MultiplyVector(normal).normalized;
            reflected.projectionMatrix = Source.CalculateObliqueMatrix(new Vector4(clipNormal.x, clipNormal.y,
                clipNormal.z, -Vector3.Dot(clipPoint, clipNormal)));
            var cameraData = reflected.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            var sourceDraft = Source.GetComponent<Production.AtelierDraftCamera>();
            if (reflectedDraft != null && sourceDraft != null) reflectedDraft.CopyVisualState(sourceDraft);
            bool previous = GL.invertCulling;
            try
            {
                // 镜像改变手性，只在本次镜像绘制期间翻转剔除，异常时也恢复。
                GL.invertCulling = true;
                RenderPipeline.SubmitRenderRequest(reflected, request);
            }
            finally { GL.invertCulling = previous; }
            // 纹理绑定到自有材质，避免Player中批处理路径忽略镜面纹理属性块。
            mirrorMaterial.SetTexture("_BaseMap", texture);
            // 平面顶点和它的镜像位置重合。用真实镜头投影更新有限椭圆网格UV，
            // 标准URP Unlit负责采样，避开Player自定义镜面通道的异常过曝。
            for (int n = 0; n < vertices.Length; n++)
            {
                Vector3 pointOnScreen = Source.WorldToViewportPoint(Surface.transform.TransformPoint(vertices[n]));
                projectedUV[n] = new Vector2(pointOnScreen.x, pointOnScreen.y);
            }
            mirrorMesh.uv = projectedUV;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (Surface != null && originalMesh != null) Surface.enabled = surfaceWasEnabled;
            TextureHandle?.Release();
            TextureHandle = null;
            // 资源完全归本验证组件，退出Play后不留下共享RT或隐藏摄像机。
            if (filter != null && originalMesh != null) filter.sharedMesh = originalMesh;
            if (mirrorMesh != null) { Destroy(mirrorMesh); mirrorMesh = null; }
            if (Surface != null && originalMaterial != null) Surface.sharedMaterial = originalMaterial;
            if (mirrorMaterial != null) { Destroy(mirrorMaterial); mirrorMaterial = null; }
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
            if (reflected != null) { Destroy(reflected.gameObject); reflected = null; }
        }
    }
}
