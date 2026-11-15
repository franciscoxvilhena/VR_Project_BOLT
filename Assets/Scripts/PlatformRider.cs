using UnityEngine;
using System.Collections;

public class PlatformRide : MonoBehaviour
{
    public Transform[] stops;
    public Renderer fader;
    public int blackoutStop = 1;
    public float speed = 0.8f, wait = 5f;

    IEnumerator Start()
    {
        for (int i = 0; i < stops.Length; i++)
        {
            if (i == blackoutStop) yield return Fade(1);
            while (transform.position != stops[i].position)
            {
                transform.position = Vector3.MoveTowards(transform.position, stops[i].position, speed * Time.deltaTime);
                yield return null;
            }
            if (i == blackoutStop) yield return Fade(0);
            yield return new WaitForSeconds(wait);
        }
    }

    IEnumerator Fade(float to)
    {
        var m = fader.material;
        var c = m.color;
        while (c.a != to)
        {
            c.a = Mathf.MoveTowards(c.a, to, Time.deltaTime);
            m.color = c;
            yield return null;
        }
    }
}