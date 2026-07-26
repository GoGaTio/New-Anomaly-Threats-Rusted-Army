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
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace NAT.Rusts
{
	[HarmonyPatch]
	public static class DestroyingROMShittyPatch
	{
		public static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("TorannMagic.TorannMagicMod");
			if (type == null)
			{
				return null;
			}
			MethodInfo method = AccessTools.Method(AccessTools.Inner(type, "Pawn_SkillTracker_Base_Patch"), "Prefix");
			return method;
		}

		public static bool Prepare(MethodBase method)
		{
			Type type = AccessTools.TypeByName("TorannMagic.TorannMagicMod");
			if (type == null)
			{
				return false;
			}
			MethodInfo m = AccessTools.Method(AccessTools.Inner(type, "Pawn_SkillTracker_Base_Patch"), "Prefix");
			if (m == null)
			{
				return false;
			}
			return true;
		}

		[HarmonyPrefix]
		[HarmonyPriority(int.MaxValue)]
		public static bool Prefix(ref bool __result)
		{
			__result = true;
			return false;
		}
	}

	[HarmonyPatch]
	public static class ISaidNoVE
	{
		public static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("VanillaQuestsExpandedCryptoforge.Hediff_CryptoSlowdown");
			if (type == null)
			{
				return null;
			}
			MethodInfo method = AccessTools.Method(type, "set_Severity");
			return method;
		}

		public static bool Prepare(MethodBase method)
		{
			Type type = AccessTools.TypeByName("VanillaQuestsExpandedCryptoforge.Hediff_CryptoSlowdown");
			if (type == null)
			{
				return false;
			}
			return true;
		}

		[HarmonyPrefix]
		public static bool Prefix(ref float value, HediffWithComps __instance)
		{
			if(__instance.pawn is RustedPawn rust)
			{
				value = Mathf.Min(value, 0.1f);
			}
			return true;
		}
	}

	[HarmonyPatch]
	public static class CombatExtended_ITab_Inventory_DrawThingRowCE
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method("CombatExtended.ITab_Inventory:DrawThingRowCE");
		}

		public static bool Prepare(MethodBase method)
		{
			return AccessTools.Method("CombatExtended.ITab_Inventory:DrawThingRowCE") != null;
		}

		[HarmonyPostfix]
		public static void Postfix(ref float y, float width, Thing thing, bool showDropButtonIfPrisoner = false)
		{
			if (thing.TryGetComp<CompUsableByRust>(out CompUsableByRust comp) && Find.Selector.SingleSelectedThing is RustedPawn rust && rust.Controllable)
			{
				Rect rect = new Rect(width - 72f, y - 28f, 24f, 24f);
				TooltipHandler.TipRegion(rect, comp.JobReport);
				if (Widgets.ButtonImage(rect, RustedArmyUtility.Use))
				{
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
					rust.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATRADefOf.NAT_UseItemByRust, thing), JobTag.DraftedOrder);
				}
			}
		}
	}

	[HarmonyPatch]
	public static class CombatExtended_ITab_Inventory_get_IsVisible
	{
		public static MethodBase TargetMethod()
		{
			return AccessTools.Method("CombatExtended.ITab_Inventory:get_IsVisible");
		}

		public static bool Prepare(MethodBase method)
		{
			return AccessTools.Method("CombatExtended.ITab_Inventory:get_IsVisible") != null;
		}

		[HarmonyPostfix]
		public static void Postfix(ref bool __result)
		{
			if (__result)
			{
				return;
			}
			if (Find.Selector.SingleSelectedThing is RustedPawn rust && (rust.Faction?.IsPlayer == true || DebugSettings.ShowDevGizmos))
			{
				__result = true;
			}
		}
	}
}