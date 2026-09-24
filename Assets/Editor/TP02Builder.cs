using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[InitializeOnLoad]
public static class TP02Builder
{
    private const int LDefault = 0;
    private const int LGround = 6;
    private const int LPlayer = 7;
    private const int LEnemy = 8;
    private const int LPlayerProj = 9;
    private const int LEnemyProj = 10;
    private const int LDetection = 11;
    private const int LOneWay = 12;
    private const float PPU = 16f;

    private const string SceneMenu = "Assets/Scenes/MainMenu.unity";
    private const string Scene1 = "Assets/Scenes/Level01.unity";
    private const string Scene2 = "Assets/Scenes/Level02.unity";

    private static readonly Color Sky = new Color(0.47f, 0.71f, 0.92f);

    private static Font uiFont;
    private static Sprite uiSprite;
    private static PhysicsMaterial2D noFriction;

    private static GameObject hitFxPrefab;
    private static Projectile bulletPlayer, bulletHmg, bulletEnemy;
    private static GameObject pickupHealth, pickupAmmo, pickupCoin;
    private static GameObject barrierPrefab, spikesPrefab, exitPrefab;
    private static GameObject playerPrefab, zombiePrefab, soldierPrefab, elitePrefab;
    private static GameObject gameSystemsPrefab;

    static TP02Builder()
    {
        EditorApplication.delayCall += TryAutoBuild;
    }

    private static void TryAutoBuild()
    {
        if (File.Exists(Scene1) || SessionState.GetBool("TP02_AutoBuildDone", false)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryAutoBuild;
            return;
        }
        SessionState.SetBool("TP02_AutoBuildDone", true);
        BuildAll();
    }

