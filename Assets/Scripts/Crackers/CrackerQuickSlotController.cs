
using System;
using UnityEngine;

namespace DiwaliTown.Crackers
{
    [DisallowMultipleComponent]
    public sealed class CrackerQuickSlotController : MonoBehaviour
    {
        public const int SlotCount = 3;

        private readonly CrackerDefinition[] slots =
            new CrackerDefinition[SlotCount];

        private int selectedSlotIndex = -1;

        public int SelectedSlotIndex => selectedSlotIndex;

        public CrackerDefinition SelectedCracker =>
            selectedSlotIndex >= 0
                ? slots[selectedSlotIndex]
                : null;

        public event Action SlotsChanged;

        public event Action<int, CrackerDefinition>
            SlotSelectionChanged;

        public event Action<int, CrackerDefinition>
            SlotActivated;

        public CrackerDefinition GetSlotItem(int index)
        {
            if (index < 0 || index >= SlotCount)
            {
                return null;
            }

            return slots[index];
        }

        public bool TryAssignToFirstEmptySlot(
            CrackerDefinition definition,
            out int assignedIndex)
        {
            assignedIndex = -1;

            if (definition == null)
            {
                return false;
            }

            // Reuse an existing slot for this cracker type.
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == definition)
                {
                    assignedIndex = i;
                    return true;
                }
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null)
                {
                    continue;
                }

                slots[i] = definition;
                assignedIndex = i;

                SlotsChanged?.Invoke();
                return true;
            }

            return false;
        }

        public bool TryActivateSlot(int index)
        {
            if (index < 0 ||
                index >= SlotCount ||
                slots[index] == null)
            {
                return false;
            }

            selectedSlotIndex = index;

            SlotSelectionChanged?.Invoke(
                index,
                slots[index]
            );

            SlotActivated?.Invoke(
                index,
                slots[index]
            );

            return true;
        }
    }
}
