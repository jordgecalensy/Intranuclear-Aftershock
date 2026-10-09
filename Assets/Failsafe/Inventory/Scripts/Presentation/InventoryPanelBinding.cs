using UnityEngine;

namespace Failsafe.Inventory.Presentation
{
    /// <summary>Fits an existing UI rectangle to three panel markers without changing its layout.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class InventoryPanelBinding : MonoBehaviour
    {
        public Transform BottomLeft;
        public Transform BottomRight;
        public Transform TopLeft;

        [Tooltip("Root to move. Must contain the reference rectangle and all related UI visuals.")]
        public Transform TargetRoot;
        [Tooltip("Rectangle whose visible area should fit the panel, for example GridCellsRoot.")]
        public RectTransform ReferenceRect;
        [Tooltip("Optional separate item roots. Do not include children of Target Root.")]
        public Transform[] AdditionalRoots = new Transform[0];

        [Tooltip("World-space gap towards the viewer. Use a negative value for the opposite side.")]
        public float SurfaceOffset = 0.001f;
        [Min(0f)] public float Padding;
        public bool PreviewInEditor = true;

        private readonly Vector3[] _corners = new Vector3[4];

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            if (Application.IsPlaying(gameObject) || PreviewInEditor)
                TryApply(out _);
        }

        public bool TryApply(out string error)
        {
            if (!TryGetFit(out Vector3 sourceCenter, out Quaternion sourceRotation,
                    out Vector3 destinationCenter, out Quaternion destinationRotation,
                    out float factor, out error))
                return false;

            Quaternion delta = destinationRotation * Quaternion.Inverse(sourceRotation);
            ApplySimilarity(TargetRoot, sourceCenter, destinationCenter, delta, factor);
            if (AdditionalRoots != null)
                foreach (Transform root in AdditionalRoots)
                    ApplySimilarity(root, sourceCenter, destinationCenter, delta, factor);
            return true;
        }

        public bool TryGetFit(out Vector3 sourceCenter, out Quaternion sourceRotation,
            out Vector3 destinationCenter, out Quaternion destinationRotation,
            out float factor, out string error)
        {
            sourceCenter = destinationCenter = Vector3.zero;
            sourceRotation = destinationRotation = Quaternion.identity;
            factor = 1f;
            error = null;
            if (!BottomLeft || !BottomRight || !TopLeft || !TargetRoot || !ReferenceRect)
                return Fail("Assign all three corners, Target Root and Reference Rect.", out error);
            if (!ReferenceRect.IsChildOf(TargetRoot))
                return Fail("Reference Rect must be Target Root or its child.", out error);
            Canvas canvas = ReferenceRect.GetComponentInParent<Canvas>();
            if (canvas && canvas.rootCanvas.renderMode != RenderMode.WorldSpace)
                return Fail("The UI Canvas must use World Space.", out error);
            if (!ValidateRoot(TargetRoot, out error))
                return false;
            if (AdditionalRoots != null)
                for (int i = 0; i < AdditionalRoots.Length; i++)
                {
                    Transform root = AdditionalRoots[i];
                    if (!root || !ValidateRoot(root, out error))
                        return Fail(error ?? "An Additional Root is not assigned.", out error);
                    if (root.IsChildOf(TargetRoot) || TargetRoot.IsChildOf(root))
                        return Fail("Additional Roots must be separate from Target Root (no shared hierarchy).", out error);
                    for (int j = 0; j < i; j++)
                        if (root.IsChildOf(AdditionalRoots[j]) || AdditionalRoots[j].IsChildOf(root))
                            return Fail("Additional Roots must not overlap or contain duplicates.", out error);
                }
            if (!Finite(SurfaceOffset) || !Finite(Padding) || Padding < 0f)
                return Fail("Offset and padding must be finite; padding cannot be negative.", out error);

            Vector3 right = BottomRight.position - BottomLeft.position;
            Vector3 up = TopLeft.position - BottomLeft.position;
            if (!TryRectangle(right, up, out destinationRotation))
                return Fail("Panel corners must form a non-zero rectangle with a right angle at Bottom Left.", out error);
            float width = right.magnitude - Padding * 2f;
            float height = up.magnitude - Padding * 2f;
            if (width <= 0f || height <= 0f)
                return Fail("Padding leaves no room for the UI.", out error);

            ReferenceRect.GetWorldCorners(_corners);
            Vector3 sourceRight = _corners[3] - _corners[0];
            Vector3 sourceUp = _corners[1] - _corners[0];
            if (!TryRectangle(sourceRight, sourceUp, out sourceRotation))
                return Fail("Reference Rect has zero size or is skewed by its parent transforms.", out error);
            sourceCenter = (_corners[0] + _corners[2]) * 0.5f;
            // UI is viewed from its negative Z side when right and up point along the screen.
            destinationCenter = BottomLeft.position + (right + up) * 0.5f
                - destinationRotation * Vector3.forward * SurfaceOffset;
            factor = Mathf.Min(width / sourceRight.magnitude, height / sourceUp.magnitude);
            if (!Finite(factor) || factor <= 0f)
                return Fail("The calculated scale is invalid.", out error);
            return true;
        }

