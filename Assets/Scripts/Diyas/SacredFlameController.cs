
using System;
using DiwaliTown.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace DiwaliTown.Diyas
{
    [DisallowMultipleComponent]
    public sealed class SacredFlameController : MonoBehaviour
    {
        [Header("Economy")]
        [SerializeField] private GoldWallet wallet;

        [SerializeField, Min(1)]
        private int sacredFlameCost = 50;

        [UnityEngine.Serialization.FormerlySerializedAs("firstDiyaReward")]
        [SerializeField, Min(0)]
        private int diyaReward = 10;

        [Header("Required Diyas")]
        [SerializeField] private DiyaAnchor[] diyaAnchors;
        public IReadOnlyList<DiyaAnchor> RequiredDiyas => diyaAnchors;

        [Header("UI")]
        [SerializeField] private Button purchaseButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private TMP_Text statusText;

        public bool FirstDiyaRewardGranted => firstDiyaRewardGranted;

        private bool hasCharge;
        private bool firstDiyaRewardGranted;

        public bool HasCharge => hasCharge;

        public event Action<DiyaAnchor> DiyaLit;
        public event Action FirstDiyaLit;

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            wallet.BalanceChanged += HandleBalanceChanged;
            purchaseButton.onClick.AddListener(
                HandlePurchaseButtonClicked
            );

            SetAllHighlights(hasCharge);
            RefreshUI();
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.BalanceChanged -= HandleBalanceChanged;
            }

            if (purchaseButton != null)
            {
                purchaseButton.onClick.RemoveListener(
                    HandlePurchaseButtonClicked
                );
            }
        }

        private bool ValidateReferences()
        {
            if (wallet == null ||
                purchaseButton == null ||
                buttonLabel == null ||
                statusText == null ||
                diyaAnchors == null ||
                diyaAnchors.Length == 0)
            {
                Debug.LogError(
                    "[SacredFlame] Required references are missing.",
                    this
                );

                return false;
            }

            return true;
        }

        private void HandlePurchaseButtonClicked()
        {
            TryPurchaseSacredFlame();
        }

        public bool TryPurchaseSacredFlame()
        {
            if (hasCharge)
            {
                statusText.text =
                    "Choose a highlighted diya first.";

                return false;
            }

            if (!HasUnlitDiyas())
            {
                RefreshUI();
                return false;
            }

            if (!wallet.TrySpend(sacredFlameCost))
            {
                statusText.text =
                    $"Not enough gold. You need {sacredFlameCost} gold.";

                return false;
            }

            hasCharge = true;
            SetAllHighlights(true);

            RefreshUI();
            return true;
        }


        public bool TryLightDiya(DiyaAnchor target)
        {
            if (!hasCharge)
            {
                statusText.text =
                    "Buy a Sacred Flame before lighting a diya.";

                return false;
            }

            if (target == null ||
                !IsRegisteredDiya(target) ||
                target.IsLit ||
                !target.IsHighlighted)
            {
                statusText.text =
                    "Choose one of the highlighted, unlit diyas.";

                return false;
            }

            if (!target.TryLight())
            {
                return false;
            }

            // One Sacred Flame charge lights one diya.
            hasCharge = false;
            SetAllHighlights(false);

            // Only the first diya triggers the special first-time event.
            bool isFirstDiya = !firstDiyaRewardGranted;

            if (isFirstDiya)
            {
                firstDiyaRewardGranted = true;
            }

            // Every successfully lit diya grants gold.
            bool rewardGranted = wallet.TryAdd(diyaReward);

            if (!rewardGranted)
            {
                Debug.LogError(
                    "[SacredFlame] Failed to award the diya reward.",
                    this
                );
            }

            DiyaLit?.Invoke(target);

            // Keep the first-diya unlock event separate from the reward.
            if (isFirstDiya)
            {
                FirstDiyaLit?.Invoke();
            }

            RefreshUI();

            if (rewardGranted)
            {
                statusText.text = isFirstDiya
                    ? $"First diya lit! +{diyaReward} gold earned."
                    : $"Diya lit! +{diyaReward} gold earned.";
            }
            else
            {
                statusText.text =
                    "Diya lit, but the gold reward could not be applied.";
            }

            return true;
        }


        private bool IsRegisteredDiya(DiyaAnchor target)
        {
            foreach (DiyaAnchor diya in diyaAnchors)
            {
                if (diya == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetAllHighlights(bool highlighted)
        {
            foreach (DiyaAnchor diya in diyaAnchors)
            {
                if (diya != null && !diya.IsLit)
                {
                    diya.SetHighlighted(highlighted);
                }
            }
        }

        private bool HasUnlitDiyas()
        {
            foreach (DiyaAnchor diya in diyaAnchors)
            {
                if (diya != null && !diya.IsLit)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleBalanceChanged(int newBalance)
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (hasCharge)
            {
                buttonLabel.text = "Sacred Flame Ready";
                purchaseButton.interactable = false;

                statusText.text =
                    "Choose one of the highlighted diyas.";

                return;
            }

            if (!HasUnlitDiyas())
            {
                buttonLabel.text = "All Diyas Lit";
                purchaseButton.interactable = false;

                statusText.text = "All required diyas are lit.";
                return;
            }

            buttonLabel.text =
                $"Buy Sacred Flame — {sacredFlameCost} Gold";

            purchaseButton.interactable = true;

            statusText.text =
                $"Spend {sacredFlameCost} gold to light one diya.";
        }
        public void RestoreProgress(
    bool restoredHasCharge,
    bool restoredFirstDiyaRewardGranted)
        {
            // Never restore a charge if there are no unlit diyas left.
            hasCharge = restoredHasCharge && HasUnlitDiyas();

            firstDiyaRewardGranted = restoredFirstDiyaRewardGranted;

            SetAllHighlights(hasCharge);
            RefreshUI();
        }
    }
}
