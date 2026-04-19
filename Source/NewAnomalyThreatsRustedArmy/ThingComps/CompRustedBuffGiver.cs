using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace NAT
{
	public class CompProperties_RustedBuffGiver : CompProperties
	{
		public float range;

		public HediffDef hediff;

		public bool onlyEquippedUse = false;

		public CompProperties_RustedBuffGiver()
		{
			compClass = typeof(CompRustedBuffGiver);
		}
	}
	public class CompRustedBuffGiver : ThingComp
	{
		public CompProperties_RustedBuffGiver Props => (CompProperties_RustedBuffGiver)props;

		public Thing Giver
		{
			get
			{
				if(Props.onlyEquippedUse)
				{
					if (parent.ParentHolder is Pawn_EquipmentTracker equipment)
					{
						return equipment.pawn;
					}
					return null;
				}
				return parent;
			}
		}

		public bool Active
		{
			get
			{
				Thing giver = Giver;
				if(giver == null)
				{
					return false;
				}
				if (!giver.Spawned)
				{
					return false;
				}
				if(giver is Pawn pawn)
				{
					if (!pawn.Awake() || pawn.health == null || pawn.Downed)
					{
						return false;
					}
					return true;
				}
				return true;
			}
		}

		public override void CompTick()
		{
			if (!parent.IsHashIntervalTick(10))
			{
				return;
			}
			if (!Active)
			{
				return;
			}
			Thing giver = Giver;
			IReadOnlyList<Pawn> readOnlyList = ((giver.Faction == null) ? giver.Map.mapPawns.AllPawnsSpawned : giver.Map.mapPawns.SpawnedPawnsInFaction(giver.Faction));
			foreach (Pawn item in readOnlyList)
			{
				if (item is RustedPawn rust && !item.Dead && item.health != null && item != giver && rust.Comp.Props.buffable && item.Position.DistanceTo(giver.Position) <= Props.range)
				{
					Hediff hediff = item.health.GetOrAddHediff(Props.hediff);
					HediffComp_RustedBuff buff = hediff.TryGetComp<HediffComp_RustedBuff>();
					if (buff != null)
					{
						buff.AffectTick(giver);
					}
				}
			}
		}
	}
}
