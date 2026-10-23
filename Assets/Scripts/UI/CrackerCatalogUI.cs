using DiwaliTown.Crackers;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiwaliTown.UI
{
    [DisallowMultipleComponent]
    public sealed class CrackerCatalogUI : MonoBehaviour
    {
        [Header("Cracker Data")]
        [SerializeField] private CrackerDefinition basicCracker;

        [Header("References")]
        [SerializeField] private CrackerUnlockController unlockController;
        [SerializeField] private CrackerInventory inventory;
        [SerializeField] private CrackerQuickSlotController loadout;

        [Header("Catalog Panel")]
        [SerializeField] private GameObject catalogPanel;
        [SerializeField] private Button openCatalogButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button actionButton;

        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text crackerNameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text actionButtonLabel;

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            unlockController.BasicCrackerUnlocked += RefreshUI;
            inventory.StockChanged += HandleStockChanged;

            openCatalogButton.onClick.AddListener(OpenCatalog);
            closeButton.onClick.AddListener(CloseCatalog);
            actionButton.onClick.AddListener(HandleActionButton);

            RefreshUI();
        }

        private void OnDisable()
        {
            if (unlockController != null)
                unlockController.BasicCrackerUnlocked -= RefreshUI;

            if (inventory != null)
                inventory.StockChanged -= HandleStockChanged;

            if (openCatalogButton != null)
                openCatalogButton.onClick.RemoveListener(OpenCatalog);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(CloseCatalog);

            if (actionButton != null)
                actionButton.onClick.RemoveListener(HandleActionButton);
        }

        private bool ValidateReferences()
        {
            if (basicCracker == null ||
                unlockController == null ||
                inventory == null ||
                loadout == null ||
                catalogPanel == null ||
                openCatalogButton == null ||
                closeButton == null ||
                actionButton == null ||
                titleText == null ||
                crackerNameText == null ||
                priceText == null ||
                actionButtonLabel == null)
            {
                Debug.LogError(
                    "[CrackerCatalogUI] Assign all required references.",
                    this
                );

                return false;
            }

            // Status text is optional.
            return true;
        }

        private void OpenCatalog()
        {
            catalogPanel.SetActive(true);
            RefreshUI();
        }

        private void CloseCatalog()
        {
            catalogPanel.SetActive(false);
        }

        private void HandleActionButton()
        {
            if (!unlockController.IsBasicCrackerUnlocked)
            {
                SetStatus("Light your first diya to unlock Basic Cracker.");
                return;
            }

            int currentStock = inventory.GetStock(basicCracker);

            if (currentStock <= 0)
            {
                PurchaseBatch();
                return;
            }

            if (!loadout.TryAssignToFirstEmptySlot(
                    basicCracker,
                    out int slotIndex))
            {
                SetStatus(
                    "All three quick slots are occupied. " +
                    "There is no empty slot for another cracker type."
                );

                return;
            }

            Debug.Log(
                $"[CrackerCatalogUI] {basicCracker.DisplayName} " +
                $"assigned to quick slot {slotIndex + 1}."
            );

            CloseCatalog();
        }

        private void PurchaseBatch()
        {
            int batchCost = inventory.GetBatchCost(basicCracker);

            if (batchCost <= 0)
            {
                SetStatus("Invalid cracker price. Check its definition.");
                return;
            }

            if (!inventory.CanPurchaseBatch(basicCracker))
            {
                SetStatus($"Not enough gold. You need {batchCost} gold.");
                return;
            }

            if (!inventory.TryPurchaseBatch(basicCracker))
            {
                SetStatus("Purchase failed. Please try again.");
                return;
            }

            RefreshUI();

            SetStatus(
                $"Purchased {basicCracker.PurchaseBatchSize} " +
                $"{basicCracker.DisplayName}s. Tap Select to equip."
            );
        }

        private void HandleStockChanged(
            CrackerDefinition definition,
            int quantity)
        {
            if (definition == basicCracker)
                RefreshUI();
        }

        private void RefreshUI()
        {
            bool unlocked = unlockController.IsBasicCrackerUnlocked;
            int stock = inventory.GetStock(basicCracker);
            int batchCost = inventory.GetBatchCost(basicCracker);

            titleText.text = "CRACKER CATALOG";
            crackerNameText.text = basicCracker.DisplayName;

            priceText.text =
                $"{basicCracker.UnitPrice} Gold each\n" +
                $"Pack of {basicCracker.PurchaseBatchSize}: " +
                $"{batchCost} Gold\n" +
                $"Blast reward: +{basicCracker.BlastReward} Gold";

            actionButton.interactable = unlocked;

            if (!unlocked)
            {
                actionButtonLabel.text = "LOCKED";

                SetStatus(
                    "Light your first diya to unlock Basic Cracker."
                );
            }
            else if (stock <= 0)
            {
                actionButtonLabel.text =
                    $"Buy {basicCracker.PurchaseBatchSize} " +
                    $"— {batchCost} Gold";

                SetStatus("Purchase a pack to add crackers to your stock.");
            }
            else
            {
                actionButtonLabel.text = "SELECT";

                SetStatus($"Available stock: {stock}. Tap Select to equip.");
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}