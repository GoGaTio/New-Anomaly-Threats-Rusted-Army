using CombatExtended;
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
using UnityEngine.Diagnostics;
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

namespace NAT.CE.Rusts
{
	[StaticConstructorOnStartup]
	public static class RustedArmyPatch_CE
	{
		static RustedArmyPatch_CE()
		{
			Harmony harmony = new Harmony("GoGaTio.NewAnomalyThreats.RustedArmy.CE.HarmonyPatch");
			harmony.PatchAll();
		}
	}

	[HarmonyPatch(typeof(CompAmmoUser))]
	[HarmonyPatch(nameof(CompAmmoUser.CompGetGizmosExtra))]
	public class Patch_UseGear
	{
		[HarmonyPostfix]
		public static void Postfix(ref IEnumerable<Gizmo> __result, CompAmmoUser __instance)
		{
			if(__instance.Wielder is RustedPawn rust && rust.EverControllable)
			{
				__result = CompGetGizmosExtra(rust, __instance);
			}
		}

		public static IEnumerable<Gizmo> CompGetGizmosExtra(RustedPawn rust, CompAmmoUser comp)
		{
			GizmoAmmoStatus ammoStatusGizmo = new GizmoAmmoStatus { compAmmo = comp };
			yield return ammoStatusGizmo;

			Action action = comp.TryStartReload;

			string tag;
			if (comp.HasMagazine)
			{
				tag = "CE_Reload";    // Teach reloading weapons with magazines
			}
			else
			{
				tag = "CE_ReloadNoMag";    // Teach about mag-less weapons
			}

			Command_Reload reloadCommandGizmo = new Command_Reload
			{
				compAmmo = comp,
				action = action,
				defaultLabel = comp.HasMagazine ? (string)"CE_ReloadLabel".Translate() : "",
				defaultDesc = "CE_ReloadDesc".Translate(),
				icon = comp.CurrentAmmo == null ? ContentFinder<Texture2D>.Get("UI/Buttons/Reload", true) : comp.SelectedAmmo.IconTexture(),
				tutorTag = tag
			};
			yield return reloadCommandGizmo;
			if (DebugSettings.godMode)
			{
				Command_Action devSetAmmoToMinCommandGizmo = new Command_Action
				{
					action = delegate { comp.CurMagCount = 0; },
					defaultLabel = "DEV: Set ammo to 0"
				};
				yield return devSetAmmoToMinCommandGizmo;

				Command_Action devSetAmmoToMaxCommandGizmo = new Command_Action
				{
					action = delegate { comp.CurrentAmmo = comp.SelectedAmmo; comp.CurMagCount = comp.MagSize; },
					defaultLabel = "DEV: Set ammo to max"
				};
				yield return devSetAmmoToMaxCommandGizmo;
			}
		}
	}
}