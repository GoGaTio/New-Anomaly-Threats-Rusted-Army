using DelaunatorSharp;
using Gilzoide.ManagedJobs;
using HarmonyLib;
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
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
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
using static UnityEngine.GraphicsBuffer;

namespace NAT
{
	public class CompProperties_RustedSidearm : CompProperties
	{
		public CompProperties_RustedSidearm()
		{
			compClass = typeof(CompRustedSidearm);
		}
	}

	public class CompRustedSidearm : ThingComp
	{
		private Pawn holder;

		private int cooldown = -1;

		public override void CompTick()
		{
			base.CompTick();
			if(cooldown > 0)
			{
				cooldown--;
				return;
			}
			if (!parent.IsHashIntervalTick(60))
			{
				return;
			}
			if (parent.ParentHolder is Pawn_InventoryTracker inventory)
			{
				holder = inventory.pawn;
			}
			else
			{
				return;
			}
			if (!holder.Spawned || holder.stances == null || holder.mindState?.enemyTarget == null || holder.Faction == Faction.OfPlayerSilentFail || holder.stances.curStance is Stance_Warmup || holder.mindState.enemyTarget.Position.DistanceTo(holder.Position) > parent.def.Verbs[0].range + 5f || holder.equipment.Primary.HasComp<CompRustedSidearm>() || !CanChangeWeapon(holder))
			{
				return;
			}
			ThingWithComps primary = holder.equipment.Primary;
			if (primary != null)
			{
				holder.equipment.Remove(primary);
				//holder.stances.CancelBusyStanceSoft();
				primary.Notify_Unequipped(holder);
				holder.inventory.TryAddAndUnforbid(primary);
			}
			ThingWithComps secondary;
			cooldown = 180;
			if (parent.stackCount > 1)
			{
				secondary = parent.SplitOff(1) as ThingWithComps;
			}
			else
			{
				secondary = parent;
				holder.inventory.innerContainer.Remove(parent);
			}
			holder.equipment.AddEquipment(secondary);
			//holder.jobs.EndCurrentJob(JobCondition.InterruptForced);
			holder = null;
		}

		public override void PostSplitOff(Thing piece)
		{
			base.PostSplitOff(piece);
			piece.TryGetComp<CompRustedSidearm>().cooldown = cooldown;
		}

		public override void Notify_UsedWeapon(Pawn pawn)
		{
			if (parent.Destroyed)
			{
				ThingWithComps primary = pawn?.inventory?.innerContainer?.FirstOrDefault(x => x.def.IsWeapon && !x.HasComp<CompRustedSidearm>()) as ThingWithComps;
				if (primary != null && pawn.equipment != null)
				{
					foreach (Thing t in pawn.inventory.innerContainer)
					{
						if (t.TryGetComp<CompRustedSidearm>(out var comp))
						{
							comp.cooldown = 210;
						}
					}
					pawn.inventory.innerContainer.Remove(primary);
					pawn.equipment.AddEquipment(primary);
				}
			}
			base.Notify_UsedWeapon(pawn);
		}

		public static bool CanChangeWeapon(Pawn pawn)
		{
			if(pawn.VerbTracker == null)
			{
				return false;
			}
			if (pawn.CurJob?.verbToUse?.Bursting == true)
			{
				return false;
			}
			foreach(Verb verb in pawn.VerbTracker.AllVerbs)
			{
				if(verb.Bursting == true)
				{
					return false;
				}
			}
			foreach (Verb verb in pawn.equipment.AllEquipmentVerbs)
			{
				if (verb.Bursting == true)
				{
					return false;
				}
			}
			return true;
		}
	}
}