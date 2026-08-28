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
	public class JobGiver_RustedTurret : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.CurJobDef == NATRADefOf.NAT_RustedTurretSetUp)
			{
				return pawn.CurJob;
			}
			int ticksGame = Find.TickManager.TicksGame;
			if (ticksGame - pawn.spawnedTick < 600 || ticksGame - pawn.mindState.lastHarmTick < 1200)
			{
				return null;
			}
			if (!pawn.TryGetComp(out CompRustedTurretPawn comp) || !pawn.TryGetComp(out CompTurret turret) || turret.currentTarget != null)
			{
				return null;
			}
			if (pawn.mindState.duty?.focus.IsValid == true && pawn.mindState.duty.focus.Cell.DistanceTo(pawn.Position) > (pawn.mindState.duty.wanderRadius ?? pawn.mindState.duty.radius))
			{
				return null;
			}
			string name = pawn.lord?.CurLordToil?.GetType().Name;
			if (name != null && (name.Contains("ExitMap") || name.Contains("Flee")))
			{
				return null;
			}
			if (!CellRectOccupied(new CellRect(pawn.Position.x, pawn.Position.z, comp.Props.buildingDef.Size.x, comp.Props.buildingDef.Size.z), pawn.Map))
			{
				Job job = JobMaker.MakeJob(NATRADefOf.NAT_RustedTurretSetUp, pawn.Position);
				return job;
			}
			if (RCellFinder.TryFindRandomCellNearWith(pawn.Position, (c) => !CellRectOccupied(new CellRect(c.x, c.z, comp.Props.buildingDef.Size.x, comp.Props.buildingDef.Size.z), pawn.Map), pawn.Map, out var cell, 2, 10))
			{
				Job job = JobMaker.MakeJob(NATRADefOf.NAT_RustedTurretSetUp, cell);
				return job;
			}
			return null;
		}

		public bool CellRectOccupied(CellRect rect, Map map)
		{
			if (!rect.InBounds(map))
			{
				return true;
			}
			foreach (IntVec3 c in rect)
			{
				if (!c.Standable(map) || !c.GetAffordances(map).Contains(TerrainAffordanceDefOf.Light))
				{
					return true;
				}
				foreach (Thing t in c.GetThingList(map))
				{
					if (t.def.IsEdifice() || t.def.Fillage != FillCategory.None)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
