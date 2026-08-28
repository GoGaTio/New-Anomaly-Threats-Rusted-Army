using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace NAT
{
	public class JobGiver_DanceRust : ThinkNode_JobGiver
	{
		public IntRange ticksRange = new IntRange(300, 600);

		public override ThinkNode DeepCopy(bool resolve = true)
		{
			JobGiver_DanceRust obj = (JobGiver_DanceRust)base.DeepCopy(resolve);
			obj.ticksRange = ticksRange;
			return obj;
		}

		protected override Job TryGiveJob(Pawn pawn)
		{
			Job job = JobMaker.MakeJob(NATRADefOf.NAT_DanceRust);
			job.expiryInterval = ticksRange.RandomInRange;
			return job;
		}
	}
}
