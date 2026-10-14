using System;
using System.Reflection;
using UnityEngine;

namespace NMJ
{
    /// <summary>
    /// BHS 보스 스크립트(BHS.BossHealth / BHS.BossWallHealth)를 컴파일 시점 참조 없이 다루는 도우미.
    /// BHS 코드가 없는 브랜치에서도 NMJ UI 가 컴파일되고, 있으면 이름으로 찾아 연결한다.
    /// </summary>
    public static class BhsBossLink
    {
        public static readonly Type BossType = FindType("BHS.BossHealth");
        public static readonly Type WallType = FindType("BHS.BossWallHealth");

        private static readonly FieldInfo BossMaxHp = BossType?.GetField("maxHp");
        private static readonly PropertyInfo BossCurrentHp = BossType?.GetProperty("CurrentHp");
        private static readonly FieldInfo WallMaxHp = WallType?.GetField("maxHp");
        private static readonly FieldInfo WallCurrentHp = WallType?.GetField("currentHp");
        private static readonly MethodInfo WallTakeDamage = WallType?.GetMethod("TakeDamage");

        public static Component FindBoss()
        {
            if (BossType == null) return null;
            return UnityEngine.Object.FindAnyObjectByType(BossType, FindObjectsInactive.Include) as Component;
        }

        public static Component[] FindWalls(Component boss)
        {
            if (boss == null || WallType == null) return Array.Empty<Component>();
            return boss.GetComponentsInChildren(WallType, true);
        }

        public static bool TryReadBoss(Component boss, out float current, out float max)
        {
            current = max = 0f;
            if (boss == null || BossCurrentHp == null || BossMaxHp == null) return false;
            current = (float)BossCurrentHp.GetValue(boss);
            max = (float)BossMaxHp.GetValue(boss);
            return true;
        }

        /// <summary>다리 체력 비율 0~1. 부서졌거나 꺼졌으면 0.</summary>
        public static float WallRatio(Component wall)
        {
            if (wall == null || WallCurrentHp == null || WallMaxHp == null) return 0f;
            if (!wall.gameObject.activeSelf) return 0f;

            float cur = (float)WallCurrentHp.GetValue(wall);
            float max = (float)WallMaxHp.GetValue(wall);
            return cur <= 0f ? 0f : Mathf.Clamp01(cur / Mathf.Max(1f, max));
        }

        public static void BreakWall(Component wall, Vector3 from)
        {
            if (wall == null || WallTakeDamage == null || WallMaxHp == null) return;
            WallTakeDamage.Invoke(wall, new object[] { (float)WallMaxHp.GetValue(wall), from });
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }

            return null;
        }
    }
}
