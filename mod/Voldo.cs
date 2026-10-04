// Voldo the Crawling Menace: a blindfolded, clawed crawler built from primitives with generated textures.
// He creeps around the park, stalks the bird, winds up and lunges. Hopping over him or grinding past makes him
// stumble and spill coins; a trick combo stuns him; ramming him at speed hurts. Below half health he is enraged.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class Voldo : MonoBehaviour
{
    public enum St { Creep, Stalk, Windup, Lunge, Recover, Stumble, Stun, Dead }

    public static Voldo Live;
    /// <summary>Optional leash (the demo keeps him inside the arena): centre and radius.</summary>
    public static Vector3? Leash;
    public static float LeashR = 11f;
    public const string Title = "Voldo the Crawling Menace";
    public const float MaxHp = 100f;
    const float Sz = 2.4f;

    public float hp = MaxHp, shownHp = MaxHp;
    public St state = St.Creep;
    public bool enraged;
    public Vector3 home;

    float stateT, flash, phase, gy, hitCooldown, dodgeCooldown, ramCooldown, birdHitT, breath;
    Vector3 lungeDir, wander;
    float wanderT;
    Color flashColor = Color.white;
    bool lungeHitBird;
    float stunLeft;

    Transform body, head, jaw;
    Transform[] shoulder = new Transform[4], elbow = new Transform[4];
    readonly List<Renderer> rends = new List<Renderer>();
    readonly List<Color> baseCols = new List<Color>();
    readonly List<Transform> stars = new List<Transform>();
    Light rageLight;
    Light dangerLight;

    static Texture2D skinTex, clothTex;

    // ---------- building ----------
    public static Voldo Spawn(Vector3 pos)
    {
        if (Live != null) Destroy(Live.gameObject);
        skinTex = Mix.Texture("skin.png");
        clothTex = Mix.Texture("cloth.png");
        var go = new GameObject("Voldo");
        go.layer = 2;
        go.transform.position = pos;
        var v = go.AddComponent<Voldo>();
        v.Build();
        v.home = pos;
        Live = v;
        return v;
    }

    Transform Part(Transform parent, PrimitiveType t, Vector3 lpos, Vector3 lscale, Color c, Texture2D tex = null, float glow = 0f, Vector3? euler = null)
    {
        var p = GameObject.CreatePrimitive(t);
        p.name = "VoldoPart";
        DestroyImmediate(p.GetComponent<Collider>());
        p.layer = 2;
        p.transform.SetParent(parent, false);
        p.transform.localPosition = lpos;
        p.transform.localScale = lscale;
        if (euler.HasValue) p.transform.localRotation = Quaternion.Euler(euler.Value);
        Mix.Paint(p, c, glow, tex);
        var r = p.GetComponent<Renderer>();
        if (tex != null) r.material.mainTextureScale = new Vector2(1.5f, 1.5f);
        rends.Add(r);
        baseCols.Add(c);
        return p.transform;
    }

    Transform Pivot(Transform parent, string name, Vector3 lpos)
    {
        var g = new GameObject(name);
        g.layer = 2;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lpos;
        return g.transform;
    }

    void Build()
    {
        var skin = new Color(0.9f, 0.82f, 0.84f);
        var skinDark = new Color(0.66f, 0.55f, 0.62f);
        var bone = new Color(0.93f, 0.9f, 0.78f);
        var cloth = new Color(0.55f, 0.55f, 0.6f);
        var red = new Color(0.55f, 0.08f, 0.1f);

        gy = GroundAt(transform.position);
        body = Pivot(transform, "Body", new Vector3(0, 0.34f * Sz, 0));
        body.localScale = Vector3.one * Sz;
        Part(body, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.4f, 0.3f, 0.85f), skin, skinTex);
        Part(body, PrimitiveType.Sphere, new Vector3(0, 0.1f, -0.27f), new Vector3(0.32f, 0.3f, 0.44f), skinDark, skinTex);
        for (int i = 0; i < 5; i++)
        {
            float z = -0.45f + i * 0.2f;
            Part(body, PrimitiveType.Cube, new Vector3(0, 0.2f + (i == 1 || i == 2 ? 0.06f : 0f), z), new Vector3(0.035f, 0.1f, 0.035f), bone, null, 0f, new Vector3(35, 0, 45));
        }
        // ribs showing through the torn skin
        for (int i = 0; i < 3; i++)
            Part(body, PrimitiveType.Cube, new Vector3(0, -0.02f, -0.1f + i * 0.14f), new Vector3(0.42f, 0.025f, 0.035f), bone * 0.9f, null, 0f, new Vector3(0, 0, 0));

        head = Pivot(body, "Head", new Vector3(0, 0.03f, 0.5f));
        Part(head, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.46f, 0.4f, 0.46f), skin, skinTex);
        // the blindfold: a ring around the eyes, a knot and two ragged tails behind
        Part(head, PrimitiveType.Cylinder, new Vector3(0, 0.03f, 0f), new Vector3(0.485f, 0.04f, 0.505f), new Color(0.16f, 0.15f, 0.18f), clothTex);
        Part(head, PrimitiveType.Cube, new Vector3(0, 0.06f, -0.25f), new Vector3(0.09f, 0.08f, 0.07f), cloth * 0.4f, clothTex);
        Part(head, PrimitiveType.Cube, new Vector3(-0.04f, 0.04f, -0.3f), new Vector3(0.04f, 0.015f, 0.18f), cloth, clothTex, 0f, new Vector3(0, 20, 0));
        Part(head, PrimitiveType.Cube, new Vector3(0.04f, 0.0f, -0.3f), new Vector3(0.04f, 0.015f, 0.2f), cloth, clothTex, 0f, new Vector3(0, -25, 10));
        // matted hair and two bone horns so the head reads from every side
        var hair = new Color(0.14f, 0.09f, 0.17f);
        for (int i = 0; i < 9; i++)
        {
            float a = i * 40f * Mathf.Deg2Rad;
            var hp = new Vector3(Mathf.Cos(a) * 0.1f, 0.16f, Mathf.Sin(a) * 0.1f - 0.02f);
            Part(head, PrimitiveType.Cube, hp, new Vector3(0.035f, 0.17f, 0.035f), hair, null, 0f, new Vector3(Mathf.Sin(a) * 40f, 0, -Mathf.Cos(a) * 40f));
        }
        Part(head, PrimitiveType.Cube, new Vector3(-0.16f, 0.17f, 0.06f), new Vector3(0.035f, 0.15f, 0.035f), bone, null, 0f, new Vector3(-10, 0, 35));
        Part(head, PrimitiveType.Cube, new Vector3(0.16f, 0.17f, 0.06f), new Vector3(0.035f, 0.15f, 0.035f), bone, null, 0f, new Vector3(-10, 0, -35));
        // ragged ears
        Part(head, PrimitiveType.Cube, new Vector3(-0.2f, 0.1f, -0.05f), new Vector3(0.14f, 0.05f, 0.1f), skinDark, skinTex, 0f, new Vector3(0, 0, 40));
        Part(head, PrimitiveType.Cube, new Vector3(0.2f, 0.1f, -0.05f), new Vector3(0.14f, 0.05f, 0.1f), skinDark, skinTex, 0f, new Vector3(0, 0, -40));
        // maw: dark mouth, upper teeth, a hinged lower jaw with teeth
        Part(head, PrimitiveType.Cube, new Vector3(0, -0.07f, 0.17f), new Vector3(0.28f, 0.06f, 0.14f), red);
        for (int i = 0; i < 5; i++)
            Part(head, PrimitiveType.Cube, new Vector3(-0.1f + i * 0.05f, -0.04f, 0.23f), new Vector3(0.022f, 0.06f, 0.022f), bone);
        jaw = Pivot(head, "Jaw", new Vector3(0, -0.09f, 0.1f));
        Part(jaw, PrimitiveType.Cube, new Vector3(0, -0.02f, 0.08f), new Vector3(0.26f, 0.04f, 0.2f), skinDark, skinTex);
        for (int i = 0; i < 5; i++)
            Part(jaw, PrimitiveType.Cube, new Vector3(-0.1f + i * 0.05f, 0.015f, 0.16f), new Vector3(0.02f, 0.06f, 0.02f), bone);

        // four limbs: shoulder pivot, long upper arm, elbow pivot, forearm, three claws
        Vector3[] sh = { new Vector3(-0.22f, 0.02f, 0.28f), new Vector3(0.22f, 0.02f, 0.28f), new Vector3(-0.2f, 0.0f, -0.28f), new Vector3(0.2f, 0.0f, -0.28f) };
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            float s = front ? 1.5f : 1.25f;
            shoulder[i] = Pivot(body, "Shoulder" + i, sh[i]);
            Part(shoulder[i], PrimitiveType.Capsule, new Vector3(0, -0.13f * s, 0), new Vector3(0.09f, 0.14f * s, 0.09f), skin, skinTex);
            elbow[i] = Pivot(shoulder[i], "Elbow" + i, new Vector3(0, -0.26f * s, 0));
            Part(elbow[i], PrimitiveType.Capsule, new Vector3(0, -0.13f * s, 0), new Vector3(0.065f, 0.14f * s, 0.065f), skinDark, skinTex);
            var hand = Pivot(elbow[i], "Hand" + i, new Vector3(0, -0.27f * s, 0));
            for (int c = -1; c <= 1; c++)
                Part(hand, PrimitiveType.Cube, new Vector3(c * 0.045f, -0.04f, 0.05f), new Vector3(0.032f, 0.03f, front ? 0.24f : 0.16f), bone, null, 0f, new Vector3(25, c * 12, 0));
        }

        // stun stars (hidden until stunned): two crossed flat bars each, spinning
        for (int i = 0; i < 5; i++)
        {
            var star = Pivot(head, "Star" + i, Vector3.zero);
            var gold = new Color(1f, 0.9f, 0.2f);
            for (int b = 0; b < 4; b++)
                Part(star, PrimitiveType.Cube, Vector3.zero, new Vector3(0.13f, 0.025f, 0.025f), gold, null, 1.3f, new Vector3(0, b * 45f, 0));
            for (int n = 0; n < 4; n++) { rends.RemoveAt(rends.Count - 1); baseCols.RemoveAt(baseCols.Count - 1); }
            star.gameObject.SetActive(false);
            stars.Add(star);
        }
        rageLight = Mix.Glow(transform.position + Vector3.up * 0.5f, new Color(1f, 0.15f, 0.1f), 2.2f, 0f, transform);
        dangerLight = Mix.Glow(transform.position + Vector3.up * 0.5f, new Color(1f, 0.3f, 0.15f), 3f, 0f, transform);

        // solid body: a kinematic box the board really bumps into
        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var bc = gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.22f * Sz, 0.05f * Sz);
        bc.size = new Vector3(0.55f, 0.26f, 1.1f) * Sz;
    }

    // ---------- helpers ----------
    static bool IsBird(Collider c)
    {
        if (G.Body == null) return false;
        var rb = c.attachedRigidbody;
        return rb == G.Body || c.transform.IsChildOf(G.Body.transform) || (G.Bird != null && c.transform.IsChildOf(G.Bird.transform));
    }

    public static float GroundAt(Vector3 p, float from = 1.2f)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * from, Vector3.down, from + 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsBird(hits[i].collider) || hits[i].collider.gameObject.layer == 2) continue;
            if (hits[i].point.y > best) best = hits[i].point.y;
        }
        return float.IsNegativeInfinity(best) ? p.y : best;
    }

    Vector3 BirdFlat()
    {
        var d = G.Pos - transform.position; d.y = 0; return d;
    }

    public float BirdDist() { return BirdFlat().magnitude; }
    public bool Vulnerable() { return state != St.Dead; }

    // ---------- combat ----------
    public void Hurt(float dmg, string why, bool crit = false)
    {
        if (state == St.Dead) return;
        hp -= dmg;
        flash = 0.18f; flashColor = new Color(1f, 0.25f, 0.2f);
        Mod.Number(transform.position + Vector3.up * 1.6f, Mathf.RoundToInt(dmg), crit);
        Mix.Play(Mix.Sound("hit.wav"), transform.position, 1f, Random.Range(0.9f, 1.15f));
        Mix.Burst(transform.position + Vector3.up * 0.4f, new Color(0.75f, 0.05f, 0.05f), 14, 3.2f, 0.045f, 0.9f);
        Mix.Burst(transform.position + Vector3.up * 0.4f, new Color(0.9f, 0.9f, 0.7f), 4, 2.5f, 0.03f, 0.7f);
        Mix.Log("voldo hurt " + dmg + " by " + why + " hp " + Mathf.Max(0, hp).ToString("F0") + " state " + state);
        if (hp <= 0f) { Die(); return; }
        if (!enraged && hp <= MaxHp * 0.5f)
        {
            enraged = true;
            Mix.Say("Voldo is enraged!", 3f, new Color(1f, 0.3f, 0.25f), 0.18f, 56);
            Mix.Play(Mix.Sound("screech.wav"), transform.position, 1f, 0.8f);
            Mix.Log("voldo enraged");
        }
    }

    /// <summary>Back on his feet and ready to be dodged again (used when the bird rides in for a grind).</summary>
    public void ReadyForDodge() { dodgeCooldown = 0f; if (state != St.Dead) Enter(St.Stalk); }

    public void Stumble(string why)
    {
        if (state == St.Dead || state == St.Stun || state == St.Stumble || dodgeCooldown > 0f) return;
        dodgeCooldown = 2.2f;
        Enter(St.Stumble);
        Mix.Log("voldo stumbles: " + why);
        Mod.Callout(transform.position + Vector3.up * 1.1f, why == "grind" ? "GRIND PAST!" : "HOPPED OVER!", new Color(1f, 0.85f, 0.3f));
        Hurt(10f, why);
        if (state == St.Dead) return;
        Mix.Play(Mix.Sound("growl.wav"), transform.position, 0.9f, 1.3f);
        Coin.Drop(transform.position + Vector3.up * 0.7f, 5 + Random.Range(0, 4), Random.value < 0.5f ? 1 : 0, 0);
    }

    public void Stun(float secs)
    {
        if (state == St.Dead) return;
        stunLeft = secs;
        Enter(St.Stun);
        Mix.Play(Mix.Sound("stun.wav"), transform.position, 1f);
        Mod.Callout(transform.position + Vector3.up * 1.2f, "STUNNED!", new Color(1f, 0.95f, 0.3f));
        Mix.Log("voldo stunned " + secs);
    }

    void Die()
    {
        Enter(St.Dead);
        Mix.Log("voldo defeated");
        Mod.kills++;
        Mix.Say(Title + " has been defeated!", 4f, new Color(0.69f, 0.3f, 1f), 0.18f, 52);
        Mix.Play(Mix.Sound("death.wav"), transform.position, 1f);
        var p = transform.position + Vector3.up * 0.5f;
        Mix.Burst(p, new Color(0.75f, 0.05f, 0.05f), 40, 4.5f, 0.06f, 1.4f);
        Mix.Burst(p, new Color(0.9f, 0.9f, 0.7f), 14, 4f, 0.04f, 1.4f);
        var l = Mix.Glow(p, new Color(1f, 0.8f, 0.3f), 5f, 3f);
        Destroy(l.gameObject, 0.6f);
        Coin.Drop(transform.position + Vector3.up * 0.8f, 24, 8, 1);
        G.Screm();
        Destroy(gameObject, 4f);
    }

    void Enter(St s) { state = s; stateT = 0f; lungeHitBird = false; if (s != St.Stun) foreach (var st in stars) st.gameObject.SetActive(false); else foreach (var st in stars) st.gameObject.SetActive(true); }

    // ---------- brain ----------
    void Update()
    {
        float dt = Time.deltaTime;
        stateT += dt; phase += dt;
        hitCooldown -= dt; dodgeCooldown -= dt; ramCooldown -= dt; birdHitT -= dt;
        if (flash > 0f) flash -= dt;
        shownHp = Mathf.MoveTowards(shownHp, Mathf.Max(0, hp), dt * 40f);

        if (state != St.Dead)
        {
            Think(dt);
            Interact();
            if (Leash.HasValue)
            {
                var o = transform.position - Leash.Value; o.y = 0;
                if (o.magnitude > LeashR) { var tp0 = Leash.Value + o.normalized * LeashR; tp0.y = transform.position.y; transform.position = tp0; }
            }
        }
        // ground and dress
        gy = Mathf.Lerp(gy, GroundAt(transform.position), 1f - Mathf.Exp(-18f * dt));
        var tp = transform.position; tp.y = gy; transform.position = tp;
        Animate(dt);
        Tint();
    }

    void Face(Vector3 dir, float turn, float dt)
    {
        dir.y = 0; if (dir.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turn * dt);
    }

    bool Blocked(Vector3 dir, float dist)
    {
        var o = transform.position + Vector3.up * 0.3f;
        RaycastHit h;
        if (Physics.SphereCast(o, 0.2f, dir, out h, dist, ~0, QueryTriggerInteraction.Ignore) && !IsBird(h.collider) && h.collider.gameObject.layer != 2 && h.normal.y < 0.7f) return true;
        // a cliff or a big step ahead
        float ahead = GroundAt(transform.position + dir * (dist + 0.3f), 1.5f);
        return Mathf.Abs(ahead - gy) > 0.5f;
    }

    void Step(Vector3 dir, float speed, float dt)
    {
        dir.y = 0; dir.Normalize();
        if (Blocked(dir, speed * dt + 0.3f)) { wanderT = 0f; return; }
        transform.position += dir * speed * dt;
        phase += speed * dt * 2.2f;
    }

    void Think(float dt)
    {
        float d = BirdDist();
        float turn = enraged ? 300f : 200f;
        float vMul = enraged ? 1.45f : 1f;
        switch (state)
        {
            case St.Creep:
                // wander around home; home drifts toward the bird so he is never far
                if (d > 14f) home = Vector3.Lerp(home, G.Pos, dt * 0.25f);
                wanderT -= dt;
                if (wanderT <= 0f)
                {
                    wanderT = Random.Range(1.5f, 3.2f);
                    var r = Random.insideUnitCircle * 5f;
                    wander = home + new Vector3(r.x, 0, r.y);
                }
                var to = wander - transform.position; to.y = 0;
                if (to.magnitude > 0.4f) { Face(to, turn, dt); Step(transform.forward, 1.2f * vMul, dt); }
                if (d < 8f) { Enter(St.Stalk); Mix.Play(Mix.Sound("growl.wav"), transform.position, 0.9f); }
                break;
            case St.Stalk:
                Face(BirdFlat(), turn, dt);
                if (d > 3.8f) Step(transform.forward, 2.2f * vMul, dt);
                if (d > 13f) Enter(St.Creep);
                else if (d < 4.6f && stateT > 0.7f) Enter(St.Windup);
                break;
            case St.Windup:
                Face(BirdFlat(), turn * 1.2f, dt);
                if (stateT > (enraged ? 0.45f : 0.7f))
                {
                    lungeDir = BirdFlat().normalized;
                    Enter(St.Lunge);
                    Mix.Play(Mix.Sound("screech.wav"), transform.position, 1f, Random.Range(0.95f, 1.1f));
                }
                break;
            case St.Lunge:
                Face(lungeDir, 900f, dt);
                Step(lungeDir, 11f * vMul, dt);
                if (stateT > 0.5f) Enter(St.Recover);
                break;
            case St.Recover:
                if (stateT > (enraged ? 0.55f : 1.0f)) Enter(d < 9f ? St.Stalk : St.Creep);
                break;
            case St.Stumble:
                Step(transform.forward, 1.5f * (1f - stateT), dt);
                if (stateT > 1.1f) Enter(St.Stalk);
                break;
            case St.Stun:
                stunLeft -= dt;
                if (stunLeft <= 0f) { Enter(St.Recover); Mix.Log("voldo shakes it off"); }
                break;
        }
    }

    void Interact()
    {
        if (G.Body == null) return;
        var bf = BirdFlat();
        float d = bf.magnitude;
        float height = G.Pos.y - gy;
        // lunge connects: knock the bird away (the bird is not hurt in the demo, but it feels it)
        if (state == St.Lunge && !lungeHitBird && d < 1.5f && height < 0.9f)
        {
            lungeHitBird = true;
            birdHitT = 1.2f;
            var dir = bf.sqrMagnitude > 0.001f ? bf.normalized : lungeDir;
            G.Push(dir * 3.2f + Vector3.up * 1.8f);
            Mod.Number(G.Pos + Vector3.up * 0.6f, 17, false, true);
            Mix.Play(Mix.Sound("hit.wav"), G.Pos, 1f, 0.8f);
            Mix.Burst(G.Pos + Vector3.up * 0.2f, new Color(1f, 0.2f, 0.2f), 12, 3f, 0.04f, 0.8f);
            G.Screm();
            if (!Mix.DemoRun) G.Bail();
            Mix.Log("voldo lunge hit the bird");
            Enter(St.Recover);
        }
        // hop over him, or grind past him: he stumbles and drops coins
        if (state != St.Dead && state != St.Stun && state != St.Stumble)
        {
            bool grinding = G.Board != null && G.Board.Trick != null && G.Board.Trick.IsPlayingGrind();
            if (grinding && d < 2.7f) Stumble("grind");
            else if (d < 1.8f && G.InAir && height > 0.3f && !(state == St.Lunge && birdHitT > 0f)) Stumble("hop");
        }
        // ramming him at speed
        if (d < 1.5f && height < 0.8f && ramCooldown <= 0f && birdHitT <= 0f && G.Speed > 3.2f)
        {
            ramCooldown = 0.7f;
            float mul = state == St.Stun ? 2f : 1f;
            Hurt(Mathf.Round(10f * mul), "ram", state == St.Stun);
            var push = -bf.normalized;
            transform.position += -push * 0.25f;
            if (state != St.Stun && state != St.Dead) { Enter(St.Recover); }
        }
    }

    // ---------- looks ----------
    void Tint()
    {
        float amt = 0f; Color col = flashColor;
        if (flash > 0f) { amt = Mathf.Clamp01(flash / 0.18f); }
        else if (state == St.Windup) { amt = 0.35f + 0.25f * Mathf.Sin(stateT * 30f); col = new Color(1f, 0.15f, 0.1f); }
        else if (enraged) { amt = 0.1f; col = new Color(1f, 0.12f, 0.08f); }
        if (state == St.Dead) { amt = Mathf.Clamp01(stateT * 0.4f); col = new Color(0.1f, 0.1f, 0.1f); }
        for (int i = 0; i < rends.Count; i++)
            if (rends[i] != null) rends[i].material.color = Color.Lerp(baseCols[i], col, amt);
        rageLight.intensity = enraged && state != St.Dead ? 1.2f + 0.4f * Mathf.Sin(Time.time * 9f) : 0f;
        dangerLight.intensity = state == St.Windup ? 2.5f : (state == St.Lunge ? 1.5f : 0f);
    }

    void Limb(int i, float swing, float flare, float bend, float dt, float rate = 14f)
    {
        float side = (i % 2 == 0) ? -1f : 1f;
        var tgt = Quaternion.Euler(swing, 0, side * -flare);
        shoulder[i].localRotation = Quaternion.Slerp(shoulder[i].localRotation, tgt, 1f - Mathf.Exp(-rate * dt));
        elbow[i].localRotation = Quaternion.Slerp(elbow[i].localRotation, Quaternion.Euler(bend, 0, 0), 1f - Mathf.Exp(-rate * dt));
    }

    void Animate(float dt)
    {
        float bodyY = 0.34f, pitch = 0f, roll = 0f, headPitch = 0f, jawOpen = 0f, scaleY = 1f, headYaw = 0f;
        float k = 1f - Mathf.Exp(-12f * dt);
        switch (state)
        {
            case St.Creep:
            case St.Stalk:
            {
                float amp = state == St.Stalk ? 38f : 28f;
                float w = phase * 2.6f;
                Limb(0, Mathf.Sin(w) * amp - 15, 38, -55 + Mathf.Max(0, Mathf.Sin(w)) * 20, dt);
                Limb(3, Mathf.Sin(w) * amp, 40, -40, dt);
                Limb(1, Mathf.Sin(w + Mathf.PI) * amp - 15, 38, -55 + Mathf.Max(0, Mathf.Sin(w + Mathf.PI)) * 20, dt);
                Limb(2, Mathf.Sin(w + Mathf.PI) * amp, 40, -40, dt);
                bodyY = 0.33f + Mathf.Abs(Mathf.Sin(w)) * 0.025f;
                roll = Mathf.Sin(w) * 3f;
                headYaw = Mathf.Sin(phase * 1.3f) * 20f;
                headPitch = 6f + Mathf.Sin(phase * 2f) * 4f;
                jawOpen = 8f + Mathf.Sin(phase * 3f) * 5f;
                break;
            }
            case St.Windup:
            {
                // rears back, claws high, trembling
                float tr = Mathf.Sin(stateT * 60f) * 3f;
                Limb(0, -95 + tr, 30, -30, dt, 20); Limb(1, -95 - tr, 30, -30, dt, 20);
                Limb(2, 30, 45, -60, dt); Limb(3, 30, 45, -60, dt);
                bodyY = 0.27f; pitch = -14f; headPitch = -10f; jawOpen = 38f; headYaw = Mathf.Sin(stateT * 40f) * 8f;
                break;
            }
            case St.Lunge:
            {
                Limb(0, -145, 15, -5, dt, 30); Limb(1, -145, 15, -5, dt, 30);
                Limb(2, 55, 15, -10, dt, 30); Limb(3, 55, 15, -10, dt, 30);
                bodyY = 0.4f; pitch = 22f; headPitch = 10f; jawOpen = 48f;
                break;
            }
            case St.Recover:
            {
                Limb(0, 10, 45, -50, dt); Limb(1, 10, 45, -50, dt); Limb(2, 5, 45, -45, dt); Limb(3, 5, 45, -45, dt);
                breath = Mathf.Sin(stateT * 14f);
                bodyY = 0.28f + breath * 0.015f; headPitch = 18f; jawOpen = 20f + breath * 8f;
                break;
            }
            case St.Stumble:
            {
                float u = Mathf.Clamp01(stateT / 1.0f);
                roll = u * 360f;
                pitch = Mathf.Sin(u * Mathf.PI) * 25f;
                bodyY = 0.34f + Mathf.Sin(u * Mathf.PI) * 0.25f;
                float f = Mathf.Sin(stateT * 28f) * 50f;
                Limb(0, f - 60, 70, -20, dt, 25); Limb(1, -f - 60, 70, -20, dt, 25);
                Limb(2, -f, 70, -20, dt, 25); Limb(3, f, 70, -20, dt, 25);
                jawOpen = 30f;
                break;
            }
            case St.Stun:
            {
                Limb(0, 30, 60, -20, dt); Limb(1, 30, 60, -20, dt); Limb(2, 10, 65, -30, dt); Limb(3, 10, 65, -30, dt);
                bodyY = 0.2f; pitch = 20f; headPitch = 35f; roll = Mathf.Sin(stateT * 6f) * 10f; jawOpen = 25f;
                headYaw = Mathf.Sin(stateT * 5f) * 35f;
                for (int i = 0; i < stars.Count; i++)
                {
                    float a = stateT * 5f + i * Mathf.PI * 2f / stars.Count;
                    stars[i].localPosition = new Vector3(Mathf.Cos(a) * 0.3f, 0.34f + Mathf.Sin(stateT * 8f + i) * 0.03f, Mathf.Sin(a) * 0.3f);
                    stars[i].localRotation = Quaternion.Euler(50f, stateT * 300f, 0);
                }
                break;
            }
            case St.Dead:
            {
                Limb(0, 20, 85, -10, dt, 8); Limb(1, 20, 85, -10, dt, 8); Limb(2, 10, 85, -10, dt, 8); Limb(3, 10, 85, -10, dt, 8);
                scaleY = Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(stateT * 2f));
                bodyY = 0.17f; roll = Mathf.Lerp(0f, 18f, Mathf.Clamp01(stateT * 2f)); headPitch = 30f; jawOpen = 40f;
                if (stateT > 2.8f) transform.localScale = Vector3.one * Mathf.Max(0.01f, 1f - (stateT - 2.8f) / 1.1f);
                break;
            }
        }
        body.localPosition = new Vector3(0, Mathf.Lerp(body.localPosition.y, bodyY * Sz, k), 0);
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(pitch, 0, roll), state == St.Stumble ? 1f : k);
        body.localScale = new Vector3(Sz, Mathf.Lerp(body.localScale.y, scaleY * Sz, k), Sz);
        head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(headPitch, headYaw, 0), k);
        jaw.localRotation = Quaternion.Slerp(jaw.localRotation, Quaternion.Euler(jawOpen, 0, 0), k);
    }
}
