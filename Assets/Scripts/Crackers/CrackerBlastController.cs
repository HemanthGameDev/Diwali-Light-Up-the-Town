
using System;
using System.Collections.Generic;
using DiwaliTown.Economy;
using UnityEngine;

namespace DiwaliTown.Crackers
{
    [DisallowMultipleComponent]
    public sealed class CrackerBlastController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GoldWallet wallet;
        [SerializeField] private CrackerPlacementController placementController;

        private bool isProcessing;

        public int PendingCount =>
            placementController != null
                ? placementController.PendingCount
                : 0;

        public event Action<int, int> BlastCompleted;

        public bool TryFireAll(
            out int ignitedCount,
            out int totalReward)
        {
            ignitedCount = 0;
            totalReward = 0;

            if (isProcessing ||
                wallet == null ||
                placementController == null ||
                placementController.PendingCount == 0)
            {
                return false;
            }

            isProcessing = true;

            try
            {
                // Work on a snapshot because the pending list changes
                // while crackers are processed.
                var pending = new List<PlacedCracker>(
                    placementController.PendingCrackers
                );

                foreach (PlacedCracker cracker in pending)
                {
                    if (cracker == null)
                    {
                        continue;
                    }

                    CrackerDefinition definition = cracker.Definition;

                    if (definition == null)
                    {
                        Debug.LogWarning(
                            "[CrackerBlast] Skipping cracker with no definition.",
                            cracker
                        );
                        continue;
                    }

                    if (cracker.HasBeenIgnited)
                    {
                        placementController.TryRemovePendingCracker(cracker);
                        continue;
                    }

                    int reward = Mathf.Max(0, definition.BlastReward);

                    // Don't ignite a cracker if its reward would overflow
                    // the wallet's integer balance.
                    if (reward > 0 &&
                        reward > int.MaxValue - wallet.CurrentGold)
                    {
                        Debug.LogWarning(
                            "[CrackerBlast] Wallet cannot accommodate reward.",
                            cracker
                        );
                        continue;
                    }

                    if (!cracker.TryMarkIgnited())
                    {
                        continue;
                    }

                    SpawnBlastEffect(definition, cracker.transform.position);

                    if (reward > 0)
                    {
                        if (!wallet.TryAdd(reward))
                        {
                            Debug.LogError(
                                "[CrackerBlast] Could not add the blast reward.",
                                this
                            );

                            continue;
                        }

                        totalReward += reward;
                    }

                    placementController.TryRemovePendingCracker(cracker);

                    Destroy(cracker.gameObject);

                    ignitedCount++;
                }
            }
            finally
            {
                isProcessing = false;
            }

            if (ignitedCount == 0)
            {
                return false;
            }

            BlastCompleted?.Invoke(ignitedCount, totalReward);
            return true;
        }

        private static void SpawnBlastEffect(
            CrackerDefinition definition,
            Vector3 position)
        {
            if (definition.BlastEffectPrefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(
                definition.BlastEffectPrefab,
                position,
                Quaternion.identity
            );

            Destroy(
                effect,
                Mathf.Max(0.1f, definition.BlastEffectLifetime)
            );
        }
    }
}
