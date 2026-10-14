using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NMJ
{
    /// <summary>
    /// 먹물 붓 획 느낌의 가로 체력 막대 한 줄. 줄어들 때 흰 잔상이 늦게 따라온다.
    /// 값은 SetValues / SetRatio 로 넣는다. boss 를 지정하면 BHS.BossHealth 의 체력을 자동으로 따라간다.
    /// UI 는 런타임에 직접 만들기 때문에 빈 오브젝트에 이 컴포넌트만 붙이면 된다.
    /// </summary>
    public class InkHealthBar : MonoBehaviour
    {
        [Header("연결 (선택)")]
        [Tooltip("지정하면 이 보스의 CurrentHp / maxHp 를 매 프레임 반영한다.")]
        [SerializeField] private BHS.BossHealth boss;

        [Header("표시")]
        [SerializeField] private string label = "玄武 현무";
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Vector2 size = new Vector2(760f, 34f);
        [SerializeField] private float topMargin = 28f;

        [Header("잔상")]
        [SerializeField] private float trailDelay = 0.45f;
        [SerializeField] private float trailSpeed = 0.6f;

        private static readonly Color Ink = new Color(0.08f, 0.07f, 0.07f, 0.95f);
        private static readonly Color Paper = new Color(0.86f, 0.80f, 0.66f, 0.92f);
        private static readonly Color Seal = new Color(0.62f, 0.14f, 0.10f, 1f);
        private static readonly Color Trail = new Color(0.97f, 0.95f, 0.90f, 0.95f);

        private Image fill;
        private Image trail;
        private CanvasGroup group;
        private float ratio = 1f;
        private float trailRatio = 1f;
        private float trailTimer;

        public float Ratio => ratio;

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
        }

        private void Update()
        {
            if (boss != null)
                SetValues(boss.CurrentHp, boss.maxHp);

            if (trailTimer > 0f)
            {
                trailTimer -= Time.deltaTime;
            }
            else if (trailRatio > ratio)
            {
                trailRatio = Mathf.Max(ratio, trailRatio - trailSpeed * Time.deltaTime);
                trail.fillAmount = trailRatio;
            }
        }

        public void SetValues(float current, float max)
        {
            SetRatio(max > 0f ? current / max : 0f);
        }

        public void SetRatio(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, ratio)) return;

            if (value < ratio) trailTimer = trailDelay;
            else trailRatio = value;

            ratio = value;
            fill.fillAmount = ratio;
            trail.fillAmount = trailRatio;
        }

        public void Show(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("InkHealthBarCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 51;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            group = canvasGo.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // 먹 테두리 → 한지 바탕 → 흰 잔상 → 먹물 체력
            var frame = MakeImage("Frame", canvasGo.transform, Ink);
            Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -topMargin), size);

            var paper = MakeImage("Paper", frame.transform, Paper);
            Stretch(paper.rectTransform, 3f);

            trail = MakeFill("Trail", paper.transform, Trail);
            fill = MakeFill("Fill", paper.transform, Ink);

            // 오른쪽 끝에 붉은 낙관 한 점
            var seal = MakeImage("Seal", frame.transform, Seal);
            Place(seal.rectTransform, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(size.y - 10f, size.y - 10f));
            seal.rectTransform.pivot = new Vector2(1f, 0.5f);

            if (!string.IsNullOrEmpty(label))
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(canvasGo.transform, false);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.text = label;
                tmp.fontSize = 28f;
                tmp.color = Paper;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                Place(tmp.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -topMargin - size.y - 2f), new Vector2(size.x, 36f));
            }
        }

        private static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image MakeFill(string name, Transform parent, Color color)
        {
            var img = MakeImage(name, parent, color);
            Stretch(img.rectTransform, 0f);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
