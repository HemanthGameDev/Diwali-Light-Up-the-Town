
using System;
using System.Collections.Generic;
using DiwaliTown.Diyas;
using TMPro;
using UnityEngine;

namespace DiwaliTown.Town
{
    [DisallowMultipleComponent]
    public sealed class TownAreaProgression : MonoBehaviour
    {
        [Header("Diya Progress")]
        [SerializeField]
        private SacredFlameController sacredFlameController;

        [SerializeField]
        private DiyaAnchor[] requiredDiyas;

        [Header("Completion Visuals")]
        [SerializeField]
        private GameObject centralSculptureLitVisual;

        [SerializeField]
        private GameObject areaCompletePanel;

        [Header("UI")]
        [SerializeField]
        private TMP_Text progressText;

        [SerializeField]
        private TMP_Text completionText;

        private readonly HashSet<DiyaAnchor> litDiyas =
            new HashSet<DiyaAnchor>();

        private bool isAreaComplete;

        public int LitDiyaCount => litDiyas.Count;

        public int RequiredDiyaCount =>
            requiredDiyas != null ? requiredDiyas.Length : 0;

        public bool IsAreaComplete => isAreaComplete;

        public event Action AreaCompleted;

        private void Awake()
        {
            if (centralSculptureLitVisual != null)
            {
                centralSculptureLitVisual.SetActive(false);
            }

            if (areaCompletePanel != null)
            {
                areaCompletePanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            sacredFlameController.DiyaLit += HandleDiyaLit;

            RebuildProgressFromScene();
            RefreshProgressUI();

            if (litDiyas.Count >= requiredDiyas.Length)
            {
                CompleteArea();
            }
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
                requiredDiyas == null ||
                requiredDiyas.Length == 0 ||
                centralSculptureLitVisual == null ||
                areaCompletePanel == null ||
                progressText == null ||
                completionText == null)
            {
                Debug.LogError(
                    "[TownProgression] Missing required references.",
                    this
                );

                return false;
            }

            var uniqueDiyas = new HashSet<DiyaAnchor>();

            foreach (DiyaAnchor diya in requiredDiyas)
            {
                if (diya == null || !uniqueDiyas.Add(diya))
                {
                    Debug.LogError(
                        "[TownProgression] Required diyas contain " +
                        "a missing or duplicate reference.",
                        this
                    );

                    return false;
                }
            }

            return true;
        }

        private void RebuildProgressFromScene()
        {
            litDiyas.Clear();

            foreach (DiyaAnchor diya in requiredDiyas)
            {
                if (diya.IsLit)
                {
                    litDiyas.Add(diya);
                }
            }
        }

        private void HandleDiyaLit(DiyaAnchor diya)
        {
            if (isAreaComplete ||
                diya == null ||
                !IsRequiredDiya(diya))
            {
                return;
            }

            // HashSet prevents the same diya being counted twice.
            if (!litDiyas.Add(diya))
            {
                return;
            }

            RefreshProgressUI();

            if (litDiyas.Count >= requiredDiyas.Length)
            {
                CompleteArea();
            }
        }

        private bool IsRequiredDiya(DiyaAnchor target)
        {
            foreach (DiyaAnchor diya in requiredDiyas)
            {
                if (diya == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshProgressUI()
        {
            progressText.text =
                $"Diyas Lit: {litDiyas.Count}/{requiredDiyas.Length}";
        }

        private void CompleteArea()
        {
            if (isAreaComplete)
            {
                return;
            }

            isAreaComplete = true;

            centralSculptureLitVisual.SetActive(true);

            completionText.text =
                "AREA RESTORED!\nAll required diyas are lit.";

            areaCompletePanel.SetActive(true);

            RefreshProgressUI();

            Debug.Log("[TownProgression] Area completed!");

            AreaCompleted?.Invoke();
        }
        public void RefreshFromRestoredDiyas()
        {
            if (requiredDiyas == null || requiredDiyas.Length == 0)
                return;

            RebuildProgressFromScene();
            RefreshProgressUI();

            if (litDiyas.Count >= requiredDiyas.Length)
            {
                CompleteArea();
            }
            else
            {
                isAreaComplete = false;

                if (centralSculptureLitVisual != null)
                    centralSculptureLitVisual.SetActive(false);

                if (areaCompletePanel != null)
                    areaCompletePanel.SetActive(false);
            }
        }
    }
}
