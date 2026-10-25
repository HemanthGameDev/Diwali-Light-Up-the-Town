
using System;
using UnityEngine;

namespace DiwaliTown.Economy
{
    [DisallowMultipleComponent]
    public sealed class GoldWallet : MonoBehaviour
    {
        [SerializeField, Min(0)]
        private int startingGold = 50;

        private int currentGold;

        public int CurrentGold => currentGold;

        public event Action<int> BalanceChanged;

        private void Awake()
        {
            currentGold = startingGold;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || amount > currentGold)
            {
                return false;
            }

            SetBalance(currentGold - amount);
            return true;
        }

        public bool TryAdd(int amount)
        {
            if (amount <= 0 || currentGold > int.MaxValue - amount)
            {
                return false;
            }

            SetBalance(currentGold + amount);
            return true;
        }

        private void SetBalance(int newBalance)
        {
            if (newBalance == currentGold)
            {
                return;
            }

            currentGold = newBalance;
            BalanceChanged?.Invoke(currentGold);
        }
        public bool RestoreBalance(int amount)
        {
            if (amount < 0)
            {
                return false;
            }

            SetBalance(amount);
            return true;
        }
    }
}
