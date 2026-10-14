using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NMJ
{
    /// <summary>
    /// 타이틀 화면 「철과 묵」.
    /// 수묵화 배경 위에 세로쓰기 제목, 낙관, 메뉴 한 줄, 조작 안내를 얹는다.
    /// 안개가 흐르고 눈은 거꾸로(위로) 떠오른다 — 시간이 거슬러 흐르는 도성.
    /// 들어올 때 먹이 걷히며 글자가 한 자씩 찍히고, 시작하면 먹물이 번져 화면을 덮은 뒤 다음 씬을 연다.
    /// UI 는 런타임에 직접 만들기 때문에 빈 오브젝트에 이 컴포넌트만 붙이면 된다.
    /// </summary>
    public class NMJ_TitleScreen : MonoBehaviour
    {
        [Header("문구")]
        [SerializeField] private string titleFirst = "철";
        [SerializeField] private string titleMiddle = "과";
        [SerializeField] private string titleLast = "묵";
        [SerializeField] private string sealText = "鐵\n墨";
        [SerializeField] private string subtitle = "一八八七年 겨울 · 멈춘 도성";
        [SerializeField] private string startLabel = "시연 시작";
        [SerializeField] private string cornerLabel = "시 연 판";

        [Header("에셋")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("미리 구워 둔 배경. 비우면 실행할 때 코드로 그린다 (몇 초 걸림).")]
        [SerializeField] private Texture2D bakedBackground;

        [Header("시작")]
        [Tooltip("시작하면 열 씬 이름. Build Settings 에 있어야 한다. 비우면 onStart 이벤트만 쏜다.")]
        [SerializeField] private string nextScene = "Main";
        [SerializeField] private float transitionTime = 1.0f;
        public UnityEvent onStart;

        private static readonly Color Ink = new Color(0.055f, 0.055f, 0.065f, 1f);
        private static readonly Color InkSoft = new Color(0.055f, 0.055f, 0.065f, 0.62f);
        private static readonly Color Paper = new Color(0.95f, 0.93f, 0.88f, 1f);
        private static readonly Color Cinnabar = new Color(0.68f, 0.16f, 0.11f, 1f);

        private Canvas canvas;
        private RectTransform background;
        private RawImage mistBack, mistFront, snow;
        private Image curtain, blot;
        private RectTransform menuRect;
        private TextMeshProUGUI menuLabel, hint;
        private Image underline, diamond;
        private bool hovered;
        private bool starting;
        private bool introDone;

        private struct Reveal
        {
            public CanvasGroup Group;
            public RectTransform Rect;
            public float Delay, Duration, FromScale;
        }

        private readonly List<Reveal> reveals = new List<Reveal>();

        public Canvas Canvas => canvas;

#if UNITY_EDITOR
        private void Reset()
        {
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Fonts/NGH/ChosunCentennial_otf SDF.asset");
        }
#endif

        private void Awake()
        {
            BuildUi();
            EnsureEventSystem();
        }

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(Intro());
        }

        private void Update()
        {
            float t = Time.unscaledTime;

            // 안개는 옆으로, 눈은 위로 (거슬러 오르는 시간)
            mistBack.uvRect = new Rect(t * 0.006f, 0f, 1f, 1f);
            mistFront.uvRect = new Rect(-t * 0.011f, 0f, 1.3f, 1f);
            var snowRect = snow.uvRect;
            snow.uvRect = new Rect(Mathf.Sin(t * 0.05f) * 0.02f, -t * 0.012f, snowRect.width, snowRect.height);

            // 배경은 아주 천천히 숨 쉰다
            if (introDone)
            {
                float k = 1.03f + 0.012f * Mathf.Sin(t * 0.09f);
                background.localScale = new Vector3(k, k, 1f);
            }

            // 메뉴 표시: 붉은 마름모가 숨 쉬고, 안내 문구가 깜빡인다
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            diamond.color = Color.Lerp(Cinnabar, new Color(0.85f, 0.30f, 0.20f), hovered ? 1f : pulse * 0.4f);
            hint.alpha = 0.30f + 0.35f * pulse;
            float targetLine = hovered ? 1f : 0.62f;
            var ls = underline.rectTransform.localScale;
            underline.rectTransform.localScale = new Vector3(Mathf.MoveTowards(ls.x, targetLine, Time.unscaledDeltaTime * 3f), 1f, 1f);
            menuLabel.color = Color.Lerp(menuLabel.color, hovered ? Cinnabar : Ink, Time.unscaledDeltaTime * 10f);

            if (starting) return;

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if ((kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                || (pad != null && (pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame)))
            {
                Begin();
            }
        }

        /// <summary>"시연 시작". 먹물이 번져 화면을 덮은 뒤 다음 씬으로.</summary>
        public void Begin()
        {
            if (starting) return;
            starting = true;
            StopAllCoroutines();
            FinishReveals();
            StartCoroutine(Outro());
        }

        /// <summary>미리보기 캡처용: 들어오는 연출 없이 완성된 화면으로 둔다.</summary>
        public void SkipIntro()
        {
            StopAllCoroutines();
            FinishReveals();
            curtain.color = new Color(Ink.r, Ink.g, Ink.b, 0f);
            background.localScale = Vector3.one * 1.03f;
            introDone = true;
        }

        private IEnumerator Intro()
        {
            foreach (var r in reveals)
            {
                r.Group.alpha = 0f;
                r.Rect.localScale = Vector3.one * r.FromScale;
            }

            const float total = 3.2f;
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                // 먹 장막이 걷히며 배경이 천천히 물러난다
                float c = Ease(Mathf.Clamp01(t / 1.6f));
                curtain.color = new Color(Ink.r, Ink.g, Ink.b, 1f - c);
                float k = Mathf.Lerp(1.12f, 1.03f, Ease(Mathf.Clamp01(t / 3f)));
                background.localScale = new Vector3(k, k, 1f);

                foreach (var r in reveals)
                {
                    float p = Mathf.Clamp01((t - r.Delay) / r.Duration);
                    float e = Ease(p);
                    r.Group.alpha = e;
                    r.Rect.localScale = Vector3.one * Mathf.Lerp(r.FromScale, 1f, e);
                }
                yield return null;
            }

            FinishReveals();
            curtain.color = new Color(Ink.r, Ink.g, Ink.b, 0f);
            introDone = true;
        }

        private void FinishReveals()
        {
            foreach (var r in reveals)
            {
                r.Group.alpha = 1f;
                r.Rect.localScale = Vector3.one;
            }
        }

        private IEnumerator Outro()
        {
            var rt = blot.rectTransform;
            rt.position = diamond.rectTransform.position;
            blot.enabled = true;

            for (float t = 0f; t < transitionTime; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * (7f * Ease(t / transitionTime));
                yield return null;
            }
            rt.localScale = Vector3.one * 7f;

            onStart?.Invoke();

            if (!string.IsNullOrEmpty(nextScene))
            {
                if (Application.CanStreamedLevelBeLoaded(nextScene))
                    SceneManager.LoadScene(nextScene);
                else
                    Debug.LogError($"[NMJ_TitleScreen] '{nextScene}' 씬이 Build Settings 에 없다.");
            }
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ---------------------------------------------------------------- UI 생성

        private void BuildUi()
        {
            var canvasGo = new GameObject("TitleCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // 세로 배치(제목 ↔ 메뉴)가 겹치지 않도록 항상 높이 기준으로 맞춘다. 넓은 화면은 가로 여백이 늘 뿐이다.
            scaler.matchWidthOrHeight = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.transform;

            // 배경 + 움직이는 안개와 눈
            var bgTex = bakedBackground != null ? bakedBackground : NMJ_InkPainter.TitleBackground();
            var bg = MakeRaw("Background", root, bgTex, Color.white);
            background = bg.rectTransform;
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)bgTex.width / bgTex.height;
            // 화면이 16:9 보다 넓으면 위아래가 잘리는데, 무사와 얼음이 있는 아래보다 빈 하늘을 자른다
            background.pivot = new Vector2(0.5f, 0.12f);

            var mistTex = NMJ_InkPainter.Mist();
            mistBack = MakeRaw("MistBack", root, mistTex, new Color(1f, 1f, 1f, 0.18f));
            Band(mistBack.rectTransform, 0.30f, 0.62f);
            mistFront = MakeRaw("MistFront", root, NMJ_InkPainter.Mist(1024, 256, 17), new Color(1f, 1f, 1f, 0.18f));
            Band(mistFront.rectTransform, 0.06f, 0.34f);

            snow = MakeRaw("Snow", root, NMJ_InkPainter.Snow(), new Color(1f, 1f, 1f, 0.85f));
            Stretch(snow.rectTransform);
            snow.uvRect = new Rect(0f, 0f, 1920f / 512f / 1.4f, 1080f / 512f / 1.4f);

            // ── 세로쓰기 제목: 철 · 과 · 묵
            var title = Group("Title", root);
            var first = MakeText("First", title, titleFirst, 230f, Ink);
            Place(first.rectTransform, TopLeft, new Vector2(150f, -95f), new Vector2(280f, 280f));
            var middle = MakeText("Middle", title, titleMiddle, 96f, InkSoft);
            Place(middle.rectTransform, TopLeft, new Vector2(150f, -360f), new Vector2(280f, 120f));
            var last = MakeText("Last", title, titleLast, 230f, Ink);
            Place(last.rectTransform, TopLeft, new Vector2(150f, -455f), new Vector2(280f, 280f));
            AddReveal(first.gameObject, 0.55f, 0.9f, 1.18f);
            AddReveal(middle.gameObject, 0.95f, 0.8f, 1.10f);
            AddReveal(last.gameObject, 1.20f, 0.9f, 1.18f);

            // 낙관: 묵 아래에 비스듬히 찍는다
            var seal = MakeImage("Seal", title, NMJ_InkPainter.ToSprite(NMJ_InkPainter.Seal()), Cinnabar);
            Place(seal.rectTransform, TopLeft, new Vector2(392f, -655f), new Vector2(56f, 84f));
            seal.rectTransform.localEulerAngles = new Vector3(0f, 0f, -3f);
            var sealLabel = MakeText("SealText", seal.transform, sealText, 27f, Paper);
            sealLabel.lineSpacing = -22f;
            Stretch(sealLabel.rectTransform);
            AddReveal(seal.gameObject, 1.85f, 0.35f, 1.6f);

            // 가는 세로 획 + 세로 부제
            var rule = MakeImage("Rule", title, null, new Color(Ink.r, Ink.g, Ink.b, 0.35f));
            Place(rule.rectTransform, TopLeft, new Vector2(488f, -120f), new Vector2(1.5f, 520f));
            var sub = MakeText("Subtitle", title, Vertical(subtitle), 25f, InkSoft);
            sub.alignment = TextAlignmentOptions.Top;
            sub.lineSpacing = -6f;
            Place(sub.rectTransform, TopLeft, new Vector2(504f, -118f), new Vector2(40f, 640f));
            AddReveal(rule.gameObject, 1.5f, 0.9f, 1f);
            AddReveal(sub.gameObject, 1.6f, 1.0f, 1f);

            // ── 메뉴 한 줄
            var menu = Group("Menu", root);
            diamond = MakeImage("Diamond", menu, null, Cinnabar);
            Place(diamond.rectTransform, BottomLeft, new Vector2(172f, 330f), new Vector2(11f, 11f));
            diamond.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

            menuLabel = MakeText("Start", menu, startLabel, 40f, Ink);
            menuLabel.alignment = TextAlignmentOptions.Left;
            menuLabel.characterSpacing = 24f;
            menuLabel.raycastTarget = true;
            menuRect = menuLabel.rectTransform;
            Place(menuRect, BottomLeft, new Vector2(200f, 303f), new Vector2(330f, 56f));

            underline = MakeImage("Underline", menu, NMJ_InkPainter.ToSprite(NMJ_InkPainter.BrushLine()), Ink);
            var ul = underline.rectTransform;
            Place(ul, BottomLeft, new Vector2(196f, 292f), new Vector2(300f, 12f));
            ul.pivot = new Vector2(0f, 0.5f);
            ul.localScale = new Vector3(0.62f, 1f, 1f);

            hint = MakeText("Hint", menu, "Enter", 19f, Ink);
            hint.alignment = TextAlignmentOptions.Left;
            hint.characterSpacing = 30f;
            Place(hint.rectTransform, BottomLeft, new Vector2(202f, 252f), new Vector2(300f, 30f));

            var button = menuLabel.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Begin);
            var hover = menuLabel.gameObject.AddComponent<HoverFlag>();
            hover.Changed = v => hovered = v;
            AddReveal(menu.gameObject, 2.2f, 0.8f, 1f);

            // ── 조작 안내: 오른쪽 아래, 낮은 목소리로
            var guide = Group("Controls", root);
            var guideRule = MakeImage("Rule", guide, null, new Color(Ink.r, Ink.g, Ink.b, 0.28f));
            Place(guideRule.rectTransform, BottomRight, new Vector2(-90f, 132f), new Vector2(980f, 1.2f));
            var guideText = MakeText("Text", guide, ControlsText(), 19f, new Color(Ink.r, Ink.g, Ink.b, 0.78f));
            guideText.alignment = TextAlignmentOptions.TopRight;
            guideText.characterSpacing = 2f;
            guideText.lineSpacing = 30f;
            guideText.richText = true;
            Place(guideText.rectTransform, BottomRight, new Vector2(-90f, 46f), new Vector2(1100f, 76f));
            AddReveal(guide.gameObject, 2.5f, 1.0f, 1f);

            // 오른쪽 위 작은 표식
            var corner = MakeText("Corner", root, cornerLabel, 16f, new Color(Paper.r, Paper.g, Paper.b, 0.55f));
            corner.alignment = TextAlignmentOptions.TopRight;
            corner.characterSpacing = 20f;
            Place(corner.rectTransform, TopRight, new Vector2(-70f, -52f), new Vector2(300f, 30f));
            AddReveal(corner.gameObject, 2.6f, 1.0f, 1f);

            // 들어올 때 걷히는 먹 장막 / 시작할 때 번지는 먹물
            curtain = MakeImage("Curtain", root, null, Ink);
            Stretch(curtain.rectTransform);
            curtain.color = new Color(Ink.r, Ink.g, Ink.b, 0f);

            blot = MakeImage("InkBlot", root, NMJ_InkPainter.ToSprite(NMJ_InkPainter.InkBlot()), Ink);
            Place(blot.rectTransform, BottomLeft, Vector2.zero, new Vector2(1300f, 1300f));
            blot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            blot.rectTransform.localScale = Vector3.zero;
            blot.enabled = false;
        }

        private static string Vertical(string text)
        {
            var chars = new List<string>();
            foreach (char ch in text)
                chars.Add(ch == ' ' ? "" : ch.ToString());
            return string.Join("\n", chars);
        }

        private static string ControlsText()
        {
            string K(string key) => $"<color=#9E2A1C>{key}</color>";
            return
                $"이동 {K("WASD")}     질주 {K("SHIFT")}     구르기 {K("SPACE")}     약공격 {K("좌클릭")}     강공격 · 조준 {K("우클릭")}\n" +
                $"무기 전환 {K("TAB")}     발도 {K("C")}     받아치기 {K("CTRL")}     증기발도 {K("Z")}     시간 역행 {K("Q")}     상호작용 {K("F")}     락온 {K("T")}";
        }

        private void AddReveal(GameObject go, float delay, float duration, float fromScale)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            g.blocksRaycasts = true;
            reveals.Add(new Reveal { Group = g, Rect = (RectTransform)go.transform, Delay = delay, Duration = duration, FromScale = fromScale });
        }

        // ---------------------------------------------------------------- 도우미

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            return go.transform;
        }

        private static RawImage MakeRaw(string name, Transform parent, Texture tex, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        /// <summary>anchor 와 pivot 을 같은 모서리에 두고 그 모서리 기준으로 놓는다.</summary>
        private static void Place(RectTransform rt, Vector2 corner, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = corner;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Band(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>마우스가 올라왔는지 알려 준다.</summary>
        private sealed class HoverFlag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public System.Action<bool> Changed;
            public void OnPointerEnter(PointerEventData _) => Changed?.Invoke(true);
            public void OnPointerExit(PointerEventData _) => Changed?.Invoke(false);
        }
    }
}
