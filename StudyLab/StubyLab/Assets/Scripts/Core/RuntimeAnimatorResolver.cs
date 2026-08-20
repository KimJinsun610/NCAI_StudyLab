using UnityEngine;

namespace VARCO_Workshop
{
    public static class RuntimeAnimatorResolver
    {
        public static Animator FindBestAnimator(GameObject owner, Transform preferredRoot = null)
        {
            if (!owner)
                return null;

            if (preferredRoot)
            {
                var preferred = preferredRoot.GetComponentInChildren<Animator>(true);
                if (preferred)
                    return preferred;
            }

            var animators = owner.GetComponentsInChildren<Animator>(true);
            foreach (var animator in animators)
            {
                if (animator && animator.transform != owner.transform)
                    return animator;
            }

            return owner.GetComponent<Animator>();
        }

        public static void DisableDuplicateRootAnimator(GameObject owner, Animator activeAnimator)
        {
            if (!owner || !activeAnimator)
                return;

            var rootAnimator = owner.GetComponent<Animator>();
            if (!rootAnimator || rootAnimator == activeAnimator)
                return;

            rootAnimator.enabled = false;
        }
    }
}
