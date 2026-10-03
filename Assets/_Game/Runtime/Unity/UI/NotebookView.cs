using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    public sealed class NotebookView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform objectList;
        [SerializeField] private Transform conceptList;
        [SerializeField] private Button rowTemplate;
        [SerializeField] private Button interpretButton;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text selectionText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private Text completedText;
        private readonly ClueId?[] selected = new ClueId?[3];
        private GameSession session;
        private float closeAt = -1;
        public bool IsOpen => panel.activeSelf;
        public IReadOnlyList<ClueId> SelectedClues => selected.Where(x => x.HasValue).Select(x => x.Value).ToArray();
        public void Configure(GameObject body, Transform objects, Transform concepts, Button template, Button interpret, Text selection, Text feedback)
        { panel = body; objectList = objects; conceptList = concepts; rowTemplate = template; interpretButton = interpret; selectionText = selection; feedbackText = feedback; }
        public void ConfigureSlots(Button[] slots, Text history) { slotButtons = slots; completedText = history; }
        public void ConfigureControls(Button open, Button close) { openButton = open; closeButton = close; }
        public void Bind(GameSession game)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game; session.EventRaised += OnEvent; closeAt = -1; ClearSelection(); SetOpen(false);
            interpretButton.onClick.RemoveAllListeners(); interpretButton.onClick.AddListener(InterpretSelected);
            openButton.onClick.RemoveAllListeners(); openButton.onClick.AddListener(Toggle);
            closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(() => SetOpen(false));
            for (int i = 0; i < slotButtons.Length; i++) { int index = i; slotButtons[i].onClick.RemoveAllListeners(); slotButtons[i].onClick.AddListener(() => { selected[index] = null; RenderSelection(); }); }
            feedbackText.text = "Click collected clues to fill up to three slots. Click a filled slot to clear it."; Render();
        }
        public void SetOpen(bool open) { panel.SetActive(open); }
        public void Toggle() { SetOpen(!IsOpen); }
        public void SelectObject(ObjectId id) => SelectClue(new ClueId(id.Value));
        public void SelectConcept(ConceptId id) => SelectClue(new ClueId(id.Value));
        public void SelectClue(ClueId clue)
        {
            if (!session.State.Knows(clue)) return;
            closeAt = -1;
            int existing = System.Array.FindIndex(selected, x => x.HasValue && x.Value == clue);
            if (existing >= 0) selected[existing] = null;
            else { int empty = System.Array.FindIndex(selected, x => !x.HasValue); selected[empty >= 0 ? empty : 2] = clue; }
            RenderSelection();
        }
        public void InterpretSelected() { if (SelectedClues.Count >= 2) session.Combine(SelectedClues); }
        private void ClearSelection() { for (int i = 0; i < selected.Length; i++) selected[i] = null; }
        private string Label(ClueId id) => session.Rules.Clues.Single(x => x.Id == id).DisplayName;
        private void OnEvent(DomainEvent e)
        {
            if (e is ClueDiscovered) Render();
            if (e is ObjectInspected inspected) feedbackText.text = inspected.Description;
            if (e is InspectionFailed inspectFailed) feedbackText.text = inspectFailed.Reason;
            if (e is CombinationFailed failed) { feedbackText.text = failed.Reason; selectionText.color = new Color(.62f, .25f, .21f); closeAt = -1; }
            if (e is CombinationSucceeded)
            {
                feedbackText.text = "An interpretation takes hold. Watch the room remember it.";
                selectionText.text = string.Join("  ─  ", SelectedClues.Select(Label)); selectionText.color = new Color(.75f, .43f, .10f);
                closeAt = Time.unscaledTime + .65f; RenderHistory();
            }
        }
        private void Render()
        {
            ClearRows(objectList); ClearRows(conceptList);
            foreach (var definition in session.Rules.Clues.Where(x => session.State.Knows(x.Id)))
            {
                var id = definition.Id; bool objectClue = definition.Category == ClueCategory.Object;
                AddRow(objectClue ? objectList : conceptList, (objectClue ? "Object_" : "Concept_") + id.Value, definition.DisplayName, () => SelectClue(id));
            }
            RenderSelection(); RenderHistory();
        }
        private void RenderHistory()
        {
            completedText.text = "ESTABLISHED INTERPRETATIONS\n" + string.Join("\n", session.Rules.Recipes.Where(x => session.State.HasCompleted(x.Id)).Select(x => string.Join(" ─ ", x.Inputs.Select(Label))));
        }
        private static void ClearRows(Transform list) { foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        private void AddRow(Transform list, string name, string label, UnityEngine.Events.UnityAction select)
        { var row = Instantiate(rowTemplate, list); row.name = name; row.gameObject.SetActive(true); row.GetComponentInChildren<Text>().text = label; row.onClick.AddListener(select); }
        private void RenderSelection()
        {
            selectionText.text = "COMBINATION  /  2–3 clues · unordered"; selectionText.color = new Color(.1f, .15f, .19f);
            for (int i = 0; i < slotButtons.Length; i++) slotButtons[i].GetComponentInChildren<Text>().text = selected[i].HasValue ? Label(selected[i].Value) + "  ×" : i == 2 ? "+ optional clue" : "+ choose a clue";
            interpretButton.interactable = SelectedClues.Count >= 2;
        }
        private void Update()
        {
            if (closeAt > 0 && Time.unscaledTime >= closeAt) { closeAt = -1; SetOpen(false); ClearSelection(); RenderSelection(); }
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.nKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) Toggle();
            if (keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
