using System;
using System.Linq;
using UnityEngine;

namespace VARCO_Workshop
{
    [DisallowMultipleComponent]
    public class PrefabFeatureMarker : MonoBehaviour
    {
        public string prefabPreset;
        public string role;
        public string[] appliedFeatures = Array.Empty<string>();
        public bool validationPassed;
        [TextArea(2, 6)] public string validationSummary;

        public void AddFeature(string featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return;

            if (appliedFeatures != null && appliedFeatures.Contains(featureName))
                return;

            var source = appliedFeatures ?? Array.Empty<string>();
            appliedFeatures = source.Concat(new[] { featureName }).ToArray();
        }
    }
}
