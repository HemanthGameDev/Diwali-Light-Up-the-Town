
using UnityEngine;

namespace DiwaliTown.Diyas
{
    [DisallowMultipleComponent]
    public sealed class DiyaAnchor : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private GameObject highlightVisual;
        [SerializeField] private GameObject litVisual;

        [Header("Save Identity")]
        [SerializeField] private string persistentId;

        public string PersistentId => persistentId;

        private bool isLit;

        public bool IsLit => isLit;

        public bool IsHighlighted =>
            highlightVisual != null &&
            highlightVisual.activeSelf;

        private void Awake()
        {
            isLit = false;

            if (highlightVisual != null)
            {
                highlightVisual.SetActive(false);
            }

            if (litVisual != null)
            {
                litVisual.SetActive(false);
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightVisual == null)
            {
                return;
            }

            highlightVisual.SetActive(
                highlighted && !isLit
            );
        }

        public bool TryLight()
        {
            if (isLit)
            {
                return false;
            }

            isLit = true;

            SetHighlighted(false);

            if (litVisual != null)
            {
                litVisual.SetActive(true);
            }

            return true;
        }
        public void RestoreLitState(bool lit)
        {
            isLit = lit;

            if (highlightVisual != null)
                highlightVisual.SetActive(false);

            if (litVisual != null)
                litVisual.SetActive(lit);
        }
    }
}
