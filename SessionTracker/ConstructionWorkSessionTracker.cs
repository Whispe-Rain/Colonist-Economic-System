using System.Collections.Generic;
using Verse;

namespace EconomicSystem
{
    public static class ConstructionWorkSessionTracker
    {
        private static readonly Dictionary<(Pawn, Thing), float> startWorkLeft
            = new();

        public static void Start(Pawn pawn, Thing target, float workLeft)
        {
            startWorkLeft[(pawn, target)] = workLeft;
        }

        public static float End(Pawn pawn, Thing target, float currentWorkLeft)
        {
            if (!startWorkLeft.TryGetValue((pawn, target), out float start))
                return 0f;

            startWorkLeft.Remove((pawn, target));

            float done = start - currentWorkLeft;
            return done > 0f ? done : 0f;
        }
    }
}