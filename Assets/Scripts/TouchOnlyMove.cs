// TouchOnlyMove.cs
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(PlayerAttack))]

public class TouchOnlyMove : MonoBehaviour
{
    [SerializeField] private string enemyTag = "Enemy";

    private NavMeshAgent agent;
    private Camera cam;
    private PlayerAttack playerAttack;
    
    // Add this to prevent double-triggering
    private bool hasProcessedTouch = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        playerAttack = GetComponent<PlayerAttack>();
        cam   = Camera.main;
    }

    private void OnEnable()
    {
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        HandleTouchInput();
    }

    private void HandleTouchInput()
    {
        if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0) 
        {
            // Reset the flag when no touches are active
            hasProcessedTouch = false;
            return;
        }

        var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];
        
        // Only process touch if it's in the Began phase and we haven't already processed this touch
        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began && !hasProcessedTouch)
        {
            hasProcessedTouch = true; // Mark this touch as processed
            
            var ray = cam.ScreenPointToRay(touch.screenPosition);
            Debug.Log("Ray: " + ray);
            if (Physics.Raycast(ray, out var hit, 2000f))
            {
                if (hit.collider.CompareTag(enemyTag))
                {
                    playerAttack.SetTarget(hit.transform);
                }
                else
                {
                    // If we didn't hit an enemy, stop any attack and move to the point
                    if (playerAttack != null)
                        playerAttack.SetTarget(null);
                    
                    Debug.Log("Setting destination: " + hit.point);
                    agent.SetDestination(hit.point);
                }
            }
        }
        
        // Reset flag when touch ends
        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || 
            touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            hasProcessedTouch = false;
        }
    }
}
