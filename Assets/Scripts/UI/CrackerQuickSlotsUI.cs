using DiwaliTown.Crackers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace DiwaliTown.UI
{
    [DisallowMultipleComponent]
    public sealed class CrackerQuickSlotsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CrackerQuickSlotController loadout;
        [SerializeField] private CrackerInventory inventory;

        [Header("Three Slots")]
        [SerializeField] private Button[] slotButtons = new Button[3];
        [SerializeField] private TMP_Text[] slotLabels = new TMP_Text[3];
        [SerializeField]
        private GameObject[] selectionVisuals =
            new GameObject[3];

        private UnityAction[] clickHandlers;

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            loadout.SlotsChanged += RefreshUI;
            loadout.SlotSelectionChanged += HandleSlotSelectionChanged;
            inventory.StockChanged += HandleStockChanged;

            clickHandlers = new UnityAction[CrackerQuickSlotController.SlotCount];

            for (int i = 0; i < clickHandlers.Length; i++)
            {
                int slotIndex = i;
                clickHandlers[i] = () => HandleSlotClicked(slotIndex);

                slotButtons[i].onClick.AddListener(clickHandlers[i]);
            }

            RefreshUI();
        }

        private void OnDisable()
        {
            if (loadout != null)
            {
                loadout.SlotsChanged -= RefreshUI;
                loadout.SlotSelectionChanged -= HandleSlotSelectionChanged;
            }

            if (inventory != null)
                inventory.StockChanged -= HandleStockChanged;

            if (clickHandlers == null || slotButtons == null)
                return;

            for (int i = 0; i < clickHandlers.Length; i++)
            {
                if (i < slotButtons.Length && slotButtons[i] != null)
                    slotButtons[i].onClick.RemoveListener(clickHandlers[i]);
            }
        }

        private bool ValidateReferences()
        {
            if (loadout == null ||
                inventory == null ||
                slotButtons == null ||
                slotLabels == null ||
                slotButtons.Length != 3 ||
                slotLabels.Length != 3)
            {
                Debug.LogError(
                    "[CrackerQuickSlotsUI] Assign exactly three buttons " +
                    "and three labels, plus the loadout and inventory.",
                    this
                );

                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (slotButtons[i] == null || slotLabels[i] == null)
                {
                    Debug.LogError(
                        $"[CrackerQuickSlotsUI] Slot {i + 1} is missing.",
                        this
                    );

                    return false;
                }
            }

            return true;
        }

        private void HandleSlotClicked(int index)
        {
            loadout.TryActivateSlot(index);
        }

        private void HandleStockChanged(
            CrackerDefinition definition,
            int quantity)
        {
            RefreshUI();
        }

        private void HandleSlotSelectionChanged(
            int index,
            CrackerDefinition definition)
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            for (int i = 0; i < 3; i++)
            {
                CrackerDefinition definition = loadout.GetSlotItem(i);
                int quantity = inventory.GetStock(definition);

                bool available = definition != null && quantity > 0;

                slotButtons[i].interactable = available;

                slotLabels[i].text = definition == null
                    ? $"SLOT {i + 1}\nEmpty"
                    : $"{definition.DisplayName}\n×{quantity}";

                if (selectionVisuals != null &&
                    i < selectionVisuals.Length &&
                    selectionVisuals[i] != null)
                {
                    selectionVisuals[i].SetActive(
                        available && loadout.SelectedSlotIndex == i
                    );
                }
            }
        }
    }
}