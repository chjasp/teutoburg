using UnityEngine;

public class GameUIHandler : MonoBehaviour
{
    // This method needs to be public so the Button's event can see it.
    public void LogHelloWorld()
    {
        Debug.Log("Hello World!");
    }
}