        private bool ValidateRoot(Transform root, out string error)
        {
            if (BottomLeft.IsChildOf(root) || BottomRight.IsChildOf(root) || TopLeft.IsChildOf(root))
                return Fail("Corner markers must not be children of a root being moved.", out error);
            // A world rotation under non-uniformly scaled parents can introduce shear.
            for (Transform parent = root.parent; parent; parent = parent.parent)
            {
                Vector3 s = parent.localScale;
                if (!Finite(s.x) || !Finite(s.y) || !Finite(s.z) || s.x <= 0f ||
                    Mathf.Abs(s.x - s.y) > s.x * 0.001f || Mathf.Abs(s.x - s.z) > s.x * 0.001f)
                    return Fail("Parents of the moved roots must have uniform positive scale.", out error);
            }
            Vector3 scale = root.localScale;
            if (!Finite(scale.x) || !Finite(scale.y) || !Finite(scale.z) ||
                scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
                return Fail("Moved roots must have finite positive scale.", out error);
            error = null;
            return true;
        }

        private static bool TryRectangle(Vector3 right, Vector3 up, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            if (!Finite(right.sqrMagnitude) || !Finite(up.sqrMagnitude) ||
                right.sqrMagnitude < 1e-12f || up.sqrMagnitude < 1e-12f ||
                Mathf.Abs(Vector3.Dot(right.normalized, up.normalized)) > 0.01f)
                return false;
            rotation = Quaternion.LookRotation(Vector3.Cross(right, up).normalized, up.normalized);
            return true;
        }

        private static void ApplySimilarity(Transform root, Vector3 source, Vector3 destination,
            Quaternion rotation, float scale)
        {
            Vector3 position = destination + rotation * (root.position - source) * scale;
            Quaternion orientation = rotation * root.rotation;
            Vector3 localScale = root.localScale * scale;
            if ((root.position - position).sqrMagnitude > 1e-14f)
                root.position = position;
            if (Quaternion.Angle(root.rotation, orientation) > 0.0001f)
                root.rotation = orientation;
            if ((root.localScale - localScale).sqrMagnitude > 1e-14f)
                root.localScale = localScale;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Fail(string message, out string error) { error = message; return false; }

        private void OnDrawGizmosSelected()
        {
            if (!BottomLeft || !BottomRight || !TopLeft) return;
            Vector3 a = BottomLeft.position, b = BottomRight.position, c = TopLeft.position;
            Vector3 d = b + c - a;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, d);
            Gizmos.DrawLine(d, c); Gizmos.DrawLine(c, a);
            Vector3 center = (b + c) * 0.5f;
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(center, -Vector3.Cross(b - a, c - a).normalized * 0.03f);
        }
    }
}
