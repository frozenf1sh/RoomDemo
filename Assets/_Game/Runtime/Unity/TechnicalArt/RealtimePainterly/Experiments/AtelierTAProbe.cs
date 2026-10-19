// 文件：AtelierTAProbe.cs
// 职责：五份独立画室TA验证的演示控制、局部接触、固定机位和复位。
// 边界：所有状态只供视觉演示，不创建会话、不发领域命令、不改正式管线资源。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Game.Unity.TechnicalArt.RealtimePainterly.Production;

namespace Game.Unity.TechnicalArt.RealtimePainterly.Experiments
{
    /// <summary>按依赖顺序组织单项验证，不是五套正式游戏模式。</summary>
    public enum AtelierProbeKind { Draft, Wash, Mirror, Palette, Roof }

    /// <summary>验证工具允许直接操纵表现，界面始终注明不构成真实玩法路线。</summary>
    [DefaultExecutionOrder(200)]
    public sealed class AtelierTAProbe : MonoBehaviour
    {
        public AtelierProbeKind Kind;
        public RenderPipelineAsset ProbePipeline;
        public Font ChineseFont;
        public Vector3 RoomPosition, RoomEuler, ClosePosition, CloseEuler;
        public float RoomFov = 58, CloseFov = 48;
        public Renderer Water;
        public Transform SupportedBrush;
        public MeshFilter BrushSurface;
        public Renderer[] ToolSurfaces;
        public Transform Knife, RoofTrace, MirrorPivot;
        public Vector3 SurfaceOrigin;
        public Vector2 SurfaceSize = new Vector2(.51f, .25f);
        public Shader WaterShader, ToolShader;
        public LivingSkyController Sky;
        [Range(.5f, 1)] public float DraftDensity = .98f;
        [Range(0, 1)] public float LeftDraft = 1, WindowDraft = 1;
        [Range(0, 1)] public float GhostAmount;
        [Range(.3f, 1.8f)] public float WaterStrength = 1;
        public bool ShowHud = true;
        private Camera view;
        private MaterialPropertyBlock waterProperties;
        private RenderPipelineAsset previousPipeline, runtimePipeline;
        private bool changedPipeline, paused, close, knifeFace, pointerHeld;
        private float targetLeft = 1, targetWindow = 1, openingAge = 100, clock, washAge = 100, pigment;
        private float mirrorAngle;
        /// <summary>固定比较机位下的安全转角，防止镜面转成侧边细线。</summary>
        public const float MirrorAngleLimit = 15;
        /// <summary>当前实际演示角度，已经过安全区间约束。</summary>
        public float MirrorAngle => mirrorAngle;
        private Quaternion mirrorBase, brushBase;
        private Vector2 previousPoint;
        private int roofStep;
        private string message = "独立视觉验证，不写入游戏事实。";
        private readonly Vector4[] erase = new Vector4[64], spread = new Vector4[64];
        private int eraseCount, spreadCount;
        private readonly List<Material> owned = new List<Material>();
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private static readonly string[] Titles = { "局部草稿遮挡与阶段显色", "杯内洗笔接触反馈", "完整手镜与真实倒像", "刀刃揭底与刀面推色", "屋面细痕与两步试刮" };
        private static readonly string[] SceneNames = { "TA01_局部草稿遮挡", "TA02_洗笔反馈", "TA03_手镜比较", "TA04_刮刀干湿表面", "TA05_屋面试刮" };
        private static readonly string[] ShortNames = { "草稿遮挡", "洗笔反馈", "手镜比较", "刀具干湿", "屋面试刮" };

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            view = GetComponent<Camera>();
            // 手镜先隔离验证真实反射，避免Player后处理放大当前镜面合成异常。
            if (Kind == AtelierProbeKind.Mirror) view.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            previousPipeline = QualitySettings.renderPipeline;
            if (ProbePipeline != null)
            {
                // 只改自己拥有的运行时副本，避开独立副本切场景时的GPU常驻批次失效。
                // 反射用于隔离可选GPUDriven程序集；构建仍复用已有Shader变体。
                runtimePipeline = Instantiate(ProbePipeline);
                runtimePipeline.name = "TA验证_运行时非驻留管线";
                var mode = runtimePipeline.GetType().GetProperty("gpuResidentDrawerMode");
                if (mode != null) mode.SetValue(runtimePipeline, Enum.ToObject(mode.PropertyType, 0));
                QualitySettings.renderPipeline = runtimePipeline;
                changedPipeline = true;
            }
            if (SupportedBrush != null) brushBase = Quaternion.FromToRotation(Vector3.right, new Vector3(-.85f, .48f, .21f).normalized);
            if (MirrorPivot != null) mirrorBase = MirrorPivot.rotation;
            if (Water != null && Kind == AtelierProbeKind.Wash) Replace(Water, WaterShader);
            if (ToolSurfaces != null && (Kind == AtelierProbeKind.Palette || Kind == AtelierProbeKind.Roof))
                foreach (var surface in ToolSurfaces) Replace(surface, ToolShader);
            if (Sky != null)
            {
                Sky.ResetEffect();
                Sky.SetCoverage(Kind == AtelierProbeKind.Roof ? 1 : 0);
                Sky.SetFlow(Kind == AtelierProbeKind.Roof ? 1 : 0);
                Sky.LightTo(Kind == AtelierProbeKind.Roof ? 1 : 0, 0);
            }
            ResetProbe();
            SetClose(Kind == AtelierProbeKind.Wash || Kind == AtelierProbeKind.Palette || Kind == AtelierProbeKind.Mirror);
        }

