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
            return;
        }

        var touch = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[0];

        // Only process touch if it's in the Began phase
        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            
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
    }
}
