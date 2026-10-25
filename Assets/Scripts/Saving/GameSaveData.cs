using System;
using System.Collections.Generic;

namespace DiwaliTown.Saving
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int schemaVersion = 2;

        public int goldBalance = 50;

        public bool hasSacredFlameCharge;
        public bool firstDiyaRewardGranted;
        public bool isAreaComplete;

        public List<string> litDiyaIds = new List<string>();
        public List<string> unlockedCrackerIds = new List<string>();
        public List<CrackerInventorySaveEntry> crackerInventory =
            new List<CrackerInventorySaveEntry>();

        public List<PlacedCrackerSaveEntry> pendingPlacedCrackers =
    new List<PlacedCrackerSaveEntry>();

        public List<string> newUnlockNotificationIds =
    new List<string>();
        // Three positions: use an empty string for an empty slot.
        public List<string> quickSlotCrackerIds = new List<string>();
    }

    [Serializable]
    public sealed class CrackerInventorySaveEntry
    {
        public string crackerId;
        public int quantity;
    }
    [Serializable]
    public sealed class PlacedCrackerSaveEntry
    {
        public string crackerId;
        public UnityEngine.Vector3 position;
        public UnityEngine.Quaternion rotation;
    }
}