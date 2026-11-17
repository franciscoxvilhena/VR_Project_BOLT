using UnityEngine;
using System.Collections;

public class PlatformRide : MonoBehaviour
{
    public Transform[] stops;
    public Renderer fader;
    public int blackoutStop = 1;
    public float speed = 0.8f, wait = 5f;

    bool released;

    // called by HideHandOnGrab once the block lands
    public void Release() => released = true;

    IEnumerator Start()
    {
        for (int i = 0; i < stops.Length; i++)
        {
            Vector3 target = stops[i].position;
            if (i == blackoutStop) yield return Fade(1);
            while (transform.position != target)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
            if (i == blackoutStop) yield return Fade(0);

            if (i == 0) yield return new WaitUntil(() => released);
            else yield return new WaitForSeconds(wait);
        }
    }

    IEnumerator Fade(float to)
    {
        // the fader has to be on the camera we're actually looking through
        if (!fader.gameObject.activeInHierarchy && Camera.main != null)
            fader.transform.SetParent(Camera.main.transform, false);

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
