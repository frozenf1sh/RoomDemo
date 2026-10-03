using System.Linq;
using Game.Application;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Game.Unity
{
    public sealed class InteractionSystem : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Text hoverText;
        private GameSession session;
        private NotebookView notebook;
        public void Configure(Camera camera, Text hover) { viewCamera = camera; hoverText = hover; }
        public void Bind(GameSession game, NotebookView view) { session = game; notebook = view; }
        public Clickable FindClickable(Vector2 screenPoint)
        {
            if (Physics.Raycast(viewCamera.ScreenPointToRay(screenPoint), out var hit, 30f))
                return hit.collider.GetComponent<Clickable>();
            return null;
        }
        public bool InspectAt(Vector2 screenPoint)
        {
            var target = FindClickable(screenPoint);
            if (target == null) return false;
            target.Inspect(session); return true;
        }
        private void Update()
        {
            if (Mouse.current == null || session == null) return;
            bool blocked = notebook.IsOpen || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
            var target = blocked ? null : FindClickable(Mouse.current.position.ReadValue());
            hoverText.text = target == null ? "" : "CLICK TO OBSERVE  /  " + session.Rules.Objects.First(x => x.Id == target.Entity.Id).Label.ToUpperInvariant();
            if (!blocked && Mouse.current.leftButton.wasPressedThisFrame) InspectAt(Mouse.current.position.ReadValue());
        }
    }
}
