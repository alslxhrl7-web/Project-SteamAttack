using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NMJ
{
    /// <summary>
    /// 지금 열린 씬에 타이틀 화면을 넣고 플레이해 본다.
    /// 메뉴: SteamAttack → NMJ → 열린 씬에서 타이틀 플레이
    /// 외부에서 Temp/nmj_play_title.trigger 파일을 만들어 두면, 다음 스크립트 리로드 때 자동으로 실행된다.
    /// 플레이 중 화면을 몇 장 찍어 Assets/Scripts/NMJ/UI/Preview~/ 에, 콘솔 로그를 Temp/nmj_play_log.txt 에 남긴다.
    /// </summary>
    [InitializeOnLoad]
    public static class NMJ_TitlePlayTest
    {
        private const string Trigger = "Temp/nmj_play_title.trigger";
        private const string LogPath = "Temp/nmj_play_log.txt";
        private const string ShotDir = "Assets/Scripts/NMJ/UI/Preview~";
        private const string StateKey = "NMJ_TitlePlayTest.Capturing";
        private static readonly float[] ShotTimes = { 0.9f, 2.0f, 4.5f };

        private static double playStart;
        private static int nextShot;
        private static readonly StringBuilder log = new StringBuilder();

        static NMJ_TitlePlayTest()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += PollTrigger;

            // 도메인 리로드 뒤 플레이 모드로 들어온 경우에도 캡처를 이어 간다
            if (EditorApplication.isPlaying && SessionState.GetBool(StateKey, false)) BeginCapture();
        }

        private static double nextPoll;

        /// <summary>트리거 파일은 1초마다 확인한다 (스크립트를 다시 컴파일하지 않아도 외부에서 실행 가능).</summary>
        private static void PollTrigger()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (!File.Exists(Trigger) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            File.Delete(Trigger);
            Run();
        }

        [MenuItem("SteamAttack/NMJ/열린 씬에서 타이틀 플레이")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 플레이 화면이 보이도록 Game 뷰를 앞으로
            EditorApplication.ExecuteMenuItem("Window/General/Game");

            var existing = Object.FindAnyObjectByType<NMJ_TitleScreen>(FindObjectsInactive.Include);
            var title = existing != null ? existing : new GameObject("NMJ_TitleScreen").AddComponent<NMJ_TitleScreen>();

            var so = new SerializedObject(title);
            so.FindProperty("font").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NGH/ChosunCentennial_otf SDF.asset");
            so.FindProperty("bakedBackground").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/Textures/NMJ/NMJ_TitleBackground.png");
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(title.gameObject.scene);

            Debug.Log($"[NMJ_TitlePlayTest] '{title.gameObject.scene.name}' 씬에 타이틀을 넣고 플레이한다.");
            SessionState.SetBool(StateKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(StateKey, false))
                BeginCapture();
            else if (change == PlayModeStateChange.ExitingPlayMode)
                Stop();
        }

        private static void BeginCapture()
        {
            playStart = -1;
            nextShot = 0;
            log.Clear();
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { Stop(); return; }

            // 씬 로딩이 끝나 실제로 화면이 그려지기 시작한 때부터 잰다
            if (playStart < 0)
            {
                if (Time.frameCount < 3) return;
                playStart = EditorApplication.timeSinceStartup;
            }

            double t = EditorApplication.timeSinceStartup - playStart;
            if (nextShot < ShotTimes.Length && t >= ShotTimes[nextShot])
            {
                Directory.CreateDirectory(ShotDir);
                string path = $"{ShotDir}/play_{nextShot}_{ShotTimes[nextShot]:0.0}s.png";
                ScreenCapture.CaptureScreenshot(path);
                log.AppendLine($"[shot] {path}");
                nextShot++;
            }

            if (nextShot >= ShotTimes.Length && t > ShotTimes[ShotTimes.Length - 1] + 1.0)
            {
                Flush();
                SessionState.SetBool(StateKey, false);
                EditorApplication.update -= Tick;
            }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            log.AppendLine($"[{type}] {message}");
            if (type == LogType.Error || type == LogType.Exception) log.AppendLine(stack);
        }

        private static void Stop()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (SessionState.GetBool(StateKey, false)) Flush();
            SessionState.SetBool(StateKey, false);
        }

        private static void Flush()
        {
            File.WriteAllText(LogPath, log.ToString());
        }
    }
}
