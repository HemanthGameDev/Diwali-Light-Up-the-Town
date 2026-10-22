
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiwaliTown.Diyas
{
    [DisallowMultipleComponent]
    public sealed class DiyaInteractionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private SacredFlameController sacredFlame;

        [Header("Raycast")]
        [SerializeField] private LayerMask diyaLayerMask = ~0;

        [SerializeField, Min(1f)]
        private float rayDistance = 500f;

        private readonly List<RaycastResult> uiRaycastResults =
            new List<RaycastResult>();

        private void Update()
        {
            Pointer pointer = Pointer.current;

            if (pointer == null ||
                !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            if (sceneCamera == null || sacredFlame == null)
            {
                return;
            }

            Vector2 screenPosition = pointer.position.ReadValue();

            // Do not let UI button clicks also select world objects.
            if (IsPointerOverUI(screenPosition))
            {
                return;
            }

            Ray ray = sceneCamera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    rayDistance,
                    diyaLayerMask))
            {
                return;
            }

            DiyaAnchor anchor =
                hit.collider.GetComponentInParent<DiyaAnchor>();

            if (anchor != null)
            {
                sacredFlame.TryLightDiya(anchor);
            }
        }

        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                return false;
            }

            PointerEventData eventData =
                new PointerEventData(eventSystem)
                {
                    position = screenPosition
                };

            uiRaycastResults.Clear();
            eventSystem.RaycastAll(eventData, uiRaycastResults);

            foreach (RaycastResult result in uiRaycastResults)
            {
                if (result.module is GraphicRaycaster)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
