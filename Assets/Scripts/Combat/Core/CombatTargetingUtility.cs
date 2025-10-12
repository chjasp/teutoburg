using System;
using UnityEngine;

namespace Teutoburg.Combat
{
    /// <summary>
    /// Helper methods for locating combat targets in the scene.
    /// </summary>
    public static class CombatTargetingUtility
    {
        /// <summary>
        /// Finds the closest target with the given tag that is considered alive by the supplied predicate.
        /// </summary>
        /// <param name="tag">Unity tag to search for.</param>
        /// <param name="origin">Origin position.</param>
        /// <param name="maxDistance">Maximum allowed distance. Use Mathf.Infinity for unlimited.</param>
        /// <param name="isDead">Predicate that returns true when the candidate should be ignored.</param>
        /// <param name="distance">Returns the distance of the selected target (Mathf.Infinity if not found).</param>
        /// <returns>The closest matching transform or null.</returns>
        public static Transform FindClosestTarget(string tag, Vector3 origin, float maxDistance, Func<Transform, bool> isDead, out float distance)
        {
            distance = Mathf.Infinity;
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            GameObject[] candidates;
            try
            {
                candidates = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException)
            {
                return null; // Tag not defined in the project yet.
            }

            Transform best = null;
            foreach (var go in candidates)
            {
                if (go == null)
                {
                    continue;
                }

                var candidate = go.transform;
                if (isDead != null && isDead(candidate))
                {
                    continue;
                }

                float d = Vector3.Distance(origin, candidate.position);
                if (d > maxDistance || d >= distance)
                {
                    continue;
                }

                distance = d;
                best = candidate;
            }

            return best;
        }
    }
}
