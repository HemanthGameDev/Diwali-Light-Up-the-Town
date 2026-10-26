
using System;
using System.Collections.Generic;
using System.IO;
using DiwaliTown.Diyas;
using DiwaliTown.Economy;
using DiwaliTown.Town;
using UnityEngine;
using DiwaliTown.Crackers;

namespace DiwaliTown.Saving
{
    [DisallowMultipleComponent]
    public sealed class GameSaveService : MonoBehaviour
    {
        private const int CurrentSchemaVersion = 2;
        private const string SaveFileName = "diwali-town-save.json";

        [Header("References")]
        [SerializeField] private GoldWallet wallet;
        [SerializeField] private SacredFlameController sacredFlameController;
        [SerializeField] private TownAreaProgression townAreaProgression;
        [SerializeField] private CrackerUnlockController crackerUnlockController;
        [SerializeField] private CrackerInventory crackerInventory;
        [SerializeField] private CrackerQuickSlotController crackerQuickSlotController;

        [SerializeField]
        private CrackerPlacementController crackerPlacementController;

        [Header("Known Cracker Definitions")]
        [SerializeField] private CrackerDefinition[] knownCrackerDefinitions;

        [Header("Development")]
        [SerializeField] private bool logSaveOperations;


        private GameSaveData saveData;
        private bool initialized;
        private bool isLoading;
        private bool isDirty;
        private bool loadFailed;

        private string SavePath =>
            Path.Combine(
                Application.persistentDataPath,
                SaveFileName
            );

        private void OnEnable()
        {
            if (wallet != null)
                wallet.BalanceChanged += HandleBalanceChanged;

            if (sacredFlameController != null)
                sacredFlameController.DiyaLit += HandleDiyaLit;

            if (townAreaProgression != null)
                townAreaProgression.AreaCompleted += HandleAreaCompleted;

            if (crackerUnlockController != null)
                crackerUnlockController.UnlockStateChanged += HandleCrackerUnlockChanged;

            if (crackerInventory != null)
                crackerInventory.StockChanged += HandleCrackerStockChanged;

            if (crackerQuickSlotController != null)
                crackerQuickSlotController.SlotsChanged += HandleQuickSlotsChanged;

            if (crackerPlacementController != null)
            {
                crackerPlacementController.PendingCountChanged +=
                    HandlePendingCountChanged;
            }
        }

        private void Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            isLoading = true;
            Debug.Log(
    $"[SAVE DIAG] START | " +
    $"Platform={Application.platform} | " +
    $"Package={Application.identifier} | " +
    $"Path={SavePath} | " +
    $"FileExists={File.Exists(SavePath)}"
);

            bool hasSave = TryLoadFromDisk();
            Debug.Log(
    $"[SAVE DIAG] LOAD RESULT | " +
    $"HasSave={hasSave} | " +
    $"LoadFailed={loadFailed}"
);

            if (loadFailed)
            {
                isLoading = false;
                enabled = false;
                return;
            }

            if (hasSave)
            {
                RestoreGameState();
            }
            else
            {
                saveData = new GameSaveData();
            }

            isLoading = false;
            initialized = true;
            isLoading = false;
            initialized = true;

            isDirty = true;
            SaveNow();


            if (!hasSave)
            {
                isDirty = true;
                SaveNow();
            }
        }

