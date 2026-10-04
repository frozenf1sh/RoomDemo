// =============================================================================
// 文件：VisualCaptureSetup.cs
// 职责：为原生试玩包绑定这轮三个美术片段的可选命令行录像入口。
// 边界：只增加默认休眠的录制组件，不增加玩家界面或改变领域内容。
// =============================================================================
using System.Linq;
using Game.Unity;
using UnityEngine;
namespace Game.Editor.VisualSprint
{
    /// <summary>显式安装录像导演；启动参数以外的正常运行没有开销。</summary>
    public static class VisualCaptureSetup
    {
        /// <summary>增量绑定现有会话、检视、相机与监听器，不覆盖人工美术或启用正常运行录制。</summary>
        public static void Apply()
        {
            var root = PresentationArtPass.Room();
            var node = PresentationArtPass.Ensure(root.Find("Presentation/Debug"), "VisualSprintCapture");
            var recorder = node.GetComponent<VisualSprintCapture>(); if (recorder == null) recorder = node.gameObject.AddComponent<VisualSprintCapture>();
            var camera = root.GetComponentInChildren<Camera>(true);
            var audio = camera.GetComponent<VisualSprintAudioCapture>();
            if (audio == null) audio = camera.gameObject.AddComponent<VisualSprintAudioCapture>();
            // 正常游玩不把声音送进临时缓存；显式录像启动参数才启用。
            audio.enabled = false;
            recorder.Configure(root.GetComponentInChildren<GameBootstrap>(true), root.GetComponentInChildren<NotebookView>(true),
                root.GetComponentInChildren<InspectionController>(true), root.GetComponentInChildren<DayTransitionController>(true),
                root.GetComponentsInChildren<InspectableObject>(true).Single(x => x.Entity.Id.Value == "book"),
                root.GetComponentInChildren<PlantPresenter>(true), camera, audio);
            PresentationArtPass.Save(root);
        }
        // 这里只绑定真实组件；录像保存位置由明确命令行参数决定。
    }
}
