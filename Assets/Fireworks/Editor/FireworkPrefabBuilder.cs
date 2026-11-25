using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fireworks.EditorTools
{
    // 글로우 텍스처, 파티클 머티리얼, 불꽃 프리팹 8종을 만든다.
    // 다시 실행하면 같은 경로의 자산을 덮어쓴다(GUID 유지).
    public static class FireworkPrefabBuilder
    {
        public const string Root = "Assets/Fireworks";
        public const string PrefabFolder = Root + "/Prefabs";
        private const string GlowTexturePath = Root + "/Textures/Glow.png";
        private const string StreakTexturePath = Root + "/Textures/Streak.png";
        public const string GlowMaterialPath = Root + "/Materials/Glow.mat";
        public const string StreakMaterialPath = Root + "/Materials/Streak.mat";
        private const string ParticleShaderPath = Root + "/Shaders/ParticleAdditive.shader";

        private static Material glow;
        private static Material streak;

        public static string PrefabPath(ShellType type) => $"{PrefabFolder}/{type}.prefab";

        [MenuItem("Fireworks/2 Build Prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and finish compilation first.");

            Directory.CreateDirectory(Root + "/Textures");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(PrefabFolder);

            var glowTexture = WriteTexture(GlowTexturePath, 128, 128, (u, v) =>
            {
                float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float halo = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) * 0.4f;
                float core = Mathf.Exp(-r * r * 45f) * 0.6f;
                return Mathf.Clamp01(halo + core);
            });
            var streakTexture = WriteTexture(StreakTexturePath, 16, 32, (u, v) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v * 2f - 1f)), 1.6f));

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ParticleShaderPath);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + ParticleShaderPath);
            glow = GetOrCreateMaterial(GlowMaterialPath, shader);
            glow.SetTexture("_MainTex", glowTexture);
            glow.SetFloat("_Intensity", 2.6f);
            streak = GetOrCreateMaterial(StreakMaterialPath, shader);
            streak.SetTexture("_MainTex", streakTexture);
            streak.SetFloat("_Intensity", 1.5f);
            EditorUtility.SetDirty(glow);
            EditorUtility.SetDirty(streak);

            var scene = EditorSceneManager.NewPreviewScene();
            int built = 0;
            try
            {
                foreach (ShellType type in Enum.GetValues(typeof(ShellType)))
                {
                    var go = BuildShell(type);
                    SceneManager.MoveGameObjectToScene(go, scene);
                    PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(type));
                    built++;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Firework prefabs built: {built} in {PrefabFolder}.");
        }

        // ---------- 종류별 구성 ----------

        private static GameObject BuildShell(ShellType type)
        {
            var root = new GameObject(type.ToString());
            var shell = root.AddComponent<FireworkShell>();
            var parts = new List<BurstPart>();

            if (type == ShellType.Fountain)
            {
                var fountain = CreateSystem("Fountain", root.transform, 900);
                fountain.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var main = fountain.main;
                main.playOnAwake = false;
                main.duration = 2.4f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2f);
                main.gravityModifier = 2.2f;
                var emission = fountain.emission;
                emission.rateOverTime = 170f;
                var shape = fountain.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 9f;
                shape.radius = 0.3f;
                ApplyFade(fountain, 0.5f, 0.3f);
                AddTrails(fountain, 0.2f, 0.6f);
                parts.Add(Part(fountain, 24f, 34f, 0.35f, 0.6f));
                shell.groundEffect = fountain;
                shell.rise = null;
                shell.riseTime = 0f;
                shell.busySeconds = 5f;
                shell.parts = parts.ToArray();
                return root;
            }

            float riseTime = type == ShellType.Comet ? 1.4f : 2f;
            var rise = CreateSystem("Rise", root.transform, 4);
            var riseMain = rise.main;
            riseMain.loop = true;          // Emit으로만 쏘지만 시스템은 계속 돌아야 한다
            riseMain.duration = 5f;
            AddTrails(rise, type == ShellType.Comet ? 0.45f : 0.25f, type == ShellType.Comet ? 1f : 0.7f);
            shell.rise = rise;
            shell.riseTime = riseTime;
            shell.riseSize = type == ShellType.Comet ? 0.9f : 0.6f;

            float longest = 2.5f;
            switch (type)
            {
                case ShellType.Peony:
                {
                    var stars = CreateBurst(rise, "Stars", 190, 1.8f, 2.4f, 0.3f, 0.1f);
                    ApplyFade(stars, 0.55f, 0.35f);
                    AddTrails(stars, 0.22f, 0.7f);
                    parts.Add(Part(stars, 27f, 31f, 0.9f, 1.3f));
                    break;
                }
                case ShellType.Willow:
                {
                    var stars = CreateBurst(rise, "Stars", 110, 3.4f, 4.4f, 0.4f, 0.1f);
                    ApplyFade(stars, 0.35f, 0.5f);
                    AddTrails(stars, 0.3f, 0.8f);
                    parts.Add(Part(stars, 15f, 20f, 0.7f, 1f));
                    longest = 4.4f;
                    break;
                }
                case ShellType.Ring:
                {
                    var ring = CreateBurst(rise, "Ring", 70, 1.6f, 2f, 0.2f, 0.1f);
                    var shape = ring.shape;
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.1f;
                    shape.radiusThickness = 0f;
                    shape.arc = 360f;
                    ApplyFade(ring, 0.6f, 0.4f);
                    AddTrails(ring, 0.18f, 0.7f);
                    var part = Part(ring, 25f, 26f, 1f, 1.3f);
                    part.randomTilt = true;
                    parts.Add(part);
                    var heart = CreateBurst(rise, "Heart", 40, 1f, 1.5f, 0.2f, 0.15f);
                    ApplyFade(heart, 0.5f, 0.3f);
                    var heartPart = Part(heart, 3f, 9f, 0.6f, 0.9f);
                    heartPart.useSecondColor = true;
                    parts.Add(heartPart);
                    break;
                }
                case ShellType.Palm:
                {
                    var fronds = CreateBurst(rise, "Fronds", 16, 2.4f, 3f, 0.75f, 0.07f);
                    ApplyFade(fronds, 0.5f, 0.6f);
                    AddTrails(fronds, 0.45f, 1.1f);
                    parts.Add(Part(fronds, 24f, 30f, 1.4f, 1.9f));
                    longest = 3f;
                    break;
                }
                case ShellType.Crackle:
                {
                    var embers = CreateBurst(rise, "Embers", 140, 0.6f, 2.3f, 0.5f, 0.1f);
                    ApplyFade(embers, 0.7f, 0.6f);
                    parts.Add(Part(embers, 8f, 30f, 0.45f, 0.7f));
                    var sparkle = CreateBurst(embers, "Sparkle", 1, 0.08f, 0.22f, 0f, 0f);
                    var sparkleShape = sparkle.shape;
                    sparkleShape.enabled = false;
                    ApplyFade(sparkle, 0.2f, 0.6f);
                    var sparklePart = Part(sparkle, 0f, 0f, 1.5f, 2.6f);
                    sparklePart.tint = false;
                    parts.Add(sparklePart);
                    break;
                }
                case ShellType.Double:
                {
                    var outer = CreateBurst(rise, "Outer", 160, 1.9f, 2.4f, 0.3f, 0.1f);
                    ApplyFade(outer, 0.55f, 0.35f);
                    AddTrails(outer, 0.22f, 0.7f);
                    parts.Add(Part(outer, 30f, 33f, 0.9f, 1.3f));
                    var inner = CreateBurst(rise, "Inner", 90, 1.5f, 2f, 0.25f, 0.2f);
                    ApplyFade(inner, 0.55f, 0.35f);
                    var innerPart = Part(inner, 14f, 16f, 0.8f, 1.1f);
                    innerPart.useSecondColor = true;
                    parts.Add(innerPart);
                    break;
                }
                case ShellType.Comet:
                {
                    var pop = CreateBurst(rise, "Pop", 36, 0.8f, 1.3f, 0.4f, 0.15f);
                    ApplyFade(pop, 0.4f, 0.3f);
                    parts.Add(Part(pop, 9f, 14f, 0.6f, 0.9f));
                    longest = 1.3f;
                    break;
                }
            }

            var flash = CreateBurst(rise, "Flash", 1, 0.12f, 0.2f, 0f, 0f);
            var flashShape = flash.shape;
            flashShape.enabled = false;
            ApplyFade(flash, 0.05f, 1f);
            float flashSize = type == ShellType.Comet ? 8f : 20f;
            var flashPart = Part(flash, 0f, 0f, flashSize, flashSize);
            flashPart.tint = false;
            parts.Add(flashPart);

            shell.busySeconds = riseTime + longest + 0.6f;
            shell.parts = parts.ToArray();
            return root;
        }

        // ---------- 파티클 시스템 도우미 ----------

        private static ParticleSystem CreateSystem(string name, Transform parent, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 1f;
            main.startColor = Color.white;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 3f;
            return ps;
        }

        // parent 파티클이 죽을 때 한 번 터지는 하위 시스템(Sub Emitter)을 만든다.
        // drag는 속도 제곱에 비례하는 공기 저항 계수다: 터진 직후 빠르게 퍼지고 곧 느려져 반경이 대략 ln(1 + drag·속도·시간) / drag에서 멈춘다.
        private static ParticleSystem CreateBurst(ParticleSystem parent, string name, int count, float lifeMin, float lifeMax, float gravity, float drag)
        {
            var ps = CreateSystem(name, parent.transform, Mathf.Max(64, count * 12));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            shape.radiusThickness = 1f;
            if (drag > 0f)
            {
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 1000f;
                limit.dampen = 0f;
                limit.drag = drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;   // 끄면 저항이 거의 걸리지 않아 파티클이 카메라 쪽으로 수십 유닛을 날아온다
            }
            var sub = parent.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(ps, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
            return ps;
        }

        // 수명 후반에 사라지고 점점 작아지게 한다.
        private static void ApplyFade(ParticleSystem ps, float fadeStart, float endSize)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
        }

        private static void AddTrails(ParticleSystem ps, float lifetimeRatio, float width)
        {
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = lifetimeRatio;
            trails.minVertexDistance = 0.4f;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.sizeAffectsWidth = true;
            trails.sizeAffectsLifetime = false;
            trails.inheritParticleColor = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(width, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = new ParticleSystem.MinMaxGradient(gradient);
            ps.GetComponent<ParticleSystemRenderer>().trailMaterial = streak;
        }

        private static BurstPart Part(ParticleSystem ps, float speedMin, float speedMax, float sizeMin, float sizeMax)
        {
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            var limit = ps.limitVelocityOverLifetime;
            return new BurstPart { system = ps, speed = new Vector2(speedMin, speedMax), size = new Vector2(sizeMin, sizeMax), drag = limit.enabled ? limit.drag.constant : 0f };
        }

        // ---------- 자산 도우미 ----------

        public static Material GetOrCreateMaterial(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            return material;
        }

        private static Texture2D WriteTexture(string path, int width, int height, Func<float, float, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha((x + 0.5f) / width, (y + 0.5f) / height));
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
