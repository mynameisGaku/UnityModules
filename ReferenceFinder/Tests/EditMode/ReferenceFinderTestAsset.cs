using UnityEngine;

namespace ReferenceFinder.Tests
{
    internal sealed class ReferenceFinderTestAsset : ScriptableObject
    {
        [SerializeField] private UnityEngine.Object _reference;

        internal UnityEngine.Object Reference
        {
            get => _reference;
            set => _reference = value;
        }
    }
}
