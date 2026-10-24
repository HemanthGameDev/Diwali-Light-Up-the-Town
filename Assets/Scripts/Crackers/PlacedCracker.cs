
using DiwaliTown.Crackers;
using UnityEngine;

namespace DiwaliTown.Crackers
{
    [DisallowMultipleComponent]
    public sealed class PlacedCracker : MonoBehaviour
    {
        public CrackerDefinition Definition { get; private set; }

        public bool HasBeenIgnited { get; private set; }

        public void Initialize(CrackerDefinition definition)
        {
            if (definition == null)
            {
                Debug.LogError(
                    "[PlacedCracker] Definition cannot be null.",
                    this
                );
                return;
            }

            Definition = definition;
            HasBeenIgnited = false;
        }

        public bool TryMarkIgnited()
        {
            if (Definition == null || HasBeenIgnited)
            {
                return false;
            }

            HasBeenIgnited = true;
            return true;
        }
    }
}
