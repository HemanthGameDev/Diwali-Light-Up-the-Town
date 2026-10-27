using UnityEngine;

namespace DiwaliTown.Crackers
{
    [CreateAssetMenu(
        fileName = "CCD_NewCategory",
        menuName = "Diwali Town/Crackers/Category Definition"
    )]
    public sealed class CrackerCategoryDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string categoryId;
        [SerializeField] private string displayName;

        [Header("Presentation")]
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int sortOrder;

        public string CategoryId => categoryId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int SortOrder => sortOrder;
    }
}