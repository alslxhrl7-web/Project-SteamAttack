using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace NMJ
{
    /// <summary>
    /// 타이틀 씬 「철과 묵」을 만들고, 배경을 PNG 로 구워 두고, 미리보기 PNG 를 뽑는다.
    /// 메뉴: SteamAttack → NMJ → 타이틀 씬 만들기 / 타이틀 미리보기 캡처
    /// 배치 모드: -executeMethod NMJ.NMJ_TitleSceneBuilder.BuildAndCapture
    /// </summary>
    public static class NMJ_TitleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/NMJ/NMJ_Title.unity";
        private const string FontPath = "Assets/Fonts/NGH/ChosunCentennial_otf SDF.asset";
        private const string BackgroundPath = "Assets/Arts/Textures/NMJ/NMJ_TitleBackground.png";
        private const string PreviewPath = "Assets/Scripts/NMJ/UI/Preview~/NMJ_Title.png";

        [MenuItem("SteamAttack/NMJ/타이틀 배경 굽기")]
        public static Texture2D BakeBackground()
        {
            var tex = NMJ_InkPainter.TitleBackground();
            Directory.CreateDirectory(Path.GetDirectoryName(BackgroundPath));
            File.WriteAllBytes(BackgroundPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(BackgroundPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(BackgroundPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            Debug.Log($"[NMJ_TitleSceneBuilder] 배경 저장: {BackgroundPath}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
        }

        [MenuItem("SteamAttack/NMJ/타이틀 씬 만들기")]
        public static void BuildScene()
        {
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (bg == null) bg = BakeBackground();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.055f, 0.065f);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var title = new GameObject("NMJ_TitleScreen").AddComponent<NMJ_TitleScreen>();
            Assign(title, bg);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[NMJ_TitleSceneBuilder] {ScenePath} 저장");
        }

        [MenuItem("SteamAttack/NMJ/타이틀 미리보기 캡처")]
        public static void Capture()
        {
            var root = new GameObject("TitleCapture");
            root.SetActive(false);
            var title = root.AddComponent<NMJ_TitleScreen>();
            Assign(title, AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath));

            // 에디터 모드라 Awake 가 자동으로 안 불리므로 직접 UI 를 만든다
            typeof(NMJ_TitleScreen).GetMethod("BuildUi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(title, null);
            root.SetActive(true);
            title.SkipIntro();

            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("CaptureCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;
            cam.orthographic = true;

            var canvas = title.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;

            Canvas.ForceUpdateCanvases();
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath));
            File.WriteAllBytes(PreviewPath, shot.EncodeToPNG());
            Debug.Log($"[NMJ_TitleSceneBuilder] 미리보기 저장: {Path.GetFullPath(PreviewPath)}");

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(root);
            rt.Release();
        }

        /// <summary>배치 모드용: 배경을 새로 굽고, 씬을 만들고, 미리보기를 뽑는다.</summary>
        public static void BuildAndCapture()
        {
            int code = 0;
            try
            {
                BakeBackground();
                BuildScene();
                Capture();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[NMJ_TitleSceneBuilder] 실패: " + e);
                code = 1;
            }

            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static void Assign(NMJ_TitleScreen title, Texture2D bg)
        {
            var so = new SerializedObject(title);
            so.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            so.FindProperty("bakedBackground").objectReferenceValue = bg;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
