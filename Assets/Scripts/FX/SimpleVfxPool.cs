using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SimpleVfxPool : MonoBehaviour
{
    public static SimpleVfxPool Instance { get; private set; }

    [Tooltip("ParticleSystem prefab to pool (root GameObject should hold the ParticleSystem).")]
    public ParticleSystem prefab;

    [Tooltip("Initial number of pooled instances to create on Awake.")]
    public int initialSize = 12;

    [Tooltip("Maximum pool size (0 = unlimited).")]
    public int maxSize = 0;

    [Tooltip("Extra seconds added to lifetime before returning to pool (safety pad).")]
    public float lifetimePad = 0.05f;

    private readonly Queue<ParticleSystem> available = new Queue<ParticleSystem>();
    private readonly HashSet<ParticleSystem> all = new HashSet<ParticleSystem>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Prewarm(Mathf.Max(0, initialSize));
    }

    public void PlayAt(Vector3 position)
    {
        var ps = Get();
        if (ps == null)
            return;
        var go = ps.gameObject;
        go.transform.position = position;
        go.SetActive(true);
        var main = ps.main;
        // Ensure pooled behavior (don't auto-destroy)
        main.stopAction = ParticleSystemStopAction.None;
        ps.Clear(true);
        ps.Play(true);
        float lt = main.duration;
        var sl = main.startLifetime;
        if (sl.mode == ParticleSystemCurveMode.TwoConstants)
            lt += Mathf.Max(sl.constantMin, sl.constantMax);
        else if (sl.mode == ParticleSystemCurveMode.Constant)
            lt += sl.constant;
        else
            lt += main.duration; // fallback conservative
        lt += Mathf.Max(0f, lifetimePad);
        StartCoroutine(ReturnAfter(ps, lt));
    }

    private System.Collections.IEnumerator ReturnAfter(ParticleSystem ps, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (ps == null)
            yield break;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var go = ps.gameObject;
        go.SetActive(false);
        available.Enqueue(ps);
    }

    private ParticleSystem Get()
    {
        if (available.Count > 0)
        {
            return available.Dequeue();
        }
        if (maxSize > 0 && all.Count >= maxSize)
        {
            // Reuse last returned if hard-capped (fallback)
            if (available.Count > 0)
                return available.Dequeue();
            return null;
        }
        return CreateOne();
    }

    private void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var ps = CreateOne();
            if (ps == null) break;
            ps.gameObject.SetActive(false);
            available.Enqueue(ps);
        }
    }

    private ParticleSystem CreateOne()
    {
        if (prefab == null)
        {
            Debug.LogWarning("SimpleVfxPool: prefab is not assigned.");
            return null;
        }
        var ps = Instantiate(prefab, transform);
        all.Add(ps);
        return ps;
    }
}


