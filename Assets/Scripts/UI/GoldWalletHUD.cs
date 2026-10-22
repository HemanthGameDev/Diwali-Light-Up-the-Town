using DiwaliTown.Economy;
using TMPro;
using UnityEngine;

namespace DiwaliTown.UI
{
    [DisallowMultipleComponent]
    public sealed class GoldWalletHUD : MonoBehaviour
    {
        [SerializeField] private GoldWallet wallet;
        [SerializeField] private TMP_Text balanceText;

        private void OnEnable()
        {
            if (wallet == null || balanceText == null)
            {
                Debug.LogError(
                    "[GoldWalletHUD] Assign the wallet and balance text.",
                    this
                );

                enabled = false;
                return;
            }

            wallet.BalanceChanged += HandleBalanceChanged;
            Refresh(wallet.CurrentGold);
        }

        private void Start()
        {
            // Refresh after scene components have initialized.
            if (wallet != null && balanceText != null)
            {
                Refresh(wallet.CurrentGold);
            }
        }

        private void OnDisable()
        {
            if (wallet != null)
            {
                wallet.BalanceChanged -= HandleBalanceChanged;
            }
        }

        private void HandleBalanceChanged(int newBalance)
        {
            Refresh(newBalance);
        }

        private void Refresh(int balance)
        {
            balanceText.text = $"Gold: {balance}";
        }
    }
}