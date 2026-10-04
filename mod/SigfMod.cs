// Voldo the Crawling Menace (Terraria-flavoured boss for SkateBIRD).
//  - a blindfolded clawed crawler with a Terraria boss bar stalks the park and lunges at the bird;
//  - hop over him or grind past him: he stumbles and spills Terraria coins the bird scoops up;
//  - a trick combo near him stuns him; ramming a stunned Voldo hurts double; below half health he is enraged.
using System.Collections;
using Sigf.Kit;
using UnityEngine;

public class SigfMod : MixMod
{
    static readonly Color Purple = new Color(0.69f, 0.3f, 1f);
    float deadAt = -1f;

    public override void OnLoad() => G.StartLevel = "playground";

    public override void OnReady()
    {
        Mix.Say(Voldo.Title, 3f, Purple, 0.2f, 60);
        G.OnTrick(OnTrick);
        foreach (var r in Object.FindObjectsOfType<Skatebirb.GrindableRail>()) Mix.Log("rail " + r.name + " at " + r.transform.position + " bounds " + r.GetComponentInChildren<Collider>());
        Awaken(G.Ahead(7f));
        Mix.Every(1f, () =>
        {
            if (Voldo.Live == null || Voldo.Live.state == Voldo.St.Dead)
            {
                if (deadAt < 0f) deadAt = Time.unscaledTime;
                if (Time.unscaledTime - deadAt > 9f) { deadAt = -1f; Awaken(G.Ahead(8f) + new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f))); }
            }
            if (G.Bailed && Mix.DemoRun) G.GetUp();
        }, "respawn");
        Mix.After(4.5f, () => Mix.Say("Hop over him or grind past for coins. Land a combo to stun him!", 6f, Color.white, 0.3f, 34));
    }

    public static void Awaken(Vector3 pos)
    {
        pos.y = Voldo.GroundAt(pos, 3f);
        var v = Voldo.Spawn(pos);
        // he claws up out of the floor like a Terraria dig: dirt chunks and dust
        Mix.Burst(pos + Vector3.up * 0.1f, new Color(0.45f, 0.3f, 0.18f), 22, 3f, 0.06f, 1.2f);
        Mix.Burst(pos + Vector3.up * 0.1f, new Color(0.3f, 0.2f, 0.12f), 12, 2f, 0.09f, 1.2f);
        Mix.Play(Mix.Sound("growl.wav"), pos, 1f, 0.85f);
        Mix.Say(Voldo.Title + " has awoken!", 3.5f, Purple, 0.2f, 48);
        Mix.Log("voldo awakens at " + pos);
    }

    void OnTrick(Skatebirb.Gameplay.Trick t)
    {
        var v = Voldo.Live;
        if (v == null || v.state == Voldo.St.Dead || v.BirdDist() > 4.5f) return;
        int combo = G.Board != null && G.Board.Combo != null ? G.Board.Combo.CurComboHistory.Count : 1;
        string name = t != null ? t.Name : "trick";
        if (name == "Screm" || name.StartsWith("Air Ollie") || name == "Manual" || name == "Nose Manual") return;
        if (v.state == Voldo.St.Stun) { v.Hurt(10f, name + " on stunned", true); return; }
        if (combo >= 2) { v.Hurt(15f, "combo " + combo, true); if (v.state != Voldo.St.Dead) v.Stun(4f); }
        else v.Hurt(6f, name);
    }

    public override void OnGui() => Mod.Draw();

    // ---------- the demo (the clip) ----------
    static Vector3 Anchor;
    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    static void FaceVoldo()
    {
        var v = Voldo.Live; if (v == null) return;
        var d = Flat(v.transform.position - G.Pos);
        if (d.sqrMagnitude > 0.01f) G.Teleport(G.Pos, Quaternion.LookRotation(d));
    }

    // put the bird dist units from Voldo, facing him, on the floor
    static void PlaceBird(float dist)
    {
        var v = Voldo.Live; if (v == null) return;
        var from = Flat(Anchor - v.transform.position); if (from.sqrMagnitude < 0.1f) from = Vector3.back;
        var p = v.transform.position + from.normalized * dist;
        p.y = Voldo.GroundAt(p, 3f) + 0.25f;
        G.Teleport(p, Quaternion.LookRotation(-from.normalized));
        G.SetVelocity(Vector3.zero);
    }

    IEnumerator GrindOverVoldo()
    {
        // a low rail near the start: Voldo is dragged under it and the bird grinds over his head
        Skatebirb.GrindableRail best = null; float bd = 1e9f;
        foreach (var r in Object.FindObjectsOfType<Skatebirb.GrindableRail>())
        {
            if (r.nextGrindable == null || r.transform.position.y > 5f) continue;
            float d = (r.transform.position - G.Pos).magnitude;
            if (d < bd) { bd = d; best = r; }
        }
        if (best == null) { Mix.Log("no rail for the grind moment"); yield break; }
        Mix.Log("grind moment on rail " + best.transform.position);
        var a = best.transform.position; var b = best.nextGrindable.transform.position;
        var dir = (b - a).normalized;
        var v = Voldo.Live;
        if (v != null && v.state != Voldo.St.Dead)
        {
            // he waits right beside the rail, on whichever side has ground at rail height
            var side = Vector3.Cross(Vector3.up, new Vector3(dir.x, 0, dir.z)).normalized;
            var pa = a + dir * 2.5f + side * 1.3f; var pb = a + dir * 2.5f - side * 1.3f;
            float ya = Voldo.GroundAt(pa, 1.5f), yb = Voldo.GroundAt(pb, 1.5f);
            var at = Mathf.Abs(ya - a.y) < Mathf.Abs(yb - a.y) ? pa : pb;
            at.y = Mathf.Max(ya, yb) > 0f ? Voldo.GroundAt(at, 1.5f) : at.y;
            v.transform.position = at; v.home = at; v.ReadyForDodge();
            Mix.Log("voldo waits by the rail at " + at);
        }
        G.Teleport(a + Vector3.up * 0.25f, Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)));
        G.SetVelocity(dir * 4f);
        for (int i = 0; i < 12; i++) { yield return Mix.Wait(0.1f); if (G.Board.AttemptToGrind()) { Mix.Log("grinding"); break; } }
        yield return Mix.Wait(2.2f);
        G.Launch(5f);
    }

    public override IEnumerator Demo()
    {
        // 1. he stalks the bird and lunges
        Anchor = G.Pos; Voldo.Leash = Anchor;
        yield return Mix.Wait(1f);
        FaceVoldo();
        float t0 = Time.unscaledTime;
        while (Voldo.Live != null && Voldo.Live.state != Voldo.St.Recover && Time.unscaledTime - t0 < 6f) yield return Mix.Wait(0.1f);
        yield return Mix.Wait(0.8f);
        // 2-4. hop over him, combo, ram him while he is stunned
        bool grinded = false;
        t0 = Time.unscaledTime;
        while (Time.unscaledTime - t0 < 38f)
        {
            var v = Voldo.Live;
            if (v == null || v.state == Voldo.St.Dead)
            {
                // scoop up the coin shower
                Coin best = null; float bdist = 1e9f;
                foreach (var c in Coin.All) { float cd = (c.transform.position - G.Pos).magnitude; if (cd < bdist) { bdist = cd; best = c; } }
                if (best != null && bdist > 1.5f)
                {
                    var dd = Flat(best.transform.position - G.Pos);
                    if (dd.sqrMagnitude > 0.01f) G.Teleport(G.Pos, Quaternion.LookRotation(dd));
                    G.Boost(3.5f);
                }
                yield return Mix.Wait(0.4f); continue;
            }
            if (G.Bailed) { G.GetUp(); yield return Mix.Wait(1f); continue; }
            if (!grinded && v.hp < 60f) { grinded = true; var gi = GrindOverVoldo(); while (gi.MoveNext()) yield return gi.Current; continue; }
            float d = v.BirdDist();
            if (d > 11f || Flat(G.Pos - Anchor).magnitude > 16f) { PlaceBird(6f); yield return Mix.Wait(0.5f); continue; }
            if (v.state == Voldo.St.Stun)
            {
                FaceVoldo();
                G.Boost(6f);
                yield return Mix.Wait(0.5f);
                continue;
            }
            FaceVoldo();
            if (d > 3.3f) { G.Boost(4.5f); yield return Mix.Wait(0.35f); continue; }
            // over he goes: a hop with a kickflip into a heelflip
            G.Launch(5.5f);
            yield return Mix.Wait(0.2f);
            G.Flip("Kickflip");
            yield return Mix.Wait(0.45f);
            G.Flip("Heelflip");
            yield return Mix.Wait(1.2f);
        }
    }
}
