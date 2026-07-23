using UnityEngine;

namespace TeamCrescendo.Boids
{
    public class BoidsZone : MonoBehaviour
    {
        public enum ZoneType
        {
            InfiniteSlab = 0, // Constrained on Y axis (Thickness), infinite X/Z
            Box = 1,          // Constrained on all axes
            Sphere = 2        // Constrained by radius
        }

        [Header("Zone Settings")]
        public ZoneType type = ZoneType.InfiniteSlab;

        [Tooltip("XYZ dimensions. For 'InfiniteSlab', only the Y value matters (Thickness).")]
        public Vector3 dimensions = new (50, 20, 50);

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Color c = Color.cyan;
            c.a = 0.2f;
            Gizmos.color = c;

            if (type == ZoneType.InfiniteSlab)
            {
                // Draw a representation of the slab (visualize generic large X/Z)
                Gizmos.DrawCube(Vector3.zero, new Vector3(100, dimensions.y, 100));
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(100, dimensions.y, 100));
            }
            else if (type == ZoneType.Box)
            {
                Gizmos.DrawCube(Vector3.zero, dimensions);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(Vector3.zero, dimensions);
            }
            else if (type == ZoneType.Sphere)
            {
                Gizmos.DrawSphere(Vector3.zero, dimensions.x); // Use X as radius
            }
        }
    }
}