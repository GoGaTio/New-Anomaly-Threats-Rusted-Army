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
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
using static RimWorld.FleshTypeDef;

namespace NAT
{
	public class CompProperties_AbilityArtOfWar : CompProperties_AbilityEffect
	{
		public CompProperties_AbilityArtOfWar()
		{
			compClass = typeof(CompAbilityEffect_ArtOfWar);
		}
	}
	public class CompAbilityEffect_ArtOfWar : CompAbilityEffect
	{
		public class ArtOfWarTarget : IExposable
		{
			public Pawn pawn;

			public IntVec3 destination;

			public void Apply(Map map)
			{
				if (pawn.Spawned)
				{
					if (pawn.carryTracker.CarriedThing != null && !pawn.Drafted)
					{
						pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out var _);
					}
					if (pawn.drafter != null)
					{
						pawn.wasDraftedBeforeSkip = pawn.drafter.Drafted;
					}
					EffecterDefOf.Skip_EntryNoDelay.Spawn(pawn, pawn.MapHeld).Cleanup();
					pawn.DeSpawnOrDeselect();
				}
				GenSpawn.Spawn(pawn, destination, map, pawn.def.defaultPlacingRot);
				EffecterDefOf.Skip_ExitNoDelay.Spawn(destination, map).Cleanup();
				if (pawn.TryGetFormingCaravanLord(out var lord) && lord.Map != pawn.Map)
				{
					CaravanFormingUtility.RemovePawnFromCaravan(pawn, pawn.GetLord(), removeFromDowned: false);
				}
				if (pawn.drafter != null && pawn.wasDraftedBeforeSkip)
				{
					pawn.drafter.Drafted = true;
				}
				pawn.Notify_Teleported();
			}

			public void ExposeData()
			{
				Scribe_References.Look(ref pawn, "pawn");
				Scribe_Values.Look(ref destination, "destination");
			}
		}
		public new CompProperties_AbilityArtOfWar Props => (CompProperties_AbilityArtOfWar)props;

		public List<ArtOfWarTarget> targets = new List<ArtOfWarTarget>();

		public List<Pawn> potentialTargets = new List<Pawn>();

		private List<IntVec3> targetCells = new List<IntVec3>();

		private List<Mote> warmupMotes = new List<Mote>();

		public bool searchingTargets = false;

		public int getTargetInterval = 0;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			searchingTargets = false;
			Map map = parent.pawn.MapHeld;
			foreach(Mote mote in warmupMotes.ToList())
			{
				mote.Destroy();
			}
			warmupMotes.Clear();
			warmupMotes = null;
			foreach (ArtOfWarTarget targ in targets)
			{
				targ.Apply(map);
			}
			targets.Clear();
			potentialTargets.Clear();
			targetCells.Clear();
		}

		public override void CompTick()
		{
			base.CompTick();
			if (!parent.verb.WarmingUp)
			{
				if(warmupMotes != null)
				{
					foreach (Mote mote in warmupMotes.ToList())
					{
						mote.Destroy();
					}
					warmupMotes.Clear();
					warmupMotes = null;
				}
				return;
			}
			if (!searchingTargets)
			{
				searchingTargets = true;
				targets.Clear();
				targetCells.Clear();
				warmupMotes = new List<Mote>();
				potentialTargets = new List<Pawn>();
				potentialTargets.AddRange(parent.pawn.Map.mapPawns.SpawnedPawnsInFaction(parent.pawn.Faction).Where((pawn) => !pawn.kindDef.hostileToAll && pawn.Position.DistanceTo(parent.pawn.Position) <= 65f && !pawn.ThreatDisabled(null)));
				potentialTargets.Remove(parent.pawn);
				getTargetInterval = Mathf.Max((parent.verb.WarmupTime / (float)potentialTargets.Count).SecondsToTicks(), 1);
			}
			if (parent.pawn.IsHashIntervalTick(getTargetInterval))
			{
				ArtOfWarTarget newTarg = GetNewTarget();
				if(newTarg != null)
				{
					targets.Add(newTarg);
				}
			}
			if(warmupMotes == null)
			{
				warmupMotes = new List<Mote>();
				foreach (ArtOfWarTarget targ in targets)
				{
					if (targ.pawn.Destroyed)
					{
						continue;
					}
					warmupMotes.Add(MoteMaker.MakeAttachedOverlay(targ.pawn, NATRADefOf.NAT_Mote_ArtOfWarPreCast, Vector3.zero, targ.pawn.ageTracker.CurKindLifeStage.bodyGraphicData.Graphic.drawSize.x));
				}
			}
			foreach (Mote warmupMote in warmupMotes)
			{
				warmupMote.Maintain();
			}
		}

		public ArtOfWarTarget GetNewTarget()
		{
			if(potentialTargets.TryRandomElement((Pawn p) => p != null && !p.Destroyed && !p.DeadOrDowned && p.Map == parent.pawn.Map, out var targ))
			{
				Map map = parent.pawn.Map;
				bool ranged = !targ.CurrentEffectiveVerb?.IsMeleeAttack ?? false;
				IntVec3 dest = IntVec3.Invalid;
				potentialTargets.Remove(targ);
				List<Thing> list = map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget).Where(ValidateEnemy).ToList();
				if (list.NullOrEmpty())
				{
					return null;
				}
				List<IntVec3> cells = new List<IntVec3>();
				int expBy = ranged ? 5 : 1;
				foreach (Thing item in list)
				{
					foreach(IntVec3 c in item.OccupiedRect().ExpandedBy(expBy).EdgeCells)
					{
						if (!targetCells.Contains(c) && c.InBounds(map) && !c.Impassable(map))
						{
							cells.Add(c);
						}
					}
				}
				if (cells.NullOrEmpty())
				{
					return null;
				}
				dest = cells.RandomElement();
				targetCells.Add(dest);
				warmupMotes.Add(MoteMaker.MakeAttachedOverlay(targ, NATRADefOf.NAT_Mote_ArtOfWarPreCast, Vector3.zero, targ.ageTracker?.CurKindLifeStage?.bodyGraphicData?.Graphic?.drawSize.x ?? 2f));
				return new ArtOfWarTarget() { pawn = targ, destination = dest };
				bool ValidateEnemy(Thing t)
				{
					float distance = t.Position.DistanceTo(targ.Position);
					if(distance < 10f || distance > 65f)
					{
						return false;
					}
					if (!t.HostileTo(targ))
					{
						return false;
					}
					if((t as IAttackTarget).ThreatDisabled(null))
					{
						return false;
					}
					return true;
				}
			}
			return null;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			string key = "ArtOfWar_";
			Scribe_Collections.Look(ref targets, key + "targets", LookMode.Deep);
			Scribe_Values.Look(ref searchingTargets, key + "searchingTargets");
			Scribe_Values.Look(ref getTargetInterval, key + "getTargetInterval");
			if(Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				targetCells = new List<IntVec3>();
				foreach(var t in targets)
				{
					targetCells.Add(t.destination);
				}
			}
		}
	}
}