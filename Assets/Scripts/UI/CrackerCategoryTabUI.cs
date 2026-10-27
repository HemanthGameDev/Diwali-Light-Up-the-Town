
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using DiwaliTown.Crackers;

namespace DiwaliTown.UI
{
    [DisallowMultipleComponent]
    public sealed class CrackerCategoryTabUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image iconImage;
        [SerializeField] private GameObject notificationDot;
        [SerializeField] private GameObject selectedVisual;

        private CrackerCategoryDefinition category;
        private Action<CrackerCategoryDefinition> onSelected;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        public void Bind(
            CrackerCategoryDefinition definition,
            bool hasNewItems,
            bool isSelected,
            Action<CrackerCategoryDefinition> selectionCallback)
        {
            if (button == null)
                button = GetComponent<Button>();

            category = definition;
            onSelected = selectionCallback;

            if (button == null || labelText == null)
            {
                Debug.LogError(
                    "[CrackerCategoryTabUI] Missing Button or label.",
                    this
                );
                return;
            }

            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);

            labelText.text = category.DisplayName;

            if (iconImage != null)
            {
                iconImage.sprite = category.Icon;
                iconImage.enabled = category.Icon != null;
            }

            if (notificationDot != null)
                notificationDot.SetActive(hasNewItems);

            if (selectedVisual != null)
                selectedVisual.SetActive(isSelected);
        }

        private void HandleClick()
        {
            if (category != null)
                onSelected?.Invoke(category);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }
    }
}
