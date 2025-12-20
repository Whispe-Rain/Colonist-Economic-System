using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace EconomicSystem
{
    public static class ResearchWorkSessionTracker
    {
        private static readonly Dictionary<Pawn, float> startProgress
            = new Dictionary<Pawn, float>();

        public static void Begin(Pawn pawn)
        {
            if (pawn == null)
                return;

            ResearchProjectDef project =
                Find.ResearchManager?.GetProject();

            if (project == null)
                return;

            float progress =
                Find.ResearchManager.GetProgress(project);

            startProgress[pawn] = progress;
        }

        public static float End(Pawn pawn)
        {
            if (pawn == null)
                return 0f;

            if (!startProgress.TryGetValue(pawn, out float begin))
                return 0f;

            ResearchProjectDef project =
                Find.ResearchManager?.GetProject();

            if (project == null)
                return 0f;

            float end =
                Find.ResearchManager.GetProgress(project);

            startProgress.Remove(pawn);

            return Mathf.Max(0f, end - begin);
        }
    }

}