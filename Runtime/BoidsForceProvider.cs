using UnityEngine;

namespace TeamCrescendo.Boids
{
    public class BoidsForceProvider : MonoBehaviour
    {
        [Tooltip("Positive (+) pulls boids towards. Negative (-) pushes boids away.")]
        public float weight = 10f;

        // TODO: currently this is unused!
        [Tooltip("Boids outside this range will ignore this target")]
        public float influenceRange = 20f;

        private void OnDrawGizmos()
        {
            // Visual debug: Green = Attraction, Red = Repulsion
            Gizmos.color = weight >= 0 ? new Color(0, 1, 0, 0.2f) : new Color(1, 0, 0, 0.2f);
            Gizmos.DrawWireSphere(transform.position, influenceRange);
        
            // Draw a solid center so it's easy to find
            Gizmos.color = weight >= 0 ? Color.green : Color.red;
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
    }
}