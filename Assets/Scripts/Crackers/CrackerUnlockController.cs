
using System;
using System.Collections.Generic;
using DiwaliTown.Diyas;
using UnityEngine;

namespace DiwaliTown.Crackers
{
    [DisallowMultipleComponent]
    public sealed class CrackerUnlockController : MonoBehaviour
    {
        [Header("Progression")]
        [SerializeField]
        private SacredFlameController sacredFlameController;

        [SerializeField]
        private CrackerDefinition basicCracker;

        [SerializeField]
        private CrackerUnlockRule[] unlockRules;

        private readonly HashSet<string> unlockedCrackerIds =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly HashSet<string> newUnlockIds =
            new HashSet<string>(StringComparer.Ordinal);

        public CrackerDefinition BasicCrackerDefinition => basicCracker;

        public bool IsBasicCrackerUnlocked => IsUnlocked(basicCracker);

        public IReadOnlyCollection<string> UnlockedCrackerIds =>
            unlockedCrackerIds;

        public IReadOnlyCollection<string> NewUnlockIds => newUnlockIds;

        public bool HasUnseenUnlocks => newUnlockIds.Count > 0;

        public event Action BasicCrackerUnlocked;
        public event Action<CrackerDefinition> CrackerUnlocked;
        public event Action UnlockStateChanged;
        public event Action NewUnlocksChanged;

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            sacredFlameController.DiyaLit += HandleDiyaLit;

            EvaluateUnlocksFromCurrentProgress();
        }

        private void OnDisable()
        {
            if (sacredFlameController != null)
            {
                sacredFlameController.DiyaLit -= HandleDiyaLit;
            }
        }

        private bool ValidateReferences()
        {
            if (sacredFlameController == null ||
                basicCracker == null ||
                unlockRules == null ||
                unlockRules.Length == 0)
            {
                Debug.LogError(
                    "[CrackerUnlock] Assign Sacred Flame Controller, " +
                    "Basic Cracker and at least one unlock rule.",
                    this
                );

                return false;
            }

            var ruleIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (CrackerUnlockRule rule in unlockRules)
            {
                if (rule == null ||
                    rule.Definition == null ||
                    string.IsNullOrWhiteSpace(rule.Definition.CrackerId) ||
                    rule.RequiredLitDiyas < 1 ||
                    !ruleIds.Add(rule.Definition.CrackerId))
                {
                    Debug.LogError(
                        "[CrackerUnlock] Each rule must reference a valid, " +
                        "unique cracker definition and diya threshold.",
                        this
                    );

                    return false;
                }
            }

            if (!ruleIds.Contains(basicCracker.CrackerId))
            {
                Debug.LogError(
                    "[CrackerUnlock] Basic Cracker needs an unlock rule.",
                    this
                );

                return false;
            }

            return true;
        }

        private void HandleDiyaLit(DiyaAnchor diya)
        {
            EvaluateUnlocksFromCurrentProgress();
        }

        public void EvaluateUnlocksFromCurrentProgress()
        {
            if (sacredFlameController == null || unlockRules == null)
                return;

            int litCount = 0;

            foreach (DiyaAnchor diya in sacredFlameController.RequiredDiyas)
            {
                if (diya != null && diya.IsLit)
                    litCount++;
            }

            foreach (CrackerUnlockRule rule in unlockRules)
            {
                if (rule != null &&
                    litCount >= rule.RequiredLitDiyas)
                {
                    TryUnlock(rule.Definition);
                }
            }
        }

        public bool IsUnlocked(CrackerDefinition definition)
        {
            return definition != null &&
                   !string.IsNullOrWhiteSpace(definition.CrackerId) &&
                   unlockedCrackerIds.Contains(definition.CrackerId);
        }

        public bool IsNewUnlock(CrackerDefinition definition)
        {
            return definition != null &&
                   newUnlockIds.Contains(definition.CrackerId);
        }

        public bool TryUnlock(CrackerDefinition definition)
        {
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.CrackerId))
            {
                return false;
            }

            if (!unlockedCrackerIds.Add(definition.CrackerId))
                return false;

            newUnlockIds.Add(definition.CrackerId);

            Debug.Log(
                $"[CrackerUnlock] Unlocked: {definition.DisplayName}"
            );

            CrackerUnlocked?.Invoke(definition);

            if (definition == basicCracker)
                BasicCrackerUnlocked?.Invoke();

            UnlockStateChanged?.Invoke();
            NewUnlocksChanged?.Invoke();

            return true;
        }

        public void MarkUnlockViewed(CrackerDefinition definition)
        {
            if (definition == null)
                return;

            if (newUnlockIds.Remove(definition.CrackerId))
                NewUnlocksChanged?.Invoke();
        }

        public void RestoreUnlockedCrackerIds(
            IEnumerable<string> savedIds,
            IReadOnlyList<CrackerDefinition> knownDefinitions)
        {
            RestoreUnlockedCrackerIds(
                savedIds,
                knownDefinitions,
                null
            );
        }

        public void RestoreUnlockedCrackerIds(
            IEnumerable<string> savedIds,
            IReadOnlyList<CrackerDefinition> knownDefinitions,
            IEnumerable<string> savedNewUnlockIds)
        {
            unlockedCrackerIds.Clear();
            newUnlockIds.Clear();

            var validIds = new HashSet<string>(StringComparer.Ordinal);

            if (knownDefinitions != null)
            {
                foreach (CrackerDefinition definition in knownDefinitions)
                {
                    if (definition != null &&
                        !string.IsNullOrWhiteSpace(definition.CrackerId))
                    {
                        validIds.Add(definition.CrackerId);
                    }
                }
            }

            if (savedIds != null)
            {
                foreach (string id in savedIds)
                {
                    if (!string.IsNullOrWhiteSpace(id) && validIds.Contains(id))
                        unlockedCrackerIds.Add(id);
                }
            }

            if (savedNewUnlockIds != null)
            {
                foreach (string id in savedNewUnlockIds)
                {
                    if (unlockedCrackerIds.Contains(id))
                        newUnlockIds.Add(id);
                }
            }

            UnlockStateChanged?.Invoke();
            NewUnlocksChanged?.Invoke();
        }
    }
}
