using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application;
using Game.Domain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    public sealed class DebugView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text stateText;
        [SerializeField] private Text eventText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text observationText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button grantButton;
        [SerializeField] private Button addFactButton;
        [SerializeField] private InputField factInput;
        [SerializeField] private Button[] presetButtons;
        private GameSession session;
        private readonly Queue<string> recent = new Queue<string>();
        private bool showHint;
        public string Objective => objectiveText.text;
        public void Configure(GameObject body, Text state, Text events, Text objective) { panel = body; stateText = state; eventText = events; objectiveText = objective; }
        public void ConfigureTools(Text observations, Button restart, Button reset, Button grant, Button add, InputField input, Button[] presets)
        { observationText = observations; restartButton = restart; resetButton = reset; grantButton = grant; addFactButton = add; factInput = input; presetButtons = presets; }
        public void Bind(GameSession game, Action restart, Action<DebugPreset> jump, Action<FactId> addFact)
        {
            if (session != null) session.EventRaised -= OnEvent;
            session = game; session.EventRaised += OnEvent; recent.Clear(); eventText.text = ""; showHint = false;
            observationText.text = "Look around. Collect what you notice, then give those meanings to the room. [H] offers a hint.";
            Wire(restartButton, () => restart()); Wire(resetButton, () => restart()); Wire(grantButton, () => session.DebugGrantAllClues());
            Wire(addFactButton, () => { try { addFact(new FactId(factInput.text.Trim())); } catch (ArgumentException e) { eventText.text = e.Message; } });
            for (int i = 0; i < presetButtons.Length; i++) { var preset = DebugPresets.All[i]; Wire(presetButtons[i], () => jump(preset)); }
            Render();
        }
        private static void Wire(Button button, UnityEngine.Events.UnityAction action) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
        private void OnEvent(DomainEvent e)
        {
            recent.Enqueue(e.ToString()); while (recent.Count > 5) recent.Dequeue(); eventText.text = string.Join("\n", recent);
            if (e is ObjectInspected inspected) observationText.text = inspected.Description;
            if (e is InspectionFailed failed) observationText.text = failed.Reason;
            if (e is CombinationSucceeded) observationText.text = "Your interpretation is part of the room now. Look for what changed.";
            if (e is FactAdded fact && fact.Fact == DemoRules.WindEntering) observationText.text = "You never asked for wind. The grown plant and distant window have begun to answer each other. A paper slips free.";
            if (e is FactAdded end && end.Fact == DemoRules.Complete) observationText.text = "The sea has reached meanings you established earlier. The plant and window changed without another interpretation.";
            if ((UnityEngine.Application.isEditor || Debug.isDebugBuild) &&
                (e is ClueDiscovered || e is CombinationSucceeded || e is CombinationFailed || e is FactAdded || e is ReactionTriggered)) Debug.Log(e.ToString());
            Render();
        }
        private void Render()
        {
            var s = session.State;
            stateText.text = "KNOWN CLUES\n" + string.Join("  ", s.KnownClues.OrderBy(x => x.Value)) +
                "\n\nFACTS\n" + string.Join("\n", s.Facts.OrderBy(x => x.Value)) +
                "\n\nCOMPLETED RECIPES\n" + string.Join("\n", s.CompletedRecipes.OrderBy(x => x)) +
                "\n\nFIRED REACTIONS\n" + string.Join("\n", s.FiredReactions.OrderBy(x => x));
            if (UnityEngine.Application.isEditor || Debug.isDebugBuild)
                stateText.text += "\n\nAVAILABLE RECIPES\n" + string.Join("\n", session.Rules.AvailableRecipes(s).Select(x => x.Id));
            objectiveText.text = s.HasFact(DemoRules.Complete) ? "The room remembers. Three interpretations; a world that answers."
                : s.Knows(DemoRules.Sea) ? (showHint ? "Hint: combine Sea + Painting in Notebook." : "A sea waits on the page. What could hold it?")
                : s.HasFact(DemoRules.PaperRevealed) ? "Wind has uncovered a sheet. Observe the paper on the rug."
                : s.Knows(DemoRules.Far) ? (showHint ? "Hint: combine Far + Window in Notebook." : "The travel book imagines distance. Look beyond the room.")
                : s.HasFact(DemoRules.BookFallen) ? "Something fell. Observe the red book on the floor."
                : s.Knows(DemoRules.Growth) ? (showHint ? "Hint: combine Plant + Growth in Notebook." : "Two meanings collected. Open Notebook [N] and try a combination.")
                : "Observe the plant. Explore the room. [H] for a hint.";
        }
        private void Update()
        {
            var keyboard = Keyboard.current; if (keyboard == null || factInput.isFocused) return;
            if (keyboard.f3Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf);
            if (keyboard.hKey.wasPressedThisFrame) { showHint = !showHint; Render(); }
        }
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
