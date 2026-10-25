
using UnityEngine;

namespace DiwaliTown.Crackers
{
    [CreateAssetMenu(
        fileName = "UnlockRule_NewCracker",
        menuName = "Diwali Town/Crackers/Unlock Rule"
    )]
    public sealed class CrackerUnlockRule : ScriptableObject
    {
        [SerializeField] private CrackerDefinition definition;

        [SerializeField, Min(1)]
        private int requiredLitDiyas = 1;

        public CrackerDefinition Definition => definition;
        public int RequiredLitDiyas => requiredLitDiyas;
    }
}
