using UnityEngine;
using System.Collections;

public class PlatformRide : MonoBehaviour
{
    public Transform[] stops;
    public float speed = 0.8f, wait = 5f;

    IEnumerator Start()
    {
        foreach (var s in stops)
        {
            while (transform.position != s.position)
            {
                transform.position = Vector3.MoveTowards(transform.position, s.position, speed * Time.deltaTime);
                yield return null;
            }
            yield return new WaitForSeconds(wait);
        }
    }
}