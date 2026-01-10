using UnityEngine;
using System.Collections.Generic;

namespace Teutoburg.Core
{
    /// <summary>
    /// Endless runner style generator. Spawns floor chunks as player moves +Z (relative to this object).
    /// </summary>
    public class ProceduralLevelGenerator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private GameObject floorPrefab;
        [SerializeField] private int initialChunks = 5; 
        [SerializeField] private float chunkLength = 30f; 
        [SerializeField] private float chunkWidth = 60f;
        
        [Header("Alignment")]
        [Tooltip("Adjust this to lower/raise the generated floor to match your terrain.")]
        [SerializeField] private float verticalOffset = 0f; 
        [Tooltip("Start generating chunks this far ahead of the Generator object.")]
        [SerializeField] private float startOffsetZ = 0f;

        [SerializeField] private Transform player;

        // Internal state
        private float spawnZ = 0f;
        private List<GameObject> activeChunks = new List<GameObject>();
        private float safeZone = 40f; 

        private void Start()
        {
            if (player == null)
            {
                var pTag = GameObject.FindGameObjectWithTag("Player");
                if (pTag != null) player = pTag.transform;
            }

            // Initialize spawnZ with the start offset
            spawnZ = startOffsetZ;

            // Code-First Fallback
            if (floorPrefab == null)
            {
                Debug.LogWarning("[ProceduralLevelGenerator] No Floor Prefab assigned. Generating placeholder.");
                floorPrefab = CreatePlaceholderFloor();
            }

            // Initial spawn
            for (int i = 0; i < initialChunks; i++)
            {
                SpawnChunk();
            }
        }

        private void Update()
        {
            if (player == null) return;

            // Calculate player's Z distance relative to the generator start
            Vector3 playerLocalPos = transform.InverseTransformPoint(player.position);

            // Spawn next chunk when player nears the end of the current generated path
            if (playerLocalPos.z > spawnZ - (initialChunks * chunkLength))
            {
                SpawnChunk();
                GameManager.Instance?.IncrementDepth();
            }
            
            CleanupChunks(playerLocalPos.z);
        }

        private void SpawnChunk()
        {
            // Calculate spawn position relative to the Generator object.
            // Apply vertical offset here.
            Vector3 localPos = new Vector3(0, verticalOffset, spawnZ);
            Vector3 worldPos = transform.TransformPoint(localPos);
            Quaternion worldRot = transform.rotation;

            GameObject go = Instantiate(floorPrefab, worldPos, worldRot);
            go.transform.SetParent(transform);
            
            activeChunks.Add(go);
            spawnZ += chunkLength;
        }

        private void CleanupChunks(float playerLocalZ)
        {
            if (activeChunks.Count > 0)
            {
                // Check distance of the oldest chunk
                if (activeChunks[0] == null) 
                {
                    activeChunks.RemoveAt(0);
                    return;
                }

                Vector3 chunkLocalPos = activeChunks[0].transform.localPosition;
                // Use chunkLength + safeZone to decide when to kill
                if (playerLocalZ > chunkLocalPos.z + chunkLength + safeZone)
                {
                    Destroy(activeChunks[0]);
                    activeChunks.RemoveAt(0);
                }
            }
        }

        private GameObject CreatePlaceholderFloor()
        {
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = "PlaceholderFloor";
            placeholder.transform.localScale = new Vector3(chunkWidth, 1f, chunkLength);
            
            GameObject chunkRoot = new GameObject("ChunkRoot");
            placeholder.transform.SetParent(chunkRoot.transform);
            // If using placeholder, we bake the -0.5 offset into the root logic
            // But the user might also use verticalOffset. 
            // To avoid confusion, let's keep the primitive centered in Y relative to root, 
            // and let verticalOffset handle the main shift, 
            // OR keep the specific fix for cubes here.
            placeholder.transform.localPosition = new Vector3(0, -0.5f, 0); 
            
            var renderer = placeholder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.2f, 0.25f); 
            }

            chunkRoot.SetActive(false);
            return chunkRoot;
        }

        // Visual debugging to help place the generator
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            // Draw a box at the "Start" of generation (respecting startOffsetZ)
            Vector3 startLocal = new Vector3(0, verticalOffset, startOffsetZ + (chunkLength/2));
            Vector3 startWorld = transform.TransformPoint(startLocal);
            
            // Approximate orientation
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(startWorld, transform.rotation, transform.lossyScale);
            Gizmos.matrix = rotationMatrix;
            
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(chunkWidth, 1f, chunkLength));
            
            // Draw arrow pointing forward
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * chunkLength * 2);
        }
    }
}
