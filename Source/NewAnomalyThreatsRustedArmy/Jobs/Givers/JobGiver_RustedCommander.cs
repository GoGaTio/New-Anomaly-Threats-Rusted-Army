using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace NAT
{
	public class JobGiver_RustedCommander : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.CurJob?.ability?.def != null)
			{
				return null;
			}
			if (pawn.Faction?.IsPlayer == false && pawn is RustedPawn rust && rust.Awake() && rust.Commander is CompRustedCommander comp && comp.units > 0)
			{
				LocalTargetInfo target = comp.TryCallSupport(out var ability);
				if (!target.IsValid)
				{
					return null;
				}
				return ability.GetJob(target, target);
			}
			return null;
		}
	}
}
