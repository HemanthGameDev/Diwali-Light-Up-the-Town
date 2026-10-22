
using UnityEngine;

namespace DiwaliTown.Crackers
{
    [CreateAssetMenu(
        fileName = "CD_NewCracker",
        menuName = "Diwali Town/Crackers/Cracker Definition"
    )]
    public sealed class CrackerDefinition : ScriptableObject
    {
        [Header("Display")]
        [SerializeField] private string displayName = "Basic Cracker";
        [SerializeField] private Sprite previewIcon;

        [Header("Economy")]
        [SerializeField, Min(1)] private int placementCost = 1;
        [SerializeField, Min(0)] private int blastReward = 2;

        public string DisplayName => displayName;
        public Sprite PreviewIcon => previewIcon;
        public int PlacementCost => placementCost;
        public int BlastReward => blastReward;
    }
}
