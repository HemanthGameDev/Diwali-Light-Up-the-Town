
using System;
using DiwaliTown.Crackers;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DiwaliTown.UI
{
    [DisallowMultipleComponent]
    public sealed class CrackerCatalogItemUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text detailsText;
        [SerializeField] private GameObject notificationDot;
        [SerializeField] private GameObject selectedVisual;

        private CrackerDefinition definition;
        private Action<CrackerDefinition> onSelected;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        public void Bind(
            CrackerDefinition cracker,
            bool isUnlocked,
            bool isNewUnlock,
            bool isSelected,
            int stock,
            Action<CrackerDefinition> selectionCallback)
        {
            if (button == null)
                button = GetComponent<Button>();

            definition = cracker;
            onSelected = selectionCallback;

            if (button == null ||
                nameText == null ||
                detailsText == null)
            {
                Debug.LogError(
                    "[CrackerCatalogItemUI] Missing Button or text references.",
                    this
                );
                return;
            }

            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);

            nameText.text = definition.DisplayName;

            if (iconImage != null)
            {
                iconImage.sprite = definition.PreviewIcon;
                iconImage.enabled = definition.PreviewIcon != null;
            }

            if (!isUnlocked)
            {
                detailsText.text = "LOCKED";
            }
            else if (stock > 0)
            {
                detailsText.text = $"OWNED ×{stock}";
            }
            else
            {
                long packCost =
                    (long)definition.UnitPrice *
                    definition.PurchaseBatchSize;

                detailsText.text =
                    $"{definition.UnitPrice} Gold each\n" +
                    $"Pack: {packCost} Gold";
            }

            if (notificationDot != null)
            {
                notificationDot.SetActive(
                    isUnlocked && isNewUnlock
                );
            }

            if (selectedVisual != null)
                selectedVisual.SetActive(isSelected);
        }

        private void HandleClick()
        {
            if (definition != null)
                onSelected?.Invoke(definition);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }
    }
}
