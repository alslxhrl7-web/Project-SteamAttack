using System;
using UnityEngine;

namespace NMJ
{
    /// <summary>
    /// 수묵화 텍스처를 코드로 그린다. 별도 이미지 에셋 없이 타이틀 배경, 안개, 눈, 먹물 번짐을 만든다.
    ///
    /// 배경 구도 (색은 먹 하나 + 주홍 하나):
    ///   하늘에 걸린 주홍 해 → 옅은 먼 산 → 안개에서 솟아 해를 감아 오르는 마른 붓 한 획(현무의 암시)
    ///   → 안개 → 작은 경복궁 지붕들 → 얼어붙은 연못 위 홀로 선 무사 (해와 무사가 얼음에 비친다).
    /// 그리지 않고 암시하는 것, 크기 대비, 여백이 그림의 핵심이다.
    /// </summary>
    public static class NMJ_InkPainter
    {
        private static readonly Color Paper = new Color(0.928f, 0.905f, 0.855f);
        private static readonly Color PaperLight = new Color(0.965f, 0.955f, 0.930f);
        private static readonly Color Ink = new Color(0.055f, 0.055f, 0.065f);
        private static readonly Color InkWash = new Color(0.20f, 0.205f, 0.22f);
        private static readonly Color Sun = new Color(0.72f, 0.17f, 0.11f);
        private static readonly Color SunDeep = new Color(0.55f, 0.10f, 0.08f);
        private static readonly Color EyeGlow = new Color(0.80f, 0.92f, 1.00f);

