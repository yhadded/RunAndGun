using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SpriteGenerator
{
    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color32 OutlineColor = new Color32(22, 22, 30, 255);
    private static System.Random rng;

    private class Pal
    {
        public Color32 Skin, Torso, Pants, Boots, Head, Belt, Claw;
    }

    private static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    private static readonly Pal Player = new Pal { Skin = C(240, 196, 150), Torso = C(86, 128, 62), Pants = C(150, 125, 80), Boots = C(60, 45, 35), Head = C(70, 100, 55), Belt = C(60, 45, 30), Claw = C(230, 230, 200) };
    private static readonly Pal Zombie = new Pal { Skin = C(140, 190, 120), Torso = C(110, 80, 140), Pants = C(70, 60, 90), Boots = C(50, 40, 50), Head = C(60, 90, 50), Belt = C(70, 60, 90), Claw = C(240, 240, 210) };
    private static readonly Pal Soldier = new Pal { Skin = C(230, 180, 140), Torso = C(170, 55, 55), Pants = C(90, 90, 95), Boots = C(40, 40, 45), Head = C(110, 110, 120), Belt = C(50, 50, 50), Claw = C(230, 230, 200) };

    private class Img
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public Img(int w, int h)
        {
            W = w;
            H = h;
            Px = new Color32[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = Clear;
        }

        public Color32 Get(int x, int y) => Px[y * W + x];

        public void Set(int x, int y, Color32 c)
        {
            if (x >= 0 && x < W && y >= 0 && y < H) Px[y * W + x] = c;
        }

        public void Rect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Math.Max(0, y0); y <= Math.Min(H - 1, y1); y++)
                for (int x = Math.Max(0, x0); x <= Math.Min(W - 1, x1); x++)
                    Px[y * W + x] = c;
        }

        public Img Copy()
        {
            var o = new Img(W, H);
            Array.Copy(Px, o.Px, Px.Length);
            return o;
        }
    }

    public static bool SpritesExist()
    {
        return File.Exists("Assets/Sprites/Player/player_idle_0.png");
    }

    public static void GenerateAll()
    {
        rng = new System.Random(3);
        foreach (var d in new[] { "Player", "Enemies", "Tiles", "Items", "FX", "UI", "Background" })
            Directory.CreateDirectory("Assets/Sprites/" + d);

        var runLegs = new[] { (new[] { 2, -2 }, new[] { 0, 1 }), (new[] { 0, 0 }, new[] { 0, 0 }), (new[] { -2, 2 }, new[] { 1, 0 }), (new[] { 0, 0 }, new[] { 0, 0 }) };

        Save(Human(Player, gunLength: 10), "Player/player_idle_0");
        Save(Human(Player, gunLength: 10, bob: 1), "Player/player_idle_1");
        for (int i = 0; i < runLegs.Length; i++)
            Save(Human(Player, runLegs[i].Item1, runLegs[i].Item2, i % 2, gunLength: 10), $"Player/player_run_{i}");
        Save(Human(Player, new[] { 1, -1 }, new[] { 3, 3 }, gunLength: 10), "Player/player_jump_0");
        Save(Human(Player, new[] { 2, -2 }, new[] { 0, 1 }, gunLength: 10), "Player/player_fall_0");
        Save(Human(Player, gunLength: 8, flash: true), "Player/player_shoot_0");
        Save(Human(Player, gunLength: 10, lean: -1), "Player/player_shoot_1");
        Save(Tint(Human(Player, gunLength: 10, lean: -2), C(255, 255, 255), 0.5f), "Player/player_hurt_0");
        Save(Lying(Human(Player)), "Player/player_dead_0");

        var zombieLegs = new[] { (new[] { 1, -1 }, new[] { 0, 0 }), (new[] { 0, 0 }, new[] { 1, 0 }), (new[] { -1, 1 }, new[] { 0, 0 }), (new[] { 0, 0 }, new[] { 0, 1 }) };
        for (int i = 0; i < zombieLegs.Length; i++)
            Save(Human(Zombie, zombieLegs[i].Item1, zombieLegs[i].Item2, i % 2, arms: "forward", helmet: false, lean: 1), $"Enemies/zombie_walk_{i}");
        Save(Human(Zombie, arms: "up", helmet: false, lean: -1), "Enemies/zombie_attack_0");
        Save(Human(Zombie, arms: "slash", helmet: false, lean: 2), "Enemies/zombie_attack_1");
        Save(Lying(Human(Zombie, arms: "forward", helmet: false)), "Enemies/zombie_dead_0");

        Save(Human(Soldier, gunLength: 10), "Enemies/soldier_idle_0");
        Save(Human(Soldier, gunLength: 10, bob: 1), "Enemies/soldier_idle_1");
        Save(Human(Soldier, gunLength: 8, flash: true), "Enemies/soldier_shoot_0");
        Save(Human(Soldier, gunLength: 10, lean: -1), "Enemies/soldier_shoot_1");
        Save(Lying(Human(Soldier)), "Enemies/soldier_dead_0");

        Save(DirtTile(true), "Tiles/tile_grass");
        Save(DirtTile(false), "Tiles/tile_dirt");
        Save(PlatformTile(), "Tiles/tile_platform");
        Save(BarrierTile(), "Tiles/tile_barrier");
        Save(SpikesTile(), "Tiles/tile_spikes");

        Save(Flag(C(200, 50, 50)), "Items/exit_locked");
        Save(Flag(C(60, 190, 80)), "Items/exit_open");
        Save(BulletPlayer(), "Items/bullet_player");
        Save(BulletHmg(), "Items/bullet_hmg");
        Save(Orb(6, 3f, 1.5f, C(255, 90, 90), C(255, 220, 220)), "Items/bullet_enemy");
        Save(HealthBox(), "Items/pickup_health");
        Save(AmmoBox(), "Items/pickup_ammo");
        Save(Orb(10, 4.6f, 2.5f, C(250, 200, 40), C(255, 235, 120)), "Items/pickup_coin");

        var white = new Img(4, 4);
        white.Rect(0, 0, 3, 3, C(255, 255, 255));
        Save(white, "FX/fx_spark");
        Save(white, "UI/ui_white");

        Save(Background(), "Background/bg_hills");
        AssetDatabase.Refresh();
    }

    private static void Save(Img img, string rel)
    {
        var tex = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false);
        var flipped = new Color32[img.Px.Length];
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
                flipped[(img.H - 1 - y) * img.W + x] = img.Get(x, y);
        tex.SetPixels32(flipped);
        tex.Apply();
        File.WriteAllBytes("Assets/Sprites/" + rel + ".png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static Img Outline(Img img)
    {
        var src = img.Copy();
        for (int y = 0; y < img.H; y++)
        {
            for (int x = 0; x < img.W; x++)
            {
                if (src.Get(x, y).a != 0) continue;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= img.W || ny >= img.H) continue;
                    var n = src.Get(nx, ny);
                    if (n.a > 0 && !(n.r == OutlineColor.r && n.g == OutlineColor.g && n.b == OutlineColor.b))
                    {
                        img.Set(x, y, OutlineColor);
                        break;
                    }
                }
            }
        }
        return img;
    }

    private static Img Tint(Img img, Color32 c, float amount)
    {
        var o = img.Copy();
        for (int i = 0; i < o.Px.Length; i++)
        {
            var p = o.Px[i];
            if (p.a == 0) continue;
            o.Px[i] = new Color32((byte)(p.r + (c.r - p.r) * amount), (byte)(p.g + (c.g - p.g) * amount), (byte)(p.b + (c.b - p.b) * amount), p.a);
        }
        return o;
    }

    private static Img Lying(Img img)
    {
        int w = img.W, h = img.H;
        var rot = new Img(h, w);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                rot.Set(y, w - 1 - x, img.Get(x, y));

        int minX = rot.W, minY = rot.H, maxX = -1, maxY = -1;
        for (int y = 0; y < rot.H; y++)
            for (int x = 0; x < rot.W; x++)
                if (rot.Get(x, y).a > 0)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        float scale = cw > w ? (float)w / cw : 1f;
        int ow = Math.Max(1, (int)(cw * scale)), oh = Math.Max(1, (int)(ch * scale));

        var o = new Img(w, h);
        int offX = (w - ow) / 2, offY = h - oh;
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                var p = rot.Get(minX + (int)(x / scale), minY + (int)(y / scale));
                if (p.a > 0) o.Set(offX + x, offY + y, p);
            }
        return o;
    }

    private static Img Human(Pal pal, int[] legs = null, int[] lift = null, int bob = 0, int gunLength = 0, bool flash = false,
                             string arms = null, int lean = 0, bool helmet = true)
    {
        legs = legs ?? new[] { 0, 0 };
        lift = lift ?? new[] { 0, 0 };
        var img = new Img(24, 32);
        int top = 4 + bob;
        int ox = 8 + lean;

        for (int i = 0; i < 2; i++)
        {
            int bx = ox + 1 + i * 4 + legs[i];
            img.Rect(bx, top + 18, bx + 2, top + 24 - lift[i], pal.Pants);
            img.Rect(bx, top + 25 - lift[i], bx + 3, top + 27 - lift[i], pal.Boots);
        }

        img.Rect(ox, top + 10, ox + 7, top + 17, pal.Torso);
        img.Rect(ox, top + 17, ox + 7, top + 17, pal.Belt);
        img.Rect(ox + 1, top + 5, ox + 6, top + 9, pal.Skin);
        img.Set(ox + 5, top + 7, C(30, 30, 30));

        if (helmet)
        {
            img.Rect(ox, top + 2, ox + 7, top + 5, pal.Head);
            img.Rect(ox + 1, top + 1, ox + 6, top + 1, pal.Head);
            img.Rect(ox + 6, top + 5, ox + 8, top + 5, pal.Head);
        }
        else
        {
            img.Rect(ox + 1, top + 3, ox + 6, top + 4, pal.Head);
            img.Set(ox + 2, top + 2, pal.Head);
            img.Set(ox + 5, top + 2, pal.Head);
        }

        switch (arms)
        {
            case "forward":
                img.Rect(ox + 5, top + 11, ox + 13, top + 12, pal.Skin);
                img.Rect(ox + 5, top + 14, ox + 12, top + 15, pal.Skin);
                break;
            case "up":
                img.Rect(ox + 5, top + 2, ox + 7, top + 11, pal.Skin);
                img.Rect(ox + 1, top + 1, ox + 3, top + 10, pal.Skin);
                break;
            case "slash":
                img.Rect(ox + 5, top + 12, ox + 15, top + 13, pal.Skin);
                img.Rect(ox + 13, top + 14, ox + 15, top + 16, pal.Claw);
                break;
            default:
                img.Rect(ox + 4, top + 12, ox + 6, top + 14, pal.Skin);
                break;
        }

        if (gunLength > 0)
        {
            int gy = top + 12;
            img.Rect(ox + 4, gy, ox + 4 + gunLength, gy + 1, C(85, 88, 100));
            img.Rect(ox + 5, gy + 2, ox + 6, gy + 3, C(70, 70, 80));
            if (flash)
            {
                int fx = ox + 5 + gunLength;
                img.Rect(fx, gy - 1, fx + 1, gy + 2, C(255, 230, 90));
                img.Set(fx + 2, gy, C(255, 250, 200));
                img.Set(fx + 2, gy + 1, C(255, 170, 40));
                img.Set(fx, gy - 2, C(255, 170, 40));
                img.Set(fx, gy + 3, C(255, 170, 40));
            }
        }
        return Outline(img);
    }

    private static Img DirtTile(bool grass)
    {
        var img = new Img(16, 16);
        img.Rect(0, 0, 15, 15, C(120, 82, 50));
        var specks = new[] { C(100, 66, 40), C(140, 98, 62), C(92, 60, 36) };
        for (int i = 0; i < 14; i++) img.Set(rng.Next(16), rng.Next(16), specks[rng.Next(3)]);
        if (grass)
        {
            img.Rect(0, 0, 15, 3, C(88, 170, 70));
            img.Rect(0, 0, 15, 0, C(130, 210, 100));
            for (int x = 0; x < 16; x++)
                if (rng.NextDouble() < 0.5) img.Set(x, 4, C(70, 140, 55));
        }
        return img;
    }

    private static Img PlatformTile()
    {
        var img = new Img(16, 8);
        img.Rect(0, 0, 15, 7, C(150, 105, 60));
        img.Rect(0, 0, 15, 1, C(190, 140, 85));
        img.Rect(0, 6, 15, 7, C(100, 68, 38));
        img.Set(2, 3, C(90, 60, 35));
        img.Set(13, 3, C(90, 60, 35));
        img.Rect(7, 2, 7, 5, C(110, 75, 45));
        return img;
    }

    private static Img BarrierTile()
    {
        var img = new Img(16, 16);
        img.Rect(0, 0, 15, 15, C(120, 125, 135));
        img.Rect(0, 0, 15, 0, C(170, 175, 185));
        img.Rect(0, 15, 15, 15, C(80, 82, 90));
        for (int y = 4; y < 12; y++)
            for (int x = 0; x < 16; x++)
                img.Set(x, y, ((x + y) / 3) % 2 == 0 ? C(230, 190, 40) : C(40, 40, 40));
        foreach (var (x, y) in new[] { (2, 2), (13, 2), (2, 13), (13, 13) }) img.Set(x, y, C(70, 70, 80));
        return img;
    }

    private static Img SpikesTile()
    {
        var img = new Img(16, 16);
        for (int s = 0; s < 4; s++)
        {
            int x0 = s * 4;
            for (int row = 0; row < 10; row++)
            {
                int y = 6 + row;
                if (row < 4)
                {
                    img.Set(x0 + 1, y, C(235, 238, 245));
                    img.Set(x0 + 2, y, C(160, 165, 180));
                }
                else
                {
                    img.Set(x0, y, C(235, 238, 245));
                    img.Set(x0 + 1, y, C(210, 214, 225));
                    img.Set(x0 + 2, y, C(160, 165, 180));
                    img.Set(x0 + 3, y, C(120, 124, 138));
                }
            }
        }
        img.Rect(0, 15, 15, 15, C(90, 90, 100));
        return img;
    }

    private static Img Flag(Color32 color)
    {
        var img = new Img(16, 32);
        img.Rect(2, 2, 3, 31, C(200, 200, 210));
        img.Rect(1, 1, 4, 2, C(240, 200, 60));
        img.Rect(4, 3, 14, 11, color);
        img.Rect(4, 3, 14, 3, C(Math.Min(255, color.r + 50), Math.Min(255, color.g + 50), Math.Min(255, color.b + 50)));
        return Outline(img);
    }

    private static Img BulletPlayer()
    {
        var img = new Img(8, 4);
        img.Rect(0, 1, 7, 2, C(255, 210, 60));
        img.Rect(5, 0, 7, 3, C(255, 245, 180));
        img.Rect(0, 1, 1, 2, C(240, 140, 40));
        return img;
    }

    private static Img BulletHmg()
    {
        var img = new Img(10, 4);
        img.Rect(0, 0, 9, 3, C(255, 120, 40));
        img.Rect(5, 1, 9, 2, C(255, 240, 170));
        return img;
    }

    private static Img Orb(int size, float radius, float inner, Color32 outer, Color32 core)
    {
        var img = new Img(size, size);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (d < radius) img.Set(x, y, d > inner ? outer : core);
            }
        return img;
    }

    private static Img HealthBox()
    {
        var img = new Img(12, 12);
        img.Rect(0, 0, 11, 11, C(240, 240, 240));
        img.Rect(4, 2, 7, 9, C(220, 40, 40));
        img.Rect(2, 4, 9, 7, C(220, 40, 40));
        return Outline(img);
    }

    private static Img AmmoBox()
    {
        var img = new Img(12, 12);
        img.Rect(0, 1, 11, 11, C(110, 120, 60));
        img.Rect(0, 1, 11, 2, C(150, 160, 90));
        for (int y = 4; y < 10; y++)
        {
            img.Set(3, y, C(250, 220, 60));
            img.Set(8, y, C(250, 220, 60));
        }
        img.Rect(3, 6, 8, 7, C(250, 220, 60));
        return Outline(img);
    }

    private static Img Background()
    {
        var img = new Img(64, 128);
        for (int y = 0; y < 128; y++)
        {
            float t = y / 127f;
            img.Rect(0, y, 63, y, C((int)(120 + 60 * t), (int)(180 + 40 * t), (int)(235 + 10 * t)));
        }
        for (int x = 0; x < 64; x++)
        {
            int hy = (int)(92 + 8 * Mathf.Sin(x / 64f * 2f * Mathf.PI) + 4 * Mathf.Sin(x / 64f * 6f * Mathf.PI));
            img.Rect(x, hy, x, 127, C(110, 160, 120));
            int hy2 = (int)(104 + 5 * Mathf.Sin(x / 64f * 4f * Mathf.PI + 1f));
            img.Rect(x, hy2, x, 127, C(80, 130, 90));
        }
        foreach (var (cx, cy) in new[] { (12, 30), (44, 50) })
            for (int dx = -7; dx <= 7; dx++)
                for (int dy = -3; dy <= 3; dy++)
                    if ((dx / 7f) * (dx / 7f) + (dy / 3f) * (dy / 3f) < 1f)
                        img.Set(((cx + dx) % 64 + 64) % 64, cy + dy, C(250, 250, 255));
        return img;
    }
}
