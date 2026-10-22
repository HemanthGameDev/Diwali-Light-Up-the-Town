
using System;
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

        public bool IsBasicCrackerUnlocked { get; private set; }

        public event Action BasicCrackerUnlocked;

        private void OnEnable()
        {
            if (sacredFlameController == null)
            {
                Debug.LogError(
                    "[CrackerUnlock] Sacred Flame Controller is missing.",
                    this
                );

                enabled = false;
                return;
            }

            sacredFlameController.FirstDiyaLit += HandleFirstDiyaLit;
        }

        private void OnDisable()
        {
            if (sacredFlameController != null)
            {
                sacredFlameController.FirstDiyaLit -= HandleFirstDiyaLit;
            }
        }

        private void HandleFirstDiyaLit()
        {
            UnlockBasicCracker();
        }

        private void UnlockBasicCracker()
        {
            if (IsBasicCrackerUnlocked)
            {
                return;
            }

            IsBasicCrackerUnlocked = true;

            Debug.Log("[CrackerUnlock] Basic Cracker unlocked!");

            BasicCrackerUnlocked?.Invoke();
        }
    }
}
