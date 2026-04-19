using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using Ionic.Crc;
using Ionic.Zlib;
using JetBrains.Annotations;
using KTrie;
using LudeonTK;
using NVorbis.NAudioSupport;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using RuntimeAudioClipLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using static NAT.IncidentWorker_RustedArmySiege;

namespace NAT
{
	public class Building_RustedTurret : Building_TurretGun
	{
		private CompRustedTurretPawn comp;

		public CompRustedTurretPawn Comp
		{
			get
			{
				if (comp == null)
				{
					comp = this.GetComp<CompRustedTurretPawn>();
				}
				return comp;
			}
		}

		public bool deadlifeAllowed = true;

		protected override bool CanSetForcedTarget => base.Faction == Faction.OfPlayer;

		public void SetTarget(LocalTargetInfo targ, bool forced = false)
		{
			if (forced)
			{
				forcedTarget = targ;
			}
			else
			{
				currentTargetInt = targ;
			}
		}

		public override LocalTargetInfo TryFindNewTarget()
		{
			IAttackTargetSearcher attackTargetSearcher = TargSearcher();
			Faction faction = attackTargetSearcher.Thing.Faction;
			float range = AttackVerb.verbProps.range;
			TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
			if (!AttackVerb.ProjectileFliesOverhead())
			{
				targetScanFlags |= TargetScanFlags.NeedLOSToAll;
			}
			if (AttackVerb.IsIncendiary_Ranged())
			{
				targetScanFlags |= TargetScanFlags.NeedNonBurning;
			}
			return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(attackTargetSearcher, targetScanFlags, IsValidTarget);
		}

		private IAttackTargetSearcher TargSearcher()
		{
			if (mannableComp != null && mannableComp.MannedNow)
			{
				return mannableComp.ManningPawn;
			}
			return this;
		}

		private bool IsValidTarget(Thing t)
		{
			if (t is Pawn pawn)
			{
				if (base.Faction == Faction.OfPlayer && pawn.IsPrisoner)
				{
					return false;
				}
				if (AttackVerb.ProjectileFliesOverhead())
				{
					RoofDef roofDef = base.Map.roofGrid.RoofAt(t.Position);
					if (roofDef != null && roofDef.isThickRoof)
					{
						return false;
					}
				}
				if (mannableComp == null)
				{
					return !GenAI.MachinesLike(base.Faction, pawn);
				}
				if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
				{
					return false;
				}
			}
			return true;
		}

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
			if (dinfo.Def.defName == "Nerve")
			{
				DamageInfo dinfo2 = dinfo;
				dinfo2.Def = DamageDefOf.NerveStun;
				dinfo2.SetAmount(4f);
				TakeDamage(dinfo2);
			}
		}

		protected override void ReceiveCompSignal(string signal)
		{
			base.ReceiveCompSignal(signal);
			if(signal == "AffectedByFlare")
			{
				GetComp<CompStunnable>()?.StunHandler?.StunFor(180, null);
			}
		}

        public override IEnumerable<Gizmo> GetGizmos()
        {
			foreach(Gizmo g in base.GetGizmos())
            {
				yield return g;
            }
			/*yield return new Command_Action
			{
				defaultLabel = "DEV: + 0.05",
				action = delegate
				{
					def.building.turretTopOffset.y += 0.05f;
				}
			};
			yield return new Command_Action
			{
				defaultLabel = "DEV: - 0.05",
				action = delegate
				{
					def.building.turretTopOffset.y -= 0.05f;
				}
			};*/
		}
    }

	public class Building_RustedTurretFoam : Building_RustedTurret
	{
		public override bool IsEverThreat => false;

		public override LocalTargetInfo TryFindNewTarget()
		{
			int num = GenRadial.NumCellsInRadius(AttackVerb.EffectiveRange);
			for (int i = 0; i < num; i++)
			{
				IntVec3 intVec = base.Position + GenRadial.RadialPattern[i];
				if (!GenSight.LineOfSight(base.Position, intVec, base.Map, skipFirstCell: true))
				{
					continue;
				}
				List<Thing> thingList = intVec.GetThingList(base.Map);
				for (int j = 0; j < thingList.Count; j++)
				{
					if (thingList[j] is Fire || thingList[j].HasAttachment(ThingDefOf.Fire))
					{
						return thingList[j].Position;
					}
				}
			}
			return LocalTargetInfo.Invalid;
		}
	}
}