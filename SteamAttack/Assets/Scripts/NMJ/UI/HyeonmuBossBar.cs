using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NMJ
{
    /// <summary>
    /// 현무 1페이즈 체력바. 화면 위 가운데에 이름 칸 + 다리 4칸을 그린다.
    /// 다리(BHS.BossWallHealth)가 하나씩 부서질 때마다 칸이 비고 놋쇠 자물쇠가 열린다.
    /// 4칸이 모두 깨지면 잠시 뒤 사라지고 OnAllLegsBroken 이벤트를 쏜다 (2페이즈 바 연결용).
    /// UI 는 런타임에 직접 만들기 때문에 빈 오브젝트에 이 컴포넌트만 붙이면 된다.
    /// </summary>
    public class HyeonmuBossBar : MonoBehaviour
    {
        private const int LegCount = 4;

        [Header("연결 (비워 두면 씬에서 BHS.BossHealth 를 자동으로 찾음)")]
        [SerializeField] private Component boss;

        [Header("표시")]
        [SerializeField] private string bossName = "玄武 현무";
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private float hideDelay = 1.2f;
        [SerializeField] private float fadeTime = 0.4f;

        [Header("테스트")]
        [Tooltip("F5 = 다리 한 대 부수기, F7 = 처음으로 (보스 없이 단독 실행일 때만 되돌리기 가능).")]
        [SerializeField] private bool testKeys;

        [Header("이벤트")]
        public UnityEvent OnAllLegsBroken;

        private static readonly Color Paper = new Color(0.86f, 0.80f, 0.66f, 0.92f);
        private static readonly Color Ink = new Color(0.10f, 0.08f, 0.07f, 0.95f);
        private static readonly Color Brass = new Color(0.78f, 0.60f, 0.24f, 1f);
        private static readonly Color BrassDim = new Color(0.36f, 0.30f, 0.20f, 1f);
        private static readonly Color Fill = new Color(0.62f, 0.14f, 0.10f, 1f);

        private CanvasGroup group;
        private readonly List<Component> walls = new List<Component>();
        private readonly Image[] fills = new Image[LegCount];
        private readonly RectTransform[] shackles = new RectTransform[LegCount];
        private readonly Image[] locks = new Image[LegCount];
        private readonly float[] simHp = new float[LegCount];
        private readonly bool[] opened = new bool[LegCount];

        private bool simulated;
        private bool finished;
        private Coroutine hideRoutine;

        public int BrokenLegs { get; private set; }

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

        private void Start()
        {
            if (boss == null) boss = BhsBossLink.FindBoss();

            if (boss != null)
            {
                walls.AddRange(BhsBossLink.FindWalls(boss));
                walls.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                if (walls.Count > LegCount) walls.RemoveRange(LegCount, walls.Count - LegCount);
            }

            simulated = walls.Count == 0;
            if (simulated)
            {
                Debug.LogWarning("[HyeonmuBossBar] BHS 보스(다리)를 못 찾아 단독 테스트 모드로 동작한다.");
                ResetSimulation();
            }
        }

        private void Update()
        {
            if (testKeys) HandleTestKeys();
            if (finished) return;

            int broken = 0;
            for (int i = 0; i < LegCount; i++)
            {
                float ratio = ReadLeg(i);
                fills[i].fillAmount = ratio;

                bool isOpen = ratio <= 0f;
                if (isOpen) broken++;
                if (isOpen != opened[i]) SetLock(i, isOpen);
            }

            BrokenLegs = broken;
            if (broken >= LegCount) Finish();
        }

        private float ReadLeg(int i)
        {
            if (simulated) return simHp[i];
            if (i >= walls.Count || walls[i] == null) return 0f;

            return BhsBossLink.WallRatio(walls[i]);
        }

        private void HandleTestKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f5Key.wasPressedThisFrame) BreakOneLeg();
            if (kb.f7Key.wasPressedThisFrame) RestartForTest();
        }

        private void BreakOneLeg()
        {
            if (simulated)
            {
                for (int i = 0; i < LegCount; i++)
                {
                    if (simHp[i] <= 0f) continue;
                    simHp[i] = 0f;
                    return;
                }
                return;
            }

            foreach (var wall in walls)
            {
                if (wall == null || !wall.gameObject.activeSelf) continue;
                BhsBossLink.BreakWall(wall, transform.position);
                return;
            }
        }

        private void RestartForTest()
        {
            if (!simulated)
            {
                Debug.Log("[HyeonmuBossBar] 실제 보스에 연결된 상태에서는 되돌릴 수 없다.");
                return;
            }

            ResetSimulation();
        }

        private void ResetSimulation()
        {
            for (int i = 0; i < LegCount; i++) simHp[i] = 1f;

            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = null;
            finished = false;
            BrokenLegs = 0;
            group.alpha = 1f;
            for (int i = 0; i < LegCount; i++) SetLock(i, false);
        }

        private void Finish()
        {
            finished = true;
            OnAllLegsBroken?.Invoke();
            hideRoutine = StartCoroutine(HideRoutine());
        }

        private IEnumerator HideRoutine()
        {
            yield return new WaitForSeconds(hideDelay);

            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                group.alpha = 1f - t / fadeTime;
                yield return null;
            }

            group.alpha = 0f;
        }

        private void SetLock(int i, bool open)
        {
            opened[i] = open;
            locks[i].color = open ? BrassDim : Brass;
            shackles[i].anchoredPosition = new Vector2(0f, open ? 9f : 4f);
            shackles[i].localEulerAngles = new Vector3(0f, 0f, open ? 22f : 0f);
        }

        // ---------------------------------------------------------------- UI 생성

        private void BuildUi()
        {
            var canvasGo = new GameObject("HyeonmuBossBarCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            group = canvasGo.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // 바깥 틀 (한지 + 먹 테두리)
            var frame = MakeImage("Frame", canvasGo.transform, Ink);
            Anchor(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(640f, 96f));

            var paper = MakeImage("Paper", frame.transform, Paper);
            Stretch(paper.rectTransform, 3f);

            // 이름 칸
            var nameText = MakeText("Name", paper.transform, bossName, 30f, Ink);
            nameText.alignment = TextAlignmentOptions.Center;
            Anchor(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(600f, 38f));

            // 다리 4칸
            const float slotW = 140f, slotH = 22f, gap = 12f;
            float total = slotW * LegCount + gap * (LegCount - 1);
            float startX = -total * 0.5f + slotW * 0.5f;

            for (int i = 0; i < LegCount; i++)
            {
                float x = startX + i * (slotW + gap);

                var slot = MakeImage($"Leg{i + 1}", paper.transform, Ink);
                Anchor(slot.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, 14f), new Vector2(slotW, slotH));

                var fill = MakeImage("Fill", slot.transform, Fill);
                Stretch(fill.rectTransform, 2f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 1f;
                fills[i] = fill;

                // 놋쇠 자물쇠: 몸통 + 고리
                var body = MakeImage("Lock", slot.transform, Brass);
                Anchor(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(18f, 14f));
                locks[i] = body;

                var shackle = MakeImage("Shackle", body.transform, BrassDim);
                var sr = shackle.rectTransform;
                sr.pivot = new Vector2(0.5f, 0f);
                Anchor(sr, new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(10f, 9f));
                shackles[i] = sr;
                SetLock(i, false);
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

        private TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            if (rt.pivot == new Vector2(0.5f, 0.5f)) rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
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
