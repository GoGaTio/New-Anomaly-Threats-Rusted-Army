using NAT;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace NAT
{
	public class HediffCompProperties_RustedBuffGiver : HediffCompProperties
	{
		public float range;

		public HediffDef hediff;

		public HediffCompProperties_RustedBuffGiver()
		{
			compClass = typeof(HediffComp_GiveHediffsInRange);
		}
	}

	public class HediffComp_GiveHediffsToRustInRange : HediffComp
	{
		public HediffCompProperties_RustedBuffGiver Props => (HediffCompProperties_RustedBuffGiver)props;

		public override void CompPostTick(ref float severityAdjustment)
		{
			if (!parent.pawn.IsHashIntervalTick(10))
			{
				return;
			}
			if (!parent.pawn.Awake() || parent.pawn.health == null || parent.pawn.Downed || !parent.pawn.Spawned)
			{
				return;
			}
			IReadOnlyList<Pawn> readOnlyList = ((parent.pawn.Faction == null) ? parent.pawn.Map.mapPawns.AllPawnsSpawned : parent.pawn.Map.mapPawns.SpawnedPawnsInFaction(parent.pawn.Faction));
			foreach (Pawn item in readOnlyList)
			{
				if(item is RustedPawn rust && !item.Dead && item.health != null && item != parent.pawn && rust.Comp.Props.buffable && item.Position.DistanceTo(parent.pawn.Position) <= Props.range)
				{
					Hediff hediff = item.health.GetOrAddHediff(Props.hediff);
					HediffComp_RustedBuff buff = hediff.TryGetComp<HediffComp_RustedBuff>();
					if (buff != null)
					{
						buff.AffectTick(parent.pawn);
					}
				}
			}
		}
	}
}
