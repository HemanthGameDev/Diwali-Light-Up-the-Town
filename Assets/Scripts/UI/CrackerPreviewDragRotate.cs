using UnityEngine;
using UnityEngine.EventSystems;

namespace DiwaliTown.UI
{
    /// <summary>
    /// Add this to the RawImage that displays the preview RenderTexture, or to a
    /// transparent UI Image placed over that preview area. UI drag events support
    /// mouse in the Editor and touch on Android through the EventSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrackerPreviewDragRotate : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private Transform targetToRotate;
        [Tooltip("Degrees rotated per horizontal drag pixel.")]
        [SerializeField, Min(0.01f)] private float degreesPerPixel = 0.5f;
        [SerializeField] private bool invertDirection;

        public void SetTarget(Transform target)
        {
            targetToRotate = target;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Implemented to make the drag intent explicit for the EventSystem.
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (targetToRotate == null)
                return;

            float direction = invertDirection ? 1f : -1f;
            float angle = eventData.delta.x * degreesPerPixel * direction;
            targetToRotate.Rotate(Vector3.up, angle, Space.Self);
        }
    }
}
