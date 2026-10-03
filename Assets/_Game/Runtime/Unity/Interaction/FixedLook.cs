using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.Unity
{
    public sealed class FixedLook : MonoBehaviour
    {
        [SerializeField] private NotebookView notebook;
        [SerializeField] private float sensitivity = 0.12f;
        private float yaw;
        private float pitch;
        private Quaternion origin;
        public void Configure(NotebookView view) { notebook = view; }
        private void Awake() { origin = transform.localRotation; }
        public void ApplyLook(Vector2 delta)
        {
            yaw = Mathf.Clamp(yaw + delta.x * sensitivity, -80f, 80f);
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -45f, 45f);
            transform.localRotation = origin * Quaternion.Euler(pitch, yaw, 0);
        }
        private void Update()
        {
            if (Mouse.current == null || notebook.IsOpen) return;
            if (Mouse.current.rightButton.isPressed) ApplyLook(Mouse.current.delta.ReadValue());
            if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame)
            { yaw = pitch = 0; transform.localRotation = origin; }
        }
    }
}
