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
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace NAT.Rusts
{
	[HarmonyPatch(typeof(ITab_Pawn_Gear))]
	[HarmonyPatch("DrawThingRow")]
	public class Patch_UseGear
	{

		[HarmonyPostfix]
		public static void Postfix(ref float y, float width, Thing thing, bool inventory = false)
		{
			if (inventory && Find.Selector.SingleSelectedThing is RustedPawn rust && rust.Controllable && thing.TryGetComp(out CompUsableByRust comp))
			{
				Rect rect = new Rect(width - 72f, y - 28f, 24f, 24f);
				AcceptanceReport report = comp.CanBeUsedBy(rust);
				bool flag = report.Accepted;
				if(!flag && report.Reason.NullOrEmpty())
				{
					return;
				}
				TooltipHandler.TipRegion(rect, (flag) ? comp.JobReport.Formatted(thing.LabelShort) : "CannotUseReason".Translate(report.Reason));
				Color color = (flag ? Color.white : Color.gray);
				Color mouseoverColor = (flag ? GenUI.MouseoverColor : color);
				if (Widgets.ButtonImage(rect, RustedArmyUtility.Use, color, mouseoverColor, flag) && flag)
				{
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
					rust.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATRADefOf.NAT_UseItemByRust, thing), JobTag.DraftedOrder);
				}
			}
		}
	}

	[HarmonyPatch(typeof(ITab_Pawn_Gear))]
	[HarmonyPatch("CanControlColonist")]
	[HarmonyPatch(MethodType.Getter)]
	public class Patch_CanDropEquipment
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result)
		{
			if (__result) return;
			if (Find.Selector.SingleSelectedThing is RustedPawn rust && rust.Controllable)
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(ITab_Pawn_Gear))]
	[HarmonyPatch("IsVisible")]
	[HarmonyPatch(MethodType.Getter)]
	public class Patch_IsVisible
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result)
		{
			if (__result) return;
			if (Find.Selector.SingleSelectedThing is RustedPawn rust && (DebugSettings.godMode || rust.EverControllable || SubModSettings_RustedArmy.Value.showGearTabOnEnemies))
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(CompAIUsablePack), "CanOpportunisticallyUseNow")]
	public class Patch_AIUSablePack
	{
		[HarmonyPostfix]
		public static void Postfix(Pawn wearer, ref bool __result)
		{
			if (wearer is RustedPawn && wearer.Faction == Faction.OfPlayerSilentFail)
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(BroadshieldPack), "NearbyActiveBroadshield")]
	public class Patch_BroadshieldPack
	{
		[HarmonyPrefix]
		public static bool Prefix(ref bool __result, BroadshieldPack __instance)
		{
			if (__instance.Wearer is RustedPawn rust && rust.Faction == Faction.OfPlayerSilentFail)
			{
				__result = true;
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(SmokepopBelt), "Notify_BulletImpactNearby")]
	public class Patch_SmokepopBelt
	{
		[HarmonyPrefix]
		public static bool Prefix(SmokepopBelt __instance)
		{
			if (__instance.Wearer is RustedPawn rust && rust.Faction == Faction.OfPlayerSilentFail)
			{
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(PawnGenerator), "GenerateGearFor")]
	public class Patch_GenereteGearFor
	{
		private static List<ThingStuffPair> tmpApparelCandidates;

		private static List<ThingStuffPair> allApparelPairs;

		private static RustedPawn tmpRust;

		private static List<ThingStuffPair> AllApparelPairs
		{
			get
			{
				if (allApparelPairs == null)
				{
					allApparelPairs = new List<ThingStuffPair>();
					allApparelPairs = ThingStuffPair.AllWith((ThingDef td) => td.IsApparel);
				}
				return allApparelPairs;
			}
		}

		private static List<ThingDef> drugs;

		private static List<ThingDef> Drugs
        {
            get
            {
				if(drugs == null)
                {
					drugs = new List<ThingDef>();
					foreach(ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading.Where(delegate (ThingDef x)
					{
						if (x.category != ThingCategory.Item)
						{
							return false;
						}
						CompProperties_UsableByRust compProperties = x.GetCompProperties<CompProperties_UsableByRust>();
						return (compProperties != null && compProperties.combatEnhancing) ? true : false;
                    }))
                    {
						drugs.Add(thingDef);
                    }
                }
				return drugs;
            }
        }

		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, PawnGenerationRequest request)
		{
			if (pawn is RustedPawn rust)
			{
				//rust.inventory.DestroyAll();
				pawn.apparel.DestroyAll();
				int drugsAmount = pawn.kindDef.combatEnhancingDrugsCount.RandomInRange;
				if (drugsAmount > 0)
				{
					for (int i = 0; i < drugsAmount; i++)
					{
						if (!Drugs.TryRandomElement(out var result))
						{
							break;
						}
						pawn.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(result));
					}
				}
				for (int i = 0; i < rust.kindDef.fixedInventory.Count; i++)
				{
					ThingDefCountClass thingDefCountClass = rust.kindDef.fixedInventory[i];
					Thing thing = ThingMaker.MakeThing(thingDefCountClass.thingDef, thingDefCountClass.stuff);
					thing.stackCount = thingDefCountClass.count;
					if (thing.TryGetComp(out CompQuality comp))
					{
						comp.SetQuality(thingDefCountClass.quality, ArtGenerationContext.Outsider);
					}
					if (thingDefCountClass.color.HasValue && thing.TryGetComp(out CompColorable comp2))
					{
						comp2.SetColor(thingDefCountClass.color.Value);
					}
					if (thing.HasComp<CompEquippable>())
					{
						rust.equipment.AddEquipment((ThingWithComps)thing);
					}
					else if (thing is Apparel newApparel)
					{
						rust.apparel.Wear(newApparel);
					}
					else
					{
						rust.inventory.innerContainer.TryAdd(thing);
					}
				}
				if (rust.kindDef.inventoryOptions != null)
				{
					foreach (Thing item in rust.kindDef.inventoryOptions.GenerateThings())
					{
						rust.inventory.innerContainer.TryAdd(item);
					}
				}
				float randomInRange = pawn.kindDef.apparelMoney.RandomInRange;
				if (randomInRange > 0f)
				{
					tmpRust = rust;
					tmpApparelCandidates = new List<ThingStuffPair>();
					lgps = new HashSet<ApparelUtility.LayerGroupPair>();
					GenerateWorkingPossibleApparelSetFor(randomInRange);
					foreach (ThingStuffPair pair in tmpApparelCandidates)
					{
						Apparel apparel = (Apparel)ThingMaker.MakeThing(pair.thing, pair.stuff);
						PawnGenerator.PostProcessGeneratedGear(apparel, pawn);
						if (ApparelUtility.HasPartsToWear(pawn, apparel.def))
						{
							pawn.apparel.Wear(apparel, dropReplacedApparel: false);
						}
					}
					foreach (Apparel item in pawn.apparel.WornApparel)
					{
						PawnApparelGenerator.PostProcessApparel(item, pawn);
						CompBiocodable compBiocodable = item.TryGetComp<CompBiocodable>();
						if (compBiocodable != null && !compBiocodable.Biocoded && Rand.Chance(request.BiocodeApparelChance))
						{
							compBiocodable.CodeFor(pawn);
						}
					}
				}
				tmpRust = null;
			}
		}

		private static void GenerateWorkingPossibleApparelSetFor(float money)
		{
			float moneyLeft = money;
			moneyLeft = GenerateSpecificRequiredApparel(moneyLeft, onlyGenerateIgnoreNaked: false);
			List<ThingDef> reqApparel = tmpRust.kindDef.apparelRequired;
			if (reqApparel != null)
			{
				for (int i = 0; i < reqApparel.Count; i++)
				{
					if (AllApparelPairs.Where((ThingStuffPair pa) => pa.thing == reqApparel[i] && CanUseStuff(pa) && !PairOverlapsAnything(pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out var result))
					{
						AddApparel(result);
						moneyLeft -= result.Price;
					}
				}
			}
			ThingStuffPair result2;
			int num = 0;
			while (num < 20 && money > 0 && AllApparelPairs.Where((ThingStuffPair pa) => pa.Price <= money && CanUseStuff(pa) && pa.thing.apparel.tags.Any((string x)=> tmpRust.kindDef.apparelTags?.Contains(x) == true) && !PairOverlapsAnything(pa)).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out result2))
			{
				AddApparel(result2);
				moneyLeft -= result2.Price;
				num++;
			}
		}

		private static float GenerateSpecificRequiredApparel(float moneyLeft, bool onlyGenerateIgnoreNaked)
		{
			List<SpecificApparelRequirement> att = tmpRust.kindDef.specificApparelRequirements;
			if (att != null)
			{
				int i;
				for (i = 0; i < att.Count; i++)
				{
					if ((!att[i].RequiredTag.NullOrEmpty() || (!att[i].AlternateTagChoices.NullOrEmpty() && (!onlyGenerateIgnoreNaked || att[i].IgnoreNaked))) && AllApparelPairs.Where((ThingStuffPair pa) => PawnApparelGenerator.ApparelRequirementTagsMatch(att[i], pa.thing) && PawnApparelGenerator.ApparelRequirementHandlesThing(att[i], pa.thing) && CanUseStuff(pa) && pa.thing.apparel.PawnCanWear(tmpRust)/* && !workingSet.PairOverlapsAnything(pa)*/).TryRandomElementByWeight((ThingStuffPair pa) => pa.Commonality, out var result))
					{
						AddApparel(result);
						moneyLeft -= result.Price;
					}
				}
			}
			return moneyLeft;
		}

		private static bool CanUseStuff(ThingStuffPair pair)
		{
			if (pair.stuff != null && tmpRust.Faction != null && !tmpRust.kindDef.ignoreFactionApparelStuffRequirements && !tmpRust.Faction.def.CanUseStuffForApparel(pair.stuff))
			{
				return false;
			}
			return true;
		}

		public static void AddApparel(ThingStuffPair pair)
		{
			tmpApparelCandidates.Add(pair);
			for (int i = 0; i < pair.thing.apparel.layers.Count; i++)
			{
				ApparelLayerDef layer = pair.thing.apparel.layers[i];
				BodyPartGroupDef[] interferingBodyPartGroups = pair.thing.apparel.GetInterferingBodyPartGroups(tmpRust.RaceProps.body);
				for (int j = 0; j < interferingBodyPartGroups.Length; j++)
				{
					lgps.Add(new ApparelUtility.LayerGroupPair(layer, interferingBodyPartGroups[j]));
				}
			}
		}

		private static  HashSet<ApparelUtility.LayerGroupPair> lgps = new HashSet<ApparelUtility.LayerGroupPair>();

		private static bool PairOverlapsAnything(ThingStuffPair pair)
		{
			if (!lgps.Any())
			{
				return false;
			}
			for (int i = 0; i < pair.thing.apparel.layers.Count; i++)
			{
				ApparelLayerDef layer = pair.thing.apparel.layers[i];
				BodyPartGroupDef[] interferingBodyPartGroups = pair.thing.apparel.GetInterferingBodyPartGroups(tmpRust.RaceProps.body);
				for (int j = 0; j < interferingBodyPartGroups.Length; j++)
				{
					if (lgps.Contains(new ApparelUtility.LayerGroupPair(layer, interferingBodyPartGroups[j])))
					{
						return true;
					}
				}
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(CompApparelVerbOwner), "CreateVerbTargetCommand")]
	public class Patch_CompApparelVerbOwner
	{
		[HarmonyPostfix]
		public static void Postfix(Thing gear, Verb verb, ref Command_VerbTarget __result, CompApparelVerbOwner __instance)
		{
			if (__instance.Wearer is RustedPawn rust && rust.Faction?.IsPlayer == true && rust.restNeed?.exhausted == false && __result.disabledReason == "CannotOrderNonControlled".Translate())
			{
				if (!__instance.CanBeUsed(out var reason))
				{
					__result.disabledReason = reason;
				}
				else
				{
					__result.Disabled = false;
					__result.disabledReason = "";
				}
			}
		}
	}

	[HarmonyPatch(typeof(Apparel), nameof(Apparel.PawnCanWear))]
	public class Patch_PawnCanWear
	{

		[HarmonyPostfix]
		public static void Postfix(Pawn pawn, bool ignoreGender, ref bool __result, Apparel __instance)
		{
			if (!__result) return;
			CompRustedEquipment comp = __instance.TryGetComp<CompRustedEquipment>();
			if (comp != null)
			{
				__result = comp.EquipableBy(pawn);
			}
		}
	}

	[HarmonyPatch(typeof(JobGiver_TakeCombatEnhancingDrug), "TryGiveJob")]
	public class Patch_TakeCombatEnhancingDrug
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn pawn, ref Job __result)
		{
			if (pawn is RustedPawn rust)
			{
				foreach (Thing item in rust.inventory.innerContainer)
				{
					CompUsableByRust comp = item.TryGetComp<CompUsableByRust>();
					if (comp != null && comp.ShouldUseForCombat(rust))
					{
						Job job = JobMaker.MakeJob(NATRADefOf.NAT_UseItemByRust, item);
						job.count = 1;
						__result = job;
						return false;
					}
				}
				return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.DropAndForbidEverything))]
	public class Patch_DropAndForbidEverything
	{
		[HarmonyPrefix]
		public static bool Prefix(Pawn __instance)
		{
			if (__instance is RustedPawn rust && rust.equipment?.Primary != null && rust.Faction != Faction.OfPlayerSilentFail)
			{
				CompRustedEquipment comp = rust.equipment.Primary.TryGetComp<CompRustedEquipment>();
				if (comp != null && comp.Props.destroyOnDrop && !comp.parent.Destroyed)
				{
					rust.equipment.DestroyEquipment(rust.equipment.Primary);
				}
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(EquipmentUtility), "CanEquip", new Type[]
	{
		typeof(Thing),
		typeof(Pawn),
		typeof(string),
		typeof(bool)
	},
	new ArgumentType[]
	{
		ArgumentType.Normal,
		ArgumentType.Normal,
		ArgumentType.Out,
		ArgumentType.Normal
	})]
	public class Patch_CanEquip
	{
		[HarmonyPostfix]
		public static void Postfix(ref bool __result, Thing thing, Pawn pawn, ref string cantReason, bool checkBonded = true)
		{
			if (!__result) return;
			if (pawn is RustedPawn rust)
			{
				if (!rust.Comp.Props.canEquipWeapons)
				{
					__result = false;
					return;
				}
				if (thing.HasComp<CompBladelinkWeapon>())
				{
					cantReason = "NAT_CannotEquip_Bladelink".Translate();
					__result = false;
					return;
				}
				if (thing is Apparel ap && !rust.Comp.CanWearApparel(ap))
				{
					cantReason = "NAT_CannotEquip_Apparel".Translate();
					__result = false;
					return;
				}
			}
			CompRustedEquipment comp = thing.TryGetComp<CompRustedEquipment>();
			if (comp != null)
			{
				AcceptanceReport report = comp.EquipableBy(pawn);
				if (!report.Accepted)
				{
					cantReason = report.Reason;
					__result = false;
					return;
				}
			}
		}
	}

	[HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.GetMaxAllowedToPickUp), new Type[]
	{
		typeof(Pawn),
		typeof(ThingDef),
	},
	new ArgumentType[]
	{
		ArgumentType.Normal,
		ArgumentType.Normal,
	})]
	public class Patch_GetMaxAllowedToPickUp
	{
		[HarmonyPostfix]
		public static void Postfix(ref int __result, Pawn pawn, ThingDef thingDef)
		{
			if (pawn is RustedPawn)
			{
				__result = int.MaxValue;
			}
		}
	}
}