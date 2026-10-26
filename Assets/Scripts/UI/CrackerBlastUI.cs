
using DiwaliTown.Crackers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiwaliTown.UI
{
    [DisallowMultipleComponent]
    public sealed class CrackerBlastUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CrackerBlastController blastController;
        [SerializeField] private CrackerPlacementController placementController;

        [Header("UI")]
        [SerializeField] private Button fireButton;
        [SerializeField] private TMP_Text fireButtonLabel;
        [SerializeField] private TMP_Text statusText;

        [Header("Ready Indicator")]
        [SerializeField] private GameObject fireReadyIndicator;

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            fireButton.onClick.AddListener(HandleFireClicked);
            placementController.PendingCountChanged += HandlePendingCountChanged;
            blastController.BlastCompleted += HandleBlastCompleted;

            RefreshUI();
        }

        private void OnDisable()
        {
            if (fireButton != null)
            {
                fireButton.onClick.RemoveListener(HandleFireClicked);
            }

            if (placementController != null)
            {
                placementController.PendingCountChanged -= HandlePendingCountChanged;
            }

            if (blastController != null)
            {
                blastController.BlastCompleted -= HandleBlastCompleted;
            }
        }

        private void HandlePendingCountChanged(int pendingCount)
        {
            RefreshUI();
        }
        private bool ValidateReferences()
        {
            if (blastController == null ||
                placementController == null ||
                fireButton == null ||
                fireButtonLabel == null ||
                statusText == null)
            {
                Debug.LogError(
                    "[CrackerBlastUI] Assign all required references.",
                    this
                );

                return false;
            }

            return true;
        }

        private void HandleFireClicked()
        {
            if (!blastController.TryFireAll(
                    out int ignitedCount,
                    out int totalReward))
            {
                statusText.text = "Place at least one cracker first.";
            }
        }

        private void HandleBlastCompleted(
            int ignitedCount,
            int totalReward)
        {
            statusText.text =
                $"Blast complete! {ignitedCount} crackers ignited. " +
                $"+{totalReward} gold earned.";

            RefreshUI();
        }

        private void RefreshUI()
        {
            int count = placementController.PendingCount;

            fireButton.interactable = count > 0;

            fireButtonLabel.text = count > 0
                ? $"FIRE ({count})"
                : "FIRE";

            if (fireReadyIndicator != null)
            {
                fireReadyIndicator.SetActive(count > 0);
            }

            if (count == 0)
            {
                statusText.text = "Place crackers to enable Fire.";
            }
        }
    }
}
