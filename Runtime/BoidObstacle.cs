using UnityEngine;

namespace TeamCrescendo.Boids
{
    public class BoidObstacle : MonoBehaviour
    {
        [Tooltip("The physical size of the obstacle. Boids will try to steer around this area.")]
        public float radius = 2.0f;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}