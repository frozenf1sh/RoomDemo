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
        private GameSession session;
        private ObjectId? selectedObject;
        private ConceptId? selectedConcept;
        public bool IsOpen => panel.activeSelf;
        public void Configure(GameObject body, Transform objects, Transform concepts, Button template, Button interpret, Text selection, Text feedback)
        { panel = body; objectList = objects; conceptList = concepts; rowTemplate = template; interpretButton = interpret; selectionText = selection; feedbackText = feedback; }
        public void ConfigureControls(Button open, Button close) { openButton = open; closeButton = close; }
        public void Bind(GameSession game)
        {
            session = game; session.EventRaised += OnEvent;
            interpretButton.onClick.AddListener(InterpretSelected);
            openButton.onClick.AddListener(Toggle); closeButton.onClick.AddListener(() => SetOpen(false)); Render();
        }
        public void SetOpen(bool open) { panel.SetActive(open); }
        public void Toggle() { SetOpen(!IsOpen); }
        public void SelectObject(ObjectId id) { selectedObject = id; RenderSelection(); }
        public void SelectConcept(ConceptId id) { selectedConcept = id; RenderSelection(); }
        public void InterpretSelected()
        { if (selectedConcept.HasValue && selectedObject.HasValue) session.Interpret(selectedConcept.Value, selectedObject.Value); }
        private void OnEvent(DomainEvent e)
        {
            if (e is ObjectDiscovered || e is ConceptDiscovered) Render();
            if (e is ObjectInspected inspected) feedbackText.text = inspected.Description;
            if (e is InspectionFailed inspectFailed) feedbackText.text = inspectFailed.Reason;
            if (e is InterpretationFailed failed) feedbackText.text = failed.Reason;
            if (e is InterpretationSucceeded success) feedbackText.text = $"{success.Concept} becomes a meaning of {success.Target}. Watch the world respond.";
            if (e is FactAdded fact && fact.Fact == DemoRules.WindEntering) feedbackText.text = "Wind enters on its own. The grown plant and distant window now coexist.";
        }
        private void Render()
        {
            ClearRows(objectList); ClearRows(conceptList);
            foreach (var definition in session.Rules.Objects.Where(x => session.State.Knows(x.Id)))
            { var id = definition.Id; AddRow(objectList, "Object_" + id.Value, definition.Label, () => SelectObject(id)); }
            foreach (var definition in session.Rules.Concepts.Where(x => session.State.Knows(x.Id)))
            { var id = definition.Id; AddRow(conceptList, "Concept_" + id.Value, definition.Label, () => SelectConcept(id)); }
            RenderSelection();
        }
        private static void ClearRows(Transform list)
        { foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
        private void AddRow(Transform list, string name, string label, UnityEngine.Events.UnityAction select)
        { var row = Instantiate(rowTemplate, list); row.name = name; row.gameObject.SetActive(true); row.GetComponentInChildren<Text>().text = label; row.onClick.AddListener(select); }
        private void RenderSelection()
        {
            string concept = selectedConcept.HasValue ? session.Rules.Concepts.First(x => x.Id == selectedConcept.Value).Label : "Select a concept";
            string target = selectedObject.HasValue ? session.Rules.Objects.First(x => x.Id == selectedObject.Value).Label : "Select an object";
            selectionText.text = concept + "  →  " + target;
            interpretButton.interactable = selectedConcept.HasValue && selectedObject.HasValue;
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.nKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame) Toggle();
            if (keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
