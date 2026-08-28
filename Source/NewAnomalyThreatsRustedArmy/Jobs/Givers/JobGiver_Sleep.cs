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
	public class JobGiver_Sleep : ThinkNode_JobGiver
	{
		public float wantedLevel = 0f;

		public float startFromLevel = 0f;

		public bool forceIfExhausted = false;
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn is RustedPawn rust && rust.restNeed != null)
			{
				if (ShouldKeep(rust))
				{
					if (pawn.CurJob?.def == JobDefOf.Wait_AsleepDormancy)
					{
						return pawn.CurJob;
					}
					if (forceIfExhausted)
					{
						return RestJob(rust);
					}
				}
				if (rust.restNeed.CurLevel < startFromLevel)
				{
					return RestJob(rust);
				}
			}
			return null;
		}
		private bool ShouldKeep(RustedPawn p)
		{
			if (forceIfExhausted && p.restNeed.exhausted)
			{
				return true;
			}
			if (p.restNeed.CurLevel < wantedLevel)
			{
				return true;
			}
			return false;
		}
		public static Job RestJob(Pawn p, bool forced = false)
		{
			p.TryGetComp<CompCanBeDormant>(out var comp);
			comp.wokeUpTick = int.MinValue;
			if (p is IAttackTarget t)
			{
				p.Map.attackTargetsCache.UpdateTarget(t);
			}
			if (p.Drafted)
			{
				p.drafter.Drafted = false;
			}
			IntVec3 cell = p.Position;
			if (!RCellFinder.TryFindRandomCellNearWith(cell, (IntVec3 c) => CanSleep(c, p, p.Map), p.Map, out cell, 5, 20))
			{
				if (!RCellFinder.TryFindRandomCellNearWith(cell, (IntVec3 c) => CanSleep(c, p, p.Map), p.Map, out cell, 5, 20))
				{
					cell = p.Position;
				}
			}
			Job job = JobMaker.MakeJob(JobDefOf.Wait_AsleepDormancy, cell);
			job.forceSleep = true;
			if (forced)
			{
				job.startInvoluntarySleep = true;
			}
			return job;
		}

		private static bool CanSleep(IntVec3 c, Pawn pawn, Map map, bool allowForbidden = false)
		{
			if (!c.InBounds(map))
			{
				return false;
			}
			if (!pawn.CanReserve(c))
			{
				return false;
			}
			if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Some))
			{
				return false;
			}
			if (!c.Standable(map))
			{
				return false;
			}
			if (c.GetTerrain(map).dangerous)
			{
				return false;
			}
			if (!allowForbidden && c.IsForbidden(pawn))
			{
				return false;
			}
			if (c.GetFirstBuilding(map) != null)
			{
				return false;
			}
			for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
			{
				IntVec3 c2 = c + GenAdj.CardinalDirections[i];
				if (!c2.InBounds(map))
				{
					continue;
				}
				List<Thing> thingList = c2.GetThingList(map);
				for (int j = 0; j < thingList.Count; j++)
				{
					if (thingList[j].def.hasInteractionCell && thingList[j].InteractionCell == c)
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}
