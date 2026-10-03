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
        private GameSession session;
        private readonly Queue<string> recent = new Queue<string>();
        public void Configure(GameObject body, Text state, Text events, Text objective)
        { panel = body; stateText = state; eventText = events; objectiveText = objective; }
        public void Bind(GameSession game) { session = game; session.EventRaised += OnEvent; Render(); }
        private void OnEvent(DomainEvent e)
        {
            recent.Enqueue(e.ToString()); while (recent.Count > 6) recent.Dequeue();
            eventText.text = string.Join("\n", recent); Render();
        }
        private void Render()
        {
            var s = session.State;
            stateText.text = "KNOWN OBJECTS\n" + string.Join("  ", s.KnownObjects.OrderBy(x => x.Value)) +
                "\n\nKNOWN CONCEPTS\n" + string.Join("  ", s.KnownConcepts.OrderBy(x => x.Value)) +
                "\n\nFACTS\n" + string.Join("\n", s.Facts.OrderBy(x => x.Value)) +
                "\n\nREACTIONS\n" + string.Join("\n", s.FiredReactions.OrderBy(x => x));
            objectiveText.text = s.HasFact(DemoRules.WindEntering) ? "COMPLETE  /  The world reacts on its own."
                : s.HasFact(DemoRules.WindowFar) ? "The window opens to Far. What else might change?"
                : s.Knows(DemoRules.Far) ? "03  /  Open Notebook. Interpret Far into Window."
                : s.HasFact(DemoRules.BookFallen) ? "02  /  Close Notebook. Observe the fallen Book."
                : s.Knows(DemoRules.Growth) ? "01  /  Open Notebook. Interpret Growth into Plant."
                : "01  /  Observe the Plant to collect Growth.";
        }
        private void Update() { if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) panel.SetActive(!panel.activeSelf); }
        private void OnDestroy() { if (session != null) session.EventRaised -= OnEvent; }
    }
}
