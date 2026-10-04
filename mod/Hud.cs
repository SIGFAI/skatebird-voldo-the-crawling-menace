// Terraria-style screen furniture: the boss health bar, the coin counter, floating damage numbers and call-outs.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public static class Mod
{
    public static int kills;

    class Floating { public Vector3 pos; public string text; public Color color; public int size; public float t, life; public float drift; }
    static readonly List<Floating> floats = new List<Floating>();
    static GUIStyle label;

    /// <summary>A Terraria damage number over a hit: orange on enemies, red on the player, bigger yellow for crits.</summary>
    public static void Number(Vector3 pos, int dmg, bool crit, bool onPlayer = false)
    {
        var col = onPlayer ? new Color(1f, 0.2f, 0.2f) : (crit ? new Color(1f, 0.95f, 0.25f) : new Color(1f, 0.55f, 0.15f));
        floats.Add(new Floating { pos = pos + new Vector3(Random.Range(-0.15f, 0.15f), 0, Random.Range(-0.15f, 0.15f)), text = dmg.ToString() + (crit ? "!" : ""), color = col, size = crit ? 54 : 38, life = 1.1f, drift = Random.Range(-30f, 30f) });
    }

    public static void Callout(Vector3 pos, string text, Color col)
    {
        floats.Add(new Floating { pos = pos, text = text, color = col, size = 44, life = 1.5f });
    }

    static void Text(Rect r, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter)
    {
        if (label == null) label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false };
        label.fontSize = size; label.alignment = a;
        var prev = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.9f * c.a);
        for (int ox = -2; ox <= 2; ox += 2)
            for (int oy = -2; oy <= 2; oy += 2)
                if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox, r.y + oy, r.width, r.height), s, label);
        GUI.color = c;
        GUI.Label(r, s, label);
        GUI.color = prev;
    }

    static void Box(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p; }

    public static void Draw()
    {
        if (Event.current.type != EventType.Repaint) return;
        float k = Screen.height / 1080f;
        DrawBossBar(k);
        DrawWallet(k);
        DrawFloats(k);
    }

    static void DrawBossBar(float k)
    {
        var v = Voldo.Live;
        if (v == null || v.state == Voldo.St.Dead) return;
        float w = 700 * k, h = 34 * k, x = (Screen.width - w) / 2f, y = 70 * k;
        Text(new Rect(x, y - 44 * k, w, 40 * k), Voldo.Title, Mathf.RoundToInt(30 * k), v.enraged ? new Color(1f, 0.45f, 0.4f) : Color.white);
        Box(new Rect(x - 5 * k, y - 5 * k, w + 10 * k, h + 10 * k), new Color(0.05f, 0.05f, 0.08f, 0.9f));
        Box(new Rect(x - 5 * k, y - 5 * k, w + 10 * k, 2 * k), new Color(0.55f, 0.55f, 0.65f, 1f));
        Box(new Rect(x - 5 * k, y + h + 3 * k, w + 10 * k, 2 * k), new Color(0.55f, 0.55f, 0.65f, 1f));
        float f = Mathf.Clamp01(v.hp / Voldo.MaxHp), s = Mathf.Clamp01(v.shownHp / Voldo.MaxHp);
        Box(new Rect(x, y, w, h), new Color(0.2f, 0.03f, 0.04f, 1f));
        Box(new Rect(x, y, w * s, h), new Color(1f, 0.85f, 0.85f, 0.9f));       // the chip of recent damage
        Box(new Rect(x, y, w * f, h), v.enraged ? new Color(0.95f, 0.18f, 0.1f) : new Color(0.8f, 0.1f, 0.12f));
        Box(new Rect(x, y, w * f, h * 0.4f), new Color(1f, 0.45f, 0.4f, 0.55f));  // the shine
        Text(new Rect(x, y, w, h), Mathf.CeilToInt(Mathf.Max(0, v.hp)) + " / " + Voldo.MaxHp, Mathf.RoundToInt(22 * k), Color.white);
    }

    static void DrawWallet(float k)
    {
        long w = Coin.Wallet;
        int gold = (int)(w / 10000), silver = (int)((w / 100) % 100), copper = (int)(w % 100);
        float pw = 330 * k, ph = 84 * k, x = Screen.width - pw - 24 * k, y = 24 * k;
        float pulse = Coin.WalletFlash > 0f ? 1f + Coin.WalletFlash * 0.4f : 1f;
        Coin.WalletFlash -= Time.unscaledDeltaTime;
        Box(new Rect(x, y, pw, ph), new Color(0.08f, 0.1f, 0.3f, 0.82f));
        Box(new Rect(x, y, pw, 3 * k), new Color(0.6f, 0.65f, 0.95f, 1f));
        Box(new Rect(x, y + ph - 3 * k, pw, 3 * k), new Color(0.6f, 0.65f, 0.95f, 1f));
        Text(new Rect(x, y + 2 * k, pw, 26 * k), "Coins", Mathf.RoundToInt(22 * k), new Color(1f, 0.95f, 0.8f));
        string[] tex = { "coin_icon.png", "coin_silver.png", "coin_copper.png" };
        int[] val = { gold, silver, copper };
        for (int i = 0; i < 3; i++)
        {
            float cx = x + 10 * k + i * (pw - 20 * k) / 3f;
            float isz = 38 * k * (i == 2 || val[i] == 0 ? 1f : 1f) * (Coin.WalletFlash > 0f ? pulse : 1f);
            GUI.DrawTexture(new Rect(cx + 6 * k, y + 34 * k, isz, isz), Mix.Texture(tex[i]));
            Text(new Rect(cx + 48 * k, y + 32 * k, 64 * k, 42 * k), val[i].ToString(), Mathf.RoundToInt(30 * k), Color.white, TextAnchor.MiddleLeft);
        }
    }

    static void DrawFloats(float k)
    {
        var cam = G.Cam;
        if (cam == null) return;
        for (int i = floats.Count - 1; i >= 0; i--)
        {
            var f = floats[i];
            f.t += Time.unscaledDeltaTime;
            if (f.t > f.life) { floats.RemoveAt(i); continue; }
            var sp = cam.WorldToScreenPoint(f.pos);
            if (sp.z < 0) continue;
            float u = f.t / f.life;
            float pop = u < 0.12f ? Mathf.Lerp(0.5f, 1.25f, u / 0.12f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((u - 0.12f) / 0.2f));
            var r = new Rect(sp.x - 350 + f.drift * u, Screen.height - sp.y - 60 * u * k * 2f - 30, 700, 60);
            var c = f.color; c.a = u > 0.65f ? 1f - (u - 0.65f) / 0.35f : 1f;
            Text(r, f.text, Mathf.RoundToInt(f.size * k * pop), c);
        }
    }
}
