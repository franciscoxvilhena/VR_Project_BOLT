using UnityEngine;

public class GestureLogger : MonoBehaviour
{
    public string gestureName;

    public void LogStarted()
    {
        Debug.Log("Gesture started: " + gestureName);
    }

    public void LogEnded()
    {
        Debug.Log("Gesture ended: " + gestureName);
    }
}