using System;
using System.Collections.Generic;
using DiwaliTown.Crackers;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DiwaliTown.UI
{
    /// <summary>
    /// Drives the existing catalog scene objects. It does not instantiate prefabs
    /// or destroy children: the category row and cracker grid are reused in place.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrackerCatalogUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CrackerDefinition[] crackerDefinitions;
        [SerializeField] private CrackerCategoryDefinition[] categories;

        [Header("Systems")]
        [SerializeField] private CrackerUnlockController unlockController;
        [SerializeField] private CrackerInventory inventory;
        [SerializeField] private CrackerQuickSlotController loadout;

        [Header("Existing Scene UI")]
        [SerializeField] private GameObject catalogPanel;
        [SerializeField] private Button openCatalogButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button actionButton;
        [Tooltip("Assign the existing DifferentCrackerSelect horizontal row.")]
        [SerializeField] private Transform categoryButtonsRoot;
        [Tooltip("Assign the existing GridcrackerParent grid.")]
        [SerializeField] private Transform itemGridRoot;

        [Header("Existing Detail Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text crackerNameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text actionButtonLabel;
        [SerializeField] private TMP_Text selectedCrackerText;

        [Header("Notifications (Optional)")]
        [SerializeField] private GameObject catalogNotificationDot;

        [Header("Icon Appearance")]
        [SerializeField] private Color selectedIconTint = new Color(1f, 0.86f, 0.45f, 1f);
        [SerializeField] private Color lockedIconTint = new Color(0.55f, 0.55f, 0.55f, 1f);

        private CrackerCategoryDefinition selectedCategory;
        private CrackerDefinition selectedCracker;
        private readonly List<ButtonBinding> categoryBindings = new List<ButtonBinding>();
        private readonly List<ButtonBinding> itemBindings = new List<ButtonBinding>();

        public CrackerDefinition SelectedCracker => selectedCracker;
        public event Action<CrackerDefinition> PreviewDefinitionChanged;

        private sealed class ButtonBinding
        {
            public Button Button;
            public UnityAction Action;
        }

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            openCatalogButton.onClick.AddListener(OpenCatalog);
            closeButton.onClick.AddListener(CloseCatalog);
            actionButton.onClick.AddListener(HandleActionButton);

            if (unlockController != null)
                unlockController.NewUnlocksChanged += HandleCatalogStateChanged;
            if (inventory != null)
                inventory.StockChanged += HandleStockChanged;

            RefreshCatalog();
        }

        private void OnDisable()
        {
            if (openCatalogButton != null)
                openCatalogButton.onClick.RemoveListener(OpenCatalog);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(CloseCatalog);
            if (actionButton != null)
                actionButton.onClick.RemoveListener(HandleActionButton);

            if (unlockController != null)
                unlockController.NewUnlocksChanged -= HandleCatalogStateChanged;
            if (inventory != null)
                inventory.StockChanged -= HandleStockChanged;

            ClearBindings(categoryBindings);
            ClearBindings(itemBindings);
        }

        private bool ValidateReferences()
        {
            if (crackerDefinitions == null || crackerDefinitions.Length == 0 ||
                categories == null || categories.Length == 0 ||
                unlockController == null || inventory == null || loadout == null ||
                catalogPanel == null || openCatalogButton == null || closeButton == null ||
                actionButton == null || categoryButtonsRoot == null || itemGridRoot == null ||
                titleText == null || crackerNameText == null || priceText == null ||
                actionButtonLabel == null)
            {
                Debug.LogError(
                    "[CrackerCatalogUI] Assign the data, systems, existing UI roots, buttons and detail text references in the Inspector.",
                    this);
                return false;
            }

            int validDefinitions = 0;
            foreach (CrackerDefinition definition in crackerDefinitions)
            {
                // Null slots are ignored so future items can be added incrementally.
                if (definition == null)
                    continue;

                validDefinitions++;
                if (definition.Category == null)
                {
                    Debug.LogError(
                        $"[CrackerCatalogUI] '{definition.DisplayName}' has no category. Assign its Category asset in the CrackerDefinition Inspector.",
                        definition);
                    return false;
                }

                bool categoryKnown = false;
                foreach (CrackerCategoryDefinition category in categories)
                {
                    if (category == definition.Category)
                    {
                        categoryKnown = true;
                        break;
                    }
                }

                if (!categoryKnown)
                {
                    Debug.LogError(
                        $"[CrackerCatalogUI] Category for '{definition.DisplayName}' is missing from this component's Categories array.",
                        this);
                    return false;
                }
            }

            if (validDefinitions == 0)
            {
                Debug.LogError("[CrackerCatalogUI] Add at least one valid CrackerDefinition asset.", this);
                return false;
            }

            if (GetReusableButtons(categoryButtonsRoot).Count == 0)
            {
                Debug.LogError(
                    "[CrackerCatalogUI] No clickable category buttons found under DifferentCrackerSelect. Each child needs a Button or an Image.",
                    categoryButtonsRoot);
                return false;
            }

            if (GetReusableButtons(itemGridRoot).Count == 0)
            {
                Debug.LogError(
                    "[CrackerCatalogUI] No clickable tile images/buttons found under GridcrackerParent. Each child needs an Image or Button.",
                    itemGridRoot);
                return false;
            }

            return true;
        }

        private void OpenCatalog()
        {
            catalogPanel.SetActive(true);
            EnsureSelection();

            // Notify the preview controller when the catalog is opened, including
            // the initial selection. The preview controller remains independent
            // from gold, inventory, and gameplay blast rewards.
            if (selectedCracker != null)
                PreviewDefinitionChanged?.Invoke(selectedCracker);

            // The starter Basic Cracker should not keep the global new-content dot active.
            CrackerDefinition basic = unlockController.BasicCrackerDefinition;
            if (basic != null && unlockController.IsNewUnlock(basic))
                unlockController.MarkUnlockViewed(basic);

            RefreshCatalog();
        }

        private void CloseCatalog()
        {
            catalogPanel.SetActive(false);
        }

        private void HandleCategorySelected(CrackerCategoryDefinition category)
        {
            selectedCategory = category;
            selectedCracker = FindFirstDefinition(category);

            if (selectedCracker != null)
                PreviewDefinitionChanged?.Invoke(selectedCracker);

            RefreshCatalog();
        }

        private void HandleCrackerSelected(CrackerDefinition definition)
        {
            selectedCracker = definition;

            if (definition != unlockController.BasicCrackerDefinition &&
                unlockController.IsNewUnlock(definition))
            {
                unlockController.MarkUnlockViewed(definition);
            }

            PreviewDefinitionChanged?.Invoke(definition);
            RefreshCatalog();
        }

        private void HandleActionButton()
        {
            if (selectedCracker == null)
            {
                SetStatus("Select a cracker first.");
                return;
            }

            if (!unlockController.IsUnlocked(selectedCracker))
            {
                SetStatus("This cracker is locked. Progress through the town to unlock it.");
                return;
            }

            int stock = inventory.GetStock(selectedCracker);
            if (stock <= 0)
            {
                int batchCost = inventory.GetBatchCost(selectedCracker);
                if (batchCost <= 0)
                {
                    SetStatus("The cracker's configured price is invalid.");
                    return;
                }

                if (!inventory.TryPurchaseBatch(selectedCracker))
                {
                    SetStatus($"Purchase unavailable. You need {batchCost} gold or your stock has reached its limit.");
                    return;
                }

                RefreshCatalog();
                SetStatus($"Purchased {selectedCracker.PurchaseBatchSize} {selectedCracker.DisplayName}(s). Tap Select to equip.");
                return;
            }

            if (!loadout.TryAssignToFirstEmptySlot(selectedCracker, out int slotIndex))
            {
                SetStatus("All three quick slots are occupied. There is no empty slot for this cracker type.");
                return;
            }

            if (selectedCrackerText != null)
                selectedCrackerText.text = $"Selected: {selectedCracker.DisplayName}";

            if (unlockController.IsNewUnlock(selectedCracker))
                unlockController.MarkUnlockViewed(selectedCracker);

            SetStatus($"Equipped {selectedCracker.DisplayName} in quick slot {slotIndex + 1}.");
            CloseCatalog();
        }

        private void HandleCatalogStateChanged()
        {
            RefreshCatalog();
        }

        private void HandleStockChanged(CrackerDefinition definition, int quantity)
        {
            RefreshCatalog();
        }

        private List<CrackerCategoryDefinition> GetAvailableCategories()
        {
            var result = new List<CrackerCategoryDefinition>();
            if (categories == null || crackerDefinitions == null)
                return result;

            foreach (CrackerCategoryDefinition category in categories)
            {
                if (category == null || result.Contains(category))
                    continue;

                foreach (CrackerDefinition definition in crackerDefinitions)
                {
                    if (definition != null && definition.Category == category)
                    {
                        result.Add(category);
                        break;
                    }
                }
            }

            result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return result;
        }

        private CrackerDefinition FindFirstDefinition(CrackerCategoryDefinition category)
        {
            if (crackerDefinitions == null)
                return null;

            foreach (CrackerDefinition definition in crackerDefinitions)
            {
                if (definition != null && definition.Category == category)
                    return definition;
            }

            return null;
        }

        private List<CrackerDefinition> GetDefinitionsInCategory(CrackerCategoryDefinition category)
        {
            var result = new List<CrackerDefinition>();
            if (crackerDefinitions == null || category == null)
                return result;

            foreach (CrackerDefinition definition in crackerDefinitions)
            {
                if (definition != null && definition.Category == category)
                    result.Add(definition);
            }

            return result;
        }

        private void EnsureSelection()
        {
            List<CrackerCategoryDefinition> available = GetAvailableCategories();
            if (available.Count == 0)
            {
                selectedCategory = null;
                selectedCracker = null;
                return;
            }

            if (selectedCategory == null || !available.Contains(selectedCategory))
                selectedCategory = available[0];

            if (selectedCracker == null || selectedCracker.Category != selectedCategory)
                selectedCracker = FindFirstDefinition(selectedCategory);
        }

        private void RefreshCatalog()
        {
            if (unlockController == null || inventory == null ||
                crackerDefinitions == null || categories == null)
                return;

            EnsureSelection();

            if (titleText != null)
                titleText.text = "CRACKER CATALOG";

            if (catalogNotificationDot != null)
                catalogNotificationDot.SetActive(unlockController.HasUnseenUnlocks);

            RefreshExistingCategoryButtons();
            RefreshExistingItemButtons();
            RefreshDetails();
        }

        private void RefreshExistingCategoryButtons()
        {
            ClearBindings(categoryBindings);
            List<Button> buttons = GetReusableButtons(categoryButtonsRoot);
            List<CrackerCategoryDefinition> available = GetAvailableCategories();

            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                Transform childRoot = FindChildRootForButton(categoryButtonsRoot, button);
                bool hasCategory = i < available.Count;
                if (childRoot != null)
                    childRoot.gameObject.SetActive(hasCategory);
                else
                    button.gameObject.SetActive(hasCategory);

                if (!hasCategory)
                    continue;

                CrackerCategoryDefinition category = available[i];
                Image icon = FindIconImage(button);
                if (icon != null)
                {
                    // Keep the existing sprite if this category has no icon asset assigned yet.
                    if (category.Icon != null)
                        icon.sprite = category.Icon;
                    icon.preserveAspect = true;
                    icon.color = category == selectedCategory ? selectedIconTint : Color.white;
                }

                button.interactable = true;
                CrackerCategoryDefinition capturedCategory = category;
                UnityAction action = () => HandleCategorySelected(capturedCategory);
                button.onClick.AddListener(action);
                categoryBindings.Add(new ButtonBinding { Button = button, Action = action });
            }
        }

        private void RefreshExistingItemButtons()
        {
            ClearBindings(itemBindings);
            List<Button> buttons = GetReusableButtons(itemGridRoot);
            List<CrackerDefinition> definitions = GetDefinitionsInCategory(selectedCategory);

            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                Transform childRoot = FindChildRootForButton(itemGridRoot, button);
                bool hasDefinition = i < definitions.Count;
                if (childRoot != null)
                    childRoot.gameObject.SetActive(hasDefinition);
                else
                    button.gameObject.SetActive(hasDefinition);

                if (!hasDefinition)
                    continue;

                CrackerDefinition definition = definitions[i];
                bool unlocked = unlockController.IsUnlocked(definition);
                Image icon = FindIconImage(button);
                if (icon != null)
                {
                    // Do not erase the current placeholder artwork if no sprite is assigned.
                    if (definition.PreviewIcon != null)
                        icon.sprite = definition.PreviewIcon;
                    icon.preserveAspect = true;
                    icon.color = !unlocked
                        ? lockedIconTint
                        : definition == selectedCracker ? selectedIconTint : Color.white;
                }

                // Let players tap locked items to inspect their details too.
                button.interactable = true;
                CrackerDefinition capturedDefinition = definition;
                UnityAction action = () => HandleCrackerSelected(capturedDefinition);
                button.onClick.AddListener(action);
                itemBindings.Add(new ButtonBinding { Button = button, Action = action });
            }
        }

        private void RefreshDetails()
        {
            if (selectedCracker == null)
            {
                if (crackerNameText != null) crackerNameText.text = "No cracker available";
                if (priceText != null) priceText.text = string.Empty;
                if (actionButtonLabel != null) actionButtonLabel.text = "UNAVAILABLE";
                if (actionButton != null) actionButton.interactable = false;
                SetStatus("No cracker definitions are assigned to the selected category.");
                return;
            }

            if (crackerNameText != null)
                crackerNameText.text = selectedCracker.DisplayName;

            int batchCost = inventory.GetBatchCost(selectedCracker);
            if (priceText != null)
            {
                priceText.text =
                    $"Cost: {selectedCracker.UnitPrice} gold each\n" +
                    $"Pack of {selectedCracker.PurchaseBatchSize}: {batchCost} gold\n" +
                    $"Blast reward: +{selectedCracker.BlastReward} gold each";
            }

            bool unlocked = unlockController.IsUnlocked(selectedCracker);
            int stock = inventory.GetStock(selectedCracker);

            if (!unlocked)
            {
                if (actionButtonLabel != null) actionButtonLabel.text = "LOCKED";
                if (actionButton != null) actionButton.interactable = false;
                SetStatus("Unlock this cracker by progressing through the town.");
            }
            else if (stock <= 0)
            {
                if (actionButtonLabel != null)
                    actionButtonLabel.text = $"BUY {selectedCracker.PurchaseBatchSize} — {batchCost} GOLD";
                if (actionButton != null) actionButton.interactable = true;
                SetStatus("Purchase a pack to add crackers to your stock.");
            }
            else
            {
                if (actionButtonLabel != null) actionButtonLabel.text = "SELECT";
                if (actionButton != null) actionButton.interactable = true;
                SetStatus($"Available stock: {stock}. Select to equip this cracker.");
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private static List<Button> GetReusableButtons(Transform root)
        {
            var result = new List<Button>();
            if (root == null)
                return result;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Button button = child.GetComponent<Button>();

                if (button == null)
                {
                    Image image = child.GetComponent<Image>();
                    if (image == null)
                        image = child.GetComponentInChildren<Image>(true);

                    if (image != null)
                    {
                        button = child.gameObject.AddComponent<Button>();
                        button.targetGraphic = image;
                    }
                }
                else if (button.targetGraphic == null)
                {
                    Image image = child.GetComponent<Image>();
                    if (image == null)
                        image = child.GetComponentInChildren<Image>(true);
                    if (image != null)
                        button.targetGraphic = image;
                }

                if (button != null)
                    result.Add(button);
            }

            return result;
        }

        private static Transform FindChildRootForButton(Transform parent, Button button)
        {
            if (parent == null || button == null)
                return null;

            Transform current = button.transform;
            while (current.parent != null && current.parent != parent)
                current = current.parent;

            return current.parent == parent ? current : button.transform;
        }

        private static Image FindIconImage(Button button)
        {
            if (button == null)
                return null;

            // Prefer the button's own image, which is the structure in the current UI.
            Image ownImage = button.GetComponent<Image>();
            if (ownImage != null)
                return ownImage;

            Image[] images = button.GetComponentsInChildren<Image>(true);
            return images.Length > 0 ? images[0] : null;
        }

        private static void ClearBindings(List<ButtonBinding> bindings)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                ButtonBinding binding = bindings[i];
                if (binding.Button != null && binding.Action != null)
                    binding.Button.onClick.RemoveListener(binding.Action);
            }
            bindings.Clear();
        }
    }
}
