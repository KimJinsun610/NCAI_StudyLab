using UnityEngine;

namespace VARCO_Workshop
{
    [DefaultExecutionOrder(10000)]
    public class RuntimeGroundAlign : MonoBehaviour
    {
        public float footClearance = 0.025f;
        public float alignDuration = 0f;
        public int alignFramesAfterEnable = 6;
        public bool alignOnEnable = true;
        public bool alignVisualChildrenOnly = true;
        public bool continuous = false;
        public bool useRootY = false;
        public float maxCorrectionPerCall = 2.5f;

        float untilTime;
        int remainingAlignFrames;
        int lastAlignedFrame = -1;
        Transform visualOffsetRoot;
        const string VisualOffsetRootName = "VARCO_VisualGroundOffset";
        static readonly string[] IgnoredRendererNameTokens =
        {
            "weapon", "equipped", "sword", "gun", "muzzle", "trail", "effect", "vfx"
        };

        void OnEnable()
        {
            if (alignOnEnable)
            {
                untilTime = Time.unscaledTime + Mathf.Max(0f, alignDuration);
                remainingAlignFrames = Mathf.Max(0, alignFramesAfterEnable);
            }
            else
            {
                untilTime = 0f;
                remainingAlignFrames = 0;
            }

            AlignNow();
        }

        void LateUpdate()
        {
            var withinTimedAlign = alignOnEnable && alignDuration > 0f && Time.unscaledTime < untilTime;
            if (continuous || remainingAlignFrames > 0 || withinTimedAlign)
            {
                AlignNowOncePerFrame();
                if (remainingAlignFrames > 0)
                    remainingAlignFrames--;
            }
        }

        void AlignNowOncePerFrame()
        {
            if (Application.isPlaying && lastAlignedFrame == Time.frameCount)
                return;

            AlignNow();
            if (Application.isPlaying)
                lastAlignedFrame = Time.frameCount;
        }

        public void AlignNow()
        {
            if (!TryGetRendererBounds(out var bounds))
                return;

            var targetY = (useRootY ? transform.position.y : SampleGroundY()) + footClearance;
            var deltaY = targetY - bounds.min.y;
            if (Mathf.Abs(deltaY) <= 0.01f)
                return;

            deltaY = Mathf.Clamp(deltaY, -Mathf.Abs(maxCorrectionPerCall), Mathf.Abs(maxCorrectionPerCall));
            var delta = Vector3.up * deltaY;
            if (alignVisualChildrenOnly && TryOffsetTopLevelVisualChildren(delta))
                return;

            transform.position += delta;
        }

        bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;
            var renderers = GetComponentsInChildren<Renderer>(true);
            var hasAny = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer || renderer is ParticleSystemRenderer)
                    continue;
                if (ShouldIgnoreRenderer(renderer))
                    continue;

                var rendererBounds = renderer.bounds;
                if (rendererBounds.size.sqrMagnitude <= 0.0001f)
                    continue;

                if (!hasAny)
                {
                    bounds = rendererBounds;
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasAny;
        }

        static bool ShouldIgnoreRenderer(Renderer renderer)
        {
            var current = renderer.transform;
            while (current)
            {
                var lower = current.name.ToLowerInvariant();
                for (int i = 0; i < IgnoredRendererNameTokens.Length; i++)
                {
                    if (lower.Contains(IgnoredRendererNameTokens[i]))
                        return true;
                }

                current = current.parent;
            }

            return false;
        }

        float SampleGroundY()
        {
            var origin = transform.position + Vector3.up * 4f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return 0f;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (!hit.collider || hit.collider.transform.IsChildOf(transform))
                    continue;
                if (hit.normal.y < 0.45f)
                    continue;
                return hit.point.y;
            }

            return 0f;
        }

        bool TryOffsetTopLevelVisualChildren(Vector3 worldDelta)
        {
            var offsetRoot = GetOrCreateVisualOffsetRoot();
            if (!offsetRoot)
                return false;

            offsetRoot.position += worldDelta;
            return true;
        }

        Transform GetOrCreateVisualOffsetRoot()
        {
            if (visualOffsetRoot)
                return visualOffsetRoot;

            var existing = transform.Find(VisualOffsetRootName);
            if (existing)
            {
                visualOffsetRoot = existing;
                if (!ChildContainsAlignmentRenderer(visualOffsetRoot))
                {
                    visualOffsetRoot.localPosition = Vector3.zero;
                    visualOffsetRoot.localRotation = Quaternion.identity;
                    visualOffsetRoot.localScale = Vector3.one;
                }

                ParentTopLevelVisualChildren(visualOffsetRoot);
                if (!ChildContainsAlignmentRenderer(visualOffsetRoot))
                    return null;

                return visualOffsetRoot;
            }

            if (transform.childCount == 0)
                return null;

            var visualChildren = CollectTopLevelVisualChildren();
            if (visualChildren.Count == 0)
                return null;

            var offsetGo = new GameObject(VisualOffsetRootName);
            visualOffsetRoot = offsetGo.transform;
            visualOffsetRoot.SetParent(transform, false);
            visualOffsetRoot.localPosition = Vector3.zero;
            visualOffsetRoot.localRotation = Quaternion.identity;
            visualOffsetRoot.localScale = Vector3.one;

            for (int i = 0; i < visualChildren.Count; i++)
                visualChildren[i].SetParent(visualOffsetRoot, true);

            return visualOffsetRoot;
        }

        void ParentTopLevelVisualChildren(Transform offsetRoot)
        {
            if (!offsetRoot)
                return;

            var visualChildren = CollectTopLevelVisualChildren();
            for (int i = 0; i < visualChildren.Count; i++)
                visualChildren[i].SetParent(offsetRoot, true);
        }

        System.Collections.Generic.List<Transform> CollectTopLevelVisualChildren()
        {
            var visualChildren = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child)
                    continue;
                if (child.name == VisualOffsetRootName)
                    continue;

                if (!ChildContainsAlignmentRenderer(child))
                    continue;

                visualChildren.Add(child);
            }

            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                var skinned = skinnedRenderers[i];
                if (!skinned || ShouldIgnoreRenderer(skinned))
                    continue;

                AddTopLevelChildFor(skinned.rootBone, visualChildren);
                AddTopLevelChildFor(skinned.transform, visualChildren);
            }

            return visualChildren;
        }

        void AddTopLevelChildFor(Transform source, System.Collections.Generic.List<Transform> results)
        {
            if (!source)
                return;

            var top = source;
            while (top.parent && top.parent != transform)
                top = top.parent;

            if (!top || top == transform || top.parent != transform)
                return;
            if (top.name == VisualOffsetRootName)
                return;
            if (results.Contains(top))
                return;

            results.Add(top);
        }

        static bool ChildContainsAlignmentRenderer(Transform child)
        {
            var renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer || renderer is ParticleSystemRenderer)
                    continue;
                if (ShouldIgnoreRenderer(renderer))
                    continue;

                return true;
            }

            return false;
        }
    }
}
