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
        public event Action<int, CrackerDefinition> SlotSelectionChanged;

        public CrackerDefinition GetSlotItem(int index)
        {
            if (index < 0 || index >= SlotCount)
                return null;

            return slots[index];
        }

        public bool TryAssignToFirstEmptySlot(
            CrackerDefinition definition,
            out int assignedIndex)
        {
            assignedIndex = -1;

            if (definition == null)
                return false;

            // Don't assign the same cracker type twice.
            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == definition)
                {
                    assignedIndex = i;
                    TrySelectSlot(i);
                    return true;
                }
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] != null)
                    continue;

                slots[i] = definition;
                assignedIndex = i;

                SlotsChanged?.Invoke();
                TrySelectSlot(i);

                return true;
            }

            return false;
        }

        public bool TrySelectSlot(int index)
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

            return true;
        }
    }
}