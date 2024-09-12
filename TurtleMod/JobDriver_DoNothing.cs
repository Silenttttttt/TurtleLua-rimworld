using Verse;
using Verse.AI;
using System.Collections.Generic; // Importing System.Collections.Generic for IEnumerable

namespace TurtleMod
{
    public class JobGiver_DoNothing : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            // Return null or a dummy job that makes the pawn do nothing
            return null;
        }
    }
}
