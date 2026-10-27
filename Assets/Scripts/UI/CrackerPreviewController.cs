using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.VFX;
using DiwaliTown.Crackers;

namespace DiwaliTown.UI
{
    /// <summary>
    /// Displays preview-only cracker models and plays preview VFX.
    /// Keeps UI selection, preview visuals, and gameplay economy separate.
    /// It deliberately does not reference GoldWallet, CrackerInventory,
    /// CrackerPlacementController, or CrackerBlastController, so previewing
    /// cannot spend gold or award gameplay rewards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrackerPreviewController : MonoBehaviour
    {
        [Serializable]
        private sealed class PreviewEntry
        {
            [Tooltip("Must be the same CrackerDefinition asset used by CrackerCatalogUI.")]
            public CrackerDefinition definition;

            [Tooltip("A visual-only prefab is recommended. Avoid gameplay/economy scripts on this prefab.")]
            public GameObject modelPrefab;

            [Tooltip("VFX-only prefab for the preview. This is spawned separately from gameplay.")]
            public GameObject blastEffectPrefab;

            public Vector3 modelLocalPosition = Vector3.zero;
            public Vector3 modelLocalEulerAngles = Vector3.zero;
            public Vector3 modelLocalScale = Vector3.one;

            public Vector3 blastLocalPosition = Vector3.zero;
            public Vector3 blastLocalEulerAngles = Vector3.zero;

            [Min(0.1f)] public float blastEffectLifetime = 4f;
        }

        [Header("Connections")]
        [SerializeField] private CrackerCatalogUI catalogUI;
        [Tooltip("An empty Transform in the preview scene. The cracker rotates around this pivot.")]
        [SerializeField] private Transform previewModelPivot;
        [Tooltip("Optional empty Transform where preview VFX should appear. If empty, the pivot position is used.")]
        [SerializeField] private Transform previewEffectAnchor;
        [SerializeField] private Button previewBlastButton;
        [SerializeField] private TMP_Text previewFeedbackText;
        [Tooltip("Hide the cracker model while its preview blast effect plays, then show it again.")]
        [SerializeField] private bool hideModelDuringBlast = true;

        [Header("Preview Models")]
        [SerializeField] private PreviewEntry[] previewEntries;

        private CrackerDefinition currentDefinition;
        private PreviewEntry currentEntry;
        private GameObject currentModelInstance;
        private GameObject currentEffectInstance;
        private Coroutine effectCleanupRoutine;
        private Quaternion initialPivotLocalRotation = Quaternion.identity;
        private bool previewPivotHiddenForBlast;
        private bool previewPivotWasActiveBeforeBlast;

        private void Awake()
        {
            if (previewModelPivot != null)
                initialPivotLocalRotation = previewModelPivot.localRotation;
        }

        private void OnEnable()
        {
            if (catalogUI != null)
                catalogUI.PreviewDefinitionChanged += HandlePreviewDefinitionChanged;

            if (previewBlastButton != null)
                previewBlastButton.onClick.AddListener(PlayPreviewBlast);

            RefreshBlastButton();
        }

        private void Start()
        {
            // CatalogUI initializes its selection during OnEnable. Start runs after
            // initial OnEnable calls, so this covers the first scene load as well.
            if (catalogUI != null && catalogUI.SelectedCracker != null)
                ShowDefinition(catalogUI.SelectedCracker);
        }

        private void OnDisable()
        {
            if (catalogUI != null)
                catalogUI.PreviewDefinitionChanged -= HandlePreviewDefinitionChanged;

            if (previewBlastButton != null)
                previewBlastButton.onClick.RemoveListener(PlayPreviewBlast);

            // If the catalog closes during a blast, clean up and restore the model.
            StopAndClearCurrentEffect();
        }

        private void OnDestroy()
        {
            if (effectCleanupRoutine != null)
                StopCoroutine(effectCleanupRoutine);
        }

        private void HandlePreviewDefinitionChanged(CrackerDefinition definition)
        {
            ShowDefinition(definition);
        }

        public void ShowDefinition(CrackerDefinition definition)
        {
            if (previewModelPivot == null)
            {
                SetFeedback("Assign Preview Model Pivot on CrackerPreviewController.");
                Debug.LogError("[CrackerPreviewController] Preview Model Pivot is not assigned.", this);
                return;
            }

            if (definition == null)
            {
                currentDefinition = null;
                currentEntry = null;
                DestroyCurrentModel();
                StopAndClearCurrentEffect();
                SetFeedback("Select a cracker to preview it.");
                RefreshBlastButton();
                return;
            }

            if (definition == currentDefinition && currentModelInstance != null)
            {
                RefreshBlastButton();
                return;
            }

            StopAndClearCurrentEffect();
            DestroyCurrentModel();
            currentDefinition = definition;
            currentEntry = FindEntry(definition);

            previewModelPivot.localRotation = initialPivotLocalRotation;

            if (currentEntry == null)
            {
                SetFeedback($"No preview entry configured for {definition.DisplayName}.");
                Debug.LogWarning(
                    $"[CrackerPreviewController] Add a Preview Entry for '{definition.DisplayName}' and assign its model prefab.",
                    this);
                RefreshBlastButton();
                return;
            }

            if (currentEntry.modelPrefab == null)
            {
                SetFeedback($"Assign a preview model for {definition.DisplayName}.");
                Debug.LogWarning(
                    $"[CrackerPreviewController] Model Prefab is empty for '{definition.DisplayName}'.",
                    this);
                RefreshBlastButton();
                return;
            }

            currentModelInstance = Instantiate(currentEntry.modelPrefab, previewModelPivot);
            currentModelInstance.name = $"Preview_{definition.name}";
            currentModelInstance.transform.localPosition = currentEntry.modelLocalPosition;
            currentModelInstance.transform.localRotation = Quaternion.Euler(currentEntry.modelLocalEulerAngles);
            currentModelInstance.transform.localScale = currentEntry.modelLocalScale;

            // Preview prefabs should ideally contain visuals only. Disable common gameplay
            // components on the spawned copy as an additional safeguard.
            DisableGameplayComponentsOnPreviewCopy(currentModelInstance);

            SetFeedback($"Previewing {definition.DisplayName}. Drag to rotate.");
            RefreshBlastButton();
        }

        public void PlayPreviewBlast()
        {
            if (currentEntry == null || currentDefinition == null)
            {
                SetFeedback("Select a cracker before previewing its blast.");
                Debug.LogWarning("[CrackerPreviewController] Cannot play a blast because no preview entry is selected.", this);
                return;
            }

            if (currentEntry.blastEffectPrefab == null)
            {
                SetFeedback("No blast effect prefab is assigned to this preview entry.");
                Debug.LogWarning(
                    $"[CrackerPreviewController] Blast Effect Prefab is empty for '{currentDefinition.DisplayName}'. Assign a VFX prefab in Preview Entries.",
                    this);
                return;
            }

            Transform anchor = previewEffectAnchor != null ? previewEffectAnchor : previewModelPivot;
            if (anchor == null)
            {
                SetFeedback("Assign a preview effect anchor or preview model pivot.");
                Debug.LogError("[CrackerPreviewController] No preview effect anchor is available.", this);
                return;
            }

            // Stop the previous effect first. This also restores the model if it was
            // hidden by a previous blast that had not finished yet.
            StopAndClearCurrentEffect();

            currentEffectInstance = Instantiate(currentEntry.blastEffectPrefab, anchor, false);
            currentEffectInstance.name = $"PreviewBlast_{currentDefinition.name}";
            currentEffectInstance.transform.localPosition = currentEntry.blastLocalPosition;
            currentEffectInstance.transform.localRotation = Quaternion.Euler(currentEntry.blastLocalEulerAngles);
            currentEffectInstance.transform.localScale = currentEntry.blastEffectPrefab.transform.localScale;

            // Preview cameras are often restricted to the model's layer. Put the
            // VFX hierarchy on that same layer so it is visible in the RenderTexture.
            int previewLayer = currentModelInstance != null
                ? currentModelInstance.layer
                : previewModelPivot.gameObject.layer;
            SetLayerRecursively(currentEffectInstance, previewLayer);

            bool startedVisualEffect = PlayAllPreviewEffects(currentEffectInstance);
            if (!startedVisualEffect)
            {
                Debug.LogWarning(
                    $"[CrackerPreviewController] '{currentEntry.blastEffectPrefab.name}' contains no ParticleSystem or Visual Effect component. Check that the assigned prefab is a playable VFX prefab.",
                    currentEffectInstance);
            }

            // Hide the entire preview pivot, not only the runtime-instantiated copy.
            // This also hides any temporary/design-time model left under the pivot,
            // which otherwise remains visible during the blast.
            if (hideModelDuringBlast && previewModelPivot != null)
            {
                previewPivotWasActiveBeforeBlast = previewModelPivot.gameObject.activeSelf;
                previewPivotHiddenForBlast = true;
                previewModelPivot.gameObject.SetActive(false);
            }

            float lifetime = Mathf.Max(0.1f, currentEntry.blastEffectLifetime);
            effectCleanupRoutine = StartCoroutine(DestroyEffectAfterRealtime(currentEffectInstance, lifetime));
            SetFeedback($"Previewing {currentDefinition.DisplayName} blast. No gold or stock used.");
        }

        private System.Collections.IEnumerator DestroyEffectAfterRealtime(GameObject effect, float lifetime)
        {
            float elapsed = 0f;
            while (elapsed < lifetime && effect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (effect != null)
                Destroy(effect);

            if (currentEffectInstance == effect)
                currentEffectInstance = null;

            effectCleanupRoutine = null;
            RestoreModelAfterBlast();
        }

        private static bool PlayAllPreviewEffects(GameObject effectRoot)
        {
            bool startedAny = false;

            ParticleSystem[] particleSystems = effectRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                if (!particleSystem.gameObject.activeSelf)
                    particleSystem.gameObject.SetActive(true);

                // ParticleSystem is a Component, not a Behaviour, so it has no
                // enabled property. Activating its GameObject and calling Play()
                // is the correct way to start this preview system.
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Play(true);
                startedAny = true;
            }

            VisualEffect[] visualEffects = effectRoot.GetComponentsInChildren<VisualEffect>(true);
            foreach (VisualEffect visualEffect in visualEffects)
            {
                if (!visualEffect.gameObject.activeSelf)
                    visualEffect.gameObject.SetActive(true);

                if (!visualEffect.enabled)
                    visualEffect.enabled = true;

                visualEffect.Reinit();
                visualEffect.Play();
                startedAny = true;
            }

            return startedAny;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void RestoreModelAfterBlast()
        {
            if (!previewPivotHiddenForBlast)
                return;

            if (previewModelPivot != null)
                previewModelPivot.gameObject.SetActive(previewPivotWasActiveBeforeBlast);

            previewPivotHiddenForBlast = false;
        }

        private PreviewEntry FindEntry(CrackerDefinition definition)
        {
            if (previewEntries == null)
                return null;

            foreach (PreviewEntry entry in previewEntries)
            {
                if (entry != null && entry.definition == definition)
                    return entry;
            }

            return null;
        }

        private void DestroyCurrentModel()
        {
            if (currentModelInstance != null)
            {
                Destroy(currentModelInstance);
                currentModelInstance = null;
            }
        }

        private void StopAndClearCurrentEffect()
        {
            if (effectCleanupRoutine != null)
            {
                StopCoroutine(effectCleanupRoutine);
                effectCleanupRoutine = null;
            }

            if (currentEffectInstance != null)
            {
                Destroy(currentEffectInstance);
                currentEffectInstance = null;
            }

            RestoreModelAfterBlast();
        }

        private void RefreshBlastButton()
        {
            // Keep the button clickable when an entry is selected even if its VFX
            // is unassigned. Pressing it then reports the missing prefab clearly.
            if (previewBlastButton != null)
                previewBlastButton.interactable = currentEntry != null;
        }

        private void SetFeedback(string message)
        {
            if (previewFeedbackText != null)
                previewFeedbackText.text = message;
        }

        private static void DisableGameplayComponentsOnPreviewCopy(GameObject instance)
        {
            foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;

            foreach (Collider colliderComponent in instance.GetComponentsInChildren<Collider>(true))
                colliderComponent.enabled = false;

            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }

            foreach (AudioSource audioSource in instance.GetComponentsInChildren<AudioSource>(true))
                audioSource.enabled = false;
        }
    }
}
