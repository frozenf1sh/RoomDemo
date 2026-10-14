// =============================================================================
// 文件：AtelierCollectionView.cs
// 职责：呈现画室藏品簿的分页、单件查看和返回笔记流程。
// 边界：藏品获得条件只读取 GameSession 的世界碎片；本组件只保存页码和当前查看项等临时 UI 状态。
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Unity
{
    /// <summary>可手调藏品 Prefab 的运行时控制器，每页固定展示十二格。</summary>
    public sealed class AtelierCollectionView : MonoBehaviour
    {
        private const int ItemsPerPage = 12;

        [SerializeField] private Button notebookEntryButton;
        [SerializeField] private Button returnToNotebookButton;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Transform leftSlots;
        [SerializeField] private Transform rightSlots;
        [SerializeField] private Button collectibleCardTemplate;
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image dimLayer;
        [SerializeField] private Button detailBackButton;
        [SerializeField] private Image detailImage;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailDescription;
        [SerializeField] private Sprite brushArtwork;

        private readonly List<Button> spawnedCards = new List<Button>();
        private GameSession session;
        private NotebookView notebook;
        private List<AtelierCollectionDefinition> owned = new List<AtelierCollectionDefinition>();
        private int pageIndex;

        /// <summary>藏品簿是否正覆盖在笔记本上方。</summary>
        public bool IsOpen => gameObject.activeSelf;

        /// <summary>由编辑器生成器一次性注入 Prefab 内部节点；布局可在 Prefab Mode 中继续手调。</summary>
        public void Configure(
            Button returnButton, Button previousButton, Button nextButton,
            Transform leftPageSlots, Transform rightPageSlots, Button cardTemplate,
            GameObject details, Image darken, Button detailsBack, Image artwork,
            Text name, Text description, Sprite brushSprite)
        {
            returnToNotebookButton = returnButton;
            previousPageButton = previousButton;
            nextPageButton = nextButton;
            leftSlots = leftPageSlots;
            rightSlots = rightPageSlots;
            collectibleCardTemplate = cardTemplate;
            detailPanel = details;
            dimLayer = darken;
            detailBackButton = detailsBack;
            detailImage = artwork;
            detailName = name;
            detailDescription = description;
            brushArtwork = brushSprite;
        }

        /// <summary>注入笔记侧边入口；入口属于笔记 Prefab，藏品页自身仍可独立手调。</summary>
        public void ConfigureEntryButton(Button button)
        {
            notebookEntryButton = button;
        }

        /// <summary>绑定会话并恢复为关闭状态；重开时不保留上一局的页码或详情。</summary>
        public void Bind(GameSession game, NotebookView notebookView)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game;
            notebook = notebookView;
            session.EventRaised += OnEvent;
            pageIndex = 0;

            // 入口位于笔记本 Prefab 而不属于藏品 Prefab；旧场景未保存跨 Prefab 引用时按稳定节点路径恢复。
            if (notebookEntryButton == null && notebook != null)
                notebookEntryButton = notebook.transform.Find("笔记纸面/藏品入口便签")?.GetComponent<Button>();

            if (notebookEntryButton == null || returnToNotebookButton == null || previousPageButton == null ||
                nextPageButton == null || collectibleCardTemplate == null || detailPanel == null || dimLayer == null ||
                detailBackButton == null || detailImage == null || detailName == null || detailDescription == null)
            {
                Debug.LogError("[藏品] 藏品簿 Prefab 缺少 UI 引用；已跳过藏品簿绑定，不影响画室玩法。");
                gameObject.SetActive(false);
                return;
            }

            notebookEntryButton.onClick.RemoveAllListeners();
            notebookEntryButton.onClick.AddListener(OpenFromNotebook);
            returnToNotebookButton.onClick.RemoveAllListeners();
            returnToNotebookButton.onClick.AddListener(CloseToNotebook);
            previousPageButton.onClick.RemoveAllListeners();
            previousPageButton.onClick.AddListener(() => ChangePage(-1));
            nextPageButton.onClick.RemoveAllListeners();
            nextPageButton.onClick.AddListener(() => ChangePage(1));
            detailBackButton.onClick.RemoveAllListeners();
            detailBackButton.onClick.AddListener(CloseDetail);

            detailPanel.SetActive(false);
            dimLayer.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (session != null) session.EventRaised -= OnEvent;
        }

        private void OnEvent(DomainEvent value)
        {
            // 任何新增碎片都可能满足未来藏品定义；只在藏品簿打开时刷新可见页面。
            if (value is ClueDiscovered && IsOpen) RenderPage();
        }

        /// <summary>由笔记侧边便签打开藏品簿；笔记本保持展开，返回时不额外翻页。</summary>
        public void OpenFromNotebook()
        {
            if (session == null || notebook == null || !notebook.IsOpen) return;
            pageIndex = 0;
            gameObject.SetActive(true);
            RenderPage();
        }

        /// <summary>回到仍保持打开状态的笔记本，不更改世界事实。</summary>
        public void CloseToNotebook()
        {
            CloseDetail();
            gameObject.SetActive(false);
        }

        /// <summary>供画室输入在 Esc 时优先关闭藏品页。</summary>
        public void CloseFromEscape()
        {
            if (!IsOpen) return;
            if (detailPanel.activeSelf) CloseDetail();
            else CloseToNotebook();
        }

        private void ChangePage(int delta)
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(owned.Count / (float)ItemsPerPage));
            pageIndex = Mathf.Clamp(pageIndex + delta, 0, pageCount - 1);
            RenderPage();
        }

        private void RenderPage()
        {
            owned = AtelierCollectionCatalog.Definitions.Where(x => session.State.Knows(x.RequiredClue)).ToList();
            foreach (var card in spawnedCards) Destroy(card.gameObject);
            spawnedCards.Clear();

            int start = pageIndex * ItemsPerPage;
            int end = Mathf.Min(start + ItemsPerPage, owned.Count);
            for (int index = start; index < end; index++)
            {
                var definition = owned[index];
                var parent = index - start < 6 ? leftSlots : rightSlots;
                var card = Instantiate(collectibleCardTemplate, parent);
                card.gameObject.SetActive(true);
                var image = card.transform.Find("藏品图")?.GetComponent<Image>();
                if (image != null) image.sprite = ArtworkFor(definition);
                var label = card.GetComponentInChildren<Text>();
                if (label != null) label.text = definition.DisplayName;
                card.onClick.RemoveAllListeners();
                card.onClick.AddListener(() => OpenDetail(definition));
                spawnedCards.Add(card);
            }

            int pageCount = Mathf.Max(1, Mathf.CeilToInt(owned.Count / (float)ItemsPerPage));
            previousPageButton.gameObject.SetActive(pageIndex > 0);
            nextPageButton.gameObject.SetActive(pageIndex < pageCount - 1);
        }

        private void OpenDetail(AtelierCollectionDefinition definition)
        {
            detailImage.sprite = ArtworkFor(definition);
            detailName.text = definition.DisplayName;
            detailDescription.text = definition.Description;
            dimLayer.gameObject.SetActive(true);
            detailPanel.SetActive(true);
        }

        private void CloseDetail()
        {
            detailPanel.SetActive(false);
            dimLayer.gameObject.SetActive(false);
        }

        private Sprite ArtworkFor(AtelierCollectionDefinition definition)
        {
            return definition.Id == "atelier.collection.brush" ? brushArtwork : null;
        }
    }
}
