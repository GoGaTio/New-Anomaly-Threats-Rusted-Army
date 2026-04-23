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
using static HarmonyLib.Code;

namespace NAT
{
	public class JobGiver_Sleep : ThinkNode_JobGiver
	{
		public float wantedLevel = 0f;

		public float startFromLevel = 0f;

		public bool forceIfExhausted = false;
		protected override Job TryGiveJob(Pawn pawn)
		{
			if(pawn is RustedPawn rust && rust.restNeed != null)
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
            if(p.restNeed.CurLevel < wantedLevel)
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

	public class JobGiver_RustedTurret : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			/*Lord lord = pawn.GetLord();
			if(lord.CurLordToil.GetType().Name.Contains("Assault"))
			{
				
			}*/
			if(pawn.CurJobDef == NATRADefOf.NAT_RustedTurretSetUp)
			{
				return pawn.CurJob;
			}
			if(!pawn.TryGetComp(out CompRustedTurretPawn comp) || !pawn.TryGetComp(out CompRustedTurret turret) || turret.currentTarget == null || !turret.ShouldKeepTarget)
			{
				return null;
			}
			if(RCellFinder.TryFindRandomCellNearWith(pawn.Position, (c) => !CellRectOccupied(new CellRect(c.x, c.z, comp.Props.buildingDef.Size.x, comp.Props.buildingDef.Size.z), pawn.Map), pawn.Map, out var cell, 0, 5))
			{
				Job job = JobMaker.MakeJob(NATRADefOf.NAT_RustedTurretSetUp, cell);
				return job;
			}
			return null;
		}

		public bool CellRectOccupied(CellRect rect, Map map)
		{
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

	public class ThinkNode_ConditionalReinforcement : ThinkNode_Conditional
	{
        protected override bool Satisfied(Pawn pawn)
        {
			if(pawn is RustedPawn rust && !rust.Controllable)
            {
				return true;
            }
			return false;
        }
    }

	/*public class JobGiver_CarryShells : ThinkNode_JobGiver
	{
		public float maxDistFromPoint = -1f;

		public override ThinkNode DeepCopy(bool resolve = true)
		{
			JobGiver_CarryShells obj = (JobGiver_CarryShells)base.DeepCopy(resolve);
			obj.maxDistFromPoint = maxDistFromPoint;
			return obj;
		}

		protected override Job TryGiveJob(Pawn pawn)
		{
			Thing thing = GenClosest.ClosestThingReachable(GetRoot(pawn), pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.Touch, TraverseParms.For(pawn), maxDistFromPoint, Validator);
			if (thing != null)
			{
				Job job = JobMaker.MakeJob(NATRADefOf.NAT_CarryShell, thing);
				return job;
			}
			return null;
			bool Validator(Thing t)
			{
				if (!(t is Building_TurretGun turret))
				{
					return false;
				}
				if (!turret.gun.HasComp<CompChangeableProjectile>())
				{
					return false;
				}
				if (!pawn.CanReserve(t))
				{
					return false;
				}
				if (JobDriver_CarryShell.FindAmmoForTurret(pawn, turret) == null)
				{
					return false;
				}
				return true;
			}
		}

		protected IntVec3 GetRoot(Pawn pawn) => pawn.GetLord()?.CurLordToil.FlagLoc ?? pawn.Position;
	}*/

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

	public class JobGiver_TakeWoundedRust : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			Pawn pawn2 = ReachableWounded(pawn);
			if (pawn2 == null)
			{
				return null;
			}
			Job job = JobMaker.MakeJob(JobDefOf.CarryDownedPawnDrafted);
			job.targetA = pawn2;
			job.count = 1;
			return job;
		}

		public static Pawn ReachableWounded(Pawn searcher)
		{
			List<Pawn> list = searcher.Map.mapPawns.SpawnedPawnsInFaction(searcher.Faction);
			for (int i = 0; i < list.Count; i++)
			{
				Pawn pawn = list[i];
				if (pawn.Downed && searcher.CanReserveAndReach(pawn, PathEndMode.OnCell, Danger.Deadly))
				{
					return pawn;
				}
			}
			return null;
		}
	}
}