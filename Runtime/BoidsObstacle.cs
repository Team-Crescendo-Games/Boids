using System.Collections.Generic;
using UnityEngine;

namespace TeamCrescendo.Boids
{
    public class BoidsObstacle : MonoBehaviour
    {
        [Tooltip("The physical size of the obstacle. Boids will try to steer around this area.")]
        [Min(0.1f)] public float radius = 2.0f;

        public bool global = false;
        
        public static readonly List<BoidsObstacle> GlobalObstacles = new ();

        private void OnEnable()
        {
            if (global)
                GlobalObstacles.Add(this);
        }

        private void OnDisable()
        {
            if (global)
                GlobalObstacles.Remove(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}