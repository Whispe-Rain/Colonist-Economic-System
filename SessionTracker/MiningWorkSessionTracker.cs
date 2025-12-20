using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace EconomicSystem
{
    public static class MiningWorkSessionTracker
    {
        private static readonly Dictionary<Pawn, int> startHP
            = new Dictionary<Pawn, int>();

        public static void Begin(Pawn pawn, Mineable mineable)
        {
            if (pawn == null || mineable == null)
                return;

            startHP[pawn] = mineable.HitPoints;
        }

        public static float End(Pawn pawn, Mineable mineable)
        {
            if (pawn == null || mineable == null)
                return 0f;

            if (!startHP.TryGetValue(pawn, out int beginHP))
                return 0f;

            int endHP = mineable.HitPoints;

            int workDone = Mathf.Max(0, beginHP - endHP);

            startHP.Remove(pawn);

            return workDone;
        }
    }

}