        // ------------------------------------------------------------------ 노이즈

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967295f;
            }
        }

        private static float Noise(float x, float y, int seed = 0)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static float Fbm(float x, float y, int octaves, int seed = 0)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y, seed + i * 17) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.03f; amp *= 0.5f;
            }
            return sum / norm;
        }

        /// <summary>보로노이 F2-F1. 0 에 가까울수록 칸의 경계(등껍질 갑판 이음매).</summary>
        private static float CellEdge(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float f1 = 9f, f2 = 9f;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                float px = xi + i + 0.15f + 0.7f * Hash(xi + i, yi + j, seed);
                float py = yi + j + 0.15f + 0.7f * Hash(xi + i, yi + j, seed + 1);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
            return f2 - f1;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        // ------------------------------------------------------------------ 그림판

        /// <summary>좌표: X = 0 ~ 가로세로비(A), Y = 0(아래) ~ 1(위).</summary>
        private sealed class Sheet
        {
            public readonly int W, H;
            public readonly float A;
            public readonly Color[] Px;

            public Sheet(int w, int h)
            {
                W = w; H = h; A = (float)w / h;
                Px = new Color[w * h];
            }

            public void Blend(int i, Color c, float a)
            {
                if (a <= 0f) return;
                if (a > 1f) a = 1f;
                var p = Px[i];
                p.r += (c.r - p.r) * a;
                p.g += (c.g - p.g) * a;
                p.b += (c.b - p.b) * a;
                Px[i] = p;
            }

            /// <summary>영역 안 모든 픽셀에 alpha(X, Y) 만큼 색을 얹는다.</summary>
            public void Paint(float xMin, float xMax, float yMin, float yMax, Color c, Func<float, float, float> alpha)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(xMin / A * W));
                int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(xMax / A * W));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(yMin * H));
                int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(yMax * H));
                for (int y = y0; y <= y1; y++)
                {
                    float Y = (y + 0.5f) / H;
                    for (int x = x0; x <= x1; x++)
                        Blend(y * W + x, c, alpha((x + 0.5f) / W * A, Y));
                }
            }

            /// <summary>작은 실루엣용: 픽셀당 3x3 표본으로 가장자리를 매끈하게.</summary>
            public void PaintAA(float xMin, float xMax, float yMin, float yMax, Color c, float strength, Func<float, float, bool> inside)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(xMin / A * W));
                int x1 = Mathf.Min(W - 1, Mathf.CeilToInt(xMax / A * W));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(yMin * H));
                int y1 = Mathf.Min(H - 1, Mathf.CeilToInt(yMax * H));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                        if (inside((x + (sx + 0.5f) / 3f) / W * A, (y + (sy + 0.5f) / 3f) / H)) hit++;
                    if (hit > 0) Blend(y * W + x, c, strength * hit / 9f);
                }
            }

            public void Disc(float cx, float cy, float r, Color c, Func<float, float> shade)
            {
                Paint(cx - r, cx + r, cy - r, cy + r, c, (X, Y) =>
                {
                    float d = Mathf.Sqrt((X - cx) * (X - cx) + (Y - cy) * (Y - cy)) / r;
                    return d > 1f ? 0f : shade(d);
                });
            }

            public Texture2D ToTexture(string name)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false) { name = name, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels(Px);
                tex.Apply(false, false);
                return tex;
            }
        }

        // ------------------------------------------------------------------ 타이틀 배경

        public static Texture2D TitleBackground(int width = 1920, int height = 1080, int seed = 1887)
        {
            var s = new Sheet(width, height);
            float A = s.A;

            var sun = new Vector2(A * 0.705f, 0.665f);
            const float sunR = 0.118f;

            PaintPaper(s, seed);
            PaintSky(s, seed);
            PaintSun(s, sun, sunR, seed);
            PaintFarMountains(s, seed);
            PaintMist(s, 0.435f, 0.055f, 0.55f, seed + 1);
            PaintSerpentStroke(s, sun, sunR, seed);
            PaintMist(s, 0.245f, 0.060f, 0.85f, seed + 2);
            PaintPalace(s);
            PaintIce(s, sun, seed);
            PaintWarrior(s, A * 0.415f, 0.062f, 0.088f, seed);
            PaintVignette(s, seed);
            PaintGrain(s, seed);

            return s.ToTexture("NMJ_TitleBackground");
        }

        private static void PaintPaper(Sheet s, int seed)
        {
            for (int y = 0; y < s.H; y++)
            {
                float Y = (y + 0.5f) / s.H;
                for (int x = 0; x < s.W; x++)
                {
                    float X = (x + 0.5f) / s.W * s.A;
                    float blot = Fbm(X * 2.5f, Y * 2.5f, 3, seed) - 0.5f;
                    float fiber = Mathf.Pow(Noise(X * 7f, Y * 300f, seed + 5), 8f);
                    float k = 1f + blot * 0.05f - fiber * 0.04f;
                    s.Px[y * s.W + x] = new Color(Paper.r * k, Paper.g * k, Paper.b * k);
                }
            }
        }

        private static void PaintSky(Sheet s, int seed)
        {
            s.Paint(0f, s.A, 0.3f, 1f, InkWash, (X, Y) =>
            {
                float cloud = Fbm(X * 1.1f + 3f, Y * 2.2f, 5, seed + 3);
                float top = Smooth(0.62f, 1.02f, Y);
                return top * (0.10f + 0.30f * Smooth(0.35f, 0.75f, cloud));
            });
        }

        private static void PaintSun(Sheet s, Vector2 c, float r, int seed)
        {
            // 번진 가장자리를 가진 주홍 해. 가운데 아래로 안개 한 줄이 지나간다.
            s.Paint(c.x - r * 2.2f, c.x + r * 2.2f, c.y - r * 2.2f, c.y + r * 2.2f, Sun, (X, Y) =>
            {
                float dx = X - c.x, dy = Y - c.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                float ang = Mathf.Atan2(dy, dx);
                float bleed = (Fbm(Mathf.Cos(ang) * 3f + 9f, Mathf.Sin(ang) * 3f + 9f, 4, seed + 7) - 0.5f) * 0.05f;
                float body = 1f - Smooth(0.965f + bleed, 1.0f + bleed, d);
                float halo = 0.10f * Mathf.Exp(-Mathf.Pow((d - 1f) / 0.45f, 2f)) * (d > 1f ? 1f : 0f);
                return 0.94f * body + halo;
            });

            // 해 안쪽의 결: 아래쪽이 조금 더 짙다
            s.Paint(c.x - r, c.x + r, c.y - r, c.y + r, SunDeep, (X, Y) =>
            {
                float d = Mathf.Sqrt((X - c.x) * (X - c.x) + (Y - c.y) * (Y - c.y)) / r;
                if (d > 0.97f) return 0f;
                return 0.35f * Smooth(0.2f, -0.9f, (Y - c.y) / r) + 0.12f * Fbm(X * 18f, Y * 18f, 3, seed + 8);
            });

            // 해를 가로지르는 안개 한 줄
            s.Paint(c.x - r * 1.8f, c.x + r * 1.8f, c.y - r * 0.6f, c.y - r * 0.15f, PaperLight, (X, Y) =>
            {
                float band = Mathf.Exp(-Mathf.Pow((Y - (c.y - r * 0.38f)) / (r * 0.09f), 2f));
                float streak = Smooth(0.35f, 0.7f, Noise(X * 9f, Y * 60f, seed + 11));
                float ends = Smooth(r * 1.8f, r * 0.6f, Mathf.Abs(X - c.x));
                return 0.55f * band * streak * ends;
            });
        }

        private static void PaintFarMountains(Sheet s, int seed)
        {
            PaintRidge(s, 0.43f, 0.30f, 1.25f, 0.13f, 0.13f, seed + 11);
            PaintRidge(s, 0.37f, 0.17f, 2.1f, 0.20f, 0.08f, seed + 23);
        }

        private static void PaintRidge(Sheet s, float baseY, float amp, float freq, float density, float fade, int seed)
        {
            var ridge = new float[s.W];
            for (int x = 0; x < s.W; x++)
            {
                float X = (x + 0.5f) / s.W * s.A;
                float n = Fbm(X * freq, 0.5f, 5, seed);
                ridge[x] = baseY + amp * Mathf.Pow(1f - Mathf.Abs(2f * n - 1f), 1.7f);
            }

            for (int y = 0; y < s.H; y++)
            {
                float Y = (y + 0.5f) / s.H;
                for (int x = 0; x < s.W; x++)
                {
                    float h = ridge[x];
                    if (Y > h) continue;
                    float X = (x + 0.5f) / s.W * s.A;
                    float depth = h - Y;

                    float wash = 0.25f + 0.75f * Mathf.Exp(-depth / fade);
                    float edge = 0.5f * Mathf.Exp(-depth / 0.003f);
                    float dry = 0.7f + 0.6f * Fbm(X * 12f, Y * 35f, 2, seed + 3);
                    float mist = Smooth(baseY - 0.10f, baseY + amp * 0.3f, Y);
                    float margin = 0.18f + 0.82f * Smooth(0.18f, 0.62f, X / s.A);
                    float snow = Smooth(0.68f, 0.88f, Noise(X * 20f + Y * 28f, Y * 6f, seed + 9)) * Mathf.Exp(-depth / (fade * 0.45f));

                    s.Blend(y * s.W + x, Ink, density * (wash * dry + edge) * mist * margin * (1f - 0.7f * snow));
                }
            }
        }

        private static void PaintMist(Sheet s, float centerY, float halfH, float strength, int seed)
        {
            s.Paint(0f, s.A, centerY - halfH * 3f, centerY + halfH * 3f, PaperLight, (X, Y) =>
            {
                float band = Mathf.Exp(-Mathf.Pow((Y - centerY) / halfH, 2f));
                float body = Smooth(0.30f, 0.75f, Fbm(X * 1.8f, Y * 7f, 5, seed));
                return strength * band * (0.35f + 0.65f * body);
            });
        }

        /// <summary>
        /// 현무를 그리지 않고 암시한다: 안개에서 솟아 해를 감아 오르는 마른 붓 한 획.
        /// 붓 획(墨)이면서 뱀의 몸이다. 끝에만 차가운 눈빛 한 점.
        /// </summary>
        private static void PaintSerpentStroke(Sheet s, Vector2 sun, float sunR, int seed)
        {
            float A = s.A;
            var p0 = new Vector2(A * 0.52f, 0.235f);
            var p1 = new Vector2(A * 0.90f, 0.25f);
            var p2 = new Vector2(A * 0.88f, 0.74f);
            var p3 = new Vector2(sun.x - sunR * 0.15f, sun.y + sunR * 0.10f);

            var buf = new float[s.W * s.H];
            float pxPerUnit = s.H;
            const int steps = 2600;
            Vector2 prev = Bezier(p0, p1, p2, p3, 0f);

            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                var p = Bezier(p0, p1, p2, p3, t);
                var tan = (p - prev).normalized;
                prev = p;
                var nrm = new Vector2(-tan.y, tan.x);

                // 굵기: 안개 속에서 가늘게 나와 가운데서 부풀고, 끝에서 날카롭게 빠진다
                float swell = Smooth(0f, 0.35f, t) * (1f - Smooth(0.72f, 1f, t));
                float half = 0.006f + 0.050f * swell + 0.006f * (Noise(t * 8f, 0f, seed + 3) - 0.5f);
                // 끝으로 갈수록 붓에 먹이 말라 털이 갈라진다
                float dryness = Smooth(0.62f, 1f, t);
                // 안개 속에서 서서히 먹이 드러난다
                float enter = Smooth(0f, 0.12f, t);

                int across = Mathf.CeilToInt(half * pxPerUnit * 2.2f);
                for (int k = -across; k <= across; k++)
                {
                    float w = k / (float)across;                         // -1 ~ 1 (폭 방향)
                    var q = p + nrm * (w * half);
                    int x = Mathf.FloorToInt(q.x / A * s.W), y = Mathf.FloorToInt(q.y * s.H);
                    if (x < 0 || x >= s.W || y < 0 || y >= s.H) continue;

                    float bristle = Noise(w * 9f + 3f, t * 2.5f, seed + 5);       // 털 한 올 = 폭 방향 위치마다 고정
                    float gap = Smooth(0.30f + 0.45f * dryness, 0.50f + 0.45f * dryness, bristle);
                    float edge = 1f - Smooth(0.82f, 1f, Mathf.Abs(w));
                    float a = edge * Mathf.Lerp(1f, gap, Mathf.Clamp01(dryness * 1.2f + Mathf.Abs(w) * 0.5f));
                    a *= (0.92f + 0.08f * Noise(w * 30f, t * 40f, seed + 6)) * enter;

                    int idx = y * s.W + x;
                    if (a > buf[idx]) buf[idx] = a;
                }
            }

            for (int i = 0; i < buf.Length; i++)
                if (buf[i] > 0f) s.Blend(i, Ink, 0.93f * buf[i]);

            // 획을 그을 때 튄 먹 방울 몇 점
            var rng = new System.Random(seed + 21);
            for (int i = 0; i < 9; i++)
            {
                float t = 0.25f + (float)rng.NextDouble() * 0.45f;
                var p = Bezier(p0, p1, p2, p3, t) + new Vector2(((float)rng.NextDouble() - 0.3f) * 0.12f, ((float)rng.NextDouble() - 0.5f) * 0.08f);
                float r = 0.0015f + (float)rng.NextDouble() * 0.004f;
                s.Disc(p.x, p.y, r, Ink, d => 0.85f * (1f - Smooth(0.7f, 1f, d)));
            }

            // 획 끝, 해 안쪽에 차가운 눈빛 한 점
            var tipDir = (p3 - Bezier(p0, p1, p2, p3, 0.97f)).normalized;
            var eye = p3 - tipDir * 0.030f + new Vector2(-tipDir.y, tipDir.x) * 0.006f;
            s.Disc(eye.x, eye.y, 0.022f, EyeGlow, d => 0.20f * (1f - d) * (1f - d));
            s.Disc(eye.x, eye.y, 0.0035f, EyeGlow, d => 0.95f * (1f - d * d));
        }

        /// <summary>처마 끝이 들린 기와지붕 한 겹.</summary>
        private static bool InRoof(float X, float Y, float cx, float halfW, float eaveY, float topY)
        {
            float u = (X - cx) / halfW;
            if (u < -1f || u > 1f) return false;
            float au = Mathf.Abs(u);
            float lower = eaveY + (topY - eaveY) * 0.40f * Mathf.Pow(au, 5f);
            float upper = topY - (topY - eaveY) * 0.72f * Mathf.Pow(au, 1.7f);
            return Y >= lower && Y <= upper;
        }

        private static void PaintPalace(Sheet s)
        {
            float A = s.A;
            const float ground = 0.163f;
            const float a = 0.78f;

            // 궁궐 담장: 안개 속으로 양끝이 사라진다
            s.Paint(A * 0.06f, A * 0.78f, ground, ground + 0.010f, Ink, (X, Y) =>
            {
                float u = X / A;
                float fade = Smooth(0.06f, 0.16f, u) * Smooth(0.78f, 0.66f, u);
                float body = Y < ground + 0.007f ? 0.42f : (Y < ground + 0.0095f ? 0.62f : 0f);
                return body * fade * (0.75f + 0.25f * Noise(X * 40f, Y * 10f, 77));
            });

            // 근정전: 월대 + 두 겹 지붕
            float gx = A * 0.335f;
            s.PaintAA(gx - 0.13f, gx + 0.13f, ground, ground + 0.10f, Ink, a, (X, Y) =>
            {
                float dx = Mathf.Abs(X - gx);
                if (Y < ground + 0.012f) return dx < 0.115f - (Y - ground) * 1.2f;
                if (Y < ground + 0.033f) return dx < 0.072f && Mathf.Repeat((X - gx) * 120f, 1f) > 0.25f;
                if (InRoof(X, Y, gx, 0.102f, ground + 0.033f, ground + 0.053f)) return true;
                if (Y < ground + 0.064f) return dx < 0.052f && Y > ground + 0.050f;
                return InRoof(X, Y, gx, 0.082f, ground + 0.064f, ground + 0.090f) || (dx < 0.002f && Y < ground + 0.096f && Y > ground + 0.085f);
            });

            // 광화문 (왼쪽, 조금 작게)
            float mx = A * 0.145f;
            s.PaintAA(mx - 0.06f, mx + 0.06f, ground, ground + 0.06f, Ink, a * 0.92f, (X, Y) =>
            {
                float dx = Mathf.Abs(X - mx);
                if (Y < ground + 0.016f) return dx < 0.046f && !(Mathf.Abs(X - mx) < 0.008f && Y < ground + 0.010f);
                if (InRoof(X, Y, mx, 0.050f, ground + 0.016f, ground + 0.030f)) return true;
                if (Y < ground + 0.036f) return dx < 0.026f && Y > ground + 0.028f;
                return InRoof(X, Y, mx, 0.040f, ground + 0.036f, ground + 0.050f);
            });

            // 경회루 (오른쪽, 기둥이 드러난 누각)
            float lx = A * 0.545f;
            s.PaintAA(lx - 0.08f, lx + 0.08f, ground, ground + 0.06f, Ink, a * 0.85f, (X, Y) =>
            {
                float dx = Mathf.Abs(X - lx);
                if (Y < ground + 0.022f) return dx < 0.058f && (Mathf.Repeat((X - lx) * 90f, 1f) < 0.22f || Mathf.Abs(Y - (ground + 0.014f)) < 0.0025f);
                return InRoof(X, Y, lx, 0.070f, ground + 0.022f, ground + 0.042f);
            });
        }

        private static void PaintIce(Sheet s, Vector2 sun, int seed)
        {
            const float shore = 0.163f;

            s.Paint(0f, s.A, 0f, shore, PaperLight, (X, Y) => 0.35f);
            s.Paint(0f, s.A, 0f, shore, InkWash, (X, Y) =>
            {
                float streak = Mathf.Pow(Noise(X * 1.3f, Y * 46f, seed + 90), 2f);
                return 0.05f + 0.16f * Smooth(shore, 0f, Y) + 0.07f * streak;
            });

            // 궁궐 그림자가 얼음에 길게 비친다
            s.Paint(0f, s.A * 0.80f, shore - 0.05f, shore, Ink, (X, Y) =>
            {
                float k = Smooth(shore - 0.05f, shore, Y);
                return 0.10f * k * Smooth(0.3f, 0.7f, Noise(X * 6f, Y * 120f, seed + 92));
            });

            // 해 그림자: 끊어진 가로 결로 얼음 위에 길게
            s.Paint(sun.x - 0.12f, sun.x + 0.12f, 0f, shore, Sun, (X, Y) =>
            {
                float w = 0.022f + (shore - Y) * 0.18f;
                float core = Mathf.Exp(-Mathf.Pow((X - sun.x) / w, 2f));
                float broken = Smooth(0.45f, 0.75f, Noise(X * 4f, Y * 220f, seed + 93));
                return 0.30f * core * broken * Smooth(0.02f, 0.12f, Y);
            });

            // 얼음 금
            var rng = new System.Random(seed + 5);
            for (int c = 0; c < 7; c++)
            {
                float x = (float)rng.NextDouble() * s.A, y = 0.015f + (float)rng.NextDouble() * 0.12f;
                float ang = ((float)rng.NextDouble() - 0.5f) * 0.6f;
                for (int k = 0; k < 40; k++)
                {
                    s.Disc(x, y, 0.0011f, Ink, d => 0.30f * (1f - d));
                    ang += ((float)rng.NextDouble() - 0.5f) * 0.5f;
                    x += Mathf.Cos(ang) * 0.004f;
                    y += Mathf.Sin(ang) * 0.0012f;
                }
            }
        }

        /// <summary>갓을 쓰고 화승총을 등에 멘 채 환도를 늘어뜨린 무사. 좌표는 키 h 단위, 발밑 0.</summary>
        private static bool InWarrior(float lx, float ly)
        {
            if (ly < 0f || ly > 1.06f) return false;

            if (ly > 0.90f && ly < 1.0f && Mathf.Abs(lx + 0.01f) < 0.075f - (ly - 0.90f) * 0.25f) return true;      // 갓 모자
            float bx = lx / 0.26f, by = (ly - 0.895f) / 0.020f;
            if (bx * bx + by * by < 1f) return true;                                                                  // 갓 양태
            if ((lx * lx + (ly - 0.835f) * (ly - 0.835f)) < 0.055f * 0.055f) return true;                           // 머리

            if (ly > 0.13f && ly < 0.79f)                                                                             // 도포 (바람에 왼쪽으로 날림)
            {
                float t = (ly - 0.13f) / 0.66f;
                float left = Mathf.Lerp(-0.30f, -0.12f, Mathf.Pow(t, 0.8f));
                float right = Mathf.Lerp(0.17f, 0.11f, t);
                if (lx > left && lx < right) return true;
            }
            if (ly < 0.15f && (Mathf.Abs(lx + 0.05f) < 0.026f || Mathf.Abs(lx - 0.07f) < 0.026f)) return true;    // 다리

            if (SegDist(lx, ly, 0.08f, 0.74f, 0.20f, 0.46f) < 0.034f) return true;                                   // 팔
            if (SegDist(lx, ly, 0.20f, 0.46f, 0.50f, 0.03f) < 0.011f) return true;                                   // 환도
            if (SegDist(lx, ly, -0.24f, 1.04f, 0.08f, 0.36f) < 0.017f) return true;                                  // 화승총
            return false;
        }

        private static void PaintWarrior(Sheet s, float x, float baseY, float h, int seed)
        {
            // 얼음 위 그림자
            s.Paint(x - h * 1.6f, x + h * 0.4f, baseY - 0.01f, baseY + 0.004f, Ink, (X, Y) =>
            {
                float dx = (X - (x - h * 0.55f)) / (h * 1.0f), dy = (Y - (baseY - 0.002f)) / 0.0045f;
                float d = dx * dx + dy * dy;
                return d < 1f ? 0.22f * (1f - d) : 0f;
            });

            // 얼음에 비친 모습 (뒤집혀 옅고 결이 끊긴다)
            s.Paint(x - h * 0.5f, x + h * 0.6f, baseY - h * 0.75f, baseY, Ink, (X, Y) =>
            {
                float lx = (X - x) / h, ly = (baseY - Y) / h;
                if (!InWarrior(lx, ly)) return 0f;
                return 0.20f * (1f - ly / 0.75f) * Smooth(0.3f, 0.6f, Noise(X * 20f, Y * 260f, seed + 61));
            });

            s.PaintAA(x - h * 0.5f, x + h * 0.6f, baseY, baseY + h * 1.1f, Ink, 0.96f,
                (X, Y) => InWarrior((X - x) / h, (Y - baseY) / h));
        }

        private static void PaintVignette(Sheet s, int seed)
        {
            for (int y = 0; y < s.H; y++)
            {
                float Y = (y + 0.5f) / s.H;
                for (int x = 0; x < s.W; x++)
                {
                    float X = (x + 0.5f) / s.W;
                    float dx = (X - 0.52f) * 1.15f, dy = (Y - 0.52f) * 1.35f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + (Fbm(X * 4f, Y * 4f, 3, seed + 99) - 0.5f) * 0.14f;
                    s.Blend(y * s.W + x, Ink, 0.42f * Smooth(0.56f, 1.0f, d));
                }
            }
        }

        private static void PaintGrain(Sheet s, int seed)
        {
            for (int i = 0; i < s.Px.Length; i++)
            {
                float g = (Hash(i, 7, seed) - 0.5f) * 0.022f;
                var p = s.Px[i];
                s.Px[i] = new Color(p.r + g, p.g + g, p.b + g);
            }
        }

        // ------------------------------------------------------------------ 움직이는 층

        /// <summary>가로로 이어지는 안개 띠 (흰색 + 알파). RawImage.uvRect 를 흘려 쓴다.</summary>
        public static Texture2D Mist(int width = 1024, int height = 256, int seed = 5)
        {
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                float band = Mathf.Sin(v * Mathf.PI);
                band *= band;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    // 끝과 끝이 이어지도록 두 번 뽑아 섞는다
                    float n0 = Fbm(u * 4f, v * 3f, 5, seed);
                    float n1 = Fbm((u - 1f) * 4f, v * 3f, 5, seed);
                    float n = Mathf.Lerp(n0, n1, u);
                    float a = Smooth(0.38f, 0.78f, n) * band;
                    px[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "NMJ_Mist", wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>눈송이 층 (위아래로 이어짐). 대부분 작고 선명하며, 몇 개는 크고 흐리다.</summary>
        public static Texture2D Snow(int size = 512, int count = 150, int seed = 12)
        {
            var alpha = new float[size * size];
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float cx = (float)rng.NextDouble() * size, cy = (float)rng.NextDouble() * size;
                bool big = rng.NextDouble() < 0.08;
                float r = big ? 4f + (float)rng.NextDouble() * 4f : 0.8f + (float)rng.NextDouble() * 1.6f;
                float peak = big ? 0.28f : 0.75f + (float)rng.NextDouble() * 0.25f;
                int ir = Mathf.CeilToInt(r + 1);
                for (int dy = -ir; dy <= ir; dy++)
                for (int dx = -ir; dx <= ir; dx++)
                {
                    int x = ((int)cx + dx + size) % size, y = ((int)cy + dy + size) % size;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (d > 1f) continue;
                    float a = peak * (big ? (1f - d) * (1f - d) : 1f - d * d);
                    alpha[y * size + x] = Mathf.Max(alpha[y * size + x], a);
                }
            }

            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(alpha[i] * 255f));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "NMJ_Snow", wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ------------------------------------------------------------------ UI 조각

        /// <summary>얇게 그은 붓 선 (밑줄용). 흰색 + 알파.</summary>
        public static Texture2D BrushLine(int width = 1024, int height = 64, int seed = 3)
        {
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float press = Smooth(0f, 0.04f, u) * Mathf.Lerp(1f, 0.25f, Smooth(0.45f, 1f, u));
                    float half = 0.75f * press + (Fbm(u * 18f, v, 2, seed + 1) - 0.5f) * 0.12f;
                    float a = 1f - Smooth(half - 0.08f, half, Mathf.Abs(v));
                    float bristle = Noise(u * 3f, v * 14f, seed + 2);
                    a *= 1f - Smooth(0.7f, 1f, u) * Smooth(0.45f, 0.7f, bristle);
                    px[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "NMJ_BrushLine", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>둥글게 번지는 먹물 자국. 가장자리가 들쭉날쭉하고 작은 먹 방울이 튄다.</summary>
        public static Texture2D InkBlot(int size = 512, int seed = 9)
        {
            var px = new Color32[size * size];
            var rng = new System.Random(seed);
            var drops = new Vector3[26];
            for (int i = 0; i < drops.Length; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = 0.36f + (float)rng.NextDouble() * 0.12f;
                drops[i] = new Vector3(0.5f + Mathf.Cos(ang) * dist, 0.5f + Mathf.Sin(ang) * dist, 0.006f + (float)rng.NextDouble() * 0.02f);
            }

            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float dx = u - 0.5f, dy = v - 0.5f;
                    float ang = Mathf.Atan2(dy, dx);
                    float edge = 0.33f + 0.07f * Fbm(Mathf.Cos(ang) * 2f + 5f, Mathf.Sin(ang) * 2f + 5f, 4, seed)
                                 + 0.025f * Noise(ang * 12f + 40f, 0f, seed + 1);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 1f - Smooth(edge - 0.012f, edge, d);
                    foreach (var dr in drops)
                    {
                        float dd = Mathf.Sqrt((u - dr.x) * (u - dr.x) + (v - dr.y) * (v - dr.y));
                        a = Mathf.Max(a, 1f - Smooth(dr.z * 0.7f, dr.z, dd));
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "NMJ_InkBlot", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>낙관(붉은 도장) 바탕. 모서리가 닳은 사각형.</summary>
        public static Texture2D Seal(int width = 96, int height = 144, int seed = 4)
        {
            var px = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float box = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                    float wear = (Fbm(u * 8f, v * 12f, 3, seed) - 0.5f) * 0.05f;
                    float a = 1f - Smooth(0.455f + wear, 0.47f + wear, box);
                    a *= 0.78f + 0.22f * Noise(u * 30f, v * 45f, seed + 1);
                    px[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "NMJ_Seal", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        public static Sprite ToSprite(Texture2D tex)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
