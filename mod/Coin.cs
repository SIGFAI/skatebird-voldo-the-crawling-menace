// Terraria coins: copper, silver, gold. They pop out of Voldo, bounce, spin, and the bird scoops them up.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Coin : MonoBehaviour
{
    public int tier;          // 0 copper = 1, 1 silver = 100, 2 gold = 10000
    public float age;
    Transform visual;
    float spin;
    bool magnet;
    Rigidbody rb;

    public static readonly List<Coin> All = new List<Coin>();
    public static long Wallet;
    public static float WalletFlash;
    static readonly int[] Value = { 1, 100, 10000 };
    static readonly string[] Tex = { "coin_copper.png", "coin_silver.png", "coin_icon.png" };

    public static void Drop(Vector3 pos, int copper, int silver, int gold)
    {
        for (int i = 0; i < copper; i++) Make(pos, 0);
        for (int i = 0; i < silver; i++) Make(pos, 1);
        for (int i = 0; i < gold; i++) Make(pos, 2);
        Mix.Play(Mix.Sound("coin.wav"), pos, 0.8f, 0.7f);
        Mix.Burst(pos, new Color(1f, 0.85f, 0.2f), 14, 3.5f, 0.05f, 0.9f);
        var l = Mix.Glow(pos, new Color(1f, 0.85f, 0.3f), 3f, 2.5f);
        Destroy(l.gameObject, 0.4f);
    }

    static void Make(Vector3 pos, int tier)
    {
        var go = new GameObject("Coin");
        go.transform.position = pos;
        var c = go.AddComponent<Coin>();
        c.tier = tier;
        float size = tier == 2 ? 0.46f : (tier == 1 ? 0.38f : 0.3f);
        var v = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(v.GetComponent<Collider>());
        v.layer = 2;
        v.transform.SetParent(go.transform, false);
        v.transform.localScale = new Vector3(size, 0.012f, size);
        Mix.Paint(v, Color.white, 0.35f, Mix.Texture(Tex[tier]));
        c.visual = v.transform;
        var sc = go.AddComponent<SphereCollider>();
        sc.radius = size * 0.5f;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.05f;
        c.rb = rb;
        rb.velocity = new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 5.5f), Random.Range(-2f, 2f));
        // coins never knock the bird about: ignore the board, the bird and Voldo
        IgnoreBird(sc);
        if (Voldo.Live != null) { var vc = Voldo.Live.GetComponent<Collider>(); if (vc != null) Physics.IgnoreCollision(sc, vc); }
        All.Add(c);
    }

    static void IgnoreBird(Collider sc)
    {
        if (G.Body != null) foreach (var c in G.Body.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(sc, c);
        if (G.Bird != null) foreach (var c in G.Bird.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(sc, c);
    }

    void Update()
    {
        age += Time.deltaTime;
        spin += 320f * Time.deltaTime;
        visual.rotation = Quaternion.Euler(0, spin, 0) * Quaternion.Euler(0, 0, 90);
        if (G.Body == null) return;
        var target = G.Pos + Vector3.up * 0.25f;
        var d = target - transform.position;
        if (age > 0.8f && d.magnitude < 4f) magnet = true;
        if (magnet && rb != null) rb.velocity = Vector3.Lerp(rb.velocity, d.normalized * 9f, 1f - Mathf.Exp(-10f * Time.deltaTime));
        if (age > 0.35f && d.magnitude < 0.55f) { Collect(); return; }
        if (age > 28f) Kill();
        else if (age > 24f) visual.gameObject.SetActive(Mathf.FloorToInt(age * 8f) % 2 == 0);
    }

    void Collect()
    {
        Wallet += Value[tier];
        WalletFlash = 0.4f;
        Mix.Play(Mix.Sound("coin.wav"), transform.position, 0.55f, 1.0f + 0.25f * tier + Random.Range(-0.05f, 0.1f));
        Mix.Burst(transform.position, tier == 2 ? new Color(1f, 0.85f, 0.2f) : (tier == 1 ? new Color(0.85f, 0.9f, 1f) : new Color(0.9f, 0.5f, 0.25f)), 5, 1.8f, 0.03f, 0.5f);
        Kill();
    }

    void Kill() { All.Remove(this); Destroy(gameObject); }
    void OnDestroy() { All.Remove(this); }
}
