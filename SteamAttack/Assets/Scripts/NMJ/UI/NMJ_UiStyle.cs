using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NMJ
{
    /// <summary>
    /// NMJ UI 공통 기준. 기존 팀 HUD 를 NMJ UI 로 바꿔 나갈 때 모든 화면 요소가 이 값을 따른다.
    ///   - 화면 기준: 1920×1080, 높이 맞춤 (넓은 화면에서도 위아래 배치가 겹치지 않음)
    ///   - 그리는 순서: HUD 40 → 보스 50 → 타이틀 100
    ///   - 색: 먹 · 한지 · 주홍 · 놋쇠 네 가지
    ///   - 여백: 화면 가장자리에서 Margin 만큼 띄운다
    /// </summary>
    public static class NMJ_UiStyle
    {
        // ───────── 화면 기준 ─────────
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public const float MatchWidthOrHeight = 1f;
        public const float Margin = 32f;

        // ───────── 그리는 순서 (클수록 위) ─────────
        public const int OrderHud = 40;
        public const int OrderSteamGauge = 41;
        public const int OrderBossBar = 50;
        public const int OrderInkHealthBar = 51;
        public const int OrderTitle = 100;

        // ───────── 색 ─────────
        public static readonly Color Ink = new Color(0.055f, 0.055f, 0.065f, 1f);
        public static readonly Color Paper = new Color(0.93f, 0.90f, 0.83f, 1f);
        public static readonly Color PaperAged = new Color(0.86f, 0.80f, 0.66f, 1f);
        public static readonly Color Cinnabar = new Color(0.68f, 0.16f, 0.11f, 1f);
        public static readonly Color Warn = new Color(0.85f, 0.15f, 0.10f, 1f);
        public static readonly Color Brass = new Color(0.66f, 0.50f, 0.22f, 1f);
        public static readonly Color Steam = new Color(0.95f, 0.95f, 0.93f, 1f);

        // ───────── 글꼴 ─────────
        public const string FontPath = "Assets/Fonts/NGH/ChosunCentennial_otf SDF.asset";

#if UNITY_EDITOR
        /// <summary>에디터에서 기본 한글 폰트(한자는 대체 폰트로 자동)를 불러온다.</summary>
        public static TMP_FontAsset LoadDefaultFont()
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        }
#endif

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>공통 기준을 따르는 화면 덮개 캔버스를 parent 아래에 만든다.</summary>
        public static CanvasGroup CreateCanvas(string name, Transform parent, int sortingOrder, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = MatchWidthOrHeight;

            if (raycast) go.AddComponent<GraphicRaycaster>();

            var group = go.AddComponent<CanvasGroup>();
            group.interactable = raycast;
            group.blocksRaycasts = raycast;
            return group;
        }
    }
}
