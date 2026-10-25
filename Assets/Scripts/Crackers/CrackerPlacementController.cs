
using System;
using System.Collections.Generic;
using DiwaliTown.Crackers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiwaliTown.Crackers
{
    [DisallowMultipleComponent]
    public sealed class CrackerPlacementController : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private CrackerInventory inventory;
        [SerializeField] private CrackerQuickSlotController loadout;

        [Header("Placement")]
        [SerializeField] private LayerMask placementSurfaceMask;
        [SerializeField, Min(1f)] private float rayDistance = 500f;
        [SerializeField] private float surfaceOffset = 0.02f;
        [SerializeField] private float placementYaw;

        [Header("Scene Objects")]
        [SerializeField] private Transform placedCrackersRoot;
        [SerializeField] private TMP_Text statusText;

        private bool touchSequenceActive;
        private bool touchStartedOverUI;

        private readonly List<PlacedCracker> pendingCrackers =
            new List<PlacedCracker>();

        private GameObject previewInstance;
        private CrackerDefinition activeDefinition;
        private int activeSlotIndex = -1;
        private bool isPlacementMode;

        private readonly List<RaycastResult> uiRaycastResults =
            new List<RaycastResult>();

        public bool IsPlacementMode => isPlacementMode;

        public IReadOnlyList<PlacedCracker> PendingCrackers =>
            pendingCrackers;

        public int PendingCount => pendingCrackers.Count;

        public event Action<int> PendingCountChanged;

        private void OnEnable()
        {
            if (sceneCamera == null ||
                inventory == null ||
                loadout == null ||
                placementSurfaceMask.value == 0)
            {
                Debug.LogError(
                    "[CrackerPlacement] Assign the camera, inventory, " +
                    "loadout and placement surface layer.",
                    this
                );

                enabled = false;
                return;
            }

            loadout.SlotActivated += HandleSlotActivated;
        }

        private void OnDisable()
        {
            if (loadout != null)
            {
                loadout.SlotActivated -= HandleSlotActivated;
            }

            ExitPlacementMode("Placement mode closed.");
        }

        private void HandleSlotActivated(
            int slotIndex,
            CrackerDefinition definition)
        {
            // Tapping the active slot again cancels placement.
            if (isPlacementMode && activeSlotIndex == slotIndex)
            {
                ExitPlacementMode("Placement cancelled.");
                return;
            }

            if (definition == null)
            {
                return;
            }

            if (inventory.GetStock(definition) <= 0)
            {
                SetStatus("No crackers remaining. Buy another pack.");
                return;
            }

            if (definition.PlaceablePrefab == null ||
                definition.PlacementPreviewPrefab == null)
            {
                Debug.LogError(
                    "[CrackerPlacement] Assign both placement prefabs " +
                    "to the cracker definition.",
                    this
                );

                SetStatus("Placement prefabs are not configured.");
                return;
            }

            ExitPreviewOnly();

            activeDefinition = definition;
            activeSlotIndex = slotIndex;
            isPlacementMode = true;

            previewInstance = Instantiate(
                definition.PlacementPreviewPrefab
            );

            // A preview is visual only; it must not block raycasts.
            foreach (Collider previewCollider in
                     previewInstance.GetComponentsInChildren<Collider>(true))
            {
                previewCollider.enabled = false;
            }

            foreach (Rigidbody previewBody in
                     previewInstance.GetComponentsInChildren<Rigidbody>(true))
            {
                previewBody.isKinematic = true;
                previewBody.useGravity = false;
                previewBody.detectCollisions = false;
            }

            SetPreviewLayerRecursively(previewInstance, 2);
            previewInstance.SetActive(false);

            SetStatus(
                $"Placing {definition.DisplayName}. " +
                "Tap valid ground to place. Tap the slot again to cancel."
            );
        }


        private void Update()
        {
            if (!isPlacementMode || previewInstance == null)
            {
                return;
            }

            if (Application.isMobilePlatform)
            {
                UpdateTouchPlacement();
            }
            else
            {
                UpdateMousePlacement();
            }
        }

        private void UpdateMousePlacement()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                previewInstance.SetActive(false);
                return;
            }

            Vector2 screenPosition = mouse.position.ReadValue();

            
            if (IsPointerOverUI(screenPosition))
            {
               
                previewInstance.SetActive(false);
                return;
            }

            Ray ray = sceneCamera.ScreenPointToRay(screenPosition);

            if (!TryGetGroundHit(ray, out RaycastHit hit))
            {
                previewInstance.SetActive(false);
                return;
            }

            UpdatePreviewPosition(hit);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryPlaceCracker(hit);
            }
        }

        private void UpdateTouchPlacement()
        {
            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen == null)
            {
                previewInstance.SetActive(false);
                return;
            }

            var touch = touchscreen.primaryTouch;

            bool pressedThisFrame = touch.press.wasPressedThisFrame;
            bool isPressed = touch.press.isPressed;
            bool releasedThisFrame = touch.press.wasReleasedThisFrame;

            Vector2 screenPosition = touch.position.ReadValue();

            if (pressedThisFrame)
            {
                touchSequenceActive = true;
                touchStartedOverUI = IsPointerOverUI(screenPosition);
            }

            if (!touchSequenceActive)
            {
                previewInstance.SetActive(false);
                return;
            }

            if (touchStartedOverUI || IsPointerOverUI(screenPosition))
            {
                previewInstance.SetActive(false);

                if (releasedThisFrame)
                {
                    ResetTouchSequence();
                }

                return;
            }

            if (!isPressed && !releasedThisFrame)
            {
                previewInstance.SetActive(false);
                ResetTouchSequence();
                return;
            }

            Ray ray = sceneCamera.ScreenPointToRay(screenPosition);

            if (!TryGetGroundHit(ray, out RaycastHit hit))
            {
                previewInstance.SetActive(false);

                if (releasedThisFrame)
                {
                    ResetTouchSequence();
                }

                return;
            }

            UpdatePreviewPosition(hit);

            // On mobile, release confirms placement.
            if (releasedThisFrame)
            {
                TryPlaceCracker(hit);

                // Prevent the ghost from overlapping the placed cracker.
                if (previewInstance != null)
                {
                    previewInstance.SetActive(false);
                }

                ResetTouchSequence();
            }
        }

        private void UpdatePreviewPosition(RaycastHit hit)
        {
            if (previewInstance == null)
            {
                return;
            }

            previewInstance.transform.position =
                hit.point + hit.normal * surfaceOffset;

            previewInstance.transform.rotation =
                Quaternion.FromToRotation(Vector3.up, hit.normal) *
                Quaternion.Euler(0f, placementYaw, 0f);

            previewInstance.SetActive(true);
        }

        private void ResetTouchSequence()
        {
            touchSequenceActive = false;
            touchStartedOverUI = false;
        }


        private bool TryGetGroundHit(
    Ray ray,
    out RaycastHit hit)
        {
            return Physics.Raycast(
                ray,
                out hit,
                rayDistance,
                placementSurfaceMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private void TryPlaceCracker(RaycastHit hit)
        {
            CrackerDefinition definition = activeDefinition;

            if (definition == null ||
                definition.PlaceablePrefab == null)
            {
                return;
            }

            if (inventory.GetStock(definition) <= 0)
            {
                ExitPlacementMode("No crackers remaining. Buy another pack.");
                return;
            }

            Vector3 position =
                hit.point + hit.normal * surfaceOffset;

            Quaternion rotation =
                Quaternion.FromToRotation(Vector3.up, hit.normal) *
                Quaternion.Euler(0f, placementYaw, 0f);

            GameObject placedObject = Instantiate(
                definition.PlaceablePrefab,
                position,
                rotation,
                placedCrackersRoot
            );

            PlacedCracker placedCracker =
                placedObject.GetComponent<PlacedCracker>();

            if (placedCracker == null)
            {
                Destroy(placedObject);

                Debug.LogError(
                    "[CrackerPlacement] PF_BasicCracker requires " +
                    "the PlacedCracker component.",
                    this
                );

                SetStatus("The cracker prefab is missing its component.");
                return;
            }

            placedCracker.Initialize(definition);

            if (!inventory.TryConsume(definition, 1))
            {
                Destroy(placedObject);
                SetStatus("Unable to place cracker. Check your stock.");
                return;
            }

            pendingCrackers.Add(placedCracker);
            PendingCountChanged?.Invoke(pendingCrackers.Count);

            int remaining = inventory.GetStock(definition);

            if (remaining <= 0)
            {
                ExitPlacementMode(
                    $"Cracker placed. No {definition.DisplayName} stock left."
                );

                return;
            }

            SetStatus(
                $"{definition.DisplayName} placed. {remaining} remaining. " +
                "Tap ground to place more."
            );
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

            eventSystem.RaycastAll(
                eventData,
                uiRaycastResults
            );

            foreach (RaycastResult result in uiRaycastResults)
            {
                if (result.module is GraphicRaycaster)
                {
                    return true;
                }
            }

            return false;
        }

        private void ExitPlacementMode(string message)
        {
            isPlacementMode = false;
            activeDefinition = null;
            activeSlotIndex = -1;

            ExitPreviewOnly();
            SetStatus(message);
        }

        private void ExitPreviewOnly()
        {
            if (previewInstance == null)
            {
                return;
            }

            Destroy(previewInstance);
            previewInstance = null;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private static void SetPreviewLayerRecursively(
            GameObject root,
            int layer)
        {
            root.layer = layer;

            foreach (Transform child in root.transform)
            {
                SetPreviewLayerRecursively(child.gameObject, layer);
            }
        }

        public bool TryRemovePendingCracker(PlacedCracker cracker)
        {
            if (cracker == null || !pendingCrackers.Remove(cracker))
            {
                return false;
            }

            PendingCountChanged?.Invoke(pendingCrackers.Count);
            return true;
        }

        public bool TryRestorePendingCracker(
    CrackerDefinition definition,
    Vector3 position,
    Quaternion rotation)
        {
            if (definition == null ||
                definition.PlaceablePrefab == null ||
                placedCrackersRoot == null)
            {
                return false;
            }

            GameObject instance = Instantiate(
                definition.PlaceablePrefab,
                position,
                rotation,
                placedCrackersRoot
            );

            PlacedCracker cracker =
                instance.GetComponent<PlacedCracker>();

            if (cracker == null)
            {
                Destroy(instance);

                Debug.LogError(
                    "[CrackerPlacement] Restored prefab is missing PlacedCracker.",
                    this
                );

                return false;
            }

            cracker.Initialize(definition);

            pendingCrackers.Add(cracker);
            PendingCountChanged?.Invoke(pendingCrackers.Count);

            return true;
        }
    }
}