    [MenuItem("TP02/Build everything")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        bool restartNeeded = false;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Progress("Project settings", 0.05f);
            restartNeeded = SetupProjectSettings();

            Progress("Cleaning", 0.1f);
            foreach (var f in new[] { "Assets/Animations", "Assets/Prefabs", "Assets/Scenes", "Assets/Physics" })
                AssetDatabase.DeleteAsset(f);
            foreach (var f in new[] { "Assets/Animations/Player", "Assets/Animations/Enemies", "Assets/Prefabs/Player", "Assets/Prefabs/Enemies",
                                      "Assets/Prefabs/Projectiles", "Assets/Prefabs/Pickups", "Assets/Prefabs/Level", "Assets/Prefabs/UI",
                                      "Assets/Prefabs/FX", "Assets/Scenes", "Assets/Physics" })
                EnsureFolder(f);

            Progress("Sprites", 0.15f);
            if (!SpriteGenerator.SpritesExist()) SpriteGenerator.GenerateAll();
            ImportSprites();

            Progress("Sounds", 0.2f);
            GenerateSounds();

            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            uiSprite = Spr("UI/ui_white");

            noFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(noFriction, "Assets/Physics/NoFriction.physicsMaterial2D");

            Progress("Prefabs: FX, projectiles, pickups", 0.3f);
            hitFxPrefab = MakeHitFx();
            bulletPlayer = MakeBullet("Bullet_Player", "Items/bullet_player", LPlayerProj, 18f, 1, 0.15f);
            bulletHmg = MakeBullet("Bullet_HMG", "Items/bullet_hmg", LPlayerProj, 22f, 1, 0.15f);
            bulletEnemy = MakeBullet("Bullet_Enemy", "Items/bullet_enemy", LEnemyProj, 8f, 1, 0.18f);
            pickupHealth = MakePickup("Pickup_Health", "Items/pickup_health", PickupType.Health, 2, 0);
            pickupAmmo = MakePickup("Pickup_Ammo", "Items/pickup_ammo", PickupType.Ammo, 120, 1);
            pickupCoin = MakePickup("Pickup_Coin", "Items/pickup_coin", PickupType.Score, 250, 0);

            Progress("Prefabs: level pieces", 0.4f);
            barrierPrefab = MakeBarrier();
            spikesPrefab = MakeSpikes();
            exitPrefab = MakeExit();

            Progress("Player", 0.5f);
            playerPrefab = MakePlayer();

            Progress("Enemies", 0.6f);
            zombiePrefab = MakeZombie();
            soldierPrefab = MakeSoldier();
            elitePrefab = MakeEliteSoldier();

            Progress("UI", 0.7f);
            gameSystemsPrefab = MakeGameSystems();

            Progress("Scenes", 0.8f);
            BuildMainMenu();
            BuildLevel01();
            BuildLevel02();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneMenu, true),
                new EditorBuildSettingsScene(Scene1, true),
                new EditorBuildSettingsScene(Scene2, true),
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(SceneMenu);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("TP02", "Build failed:\n" + e.Message + "\n\nSee the Console, then run TP02 > Build everything again.", "OK");
            return;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (restartNeeded)
        {
            if (EditorUtility.DisplayDialog("TP02", "The game is built.\n\nUnity must restart once to enable the new Input System.", "Restart now", "Later"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }
        else
        {
            EditorUtility.DisplayDialog("TP02", "The game is built.\n\nMainMenu is open: press Play.", "OK");
        }
    }

    private static void Progress(string info, float p)
    {
        EditorUtility.DisplayProgressBar("TP02 - building the game", info, p);
    }

    private static bool SetupProjectSettings()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        string[] names = { "Ground", "Player", "Enemy", "PlayerProjectile", "EnemyProjectile", "Detection", "OneWay" };
        for (int i = 0; i < names.Length; i++) layers.GetArrayElementAtIndex(6 + i).stringValue = names[i];
        tagManager.ApplyModifiedPropertiesWithoutUndo();

        int[] ours = { LDefault, LGround, LPlayer, LEnemy, LPlayerProj, LEnemyProj, LDetection, LOneWay };
        foreach (int a in ours)
            foreach (int b in ours)
                Physics2D.IgnoreLayerCollision(a, b, true);

        void Allow(int a, int b) => Physics2D.IgnoreLayerCollision(a, b, false);
        Allow(LDefault, LDefault);
        Allow(LDefault, LGround);
        Allow(LDefault, LPlayer);
        Allow(LDefault, LEnemy);
        Allow(LGround, LPlayer);
        Allow(LGround, LEnemy);
        Allow(LGround, LPlayerProj);
        Allow(LGround, LEnemyProj);
        Allow(LOneWay, LPlayer);
        Allow(LOneWay, LEnemy);
        Allow(LPlayer, LEnemyProj);
        Allow(LPlayer, LDetection);
        Allow(LEnemy, LPlayerProj);

        var p2d = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset");
        if (p2d.Length > 0)
        {
            var so = new SerializedObject(p2d[0]);
            var matrix = so.FindProperty("m_LayerCollisionMatrix");
            if (matrix != null && matrix.isArray)
            {
                for (int i = 0; i < 32 && i < matrix.arraySize; i++)
                {
                    long bits = 0;
                    for (int j = 0; j < 32; j++)
                        if (!Physics2D.GetIgnoreLayerCollision(i, j)) bits |= 1L << j;
                    matrix.GetArrayElementAtIndex(i).longValue = bits;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        bool restart = false;
        var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (ps.Length > 0)
        {
            var so = new SerializedObject(ps[0]);
            var handler = so.FindProperty("activeInputHandler");
            if (handler != null && handler.intValue == 0)
            {
                handler.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                restart = true;
            }
        }

        AssetDatabase.SaveAssets();
        return restart;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void ImportSprites()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PPU;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;

            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.wrapMode = path.Contains("/Tiles/") || path.Contains("/Background/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }
    }

    private enum Wave { Square, Triangle, Sine }

    private const int SampleRate = 22050;
    private static readonly System.Random Rng = new System.Random(3);

    private static void GenerateSounds()
    {
        EnsureFolder("Assets/Audio");
        WriteWav("shoot", Tone(900, 300, 0.08f, Wave.Square, 0.3f, 0.2f));
        WriteWav("shoot_hmg", Tone(700, 250, 0.06f, Wave.Square, 0.25f, 0.4f));
        WriteWav("enemy_shoot", Tone(500, 200, 0.12f, Wave.Square, 0.25f, 0.3f));
        WriteWav("jump", Tone(300, 700, 0.12f, Wave.Triangle, 0.4f));
        WriteWav("hurt", Tone(400, 120, 0.2f, Wave.Square, 0.35f, 0.5f));
        WriteWav("enemy_death", Tone(300, 40, 0.35f, Wave.Square, 0.35f, 0.7f));
        WriteWav("player_death", Tone(500, 60, 0.8f, Wave.Triangle, 0.45f, 0.3f));
        WriteWav("pickup", Concat(Tone(600, 600, 0.06f, Wave.Sine, 0.4f), Tone(900, 900, 0.06f, Wave.Sine, 0.4f), Tone(1200, 1200, 0.1f, Wave.Sine, 0.4f)));
        WriteWav("level_complete", Concat(Tone(523, 523, 0.12f, Wave.Triangle, 0.4f), Tone(659, 659, 0.12f, Wave.Triangle, 0.4f), Tone(784, 784, 0.12f, Wave.Triangle, 0.4f), Tone(1046, 1046, 0.2f, Wave.Triangle, 0.4f)));
        WriteWav("game_over", Concat(Tone(392, 392, 0.2f, Wave.Triangle, 0.4f), Tone(330, 330, 0.2f, Wave.Triangle, 0.4f), Tone(262, 262, 0.2f, Wave.Triangle, 0.4f), Tone(196, 196, 0.4f, Wave.Triangle, 0.4f)));
        WriteWav("alarm", Concat(Tone(880, 660, 0.18f, Wave.Square, 0.25f), Tone(880, 660, 0.18f, Wave.Square, 0.25f), Tone(880, 660, 0.18f, Wave.Square, 0.25f)));
        AssetDatabase.Refresh();
    }

    private static float[] Tone(float f0, float f1, float duration, Wave wave, float volume, float noise = 0f)
    {
        int n = (int)(SampleRate * duration);
        var samples = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            phase += (f0 + (f1 - f0) * t) / SampleRate;
            float p = (float)(phase % 1.0);
            float s = wave == Wave.Square ? (p < 0.5f ? 1f : -1f)
                    : wave == Wave.Triangle ? 4f * Mathf.Abs(p - 0.5f) - 1f
                    : Mathf.Sin(2f * Mathf.PI * p);
            s = s * (1f - noise) + ((float)Rng.NextDouble() * 2f - 1f) * noise;
            float envelope = Mathf.Min(1f, i / (SampleRate * 0.005f)) * Mathf.Pow(1f - t, 1.5f);
            samples[i] = s * envelope * volume;
        }
        return samples;
    }

    private static float[] Concat(params float[][] parts)
    {
        int total = 0;
        foreach (var p in parts) total += p.Length;
        var result = new float[total];
        int offset = 0;
        foreach (var p in parts)
        {
            Array.Copy(p, 0, result, offset, p.Length);
            offset += p.Length;
        }
        return result;
    }

    private static void WriteWav(string name, float[] samples)
    {
        string path = "Assets/Audio/" + name + ".wav";
        if (File.Exists(path)) return;

        int dataLength = samples.Length * 2;
        using (var stream = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(stream))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataLength);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(SampleRate);
            w.Write(SampleRate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(dataLength);
            foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 30000f));
        }
    }

