// =============================================================================
// 文件：AtelierMenuReferences.cs
// 职责：保存可手调菜单 Prefab 内部控件与画室玩法入口之间的绑定。
// 边界：仅传递 Unity UI 引用，不读取或改写领域状态。
// =============================================================================
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity.ArtRebuild
{
    /// <summary>让场景中的 <see cref="AtelierGameplay"/> 从菜单 Prefab 恢复 UI 引用。</summary>
    public sealed class AtelierMenuReferences : MonoBehaviour
    {
        [SerializeField] private GameObject contextPanel;
        [SerializeField] private Text observationText;
        [SerializeField] private Button contextAction;
        [SerializeField] private Button returnButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        /// <summary>由场景生成器在首次迁移时写入 Prefab 内部控件。</summary>
        public void Configure(GameObject context, Text observation, Button action, Button returnToStudio,
            GameObject pause, Button resume, Button restart, Button quit)
        {
            contextPanel = context;
            observationText = observation;
            contextAction = action;
            returnButton = returnToStudio;
            pausePanel = pause;
            resumeButton = resume;
            restartButton = restart;
            quitButton = quit;
        }

        /// <summary>将当前 Prefab 实例中的控件绑定给玩法组件。</summary>
        public void Apply(AtelierGameplay game)
        {
            game.ContextPanel = contextPanel;
            game.ObservationText = observationText;
            game.ContextAction = contextAction;
            game.ReturnButton = returnButton;
            game.PausePanel = pausePanel;
            game.ResumeButton = resumeButton;
            game.RestartButton = restartButton;
            game.QuitButton = quitButton;
        }
    }
}
