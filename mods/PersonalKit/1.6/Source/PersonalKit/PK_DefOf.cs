using RimWorld;
using Verse;

namespace PersonalKit
{
    [DefOf]
    public static class PK_DefOf
    {
        static PK_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PK_DefOf));
        }
    }
}
