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
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
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
using HarmonyLib;

namespace NAT.Rusts
{

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AnyPawnBlockingMapRemoval), MethodType.Getter)]
    public class Patch_AnyPawnBlockingMapRemoval
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result, MapPawns __instance)
        {
            if (__result) return;
            foreach (Pawn item in __instance.AllPawns)
            {
                if (item is RustedPawn rust && rust.EverControllable)
                {
                    __result = true;
                    return;
                }
            }
        }
    }

	[HarmonyPatch(typeof(TransportersArrivalActionUtility), nameof(TransportersArrivalActionUtility.AnyNonDownedColonist))]
	public static class Patch_TransportersArrivalActionUtility
	{
        [HarmonyPrefix]
        public static void Postfix(IEnumerable<IThingHolder> pods, ref bool __result)
        {
			if (!__result) return;
			foreach (IThingHolder pod in pods)
			{
				ThingOwner directlyHeldThings = pod.GetDirectlyHeldThings();
				for (int i = 0; i < directlyHeldThings.Count; i++)
				{
					if (directlyHeldThings[i] is RustedPawn rust && rust.EverControllable && !rust.Downed)
					{
						__result = true;
						return;
					}
				}
			}
		}
    }

	[HarmonyPatch(typeof(CompShuttle), nameof(CompShuttle.HasPilot), MethodType.Getter)]
	public class Patch_HasPilot
	{

		[HarmonyPostfix]
		public static void Postfix(ref bool __result, CompShuttle __instance)
		{
			if (__result) return;
			ThingOwner innerContainer = __instance.Transporter.innerContainer;
			for (int i = 0; i < innerContainer.Count; i++)
			{
				if (innerContainer[i] is RustedPawn rust && rust.EverControllable && rust.Comp.Props.isHumanlike)
				{
					__result = true;
					return;
				}
			}
		}
	}

	[HarmonyPatch(typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndJoinOrCreateCaravan))]
    public static class Patch_ExitMapAndJoinOrCreateCaravan
    {
        [HarmonyPrefix]
        [HarmonyPriority(501)]
        public static bool Prefix(Pawn pawn, Rot4 exitDir)
        {
            if (pawn is RustedPawn && pawn.Faction?.IsPlayer == true)
            {
                Caravan caravan = CaravanExitMapUtility.FindCaravanToJoinFor(pawn);
                if (caravan != null)
                {
                    //CaravanExitMapUtility.AddCaravanExitTaleIfShould(pawn);
                    caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: true);
                    pawn.ExitMap(allowedToJoinOrCreateCaravan: false, exitDir);
                }
                else
                {
                    Map map = pawn.Map;
                    PlanetTile directionTile = (PlanetTile)findRandomStartingTileBasedOnExitDir.Invoke(null, new object[2] { map.Tile, exitDir });
                    Caravan caravan2 = CaravanExitMapUtility.ExitMapAndCreateCaravan(Gen.YieldSingle(pawn), pawn.Faction, map.Tile, directionTile, PlanetTile.Invalid, sendMessage: false);
                    caravan2.autoJoinable = true;
                    bool flag = false;
                    IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
                    for (int i = 0; i < allPawnsSpawned.Count; i++)
                    {
                        if (CaravanExitMapUtility.FindCaravanToJoinFor(allPawnsSpawned[i]) != null && !allPawnsSpawned[i].Downed && !allPawnsSpawned[i].Drafted)
                        {
                            if (allPawnsSpawned[i].IsAnimal)
                            {
                                flag = true;
                            }
                            RestUtility.WakeUp(allPawnsSpawned[i]);
                            allPawnsSpawned[i].jobs.CheckForJobOverride();
                        }
                    }
                    TaggedString taggedString = "MessagePawnLeftMapAndCreatedCaravan".Translate(pawn.LabelShort, pawn).CapitalizeFirst();
                    if (flag)
                    {
                        taggedString += " " + "MessagePawnLeftMapAndCreatedCaravan_AnimalsWantToJoin".Translate();
                    }
                    Messages.Message(taggedString, caravan2, MessageTypeDefOf.TaskCompletion);
                }
                return false;
            }
            return true;
        }

        public static MethodInfo findRandomStartingTileBasedOnExitDir = AccessTools.Method(typeof(CaravanExitMapUtility), "FindRandomStartingTileBasedOnExitDir", new Type[2] { typeof(PlanetTile), typeof(Rot4) }, (Type[])null);
    }

    [HarmonyPatch(typeof(CaravanExitMapUtility), "CanExitMapAndJoinOrCreateCaravanNow")]
    public static class Patch_CanExitMapAndJoinOrCreateCaravanNow
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result || !pawn.Spawned)
            {
                return;
            }
            if (!pawn.Map.exitMapGrid.MapUsesExitGrid)
            {
                return;
            }
            if (pawn is RustedPawn rust && rust.EverControllable)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(JobDriver_PrepareCaravan_GatherItems), nameof(JobDriver_PrepareCaravan_GatherItems.IsUsableCarrier))]
    public static class Patch_IsUsableCarrier
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn p, Pawn forPawn, bool allowColonists, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (!p.IsFormingCaravan())
            {
                return;
            }
            if (p.DestroyedOrNull() || !p.Spawned || p.inventory.UnloadEverything || !forPawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
            {
                return;
            }
            if (allowColonists && p is RustedPawn rust && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true && rust.EverControllable)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch]
    public static class Patch_CheckForErrors
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.Inner(typeof(Dialog_FormCaravan), "<>c__DisplayClass95_0"), "<CheckForErrors>b__1");
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            var jumpLabel = il.DefineLabel();
            codes[3].labels.Add(jumpLabel);

            var newCodes = new List<CodeInstruction>();

            newCodes.Add(new CodeInstruction(OpCodes.Ldarg_1));
            //newCodes.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Pawn), "get_RaceProps")));
            //newCodes.Add(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(RaceProperties), "get_IsMechanoid")));
            newCodes.Add(new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Patch_CheckForErrors), "GoodCaravanLeader", (Type[])null, (Type[])null)));
            newCodes.Add(new CodeInstruction(OpCodes.Brtrue_S, jumpLabel));

            codes.InsertRange(0, newCodes);
            return codes.AsEnumerable();
        }

        public static bool GoodCaravanLeader(Pawn pawn)
        {
            return pawn is RustedPawn rust && rust.EverControllable;
        }
    }

    [HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherItems), "UpdateAllDuties")]
    public static class Patch_LordToil_PrepareCaravan_GatherItems
    {
        public static FieldInfo meetingPoint = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherItems), "meetingPoint");

        [HarmonyPostfix]
        public static void Postfix(LordToil_PrepareCaravan_GatherDownedPawns __instance)
        {
            for (int i = 0; i < __instance.lord.ownedPawns.Count; i++)
            {
                Pawn pawn = __instance.lord.ownedPawns[i];
                if (pawn is RustedPawn p && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.PrepareCaravan_GatherItems, (IntVec3)meetingPoint.GetValue(__instance));
                }
            }
        }
    }

    [HarmonyPatch(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "UpdateAllDuties")]
    public static class Patch_LordToil_PrepareCaravan_GatherDownedPawns
    {
        public static FieldInfo meetingPoint = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "meetingPoint");

        public static FieldInfo exitSpot = AccessTools.Field(typeof(LordToil_PrepareCaravan_GatherDownedPawns), "exitSpot");

        [HarmonyPostfix]
        public static void Postfix(LordToil_PrepareCaravan_GatherDownedPawns __instance)
        {
            for (int i = 0; i < __instance.lord.ownedPawns.Count; i++)
            {
                Pawn pawn = __instance.lord.ownedPawns[i];
                if (pawn is RustedPawn p && p.workSettings?.WorkIsActive(WorkTypeDefOf.Hauling) == true)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.PrepareCaravan_GatherDownedPawns, (IntVec3)meetingPoint.GetValue(__instance), (IntVec3)exitSpot.GetValue(__instance));
                }
            }
        }
    }

    [HarmonyPatch(typeof(CaravanUtility), "IsOwner")]
    public static class Patch_CaravanUtility
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, Faction caravanFaction, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            if (caravanFaction == null)
            {
                return;
            }
            if (pawn is RustedPawn p && p.EverControllable && pawn.Faction == caravanFaction)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(WITab_Caravan_Social), "OnOpen")]
    public static class Patch_WITab_Caravan_Social
    {
        public static FieldInfo specificSocialTabForPawn = AccessTools.Field(typeof(WITab_Caravan_Social), "specificSocialTabForPawn");

        [HarmonyPostfix]
        public static void Postfix(WITab_Caravan_Social __instance)
        {
            if ((Pawn)specificSocialTabForPawn.GetValue(__instance) is RustedPawn)
            {
                specificSocialTabForPawn.SetValue(__instance, null);
            }
        }
    }

    [HarmonyPatch(typeof(SettleInExistingMapUtility), "SettleCommand")]
    public static class Patch_SettleInExistingMapUtility
    {
        [HarmonyPostfix]
        public static void Postfix(Map map, bool requiresNoEnemies, ref Command __result)
        {
            if (__result.disabledReason == "CommandSettleFailNoColonists".Translate() && map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayerSilentFail).Any((Pawn x) => x is RustedPawn rust && rust.Controllable))
            {
                if (requiresNoEnemies)
                {
                    foreach (IAttackTarget item in map.attackTargetsCache.TargetsHostileToColony)
                    {
                        if (GenHostility.IsActiveThreatToPlayer(item))
                        {
                            __result.Disable("CommandSettleFailEnemies".Translate());
                            return;
                        }
                    }
                }
                __result.disabledReason = null;
                __result.Disabled = false;
            }
        }
    }

	[HarmonyPatch(typeof(FormCaravanComp))]
	[HarmonyPatch(nameof(FormCaravanComp.GetGizmos))]
	public class Patch_ReformCaravan
	{
		[HarmonyPostfix]
		public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, FormCaravanComp __instance)
		{
			bool flag = false;
			foreach (Gizmo g in __result)
			{
				if (g is Command_Action action && action.tutorTag == "ReformCaravan")
				{
					flag = true;
				}
				yield return g;
			}
			if (flag)
			{
				yield break;
			}
			MapParent mapParent = (MapParent)__instance.parent;
			if (mapParent.HasMap && __instance.Reform && mapParent.Map.mapPawns.FreeColonistsSpawned.Count == 0 && !__instance.AnyActiveThreatNow && mapParent.Map.mapPawns.PawnsInFaction(Faction.OfPlayerSilentFail).Any((x) => x is RustedPawn))
			{
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "CommandReformCaravan".Translate();
				command_Action.defaultDesc = "CommandReformCaravanDesc".Translate();
				command_Action.icon = FormCaravanComp.FormCaravanCommand;
				command_Action.hotKey = KeyBindingDefOf.Misc2;
				command_Action.tutorTag = "ReformCaravan";
				command_Action.action = delegate
				{
					if (ModsConfig.OdysseyActive && mapParent.Map.listerThings.ThingsOfDef(ThingDefOf.GravEngine).Any())
					{
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmLoseGravship".Translate(), Form));
					}
					else if (ModsConfig.OdysseyActive && mapParent.Map.listerThings.ThingsInGroup(ThingRequestGroup.PassengerShuttle).Any())
					{
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmLoseShuttle".Translate(), Form));
					}
					else
					{
						Form();
					}
				};
				if (GenHostility.AnyHostileActiveThreatToPlayer(mapParent.Map, countDormantPawnsAsHostile: true))
				{
					command_Action.Disable("CommandReformCaravanFailHostilePawns".Translate());
				}
				yield return command_Action;
			}
			void Form()
			{
				Find.WindowStack.Add(new Dialog_FormCaravan(mapParent.Map, reform: true));
			}
		}
	}
}

