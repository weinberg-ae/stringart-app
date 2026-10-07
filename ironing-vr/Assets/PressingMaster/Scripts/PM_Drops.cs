using System.Collections.Generic;
using UnityEngine;

// Water drops spat by the iron: where they land they leave a wet mark (and brown limescale on fabric).
public class PM_Drops : MonoBehaviour
{
    ParticleSystem ps;
    readonly List<ParticleCollisionEvent> events = new List<ParticleCollisionEvent>();
    static readonly Queue<GameObject> marks = new Queue<GameObject>();
    static Material wetMat;

    void Awake() { ps = GetComponent<ParticleSystem>(); }

    void OnParticleCollision(GameObject other)
    {
        int n = ps.GetCollisionEvents(other, events);
        PM_Fabric fabric = other.GetComponent<PM_Fabric>();
        PM_WaterTank tank = PM_Game.I != null ? PM_Game.I.Tank : null;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = events[i].intersection, nrm = events[i].normal;
            if (fabric != null)
            {
                // Find the texture point under the drop.
                foreach (RaycastHit h in Physics.RaycastAll(p + nrm * 0.02f, -nrm, 0.05f))
                {
                    if (h.collider.gameObject != other) continue;
                    fabric.Splash(h.textureCoord, 0.01f, 1, tank != null ? tank.Limescale * 0.5f : 0.15f);
                    break;
                }
            }
            else WetMark(p, nrm);
        }
    }

    static void WetMark(Vector3 p, Vector3 n)
    {
        if (wetMat == null) { wetMat = PM_Util.TransparentMaterial(new Color(0.02f, 0.03f, 0.05f, 0.45f)); wetMat.mainTexture = PM_Util.SoftDot(32); }
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = "PM_WetMark";
        q.transform.position = p + n * 0.002f;
        q.transform.rotation = Quaternion.LookRotation(-n) * Quaternion.Euler(0, 0, Random.value * 360f);
        q.transform.localScale = Vector3.one * Random.Range(0.012f, 0.03f);
        q.GetComponent<Renderer>().sharedMaterial = wetMat;
        q.AddComponent<PM_Fade>();
        marks.Enqueue(q);
        while (marks.Count > 80) { GameObject old = marks.Dequeue(); if (old != null) Destroy(old); }
    }
}

// A wet mark dries after a while.
public class PM_Fade : MonoBehaviour
{
    float t;
    void Update()
    {
        t += Time.deltaTime;
        if (t > 25f) transform.localScale *= 1f - Time.deltaTime * 0.5f;
        if (t > 32f) Destroy(gameObject);
    }
}