        private void Replace(Renderer target, Shader shader)
        {
            if (target == null || shader == null || originals.ContainsKey(target)) return;
            var source = target.sharedMaterials;
            originals.Add(target, source);
            var output = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var copy = source[i] != null ? new Material(source[i]) : new Material(shader);
                copy.name = "TA验证_运行时拥有_" + target.name;
                copy.shader = shader;
                copy.SetFloat("_ProbeRoof", Kind == AtelierProbeKind.Roof ? 1 : 0);
                owned.Add(copy);
                output[i] = copy;
            }
            target.sharedMaterials = output;
        }

        /// <summary>显式切换演示阶段；第三、四阶段都保持全景，不自动授任何材料。</summary>
        public void SetStage(int stage, bool immediate = false)
        {
            targetLeft = stage == 1 ? 1 : 0;
            targetWindow = stage == 1 || stage == 2 ? 1 : 0;
            if (immediate) { LeftDraft = targetLeft; WindowDraft = targetWindow; }
            message = stage == 0 ? "原场景对照：所有阶段遮挡关闭。"
                : stage == 1 ? "阶段一：杯、便签、画布和窗台小画清楚；左侧组与窗景保留底稿。"
                : stage == 2 ? "阶段二：只解除左侧物件组，窗景继续遮挡。"
                : "阶段三／四：解除窗景，房间保持完整原材质。";
        }

        /// <summary>只演示一次短重影；结束后稳定在第一阶段底稿。</summary>
        public void Opening()
        {
            openingAge = 0;
            SetStage(1);
        }

        /// <summary>通过同一入口夹紧滑块和自动演示输入，不允许绕过转角限制。</summary>
        public void SetMirrorAngle(float degrees)
        {
            mirrorAngle = Mathf.Clamp(degrees, -MirrorAngleLimit, MirrorAngleLimit);
            if (MirrorPivot != null) MirrorPivot.rotation = Quaternion.AngleAxis(mirrorAngle, Vector3.up) * mirrorBase;
        }

        /// <summary>重新演示有限的一次洗笔接触，不依赖游戏洗笔事实。</summary>
        public void BeginWash()
        {
            if (Kind != AtelierProbeKind.Wash) return;
            washAge = 0;
            pigment = 0;
            message = "笔尖接触水面后才开始散色；请比较杯口内变化，陶杯外壁保持原样。";
        }

        /// <summary>记录一段明确的表面接触；用于现场鼠标或自动视觉取证，不是玩家验收。</summary>
        public void AddContact(Vector2 from, Vector2 to, bool face)
        {
            var item = new Vector4(from.x, from.y, to.x, to.y);
            if (face && Kind == AtelierProbeKind.Palette)
            {
                if (spreadCount < 64) spread[spreadCount++] = item;
                message = "刀面：右侧软色摊开为薄带；木底不被挖掉。";
            }
            else
            {
                if (eraseCount < 64) erase[eraseCount++] = item;
                message = Kind == AtelierProbeKind.Roof ? "小孔已真实裁剪颜色、深度和阴影；其后为现有星空。" : "刀刃：左侧干壳揭开，露出原来的木底。";
            }
            ApplySurface();
        }

        /// <summary>屋面首次小试与主动扩大分开；不触发整间房间擦除。</summary>
        public void RoofStep()
        {
            if (Kind != AtelierProbeKind.Roof) return;
            roofStep = Mathf.Min(2, roofStep + 1);
            float half = roofStep == 1 ? .025f : .15f;
            AddContact(new Vector2(.5f - half, .5f), new Vector2(.5f + half, .51f), false);
            message = roofStep == 1 ? "第一次仅开一处小孔，木梁保留；可复位。" : "第二次主动扩大，仍只验证局部屋面，不播放全屋终局。";
        }

        /// <summary>所有模式回到初始视觉；原始场景材质资产不被写入。</summary>
        public void ResetProbe()
        {
            paused = false;
            pointerHeld = false;
            openingAge = washAge = 100;
            clock = pigment = GhostAmount = 0;
            eraseCount = spreadCount = roofStep = 0;
            knifeFace = false;
            if (Kind == AtelierProbeKind.Draft) SetStage(1, true);
            if (Sky != null) Sky.SetPaused(false);
            if (Kind == AtelierProbeKind.Palette || Kind == AtelierProbeKind.Roof) ApplySurface();
            if (Knife != null) Knife.gameObject.SetActive(false);
            if (SupportedBrush != null) SupportedBrush.localRotation = brushBase;
            if (MirrorPivot != null) { MirrorPivot.rotation = mirrorBase; mirrorAngle = 0; }
            if (Kind != AtelierProbeKind.Draft) message = "已复位；这是表现验证，不发调查、收集或结局命令。";
            ApplyWater();
        }

        /// <summary>原主镜头和固定近景之间切换，不提供自由拖拽转头。</summary>
        public void SetClose(bool value)
        {
            close = value;
            if (view == null) view = GetComponent<Camera>();
            view.transform.SetPositionAndRotation(close ? ClosePosition : RoomPosition,
                Quaternion.Euler(close ? CloseEuler : RoomEuler));
            view.fieldOfView = close ? CloseFov : RoomFov;
        }

        private void Update()
        {
            if (view == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.hKey.wasPressedThisFrame) ShowHud = !ShowHud;
                if (keyboard.rKey.wasPressedThisFrame) ResetProbe();
                if (keyboard.fKey.wasPressedThisFrame) SetClose(!close);
                if (keyboard.spaceKey.wasPressedThisFrame) TogglePause();
                if (Kind == AtelierProbeKind.Draft)
                {
                    if (keyboard.digit0Key.wasPressedThisFrame) SetStage(0);
                    if (keyboard.digit1Key.wasPressedThisFrame) SetStage(1);
                    if (keyboard.digit2Key.wasPressedThisFrame) SetStage(2);
                    if (keyboard.digit3Key.wasPressedThisFrame) SetStage(3);
                    if (keyboard.digit4Key.wasPressedThisFrame) SetStage(4);
                }
            }
            float delta = paused ? 0 : Time.deltaTime;
            clock += delta;
            openingAge += delta;
            GhostAmount = openingAge < .32f ? Mathf.Sin(openingAge / .32f * Mathf.PI) * .42f : 0;
            LeftDraft = Mathf.MoveTowards(LeftDraft, targetLeft, delta / .95f);
            WindowDraft = Mathf.MoveTowards(WindowDraft, targetWindow, delta / .95f);
            if (Kind == AtelierProbeKind.Wash)
            {
                washAge += delta;
                if (washAge < 5 && SupportedBrush != null)
                {
                    float contact = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .55f, washAge)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.1f, 2.8f, washAge)));
                    var down = Quaternion.FromToRotation(Vector3.right, new Vector3(-.80f, -.57f, .20f).normalized);
                    SupportedBrush.localRotation = Quaternion.Slerp(brushBase, down, contact);
                    if (BrushSurface != null)
                    {
                        float tip = BrushSurface.transform.TransformPoint(new Vector3(BrushSurface.sharedMesh.bounds.min.x, 0, 0)).y;
                        if (tip < Water.bounds.center.y + .008f) pigment = Mathf.MoveTowards(pigment, 1, delta / 1.35f);
                    }
                }
                ApplyWater();
            }
            HandlePointer();
        }

        private void ApplyWater()
        {
            if (Kind != AtelierProbeKind.Wash || Water == null) return;
            var block = waterProperties ?? (waterProperties = new MaterialPropertyBlock());
            Water.GetPropertyBlock(block);
            block.SetFloat("_ProbeProgress", pigment);
            block.SetFloat("_ProbeTime", clock);
            block.SetFloat("_ProbeStrength", WaterStrength);
            Water.SetPropertyBlock(block);
        }

        private void ApplySurface()
        {
            if (ToolSurfaces == null) return;
            foreach (var surface in ToolSurfaces)
            {
                if (surface == null) continue;
                var block = new MaterialPropertyBlock();
                block.SetVector("_ProbeOrigin", SurfaceOrigin);
                block.SetVector("_ProbeScale", new Vector4(SurfaceSize.x, SurfaceSize.y, 0, 0));
                block.SetFloat("_ProbeRadius", Kind == AtelierProbeKind.Roof ? (roofStep >= 2 ? .13f : .055f) : .048f);
                block.SetFloat("_ProbeEraseCount", eraseCount);
                block.SetFloat("_ProbeSpreadCount", spreadCount);
                block.SetVectorArray("_ProbeErase", erase);
                block.SetVectorArray("_ProbeSpread", spread);
                surface.SetPropertyBlock(block);
            }
        }

        private void HandlePointer()
        {
            var mouse = Mouse.current;
            if (mouse == null || paused) return;
            if (!mouse.leftButton.isPressed) pointerHeld = false;
            Vector2 screen = mouse.position.ReadValue();
            float panelHeight = 180 * Mathf.Clamp(Screen.width / 1280f, .7f, 1.5f);
            if (ShowHud && screen.y < panelHeight) return;
            if (Kind == AtelierProbeKind.Wash)
            {
                if (!mouse.leftButton.wasPressedThisFrame || Water == null) return;
                var plane = new Plane(Vector3.up, Water.bounds.center);
                Ray ray = view.ScreenPointToRay(screen);
                if (plane.Raycast(ray, out float distance) && Vector3.Distance(ray.GetPoint(distance), Water.bounds.center) < .14f) BeginWash();
                return;
            }
            if (Kind != AtelierProbeKind.Palette && Kind != AtelierProbeKind.Roof) return;
            var contactPlane = new Plane(Vector3.up, SurfaceOrigin);
            Ray pointer = view.ScreenPointToRay(screen);
            if (!contactPlane.Raycast(pointer, out float length)) return;
            Vector3 point = pointer.GetPoint(length);
            var uv = new Vector2((point.x - SurfaceOrigin.x) / SurfaceSize.x + .5f, (point.z - SurfaceOrigin.z) / SurfaceSize.y + .5f);
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) { if (Knife != null) Knife.gameObject.SetActive(false); pointerHeld = false; return; }
            if (Knife != null)
            {
                Knife.gameObject.SetActive(true);
                Knife.position = point + (Kind == AtelierProbeKind.Roof ? Vector3.down : Vector3.up) * .014f;
                Knife.rotation = Quaternion.Euler(Kind == AtelierProbeKind.Roof ? 180 : 0, -20, knifeFace ? 8 : 68);
            }
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (Kind == AtelierProbeKind.Roof && knifeFace) { message = "刀面只推色；屋面揭底请用刀刃。"; return; }
                previousPoint = uv;
                pointerHeld = true;
                AddContact(uv, uv, knifeFace);
            }
            else if (pointerHeld && mouse.leftButton.isPressed && Vector2.Distance(previousPoint, uv) > .012f)
            {
                AddContact(previousPoint, uv, knifeFace);
                previousPoint = uv;
            }
        }

        private void TogglePause()
        {
            paused = !paused;
            pointerHeld = false;
            if (Sky != null) Sky.SetPaused(paused);
        }

        private void OnGUI()
        {
            if (!ShowHud) return;
            float scale = Mathf.Clamp(Screen.width / 1280f, .7f, 1.5f);
            Matrix4x4 matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            var label = new GUIStyle(GUI.skin.label) { font = ChineseFont, fontSize = 15, wordWrap = true };
            var button = new GUIStyle(GUI.skin.button) { font = ChineseFont, fontSize = 15 };
            var title = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold };
            GUI.Box(new Rect(8, height - 176, width - 16, 168), GUIContent.none);
            GUI.Label(new Rect(20, height - 170, width - 40, 26), "TA验证 · " + Titles[(int)Kind] + "　　仅演示表现，不写游戏事实", title);
            float x = 20, y = height - 140;
            bool Button(string text, float size = 120)
            {
                bool pressed = GUI.Button(new Rect(x, y, size, 29), text, button);
                x += size + 6;
                return pressed;
            }
            if (Kind == AtelierProbeKind.Draft)
            {
                if (Button("原场景对照")) SetStage(0);
                if (Button("阶段一")) SetStage(1);
                if (Button("阶段二")) SetStage(2);
                if (Button("阶段三／四")) SetStage(3);
                if (Button("短重影预演")) Opening();
                GUI.Label(new Rect(x, y, 75, 26), "底稿强度", label);
                DraftDensity = GUI.HorizontalSlider(new Rect(x + 78, y + 10, 120, 20), DraftDensity, .5f, 1);
                x += 210;
            }
            else if (Kind == AtelierProbeKind.Wash)
            {
                if (Button("接触洗笔")) BeginWash();
                GUI.Label(new Rect(x, y, 92, 26), "色带可读性", label);
                WaterStrength = GUI.HorizontalSlider(new Rect(x + 92, y + 10, 130, 20), WaterStrength, .3f, 1.8f);
                x += 240;
            }
            else if (Kind == AtelierProbeKind.Mirror)
            {
                GUI.Label(new Rect(x, y, 145, 26), "转镜角度 " + mirrorAngle.ToString("F0") + "°", label);
                SetMirrorAngle(GUI.HorizontalSlider(new Rect(x + 145, y + 10, 230, 20), mirrorAngle, -MirrorAngleLimit, MirrorAngleLimit));
                x += 395;
            }
            else
            {
                if (Button(knifeFace ? "刀面：推色" : "刀刃：揭底", 135)) { knifeFace = !knifeFace; pointerHeld = false; }
                if (Kind == AtelierProbeKind.Roof && Button(roofStep == 0 ? "第一次：小孔" : "第二次：扩大", 150)) RoofStep();
            }
            if (x < width - 360)
            {
                x = width - 360;
                if (Button(close ? "返回主镜头 F" : "固定近景 F", 130)) SetClose(!close);
                if (Button(paused ? "继续 空格" : "暂停 空格", 112)) TogglePause();
                if (Button("复位 R", 88)) ResetProbe();
            }
            GUI.Label(new Rect(20, height - 105, width - 40, 46), message + "　H隐藏面板；F切镜头；R复位；空格暂停。", label);
            y = height - 49;
            x = 20;
            for (int i = 0; i < ShortNames.Length; i++)
                if (Button((i + 1) + " " + ShortNames[i], Mathf.Min(170, (width - 65) / 5))) OpenProbe(i);
            GUI.matrix = matrix;
        }

        private void OpenProbe(int index)
        {
            pointerHeld = false;
            string path = "Assets/_Game/Content/Scenes/Experiments/TechnicalArt/AtelierProbes/" + SceneNames[index] + ".unity";
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            #else
            SceneManager.LoadScene(SceneNames[index]);
            #endif
        }

        private void OnDisable()
        {
            // 先恢复场景引用，再释放自己创建的材质；临时管线覆盖也总是归还。
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            foreach (var material in owned) if (material != null) Destroy(material);
            originals.Clear();
            owned.Clear();
            if (changedPipeline) QualitySettings.renderPipeline = previousPipeline;
            changedPipeline = false;
            if (runtimePipeline != null) { Destroy(runtimePipeline); runtimePipeline = null; }
        }
    }
}