    private static Sprite Spr(string rel)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/" + rel + ".png");
        if (s == null) throw new Exception("Missing sprite: Assets/Sprites/" + rel + ".png");
        return s;
    }

    private static AudioClip Sfx(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/" + name + ".wav");
    }

    private static void Set(UnityEngine.Object target, Action<SerializedObject> edit)
    {
        var so = new SerializedObject(target);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty P(this SerializedObject so, string name)
    {
        var p = so.FindProperty(name);
        if (p == null) throw new Exception($"Field '{name}' not found on {so.targetObject.GetType().Name}");
        return p;
    }

    private static void SetArray(SerializedProperty array, params UnityEngine.Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject Child(GameObject parent, string name, Vector2 localPos, int layer = -1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.layer = layer >= 0 ? layer : parent.layer;
        return go;
    }

    private static SpriteRenderer AddSprite(GameObject go, string sprite, int order)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Spr(sprite);
        sr.sortingOrder = order;
        return sr;
    }

    private static SpriteRenderer AddTiled(GameObject go, string sprite, Vector2 size, int order)
    {
        var sr = AddSprite(go, sprite, order);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        return sr;
    }

    private static AnimationClip Clip(string folder, string name, float fps, bool loop, params string[] frames)
    {
        var clip = new AnimationClip { frameRate = fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = Spr(frames[i]) };
        keys[frames.Length] = new ObjectReferenceKeyframe
        {
            time = frames.Length / fps,
            value = Spr(loop ? frames[0] : frames[frames.Length - 1])
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, $"Assets/Animations/{folder}/{name}.anim");
        return clip;
    }

    private struct Cond
    {
        public string Param;
        public AnimatorConditionMode Mode;
        public float Value;
        public Cond(string p, AnimatorConditionMode m, float v = 0f) { Param = p; Mode = m; Value = v; }
    }

    private static Cond Is(string p) => new Cond(p, AnimatorConditionMode.If);
    private static Cond Not(string p) => new Cond(p, AnimatorConditionMode.IfNot);
    private static Cond Gt(string p, float v) => new Cond(p, AnimatorConditionMode.Greater, v);
    private static Cond Lt(string p, float v) => new Cond(p, AnimatorConditionMode.Less, v);

    private static void Tr(AnimatorState from, AnimatorState to, params Cond[] conds)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        foreach (var c in conds) t.AddCondition(c.Mode, c.Value, c.Param);
    }

    private static void AnyTr(AnimatorStateMachine sm, AnimatorState to, bool toSelf, params Cond[] conds)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.canTransitionToSelf = toSelf;
        foreach (var c in conds) t.AddCondition(c.Mode, c.Value, c.Param);
    }

    private static void ExitTr(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.duration = 0f;
    }

    private static AnimatorState State(AnimatorStateMachine sm, string name, Motion clip, Vector2 pos)
    {
        var s = sm.AddState(name, pos);
        s.motion = clip;
        return s;
    }

    private static AnimatorController PlayerAnimator()
    {
        const string f = "Player";
        var idle = Clip(f, "Player_Idle", 2, true, "Player/player_idle_0", "Player/player_idle_1");
        var run = Clip(f, "Player_Run", 10, true, "Player/player_run_0", "Player/player_run_1", "Player/player_run_2", "Player/player_run_3");
        var jump = Clip(f, "Player_Jump", 1, false, "Player/player_jump_0");
        var fall = Clip(f, "Player_Fall", 1, false, "Player/player_fall_0");
        var shoot = Clip(f, "Player_Shoot", 16, false, "Player/player_shoot_0", "Player/player_shoot_1");
        var hurt = Clip(f, "Player_Hurt", 8, false, "Player/player_hurt_0", "Player/player_hurt_0");
        var dead = Clip(f, "Player_Dead", 1, false, "Player/player_dead_0");

        var c = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animations/Player/Player.controller");
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        c.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        c.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        c.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        c.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        var sm = c.layers[0].stateMachine;
        var sIdle = State(sm, "Idle", idle, new Vector2(300, 0));
        var sRun = State(sm, "Run", run, new Vector2(550, 0));
        var sJump = State(sm, "Jump", jump, new Vector2(300, 150));
        var sFall = State(sm, "Fall", fall, new Vector2(550, 150));
        var sShoot = State(sm, "Shoot", shoot, new Vector2(300, -150));
        var sHurt = State(sm, "Hurt", hurt, new Vector2(550, -150));
        var sDead = State(sm, "Dead", dead, new Vector2(0, 250));
        sm.defaultState = sIdle;

        Tr(sIdle, sRun, Gt("Speed", 0.1f));
        Tr(sRun, sIdle, Lt("Speed", 0.1f));
        Tr(sJump, sIdle, Is("Grounded"));
        Tr(sFall, sIdle, Is("Grounded"));
        ExitTr(sShoot, sIdle);
        ExitTr(sHurt, sIdle);

        AnyTr(sm, sDead, false, Is("IsDead"));
        AnyTr(sm, sHurt, false, Is("Hurt"), Not("IsDead"));
        AnyTr(sm, sShoot, true, Is("Shoot"), Not("IsDead"));
        AnyTr(sm, sJump, false, Not("Grounded"), Gt("VelocityY", 0.1f), Not("IsDead"));
        AnyTr(sm, sFall, false, Not("Grounded"), Lt("VelocityY", -0.1f), Not("IsDead"));
        return c;
    }

    private static AnimatorController ZombieAnimator()
    {
        const string f = "Enemies";
        var idle = Clip(f, "Zombie_Idle", 1, true, "Enemies/zombie_walk_0");
        var walk = Clip(f, "Zombie_Walk", 6, true, "Enemies/zombie_walk_0", "Enemies/zombie_walk_1", "Enemies/zombie_walk_2", "Enemies/zombie_walk_3");
        var attack = Clip(f, "Zombie_Attack", 6, false, "Enemies/zombie_attack_0", "Enemies/zombie_attack_0", "Enemies/zombie_attack_1", "Enemies/zombie_attack_1");
        var dead = Clip(f, "Zombie_Dead", 1, false, "Enemies/zombie_dead_0");

        var c = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animations/Enemies/Zombie.controller");
        c.AddParameter("Speed", AnimatorControllerParameterType.Float);
        c.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        c.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        var sm = c.layers[0].stateMachine;
        var sIdle = State(sm, "Idle", idle, new Vector2(300, 0));
        var sWalk = State(sm, "Walk", walk, new Vector2(550, 0));
        var sAttack = State(sm, "Attack", attack, new Vector2(300, -150));
        var sDead = State(sm, "Dead", dead, new Vector2(0, 250));
        sm.defaultState = sIdle;

        Tr(sIdle, sWalk, Gt("Speed", 0.1f));
        Tr(sWalk, sIdle, Lt("Speed", 0.1f));
        ExitTr(sAttack, sIdle);
        AnyTr(sm, sDead, false, Is("IsDead"));
        AnyTr(sm, sAttack, false, Is("Attack"), Not("IsDead"));
        return c;
    }

    private static AnimatorController SoldierAnimator()
    {
        const string f = "Enemies";
        var idle = Clip(f, "Soldier_Idle", 2, true, "Enemies/soldier_idle_0", "Enemies/soldier_idle_1");
        var shoot = Clip(f, "Soldier_Shoot", 8, false, "Enemies/soldier_shoot_1", "Enemies/soldier_shoot_1", "Enemies/soldier_shoot_0", "Enemies/soldier_shoot_1");
        var dead = Clip(f, "Soldier_Dead", 1, false, "Enemies/soldier_dead_0");

        var c = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animations/Enemies/Soldier.controller");
        c.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        c.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

        var sm = c.layers[0].stateMachine;
        var sIdle = State(sm, "Idle", idle, new Vector2(300, 0));
        var sShoot = State(sm, "Shoot", shoot, new Vector2(550, 0));
        var sDead = State(sm, "Dead", dead, new Vector2(0, 250));
        sm.defaultState = sIdle;

        ExitTr(sShoot, sIdle);
        AnyTr(sm, sDead, false, Is("IsDead"));
        AnyTr(sm, sShoot, false, Is("Shoot"), Not("IsDead"));
        return c;
    }

    private static GameObject MakeHitFx()
    {
        var go = new GameObject("HitEffect");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.3f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = 0.25f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.5f, 0.1f));
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        var mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        if (mat == null) mat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        r.sharedMaterial = mat;
        r.sortingOrder = 20;

        return SavePrefab(go, "Assets/Prefabs/FX/HitEffect.prefab");
    }

    private static Projectile MakeBullet(string name, string sprite, int layer, float speed, int damage, float radius)
    {
        var go = new GameObject(name) { layer = layer };
        AddSprite(go, sprite, 11);
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = radius;
        col.isTrigger = true;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var p = go.AddComponent<Projectile>();
        Set(p, so =>
        {
            so.P("speed").floatValue = speed;
            so.P("damage").intValue = damage;
            so.P("lifetime").floatValue = 2.5f;
            so.P("destroyOnLayers").intValue = 1 << LGround;
            so.P("hitEffectPrefab").objectReferenceValue = hitFxPrefab;
        });
        return SavePrefab(go, $"Assets/Prefabs/Projectiles/{name}.prefab").GetComponent<Projectile>();
    }

    private static GameObject MakePickup(string name, string sprite, PickupType type, int amount, int weaponIndex)
    {
        var go = new GameObject(name) { layer = LDetection };
        AddSprite(go, sprite, 5);
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;
        col.isTrigger = true;
        var pk = go.AddComponent<Pickup>();
        Set(pk, so =>
        {
            so.P("type").enumValueIndex = (int)type;
            so.P("amount").intValue = amount;
            so.P("weaponIndex").intValue = weaponIndex;
            so.P("pickupClip").objectReferenceValue = Sfx("pickup");
        });
        return SavePrefab(go, $"Assets/Prefabs/Pickups/{name}.prefab");
    }

    private static GameObject MakeBarrier()
    {
        var go = new GameObject("Barrier") { layer = LGround };
        AddTiled(go, "Tiles/tile_barrier", new Vector2(1f, 6f), 3);
        go.AddComponent<BoxCollider2D>().size = new Vector2(1f, 6f);
        return SavePrefab(go, "Assets/Prefabs/Level/Barrier.prefab");
    }

    private static GameObject MakeSpikes()
    {
        var go = new GameObject("Spikes") { layer = LDefault };
        AddTiled(go, "Tiles/tile_spikes", new Vector2(3f, 1f), 2);
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2.8f, 0.5f);
        col.offset = new Vector2(0f, -0.2f);
        var d = go.AddComponent<DamageOnTouch>();
        Set(d, so =>
        {
            so.P("damage").intValue = 1;
            so.P("hurtsEveryone").boolValue = true;
            so.P("repeatDelay").floatValue = 1f;
        });
        return SavePrefab(go, "Assets/Prefabs/Level/Spikes.prefab");
    }

    private static GameObject MakeExit()
    {
        var go = new GameObject("LevelExit") { layer = LDetection };
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.5f, 3f);
        col.offset = new Vector2(0f, 1.5f);
        var locked = Child(go, "Locked", new Vector2(0f, 1f));
        AddSprite(locked, "Items/exit_locked", 4);
        var open = Child(go, "Open", new Vector2(0f, 1f));
        AddSprite(open, "Items/exit_open", 4);
        open.SetActive(false);
        var exit = go.AddComponent<LevelExit>();
        Set(exit, so =>
        {
            so.P("lockedVisual").objectReferenceValue = locked;
            so.P("openVisual").objectReferenceValue = open;
        });
        return SavePrefab(go, "Assets/Prefabs/Level/LevelExit.prefab");
    }

    private static GameObject Body(string name, int layer, string sprite, int order, RuntimeAnimatorController controller, out Animator animator)
    {
        var go = new GameObject(name) { layer = layer };
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var cap = go.AddComponent<CapsuleCollider2D>();
        cap.direction = CapsuleDirection2D.Vertical;
        cap.size = new Vector2(0.8f, 1.9f);
        cap.offset = new Vector2(0f, -0.05f);
        cap.sharedMaterial = noFriction;

        var visual = Child(go, "Visual", Vector2.zero);
        AddSprite(visual, sprite, order);
        animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        return go;
    }

    private static GameObject MakePlayer()
    {
        var go = Body("Player", LPlayer, "Player/player_idle_0", 10, PlayerAnimator(), out var animator);
        go.tag = "Player";
        var groundCheck = Child(go, "GroundCheck", new Vector2(0f, -1.0f));
        var aimOrigin = Child(go, "AimOrigin", new Vector2(0.1f, 0f));

        var health = go.AddComponent<Health>();
        go.AddComponent<PlayerInputReader>();
        var controller = go.AddComponent<PlayerController>();
        var shooter = go.AddComponent<PlayerShooter>();

        Set(health, so =>
        {
            so.P("team").enumValueIndex = (int)Team.Player;
            so.P("maxHealth").intValue = 6;
            so.P("invincibilityDuration").floatValue = 1.2f;
            so.P("hurtClip").objectReferenceValue = Sfx("hurt");
            so.P("deathClip").objectReferenceValue = Sfx("player_death");
        });

        Set(controller, so =>
        {
            so.P("jumpVelocity").floatValue = 15f;
            so.P("groundCheck").objectReferenceValue = groundCheck.transform;
            so.P("groundCheckSize").vector2Value = new Vector2(0.7f, 0.12f);
            so.P("groundLayer").intValue = (1 << LGround) | (1 << LOneWay);
            so.P("animator").objectReferenceValue = animator;
            so.P("jumpClip").objectReferenceValue = Sfx("jump");
        });

        Set(shooter, so =>
        {
            so.P("aimOrigin").objectReferenceValue = aimOrigin.transform;
            so.P("muzzleDistance").floatValue = 0.8f;
            so.P("animator").objectReferenceValue = animator;

            var weapons = so.P("weapons");
            weapons.arraySize = 2;

            var pistol = weapons.GetArrayElementAtIndex(0);
            pistol.FindPropertyRelative("name").stringValue = "PISTOLET";
            pistol.FindPropertyRelative("projectilePrefab").objectReferenceValue = bulletPlayer;
            pistol.FindPropertyRelative("fireRate").floatValue = 5f;
            pistol.FindPropertyRelative("automatic").boolValue = false;
            pistol.FindPropertyRelative("spreadAngle").floatValue = 0f;
            pistol.FindPropertyRelative("maxAmmo").intValue = -1;
            pistol.FindPropertyRelative("startAmmo").intValue = 0;
            pistol.FindPropertyRelative("shotClip").objectReferenceValue = Sfx("shoot");

            var hmg = weapons.GetArrayElementAtIndex(1);
            hmg.FindPropertyRelative("name").stringValue = "MITRAILLEUSE";
            hmg.FindPropertyRelative("projectilePrefab").objectReferenceValue = bulletHmg;
            hmg.FindPropertyRelative("fireRate").floatValue = 12f;
            hmg.FindPropertyRelative("automatic").boolValue = true;
            hmg.FindPropertyRelative("spreadAngle").floatValue = 4f;
            hmg.FindPropertyRelative("maxAmmo").intValue = 200;
            hmg.FindPropertyRelative("startAmmo").intValue = 0;
            hmg.FindPropertyRelative("shotClip").objectReferenceValue = Sfx("shoot_hmg");
        });

        return SavePrefab(go, "Assets/Prefabs/Player/Player.prefab");
    }

    private static DetectionZone Zone(GameObject parent, string name, Vector2 localPos, Vector2 size)
    {
        var go = Child(parent, name, localPos, LDetection);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        return go.AddComponent<DetectionZone>();
    }

    private static void EnemyHealth(Health h, int hp)
    {
        Set(h, so =>
        {
            so.P("team").enumValueIndex = (int)Team.Enemy;
            so.P("maxHealth").intValue = hp;
            so.P("invincibilityDuration").floatValue = 0f;
            so.P("deathClip").objectReferenceValue = Sfx("enemy_death");
        });
    }

    private static void EnemyBase(SerializedObject so, DetectionZone detection, Animator animator, int score)
    {
        so.P("detectionZone").objectReferenceValue = detection;
        so.P("animator").objectReferenceValue = animator;
        so.P("scoreValue").intValue = score;
        SetArray(so.P("dropPrefabs"), pickupHealth, pickupAmmo);
        so.P("dropChance").floatValue = 0.3f;
    }

    private static GameObject MakeZombie()
    {
        var go = Body("Zombie", LEnemy, "Enemies/zombie_walk_0", 8, ZombieAnimator(), out var animator);
        var detection = Zone(go, "DetectionZone", new Vector2(0f, 0.5f), new Vector2(12f, 4f));
        var attackZone = Zone(go, "AttackZone", new Vector2(0.9f, 0f), new Vector2(1.4f, 1.8f));

        var hitbox = Child(go, "AttackHitbox", new Vector2(1.0f, 0f), LEnemyProj);
        var hb = hitbox.AddComponent<BoxCollider2D>();
        hb.isTrigger = true;
        hb.size = new Vector2(1.2f, 1.4f);
        var dmg = hitbox.AddComponent<DamageOnTouch>();
        Set(dmg, so =>
        {
            so.P("damage").intValue = 1;
            so.P("ownerTeam").enumValueIndex = (int)Team.Enemy;
            so.P("repeatDelay").floatValue = 0.3f;
        });
        hitbox.SetActive(false);

        var groundAhead = Child(go, "GroundAheadCheck", new Vector2(0.7f, -0.8f));

        EnemyHealth(go.AddComponent<Health>(), 3);
        var melee = go.AddComponent<MeleeEnemy>();
        Set(melee, so =>
        {
            EnemyBase(so, detection, animator, 100);
            so.P("attackZone").objectReferenceValue = attackZone;
            so.P("attackHitbox").objectReferenceValue = hitbox;
            so.P("chaseSpeed").floatValue = 2.8f;
            so.P("groundAheadCheck").objectReferenceValue = groundAhead.transform;
            so.P("groundLayer").intValue = (1 << LGround) | (1 << LOneWay);
        });
        return SavePrefab(go, "Assets/Prefabs/Enemies/Zombie.prefab");
    }

    private static GameObject MakeSoldier()
    {
        var go = Body("Soldier", LEnemy, "Enemies/soldier_idle_0", 8, SoldierAnimator(), out var animator);
        var detection = Zone(go, "DetectionZone", new Vector2(0f, 1f), new Vector2(18f, 9f));
        var muzzle = Child(go, "Muzzle", new Vector2(0.8f, 0f));

        EnemyHealth(go.AddComponent<Health>(), 2);
        var ranged = go.AddComponent<RangedEnemy>();
        Set(ranged, so =>
        {
            EnemyBase(so, detection, animator, 150);
            so.P("projectilePrefab").objectReferenceValue = bulletEnemy;
            so.P("muzzle").objectReferenceValue = muzzle.transform;
            so.P("aimAtTarget").boolValue = true;
            so.P("targetHeightOffset").floatValue = 0.2f;
            so.P("fireCooldown").floatValue = 1.8f;
            so.P("shootWindup").floatValue = 0.25f;
            so.P("burstCount").intValue = 1;
            so.P("shotClip").objectReferenceValue = Sfx("enemy_shoot");
        });
        return SavePrefab(go, "Assets/Prefabs/Enemies/Soldier.prefab");
    }

    private static GameObject MakeEliteSoldier()
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(soldierPrefab);
        go.name = "SoldierElite";
        go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        go.GetComponentInChildren<SpriteRenderer>().color = new Color(1f, 0.75f, 0.45f);
        EnemyHealth(go.GetComponent<Health>(), 5);
        Set(go.GetComponent<RangedEnemy>(), so =>
        {
            so.P("scoreValue").intValue = 400;
            so.P("burstCount").intValue = 3;
            so.P("burstInterval").floatValue = 0.15f;
            so.P("fireCooldown").floatValue = 2f;
        });
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/Enemies/SoldierElite.prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    private static RectTransform UI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private static Text UIText(Transform parent, string name, string content, int size, TextAnchor align, Color color)
    {
        var rt = UI(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = uiFont;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.85f);
        o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private static Image UIImage(Transform parent, string name, Color color)
    {
        var rt = UI(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = uiSprite;
        img.color = color;
        return img;
    }

    private static Button UIButton(Transform parent, string label, Vector2 pos, UnityAction onClick)
    {
        var img = UIImage(parent, "Button_" + label, new Color(0.95f, 0.75f, 0.2f));
        Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(380f, 84f));
        var b = img.gameObject.AddComponent<Button>();
        var colors = b.colors;
        colors.highlightedColor = new Color(1f, 0.9f, 0.5f);
        colors.pressedColor = new Color(0.8f, 0.6f, 0.1f);
        colors.selectedColor = new Color(1f, 0.9f, 0.5f);
        b.colors = colors;
        var t = UIText(img.transform, "Label", label, 40, TextAnchor.MiddleCenter, new Color(0.15f, 0.1f, 0.05f));
        UnityEngine.Object.DestroyImmediate(t.GetComponent<Outline>());
        Stretch(t.rectTransform);
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        return b;
    }

    private static Canvas MakeCanvas(Transform parent, string name)
    {
        var rt = UI(name, parent);
        var canvas = rt.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = rt.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        rt.gameObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void MakeEventSystem(Transform parent)
    {
        var go = new GameObject("EventSystem");
        if (parent) go.transform.SetParent(parent, false);
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private static GameObject Panel(Transform canvas, string name, string title, Color titleColor)
    {
        var bg = UIImage(canvas, name, new Color(0f, 0f, 0f, 0.72f));
        Stretch(bg.rectTransform);
        var t = UIText(bg.transform, "Title", title, 110, TextAnchor.MiddleCenter, titleColor);
        Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 160f));
        bg.gameObject.SetActive(false);
        return bg.gameObject;
    }

    private static GameObject MakeGameSystems()
    {
        var root = new GameObject("GameSystems");
        var gm = root.AddComponent<GameManager>();
        MakeEventSystem(root.transform);

        var canvas = MakeCanvas(root.transform, "Canvas");
        var hud = canvas.gameObject.AddComponent<HUD>();

        var hpBg = UIImage(canvas.transform, "HealthBar", new Color(0.1f, 0.1f, 0.12f, 0.85f));
        Place(hpBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(440f, 40f));
        var hpFill = UIImage(hpBg.transform, "Fill", new Color(0.3f, 0.85f, 0.3f));
        Stretch(hpFill.rectTransform, 5f);
        hpFill.type = Image.Type.Filled;
        hpFill.fillMethod = Image.FillMethod.Horizontal;
        hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        var hpText = UIText(hpBg.transform, "Text", "6 / 6", 24, TextAnchor.MiddleCenter, Color.white);
        Stretch(hpText.rectTransform);
        var hpLabel = UIText(canvas.transform, "HealthLabel", "VIE", 26, TextAnchor.UpperLeft, Color.white);
        Place(hpLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -86f), new Vector2(200f, 40f));

        var score = UIText(canvas.transform, "ScoreText", "SCORE 000000", 40, TextAnchor.UpperRight, new Color(1f, 0.9f, 0.4f));
        Place(score.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(600f, 60f));

        var weapon = UIText(canvas.transform, "WeaponText", "PISTOLET  --", 34, TextAnchor.LowerLeft, Color.white);
        Place(weapon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 36f), new Vector2(700f, 50f));

        var hint = UIText(canvas.transform, "Controls", "WASD/ZQSD bouger/viser   ESPACE sauter   J tirer   E changer d'arme", 22, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.8f));
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(1100f, 40f));

        var gameOver = Panel(canvas.transform, "GameOverPanel", "GAME OVER", new Color(1f, 0.3f, 0.25f));
        UIButton(gameOver.transform, "REJOUER", new Vector2(0f, -20f), gm.Retry);
        UIButton(gameOver.transform, "MENU", new Vector2(0f, -130f), gm.LoadMainMenu);

        var levelComplete = Panel(canvas.transform, "LevelCompletePanel", "MISSION TERMINEE", new Color(0.5f, 1f, 0.5f));

        var victory = Panel(canvas.transform, "VictoryPanel", "VICTOIRE !", new Color(1f, 0.85f, 0.3f));
        var vtxt = UIText(victory.transform, "Sub", "Tous les niveaux sont termines", 40, TextAnchor.MiddleCenter, Color.white);
        Place(vtxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 60f));
        UIButton(victory.transform, "MENU", new Vector2(0f, -80f), gm.LoadMainMenu);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f), new GradientColorKey(new Color(0.95f, 0.8f, 0.2f), 0.5f), new GradientColorKey(new Color(0.3f, 0.85f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        Set(hud, so =>
        {
            so.P("healthFill").objectReferenceValue = hpFill;
            so.P("healthText").objectReferenceValue = hpText;
            so.P("useColorGradient").boolValue = true;
            so.P("healthColor").gradientValue = gradient;
            so.P("scoreText").objectReferenceValue = score;
            so.P("weaponText").objectReferenceValue = weapon;
        });

        Set(gm, so =>
        {
            so.P("gameOverPanel").objectReferenceValue = gameOver;
            so.P("levelCompletePanel").objectReferenceValue = levelComplete;
            so.P("victoryPanel").objectReferenceValue = victory;
            so.P("mainMenuScene").stringValue = "MainMenu";
            so.P("levelCompleteClip").objectReferenceValue = Sfx("level_complete");
            so.P("gameOverClip").objectReferenceValue = Sfx("game_over");
        });

        return SavePrefab(root, "Assets/Prefabs/UI/GameSystems.prefab");
    }

    private static void BuildMainMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        var menuGo = new GameObject("MainMenu");
        var menu = menuGo.AddComponent<MainMenu>();
        Set(menu, so => so.P("firstLevelScene").stringValue = "Level01");

        MakeEventSystem(null);
        var canvas = MakeCanvas(null, "Canvas");

        var bg = UIImage(canvas.transform, "Background", Color.white);
        bg.sprite = Spr("Background/bg_hills");
        Stretch(bg.rectTransform);

        var title = UIText(canvas.transform, "Title", "RUN & GUN", 150, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.25f));
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1400f, 200f));
        var sub = UIText(canvas.transform, "Subtitle", "TP02  -  ST2OOS  -  Unity 2D", 40, TextAnchor.MiddleCenter, Color.white);
        Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1200f, 60f));

        UIButton(canvas.transform, "JOUER", new Vector2(0f, 20f), menu.Play);
        UIButton(canvas.transform, "QUITTER", new Vector2(0f, -90f), menu.Quit);

        var controls = UIText(canvas.transform, "Controls",
            "A/D (Q/D) : bouger     W/S (Z/S) : viser haut/bas     ESPACE : sauter     J / clic : tirer     E : changer d'arme",
            28, TextAnchor.MiddleCenter, Color.white);
        Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1800f, 50f));

        EditorSceneManager.SaveScene(scene, SceneMenu);
    }

    private class Level
    {
        public Transform Geometry, Enemies, Items, Zones;
    }

    private static Level NewLevel(float minX, float maxX, float maxY, Vector2 spawn)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lvl = new Level
        {
            Geometry = new GameObject("Level").transform,
            Enemies = new GameObject("Enemies").transform,
            Items = new GameObject("Pickups").transform,
            Zones = new GameObject("Triggers").transform,
        };

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        camGo.transform.position = new Vector3(spawn.x, spawn.y + 2f, -10f);
        camGo.AddComponent<AudioListener>();
        var follow = camGo.AddComponent<CameraFollow2D>();
        Set(follow, so =>
        {
            so.P("offset").vector2Value = new Vector2(0f, 1.5f);
            so.P("lookAhead").floatValue = 2.5f;
            so.P("smoothTime").floatValue = 0.18f;
            so.P("useBounds").boolValue = true;
            so.P("minBounds").vector2Value = new Vector2(minX, -4f);
            so.P("maxBounds").vector2Value = new Vector2(maxX, maxY);
        });

        var bg = new GameObject("Background");
        bg.transform.position = new Vector3((minX + maxX) * 0.5f, 2f, 0f);
        AddTiled(bg, "Background/bg_hills", new Vector2(maxX - minX + 40f, 8f), -20);

        var kill = new GameObject("KillZone") { layer = LDefault };
        kill.transform.SetParent(lvl.Zones, false);
        kill.transform.position = new Vector3((minX + maxX) * 0.5f, -12f, 0f);
        var kb = kill.AddComponent<BoxCollider2D>();
        kb.isTrigger = true;
        kb.size = new Vector2(maxX - minX + 60f, 6f);
        var kd = kill.AddComponent<DamageOnTouch>();
        Set(kd, so =>
        {
            so.P("instantKill").boolValue = true;
            so.P("hurtsEveryone").boolValue = true;
        });

        Ground(lvl, minX - 2f, maxY, 2f, maxY + 8f);
        Ground(lvl, maxX, maxY, 2f, maxY + 8f);

        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = spawn;
        PrefabUtility.InstantiatePrefab(gameSystemsPrefab);
        return lvl;
    }

    private static void Ground(Level lvl, float x, float top, float w, float depth = 8f)
    {
        var go = new GameObject("Ground") { layer = LGround };
        go.transform.SetParent(lvl.Geometry, false);
        go.transform.position = new Vector3(x + w * 0.5f, top - depth * 0.5f, 0f);
        go.AddComponent<BoxCollider2D>().size = new Vector2(w, depth);

        var dirt = Child(go, "Dirt", new Vector2(0f, -0.5f));
        AddTiled(dirt, "Tiles/tile_dirt", new Vector2(w, depth - 1f), 0);
        var grass = Child(go, "Grass", new Vector2(0f, depth * 0.5f - 0.5f));
        AddTiled(grass, "Tiles/tile_grass", new Vector2(w, 1f), 1);
    }

    private static void Platform(Level lvl, float x, float top, float w)
    {
        var go = new GameObject("OneWayPlatform") { layer = LOneWay };
        go.transform.SetParent(lvl.Geometry, false);
        go.transform.position = new Vector3(x + w * 0.5f, top - 0.25f, 0f);
        AddTiled(go, "Tiles/tile_platform", new Vector2(w, 0.5f), 1);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(w, 0.5f);
        box.usedByEffector = true;
        var eff = go.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true;
        eff.surfaceArc = 160f;
    }

    private static GameObject Place(GameObject prefab, Transform parent, float x, float y)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, y, 0f);
        return go;
    }

    private static GameObject SpawnEnemy(Level lvl, GameObject prefab, float x, float groundTop, bool faceLeft = true)
    {
        float h = prefab == elitePrefab ? 1.2f : 1.0f;
        var go = Place(prefab, lvl.Enemies, x, groundTop + 1.02f * h);
        if (faceLeft)
        {
            var s = go.transform.localScale;
            s.x = -Mathf.Abs(s.x);
            go.transform.localScale = s;
        }
        return go;
    }

    private static void PlaceSpikes(Level lvl, float x, float groundTop)
    {
        Place(spikesPrefab, lvl.Geometry, x + 1.5f, groundTop + 0.5f);
    }

    private static void Item(Level lvl, GameObject prefab, float x, float y)
    {
        Place(prefab, lvl.Items, x, y);
    }

    private static CombatZone Arena(Level lvl, string name, float x0, float x1, float groundTop, params GameObject[] enemies)
    {
        var go = new GameObject(name) { layer = LDetection };
        go.transform.SetParent(lvl.Zones, false);
        go.transform.position = new Vector3((x0 + x1) * 0.5f + 0.5f, groundTop + 6f, 0f);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(x1 - x0 - 1f, 12f);

        var left = Place(barrierPrefab, lvl.Geometry, x0 - 0.5f, groundTop + 3f);
        var right = Place(barrierPrefab, lvl.Geometry, x1 + 0.5f, groundTop + 3f);
        left.name = name + "_BarrierLeft";
        right.name = name + "_BarrierRight";

        var zone = go.AddComponent<CombatZone>();
        Set(zone, so =>
        {
            SetArray(so.P("enemies"), enemies);
            SetArray(so.P("barriers"), left, right);
            so.P("lockCamera").boolValue = true;
            so.P("alarmClip").objectReferenceValue = Sfx("alarm");
        });
        return zone;
    }

    private static void PlaceExit(Level lvl, float x, float groundTop, CombatZone required)
    {
        var go = Place(exitPrefab, lvl.Zones, x, groundTop);
        Set(go.GetComponent<LevelExit>(), so => so.P("requiredZone").objectReferenceValue = required);
    }

    private static void BuildLevel01()
    {
        var lvl = NewLevel(-10f, 133f, 20f, new Vector2(0f, 1.1f));

        Ground(lvl, -10f, 0f, 35f);
        Ground(lvl, 29f, 0f, 24f);
        Ground(lvl, 53f, 1f, 20f);
        Ground(lvl, 73f, 0f, 60f);

        Platform(lvl, 8f, 3f, 4f);
        Platform(lvl, 14f, 5.5f, 4f);
        Platform(lvl, 34f, 3f, 5f);
        Platform(lvl, 58f, 4f, 4f);
        Platform(lvl, 64f, 6.5f, 4f);
        Platform(lvl, 99f, 3f, 4f);

        PlaceSpikes(lvl, 44f, 0f);

        SpawnEnemy(lvl, zombiePrefab, 12f, 0f);
        SpawnEnemy(lvl, soldierPrefab, 16f, 5.5f);
        SpawnEnemy(lvl, zombiePrefab, 40f, 0f);
        SpawnEnemy(lvl, soldierPrefab, 60f, 4f);
        SpawnEnemy(lvl, zombiePrefab, 80f, 0f);

        Item(lvl, pickupAmmo, 16f, 7.2f);
        Item(lvl, pickupCoin, 35.5f, 4.2f);
        Item(lvl, pickupCoin, 37f, 4.2f);
        Item(lvl, pickupCoin, 38.5f, 4.2f);
        Item(lvl, pickupHealth, 66f, 7.7f);

        var arena = Arena(lvl, "CombatZone", 90f, 116f, 0f,
            SpawnEnemy(lvl, zombiePrefab, 104f, 0f),
            SpawnEnemy(lvl, zombiePrefab, 110f, 0f),
            SpawnEnemy(lvl, soldierPrefab, 101f, 3f),
            SpawnEnemy(lvl, soldierPrefab, 114f, 0f));

        Item(lvl, pickupHealth, 120f, 0.8f);
        PlaceExit(lvl, 127f, 0f, arena);

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene1);
    }

    private static void BuildLevel02()
    {
        var lvl = NewLevel(-10f, 152f, 22f, new Vector2(0f, 1.1f));

        Ground(lvl, -10f, 0f, 25f);
        Ground(lvl, 19f, 0f, 26f);
        Ground(lvl, 45f, 2.5f, 8f);
        Ground(lvl, 53f, 0f, 8f);
        Ground(lvl, 61f, 0f, 40f);
        Ground(lvl, 105f, 0f, 47f);

        Platform(lvl, 5f, 3f, 4f);
        Platform(lvl, 27f, 3f, 4f);
        Platform(lvl, 66f, 3f, 4f);
        Platform(lvl, 72f, 6f, 4f);
        Platform(lvl, 78f, 9f, 5f);
        Platform(lvl, 121f, 3f, 4f);

        PlaceSpikes(lvl, 54f, 0f);
        PlaceSpikes(lvl, 57f, 0f);
        PlaceSpikes(lvl, 90f, 0f);

        SpawnEnemy(lvl, zombiePrefab, 10f, 0f);
        SpawnEnemy(lvl, soldierPrefab, 7f, 3f);

        var arena1 = Arena(lvl, "CombatZone_1", 20f, 44f, 0f,
            SpawnEnemy(lvl, zombiePrefab, 32f, 0f),
            SpawnEnemy(lvl, zombiePrefab, 40f, 0f),
            SpawnEnemy(lvl, soldierPrefab, 29f, 3f),
            SpawnEnemy(lvl, soldierPrefab, 42f, 0f));

        SpawnEnemy(lvl, soldierPrefab, 49f, 2.5f);
        SpawnEnemy(lvl, zombiePrefab, 70f, 0f);
        SpawnEnemy(lvl, soldierPrefab, 74f, 6f);
        SpawnEnemy(lvl, elitePrefab, 80.5f, 9f);
        SpawnEnemy(lvl, zombiePrefab, 95f, 0f);

        Item(lvl, pickupAmmo, 79.5f, 10.2f);
        Item(lvl, pickupHealth, 97f, 0.8f);
        Item(lvl, pickupCoin, 67f, 4.2f);
        Item(lvl, pickupCoin, 68.5f, 4.2f);
        Item(lvl, pickupCoin, 73f, 7.2f);
        Item(lvl, pickupCoin, 74.5f, 7.2f);

        var arena2 = Arena(lvl, "CombatZone_2", 110f, 140f, 0f,
            SpawnEnemy(lvl, zombiePrefab, 124f, 0f),
            SpawnEnemy(lvl, zombiePrefab, 130f, 0f),
            SpawnEnemy(lvl, zombiePrefab, 136f, 0f),
            SpawnEnemy(lvl, soldierPrefab, 123f, 3f),
            SpawnEnemy(lvl, elitePrefab, 138f, 0f));

        PlaceExit(lvl, 146f, 0f, arena2);

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene2);
    }
}
