// 文件：AtelierGameplayEvidence.cs
// 职责：显式取证模式下保存独立构建画面和当前状态，不自动执行玩法输入。
// 边界：普通玩家模式无取证行为；记录只读会话，绝不授予词条或添加事实。
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Game.Unity.ArtRebuild
{
    /// <summary>开发者通过 F9 保存当前真实屏幕，记录与画面同时发生。</summary>
    public sealed class AtelierGameplayEvidence : MonoBehaviour
    {
        private string folder;
        private int index, errors;
        private float nextInputProbe;
        private bool eventReplay;
        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            int key = Array.IndexOf(args, "--atelier-gameplay-evidence");
            if (key < 0 || key + 1 >= args.Length) return;
            folder = Path.GetFullPath(args[key + 1]); Directory.CreateDirectory(folder);
            UnityEngine.Application.runInBackground = true;
            UnityEngine.Application.logMessageReceived += Log;
            StartCoroutine(Opening());
        }
        private IEnumerator Opening()
        {
            yield return new WaitForSecondsRealtime(5);
            Capture();
        }
        private void Update()
        {
            if (folder != null && Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                Screen.SetResolution(1024,640,FullScreenMode.Windowed);
            if (folder != null && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) Capture();
            if (folder != null && Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
                StartCoroutine(ReplayWaterInput());
            if (folder == null || Time.unscaledTime < nextInputProbe) return;
            nextInputProbe = Time.unscaledTime + .2f;
            // 取证模式给真实纸面拖动附加只读记录器，区分输入丢失与配方逻辑问题。
            foreach (var drag in FindObjectsByType<NotebookClueDrag>())
                if (drag.GetComponent<AtelierPointerEvidence>() == null)
                    drag.gameObject.AddComponent<AtelierPointerEvidence>().Folder = folder;
        }
        private IEnumerator ReplayWaterInput()
        {
            var game = FindAnyObjectByType<AtelierGameplay>();
            if (game == null || game.Session == null || !game.Notebook.IsOpen) yield break;
            ClueId[] inputs;
            if (!game.Session.State.Knows(StudioRules.Flow))
                inputs = new[] { AtelierRules.Observation("still"), AtelierRules.Observation("spread") };
            else if (game.IsPaintingFocused && !game.Session.State.HasFact(StudioRules.PictureFlow))
                inputs = new[] { new ClueId(StudioRules.PictureWater.Value), new ClueId(StudioRules.Flow.Value) };
            else inputs = new[] { new ClueId(StudioRules.Cup.Value), new ClueId(StudioRules.Flow.Value) };
            // 只回放已经亲自收集的输入。此入口不授予碎片，不直接调用配方或更改事实。
            if (inputs.Any(id => !game.Session.State.Knows(id))) yield break;
            var canvas = game.Notebook.GetComponentInChildren<NotebookCanvasView>(true);
            var area = canvas.GetComponentsInChildren<RectTransform>(true).Single(x => x.name == "有限解释区");
            eventReplay = true;
            for (int i = 0; i < inputs.Length; i++)
            {
                var row = FindObjectsByType<NotebookClueDrag>().FirstOrDefault(x => !x.name.StartsWith("CanvasClue_") && x.name.EndsWith("_" + inputs[i].Value));
                if (row == null) yield break;
                var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                data.position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)row.transform).position);
                ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.beginDragHandler);
                yield return null;
                Vector2 destination = RectTransformUtility.WorldToScreenPoint(null, area.TransformPoint(new Vector3(i == 0 ? -95 : 95, 30, 0)));
                data.position = destination;
                ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.dragHandler);
                yield return null;
                ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.endDragHandler);
                ExecuteEvents.Execute(row.gameObject, data, ExecuteEvents.pointerUpHandler);
            }
            // 保留摆好的真实纸面供截图；提交仍由操作者点击“解释”。
            File.AppendAllText(Path.Combine(folder,"纸面输入记录.txt"),"以上输入由 F10 显式触发 Unity UI 事件回放，不能证明原生拖动送达。\n");
        }
        private void Log(string text,string stack,LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        private void Capture()
        {
            var game = FindAnyObjectByType<AtelierGameplay>();
            if (game == null || game.Session == null) return;
            string name = (++index).ToString("D2") + "_独立构建";
            // ScreenCapture 包含真实 Overlay 中文界面，不以相机离屏渲染替代 UI 验收。
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
            string state = "时间=" + Time.time.ToString("F3") + "\n分辨率=" + Screen.width + "×" + Screen.height + "\n错误累计=" + errors +
                "\n画作近看=" + game.IsPaintingFocused + "\n笔记打开=" + game.Notebook.IsOpen +
                "\n使用过UI事件回放=" + eventReplay +
                "\n事实=" + string.Join("、",game.Session.State.Facts.Select(x=>x.Value)) +
                "\n已知=" + string.Join("、",game.Session.State.KnownClues.Select(x=>x.Value));
            File.WriteAllText(Path.Combine(folder,name+".txt"),state);
        }
        private void OnDestroy()
        {
            if (folder != null) UnityEngine.Application.logMessageReceived -= Log;
        }
    }

    /// <summary>只记录实际 UI 指针事件，不模拟输入或替玩家提交。</summary>
    public sealed class AtelierPointerEvidence : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler
    {
        public string Folder;
        /// <summary>记录实际按下坐标与当前帧耗时。</summary>
        public void OnPointerDown(PointerEventData data) => Record("按下", data);
        /// <summary>记录 UI 接收到的开始拖动事件。</summary>
        public void OnBeginDrag(PointerEventData data) => Record("开始拖动", data);
        /// <summary>记录释放坐标，不改变原拖动适配器的行为。</summary>
        public void OnEndDrag(PointerEventData data) => Record("结束拖动", data);
        private void Record(string action, PointerEventData data)
        {
            File.AppendAllText(Path.Combine(Folder,"纸面输入记录.txt"),
                Time.unscaledTime.ToString("F3") + " " + action + " " + name + " " + data.position +
                " 帧毫秒=" + (Time.unscaledDeltaTime * 1000).ToString("F1") + "\n");
        }
    }
}
