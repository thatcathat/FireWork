using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Fireworks.EditorTools
{
    // FireworkShow 씬을 처음부터 다시 만든다(밤하늘, 수면 반사, 도시 실루엣, 카메라, 후처리, 쇼 진행자).
    // 씬 전체가 생성물이므로 씬에서 직접 고친 내용은 다시 실행하면 사라진다. 값은 이 파일이나 ShowData에서 고친다.
    public static class FireworkSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/FireworkShow.unity";
        private const string SkyMaterialPath = FireworkPrefabBuilder.Root + "/Materials/NightSky.mat";
        private const string WaterMaterialPath = FireworkPrefabBuilder.Root + "/Materials/Water.mat";
        private const string SilhouetteMaterialPath = FireworkPrefabBuilder.Root + "/Materials/Silhouette.mat";
        private const string VolumeProfilePath = FireworkPrefabBuilder.Root + "/Data/ShowVolume.asset";
        private const int WaterLayer = 4;

        [MenuItem("Fireworks/3 Build Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and finish compilation first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");

            var data = ShowComposer.GetOrCreateData();
            var prefabs = ((ShellType[])Enum.GetValues(typeof(ShellType)))
                .Select(t => AssetDatabase.LoadAssetAtPath<FireworkShell>(FireworkPrefabBuilder.PrefabPath(t))).ToArray();
            if (prefabs.Any(p => p == null)) throw new InvalidOperationException("Run 'Fireworks/2 Build Prefabs' first.");

            var sky = FireworkPrefabBuilder.GetOrCreateMaterial(SkyMaterialPath, LoadShader("NightSky"));
            var water = FireworkPrefabBuilder.GetOrCreateMaterial(WaterMaterialPath, LoadShader("Water"));
            var silhouette = FireworkPrefabBuilder.GetOrCreateMaterial(SilhouetteMaterialPath, Shader.Find("Universal Render Pipeline/Unlit"));
            silhouette.SetColor("_BaseColor", new Color(0.006f, 0.008f, 0.014f, 1f));
            EditorUtility.SetDirty(silhouette);
            var profile = BuildVolumeProfile();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.04f);
            RenderSettings.fog = false;

            // 카메라
            var first = data.cameraKeys.Count > 0 ? data.cameraKeys[0] : new CameraKey { position = new Vector3(0f, 6f, -110f), lookAt = new Vector3(0f, 35f, 0f) };
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 3000f;
            cam.fieldOfView = first.fov;
            cam.allowHDR = true;
            cameraGo.transform.SetPositionAndRotation(first.position, Quaternion.LookRotation(first.lookAt - first.position, Vector3.up));
            cameraGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var director = cameraGo.AddComponent<CameraDirector>();
            director.cam = cam;

            var reflectionGo = new GameObject("Reflection Camera");
            var reflectionCam = reflectionGo.AddComponent<Camera>();
            reflectionCam.clearFlags = CameraClearFlags.Skybox;
            reflectionCam.cullingMask = ~(1 << WaterLayer);
            reflectionCam.depth = -10f;
            reflectionCam.allowHDR = true;
            var reflectionData = reflectionCam.GetUniversalAdditionalCameraData();
            reflectionData.renderPostProcessing = false;
            reflectionData.renderShadows = false;
            var reflection = reflectionGo.AddComponent<PlanarReflection>();
            reflection.source = cam;
            reflection.waterHeight = 0f;

            // 후처리
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // 배경
            var environment = new GameObject("Environment");
            var waterGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterGo.name = "Water";
            waterGo.layer = WaterLayer;
            waterGo.transform.SetParent(environment.transform, false);
            waterGo.transform.localScale = new Vector3(400f, 1f, 400f);
            UnityEngine.Object.DestroyImmediate(waterGo.GetComponent<Collider>());
            var waterRenderer = waterGo.GetComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = water;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;

            int buildings = BuildSkyline(environment.transform, silhouette);
            BuildCityLights(environment.transform);

            // 쇼
            var show = new GameObject("Show");
            var audioSource = show.AddComponent<AudioSource>();
            audioSource.clip = data.clip;
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            var launcherGo = new GameObject("Launcher");
            launcherGo.transform.SetParent(show.transform, false);
            var launcher = launcherGo.AddComponent<FireworkLauncher>();
            launcher.prefabs = prefabs;

            var canvasGo = new GameObject("Credit Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var textGo = new GameObject("Credit");
            textGo.transform.SetParent(canvasGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.lineSpacing = 1.2f;
            text.alignment = TextAnchor.LowerLeft;
            text.color = new Color(1f, 1f, 1f, 0.85f);
            text.text = data.credit;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(56f, 48f);
            rect.sizeDelta = new Vector2(900f, 120f);

            var conductor = show.AddComponent<ShowConductor>();
            conductor.data = data;
            conductor.audioSource = audioSource;
            conductor.launcher = launcher;
            conductor.cameraDirector = director;
            conductor.creditGroup = group;
            conductor.creditText = text;

            PlayerSettings.runInBackground = true;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save the scene.");
            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Firework scene built: {ScenePath}, {buildings} skyline blocks, {prefabs.Length} shell prefabs linked. Play Mode not started.");
        }

        private static Shader LoadShader(string name)
        {
            string path = $"{FireworkPrefabBuilder.Root}/Shaders/{name}.shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + path);
            return shader;
        }

        private static VolumeProfile BuildVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VolumeProfilePath));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }
            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.3f);
            bloom.scatter.Override(0.7f);
            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);
            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(false);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // 멀리 보이는 도시 실루엣: 높이가 제각각인 검은 상자들.
        private static int BuildSkyline(Transform parent, Material material)
        {
            var root = new GameObject("Skyline");
            root.transform.SetParent(parent, false);
            var rng = new System.Random(7);
            int count = 0;
            float x = -900f;
            while (x < 900f)
            {
                float width = 14f + (float)rng.NextDouble() * 26f;
                float height = 8f + (float)rng.NextDouble() * 28f;
                if (rng.NextDouble() < 0.12) height += 16f + (float)rng.NextDouble() * 16f;
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Block";
                block.transform.SetParent(root.transform, false);
                block.transform.localPosition = new Vector3(x + width * 0.5f, height * 0.5f, 430f + (float)rng.NextDouble() * 40f);
                block.transform.localScale = new Vector3(width, height, 20f);
                UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
                var renderer = block.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                x += width;
                count++;
            }
            return count;
        }

        // 실루엣 아래쪽에서 켜졌다 꺼지는 창문 불빛.
        private static void BuildCityLights(Transform parent)
        {
            var go = new GameObject("City Lights");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 5f, 415f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 16f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 2.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.45f, 0.8f), new Color(0.75f, 0.85f, 1f, 0.6f));
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 110f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1800f, 8f, 1f);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FireworkPrefabBuilder.GlowMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