        private bool ValidateReferences()
        {
            if (wallet == null ||
                sacredFlameController == null ||
                townAreaProgression == null ||
                crackerUnlockController == null ||
                crackerInventory == null ||
                crackerQuickSlotController == null ||
                knownCrackerDefinitions == null || crackerPlacementController == null ||
                knownCrackerDefinitions.Length == 0)
            {
                Debug.LogError(
                    "[GameSaveService] Assign Wallet, Sacred Flame " +
                    "Controller and Town Area Progression.",
                    this
                );

                return false;
            }

            IReadOnlyList<DiyaAnchor> diyas =
                sacredFlameController.RequiredDiyas;

            if (diyas == null || diyas.Count == 0)
            {
                Debug.LogError(
                    "[GameSaveService] No required diyas are assigned.",
                    this
                );

                return false;
            }

            var crackerIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (CrackerDefinition definition in knownCrackerDefinitions)
            {
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.CrackerId) ||
                    !crackerIds.Add(definition.CrackerId))
                {
                    Debug.LogError(
                        "[GameSaveService] Cracker definitions must have " +
                        "unique, non-empty IDs.",
                        this
                    );

                    return false;
                }
            }

            CrackerDefinition basicCracker =
                crackerUnlockController.BasicCrackerDefinition;

            if (basicCracker == null ||
                !crackerIds.Contains(basicCracker.CrackerId))
            {
                Debug.LogError(
                    "[GameSaveService] The Basic Cracker must be included " +
                    "in Known Cracker Definitions.",
                    this
                );

                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (DiyaAnchor diya in diyas)
            {
                if (diya == null ||
                    string.IsNullOrWhiteSpace(diya.PersistentId) ||
                    !ids.Add(diya.PersistentId))
                {
                    Debug.LogError(
                        "[GameSaveService] Every required diya needs a " +
                        "unique, non-empty Persistent ID.",
                        this
                    );

                    return false;
                }
            }

            return true;
        }

        private CrackerDefinition FindDefinitionById(string crackerId)
        {
            if (string.IsNullOrWhiteSpace(crackerId) ||
                knownCrackerDefinitions == null)
            {
                return null;
            }

            foreach (CrackerDefinition definition in knownCrackerDefinitions)
            {
                if (definition != null &&
                    string.Equals(
                        definition.CrackerId,
                        crackerId,
                        StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private bool TryLoadFromDisk()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[SAVE DIAG] No save file found. Starting a new game.");
                return false;
            }

            string json;

            // First, read the file. If this fails, don't overwrite it.
            try
            {
                json = File.ReadAllText(SavePath);
            }
            catch (Exception exception)
            {
                loadFailed = true;

                Debug.LogError(
                    "[GameSaveService] Could not read the save file. " +
                    "Existing data has been preserved.\n" + exception,
                    this
                );

                return false;
            }

            Debug.Log(
                $"[SAVE DIAG] Read save file: {json.Length} characters."
            );

            // An existing but empty file is not a usable save.
            if (string.IsNullOrWhiteSpace(json))
            {
                return HandleInvalidSaveFile(
                    "The save file is empty or contains only whitespace."
                );
            }

            GameSaveData loaded;

            try
            {
                loaded = JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (Exception exception)
            {
                return HandleInvalidSaveFile(
                    "JSON deserialization failed: " + exception.Message
                );
            }

            if (loaded == null)
            {
                return HandleInvalidSaveFile(
                    "Unity returned null while loading the save."
                );
            }

            // Never overwrite a save created by a newer version of the game.
            if (loaded.schemaVersion > CurrentSchemaVersion)
            {
                loadFailed = true;

                Debug.LogError(
                    "[GameSaveService] Save belongs to a newer game version. " +
                    "It has been left untouched.",
                    this
                );

                return false;
            }

            EnsureCollectionsExist(loaded);

            saveData = loaded;
            saveData.schemaVersion = CurrentSchemaVersion;

            Debug.Log(
                $"[SAVE DIAG] Save loaded successfully. " +
                $"Schema={saveData.schemaVersion}, Gold={saveData.goldBalance}"
            );

            return true;
        }

        private bool HandleInvalidSaveFile(string reason)
        {
            Debug.LogWarning(
                $"[GameSaveService] Invalid save detected. {reason}"
            );

            // Preserve the original file before creating a fresh save.
            try
            {
                string backupPath = SavePath + ".invalid-backup";

                if (File.Exists(backupPath))
                {
                    backupPath = SavePath +
                                 ".invalid-backup-" +
                                 DateTime.UtcNow.Ticks;
                }

                File.Copy(SavePath, backupPath, false);

                Debug.LogWarning(
                    $"[GameSaveService] Invalid save backed up to: {backupPath}. " +
                    "Starting a fresh save."
                );

                // Keep loadFailed false. Start() can initialize a new save.
                return false;
            }
            catch (Exception exception)
            {
                // If the backup fails, preserve the existing file and stop.
                loadFailed = true;

                Debug.LogError(
                    "[GameSaveService] Could not back up the invalid save. " +
                    "The original file will not be overwritten.\n" + exception,
                    this
                );

                return false;
            }
        }


        private static void EnsureCollectionsExist(GameSaveData data)
        {
            if (data.litDiyaIds == null)
                data.litDiyaIds = new List<string>();

            if (data.unlockedCrackerIds == null)
                data.unlockedCrackerIds = new List<string>();

            if (data.crackerInventory == null)
                data.crackerInventory =
                    new List<CrackerInventorySaveEntry>();

            if (data.quickSlotCrackerIds == null)
                data.quickSlotCrackerIds = new List<string>();

            if (data.pendingPlacedCrackers == null)
            {
                data.pendingPlacedCrackers =
                    new List<PlacedCrackerSaveEntry>();
            }

            if (data.newUnlockNotificationIds == null)
            {
                data.newUnlockNotificationIds = new List<string>();
            }
        }

        private void RestoreGameState()
        {
            wallet.RestoreBalance(Mathf.Max(0, saveData.goldBalance));

            var litIds = new HashSet<string>(
                saveData.litDiyaIds,
                StringComparer.Ordinal
            );

            bool anyDiyaLit = false;

            foreach (DiyaAnchor diya in sacredFlameController.RequiredDiyas)
            {
                bool shouldBeLit = litIds.Contains(diya.PersistentId);

                diya.RestoreLitState(shouldBeLit);
                anyDiyaLit |= shouldBeLit;
            }

            // 1. Restore unlocked cracker types.
            crackerUnlockController.RestoreUnlockedCrackerIds(
     saveData.unlockedCrackerIds,
     knownCrackerDefinitions,
     saveData.newUnlockNotificationIds
 );

            crackerUnlockController.EvaluateUnlocksFromCurrentProgress();

            // 2. Restore purchased stock without spending gold.
            crackerInventory.ClearAllStock();

            foreach (CrackerInventorySaveEntry entry in saveData.crackerInventory)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.crackerId))
                    continue;

                CrackerDefinition definition =
                    FindDefinitionById(entry.crackerId);

                if (definition == null)
                    continue;

                crackerInventory.RestoreStock(
                    definition,
                    Mathf.Max(0, entry.quantity)
                );
            }

            // 3. Restore the three quick-slot assignments.
            var restoredSlots = new CrackerDefinition[
                CrackerQuickSlotController.SlotCount
            ];

            for (int i = 0;
                 i < restoredSlots.Length &&
                 i < saveData.quickSlotCrackerIds.Count;
                 i++)
            {
                restoredSlots[i] = FindDefinitionById(
                    saveData.quickSlotCrackerIds[i]
                );
            }

            crackerQuickSlotController.RestoreSlots(restoredSlots);
            // If any diya is already lit, the first-diya reward must not
            // be awarded again, even if an older save omitted this flag.
            bool firstRewardAlreadyGranted =
                saveData.firstDiyaRewardGranted || anyDiyaLit;

            sacredFlameController.RestoreProgress(
                saveData.hasSacredFlameCharge,
                firstRewardAlreadyGranted
            );

            foreach (PlacedCrackerSaveEntry entry
         in saveData.pendingPlacedCrackers)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.crackerId))
                {
                    continue;
                }

                CrackerDefinition definition =
                    FindDefinitionById(entry.crackerId);

                if (definition == null)
                {
                    Debug.LogWarning(
                        $"[GameSaveService] Unknown cracker ID: " +
                        $"{entry.crackerId}",
                        this
                    );

                    continue;
                }

                bool restored =
                    crackerPlacementController.TryRestorePendingCracker(
                        definition,
                        entry.position,
                        entry.rotation
                    );

                if (!restored)
                {
                    Debug.LogError(
                        $"[GameSaveService] Failed to restore placed cracker: " +
                        $"{entry.crackerId}",
                        this
                    );
                }
            }

            // Area completion is derived from the restored required diyas.
            townAreaProgression.RefreshFromRestoredDiyas();

            if (logSaveOperations)
            {
                Debug.Log(
                    $"[GameSaveService] Restored gold={wallet.CurrentGold}, " +
                    $"litDiyas={litIds.Count}, " +
                    $"sacredFlameReady={sacredFlameController.HasCharge}",
                    this
                );
            }
        }

        private void HandleBalanceChanged(int newBalance)
        {
            MarkDirty();
        }

        private void HandleDiyaLit(DiyaAnchor diya)
        {
            MarkDirty();
        }

        private void HandleAreaCompleted()
        {
            MarkDirty();
        }

        private void HandlePendingCountChanged(int pendingCount)
        {
            MarkDirty();
        }
        private void MarkDirty()
        {
            if (initialized && !isLoading)
                isDirty = true;
        }

        private void LateUpdate()
        {
            if (initialized && isDirty)
                SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveNow();
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }


        private void OnDisable()
        {
            // Do not save here. During scene shutdown, referenced objects
            // may already be destroyed, which can overwrite valid save data.

            if (wallet != null)
                wallet.BalanceChanged -= HandleBalanceChanged;

            if (sacredFlameController != null)
                sacredFlameController.DiyaLit -= HandleDiyaLit;

            if (townAreaProgression != null)
                townAreaProgression.AreaCompleted -= HandleAreaCompleted;

            if (crackerUnlockController != null)
                crackerUnlockController.UnlockStateChanged -= HandleCrackerUnlockChanged;

            if (crackerInventory != null)
                crackerInventory.StockChanged -= HandleCrackerStockChanged;

            if (crackerQuickSlotController != null)
                crackerQuickSlotController.SlotsChanged -= HandleQuickSlotsChanged;

            if (crackerPlacementController != null)
            {
                crackerPlacementController.PendingCountChanged -=
                    HandlePendingCountChanged;
            }
        }


        private void HandleCrackerUnlockChanged()
        {
            MarkDirty();
        }

        private void HandleCrackerStockChanged(
            CrackerDefinition definition,
            int quantity)
        {
            MarkDirty();
        }

        private void HandleQuickSlotsChanged()
        {
            MarkDirty();
        }
        private void CaptureCurrentState()
        {
            saveData.schemaVersion = CurrentSchemaVersion;
            saveData.goldBalance = wallet.CurrentGold;

            saveData.hasSacredFlameCharge =
                sacredFlameController.HasCharge;

            saveData.firstDiyaRewardGranted =
                sacredFlameController.FirstDiyaRewardGranted;

            saveData.isAreaComplete =
                townAreaProgression.IsAreaComplete;

            saveData.litDiyaIds.Clear();

            foreach (DiyaAnchor diya in sacredFlameController.RequiredDiyas)
            {
                if (diya == null)
                {
                    Debug.LogError(
                        "[GameSaveService] A required diya reference is missing.",
                        this
                    );

                    continue;
                }

                if (logSaveOperations)
                {
                    Debug.Log(
                        $"[GameSaveService] Capturing diya: " +
                        $"ID='{diya.PersistentId}', IsLit={diya.IsLit}",
                        diya
                    );
                }

                if (diya.IsLit)
                {
                    saveData.litDiyaIds.Add(diya.PersistentId);
                }
            }

            if (logSaveOperations)
            {
                Debug.Log(
                    $"[GameSaveService] Total saved lit diyas: " +
                    $"{saveData.litDiyaIds.Count}",
                    this
                );
            }

            // Save the unlocked cracker IDs.
            saveData.unlockedCrackerIds.Clear();

            saveData.newUnlockNotificationIds.Clear();

            foreach (string id in crackerUnlockController.NewUnlockIds)
            {
                saveData.newUnlockNotificationIds.Add(id);
            }

            foreach (string crackerId in crackerUnlockController.UnlockedCrackerIds)
            {
                saveData.unlockedCrackerIds.Add(crackerId);
            }

            // Save positive inventory quantities.
            saveData.crackerInventory.Clear();

            foreach (CrackerDefinition definition in knownCrackerDefinitions)
            {
                if (definition == null)
                    continue;

                int quantity = crackerInventory.GetStock(definition);

                if (quantity <= 0)
                    continue;

                saveData.crackerInventory.Add(
                    new CrackerInventorySaveEntry
                    {
                        crackerId = definition.CrackerId,
                        quantity = quantity
                    }
                );
            }

            // Save all three slots, using an empty string for an empty slot.
            saveData.quickSlotCrackerIds.Clear();

            for (int i = 0;
                 i < CrackerQuickSlotController.SlotCount;
                 i++)
            {
                CrackerDefinition definition =
                    crackerQuickSlotController.GetSlotItem(i);

                saveData.quickSlotCrackerIds.Add(
                    definition != null ? definition.CrackerId : string.Empty
                );
            }

            saveData.pendingPlacedCrackers.Clear();

            foreach (PlacedCracker cracker
                     in crackerPlacementController.PendingCrackers)
            {
                if (cracker == null ||
                    cracker.HasBeenIgnited ||
                    cracker.Definition == null)
                {
                    continue;
                }

                saveData.pendingPlacedCrackers.Add(
                    new PlacedCrackerSaveEntry
                    {
                        crackerId = cracker.Definition.CrackerId,
                        position = cracker.transform.position,
                        rotation = cracker.transform.rotation
                    }
                );
            }
        }

        private void SaveNow()
        {
            if (!initialized || saveData == null || wallet == null)
                return;

            CaptureCurrentState();

            try
            {
                Directory.CreateDirectory(
                    Application.persistentDataPath
                );

                string json = JsonUtility.ToJson(saveData, true);
                string temporaryPath = SavePath + ".tmp";

                File.WriteAllText(temporaryPath, json);
                Debug.Log(
    $"[SAVE DIAG] TEMP WRITTEN | " +
    $"Path={temporaryPath} | " +
    $"Bytes={new FileInfo(temporaryPath).Length}"
);

                // The temporary file prevents a partial write from
                // directly overwriting the previous save.
                if (File.Exists(SavePath))
                {
                    File.Copy(temporaryPath, SavePath, true);
                    File.Delete(temporaryPath);
                }
                else
                {
                    File.Move(temporaryPath, SavePath);
                }
                Debug.Log(
    $"[SAVE DIAG] SAVE SUCCESS | " +
    $"FileExists={File.Exists(SavePath)} | " +
    $"Bytes={(File.Exists(SavePath) ? new FileInfo(SavePath).Length : 0)}"
);
                isDirty = false;

                if (logSaveOperations)
                {
                    Debug.Log(
                        $"[GameSaveService] Saved game to {SavePath}",
                        this
                    );
                }
            }
            catch (Exception exception)
            {
                isDirty = true;

                Debug.LogError(
                    "[GameSaveService] Failed to save game data.\n" +
                    exception,
                    this
                );
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Development/Delete Save File (Testing Only)")]
        private void DeleteSaveFileForTesting()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    "Stop Play Mode before deleting the test save.",
                    this
                );

                return;
            }

            try
            {
                if (File.Exists(SavePath))
                    File.Delete(SavePath);

                if (File.Exists(SavePath + ".tmp"))
                    File.Delete(SavePath + ".tmp");

                Debug.Log(
                    "[GameSaveService] Development save cleared.",
                    this
                );
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[GameSaveService] Could not clear the save.\n" +
                    exception,
                    this
                );
            }
        }
#endif
    }
}
