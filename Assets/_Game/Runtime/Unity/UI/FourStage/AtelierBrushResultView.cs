// 文件：AtelierBrushResultView.cs
// 职责：展示真实笔刷名称、原图与已使用来源。边界：不装备、不授予、不自动进入画布。
using System;
using UnityEngine;
using UnityEngine.UI;
namespace Game.Unity.UI.FourStage
{
    /// <summary>形成页的最小交付，场景侧只在真实已知笔刷时调用Show。</summary>
    public sealed class AtelierBrushResultView : MonoBehaviour
    {
        public GameObject Panel;
        public Text Title, Sources;
        public RawImage Preview;
        public AspectRatioFitter Aspect;
        public Button Back, Close;
        /// <summary>仅请求关闭笔记，调用者负责绑定。</summary>
        public event Action CloseNotebookRequested;
        private void OnEnable()
        {
            Back.onClick.AddListener(Hide);
            Close.onClick.AddListener(CloseNotebook);
        }
        private void OnDisable()
        {
            Back.onClick.RemoveListener(Hide);
            Close.onClick.RemoveListener(CloseNotebook);
        }
        /// <summary>传入已经获得的笔刷原图和实际来源；纹理不归本页所有。</summary>
        public void Show(string name, Texture texture, string sources)
        {
            Title.text = "形成了" + name;
            Sources.text = sources + "\n收起笔记后，亲点画架开始绘画。";
            Preview.texture = texture;
            Preview.enabled = texture != null;
            if (texture != null) Aspect.aspectRatio = texture.width / (float)texture.height;
            Panel.SetActive(true);
        }
        /// <summary>返回组合不清除右页临时选择。</summary>
        public void Hide() => Panel.SetActive(false);
        private void CloseNotebook()
        {
            Hide();
            // 只有关闭意图，绝不代替玩家点画架或落第一笔。
            CloseNotebookRequested?.Invoke();
        }
    }
}
