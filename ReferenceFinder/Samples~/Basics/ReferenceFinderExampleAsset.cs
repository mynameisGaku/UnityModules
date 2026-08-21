using UnityEngine;

namespace ReferenceFinder.Samples.Editor
{
    /// <summary>Provides a small asset reference for the imported sample.</summary>
    public sealed class ReferenceFinderExampleAsset : ScriptableObject
    {
        [SerializeField] private UnityEngine.Object _reference;

        /// <summary>Gets the optional example asset reference.</summary>
        public UnityEngine.Object Reference => _reference;
    }
}
