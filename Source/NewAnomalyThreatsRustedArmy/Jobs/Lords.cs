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

namespace NAT
{
	public class LordJob_DefendRust : LordJob
	{
		private bool sendWokenUpMessage;

		public bool awakeOnClamor;

		public IntVec3 position;

		public float wanderRadius;

		public string attackSignal = "";

		public bool forceWakeUp = false;

		public bool sleep = true;

		public int ticksTillFallback = 2500;

		public int ticksTillBackToWork = 5000;

		public LordJob_DefendRust()
		{
		}

		public LordJob_DefendRust(IntVec3 position, float wanderRadius, bool sleep, bool sendWokenUpMessage = true, bool awakeOnClamor = false, bool forceWakeUp = false, string attackSignal = "", int ticksTillFallback = 2500, int ticksTillBackToWork = 5000)
		{
			this.sendWokenUpMessage = sendWokenUpMessage;
			this.position = position;
			this.wanderRadius = wanderRadius;
			this.awakeOnClamor = awakeOnClamor;
			this.forceWakeUp = forceWakeUp;
			this.attackSignal = attackSignal;
			this.sleep = sleep;
			this.ticksTillFallback = ticksTillFallback;
			this.ticksTillBackToWork = ticksTillBackToWork;
		}

		protected virtual LordToil GetIdleToil()
		{
			if (sleep)
			{
				return new LordToil_Sleep();
			}
			return new LordToil_StageRust(position);
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil firstSource = (stateGraph.StartingToil = GetIdleToil());
			LordToil_StageRust lordToil_Stage = new LordToil_StageRust(position);
			stateGraph.AddToil(lordToil_Stage);
			LordToil_AssaultColonyRust lordToil_AssaultColony = new LordToil_AssaultColonyRust();
			stateGraph.AddToil(lordToil_AssaultColony);
			Transition transition = new Transition(firstSource, lordToil_Stage);
			transition.AddTrigger(new Trigger_Custom((TriggerSignal signal) => sleep && (signal.type == TriggerSignalType.DormancyWakeup || (awakeOnClamor && signal.type == TriggerSignalType.Clamor))));
			if (sendWokenUpMessage)
			{
				transition.AddPreAction(new TransitionAction_Message("MessageSleepingPawnsWokenUp".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst()).CapitalizeFirst(), MessageTypeDefOf.ThreatBig, null, 1f, AnyAsleep));
			}
			transition.AddPostAction(new TransitionAction_WakeAll());
			stateGraph.AddTransition(transition);
			Transition transition2 = new Transition(firstSource, lordToil_AssaultColony);
			transition2.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: false));
			transition2.AddTrigger(new Trigger_Custom((TriggerSignal signal) => ((signal.type == TriggerSignalType.BuildingDamaged || signal.type == TriggerSignalType.BuildingLost) && signal.thing is Building b && b.GetLord() == lord) || signal.signal.tag == "NAT_CrateOpened" || (!attackSignal.NullOrEmpty() && signal.signal.tag == attackSignal && (!sleep || signal.signal.args.GetArg<bool>("wakeUp") == true))));
			transition2.AddPostAction(new TransitionAction_Custom(delegate (Transition t)
			{
				foreach (Lord lord in t.Map.lordManager.lords)
				{
					lord.Notify_SignalReceived(new Signal(attackSignal, new NamedArgument(forceWakeUp == true, "wakeUp")));
				}
			}));
			if (sendWokenUpMessage)
			{
				transition2.AddPreAction(new TransitionAction_Message("MessageSleepingPawnsWokenUp".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst()).CapitalizeFirst(), MessageTypeDefOf.ThreatBig, null, 1f, AnyAsleep));
			}
			transition2.AddPostAction(new TransitionAction_WakeAll());
			stateGraph.AddTransition(transition2);
			Transition transition3 = new Transition(lordToil_Stage, lordToil_AssaultColony);
			transition3.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: false));
			transition3.AddTrigger(new Trigger_Custom((TriggerSignal signal) => signal.type == TriggerSignalType.BuildingDamaged || signal.type == TriggerSignalType.BuildingLost || (!attackSignal.NullOrEmpty() && signal.signal.tag == attackSignal)));
			transition3.AddPostAction(new TransitionAction_Custom(delegate (Transition t)
			{
				foreach (Lord lord in t.Map.lordManager.lords)
				{
					lord.Notify_SignalReceived(new Signal(attackSignal, new NamedArgument(forceWakeUp == true, "wakeUp")));
				}
			}));
			stateGraph.AddTransition(transition3);
			Transition transition4 = new Transition(lordToil_AssaultColony, lordToil_Stage);
			transition4.AddTrigger(new Trigger_TicksPassedWithoutHarm(ticksTillFallback));
			stateGraph.AddTransition(transition4);
			Transition transition5 = new Transition(lordToil_Stage, firstSource);
			transition5.AddTrigger(new Trigger_TicksPassedWithoutHarm(ticksTillBackToWork));
			stateGraph.AddTransition(transition5);
			return stateGraph;
		}

		private bool AnyAsleep()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if (lord.ownedPawns[i].Spawned && !lord.ownedPawns[i].Dead && !lord.ownedPawns[i].Awake())
				{
					return true;
				}
			}
			return false;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref sendWokenUpMessage, "sendWokenUpMessage", defaultValue: true);
			Scribe_Values.Look(ref awakeOnClamor, "awakeOnClamor", defaultValue: false);
			Scribe_Values.Look(ref position, "position");
			Scribe_Values.Look(ref ticksTillFallback, "ticksTillFallback");
			Scribe_Values.Look(ref ticksTillBackToWork, "ticksTillBackToWork");
			Scribe_Values.Look(ref wanderRadius, "wanderRadius", 0f);
		}
	}

	public class LordJob_AssistColony_Rust : LordJob
	{
		private IntVec3 fallbackLocation;

		public LordJob_AssistColony_Rust()
		{
		}

		public LordJob_AssistColony_Rust(IntVec3 fallbackLocation)
		{
			this.fallbackLocation = fallbackLocation;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil_HuntEnemies lordToil_HuntEnemies = (LordToil_HuntEnemies)(stateGraph.StartingToil = new LordToil_HuntEnemies(fallbackLocation));
			LordToil_ExitMap lordToil_ExitMap = new LordToil_ExitMap();
			stateGraph.AddToil(lordToil_ExitMap);
			Transition transition = new Transition(lordToil_HuntEnemies, lordToil_ExitMap);
			transition.AddPreAction(new TransitionAction_Message("NAT_MessageRustedTroopersLeaving".Translate()));
			transition.AddTrigger(new Trigger_TicksPassed(30000));
			//transition.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
			stateGraph.AddTransition(transition);
			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref fallbackLocation, "fallbackLocation");
		}
	}

	public class LordJob_RustedArmy : LordJob
	{
		private bool canKidnap = true;

		private bool canTimeoutOrFlee = true;

		private IntVec3 stageLoc;

		private bool canLeave = true;

		private bool breachers;

		private bool canPickUpOpportunisticWeapons;

		private int stageTicks = 0;

		private float fractionLostToAssault = 0.05f;

		private bool waitForever = false;

		public override bool GuiltyOnDowned => true;

		public LordJob_RustedArmy()
		{
		}

		public LordJob_RustedArmy(SpawnedPawnParams parms)
		{
			canKidnap = false;
			canTimeoutOrFlee = false;
			canLeave = false;
		}

		public LordJob_RustedArmy(IntVec3 stageLoc, int stageTicks, bool waitForever = false, bool canLeave = true, bool breachers = false, bool canPickUpOpportunisticWeapons = false)
		{
			this.stageLoc = stageLoc;
			this.stageTicks = stageTicks;
			this.canLeave = canLeave;
			this.breachers = breachers;
			this.canPickUpOpportunisticWeapons = canPickUpOpportunisticWeapons;
			this.waitForever = waitForever;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			List<LordToil> list = new List<LordToil>();
			LordToil lordToil = null;
			LordToil_StageRust lordToil_Stage = null;
			if (breachers)
			{
				lordToil = new LordToil_AssaultColonyBreaching();
				stateGraph.AddToil(lordToil);
				list.Add(lordToil);
			}
			else
			{
				lordToil = new LordToil_AssaultColonyRust(attackDownedIfStarving: false, canPickUpOpportunisticWeapons);
				stateGraph.AddToil(lordToil);
			}
			if (waitForever || stageTicks > 0)
			{
				lordToil_Stage = new LordToil_StageRust(stageLoc);
				Transition transition = new Transition(lordToil_Stage, lordToil);
				if (!waitForever)
				{
					transition.AddTrigger(new Trigger_TicksPassed(stageTicks));
				}
				transition.AddTrigger(new Trigger_FractionPawnsLost(fractionLostToAssault));
				transition.AddPreAction(new TransitionAction_Message("MessageRaidersBeginningAssault".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst(), "NAT_RustedArmy".Translate()), MessageTypeDefOf.ThreatBig));
				transition.AddPostAction(new TransitionAction_WakeAll());
				stateGraph.AddTransition(transition);
				stateGraph.AddToil(lordToil_Stage);
				stateGraph.StartingToil = lordToil_Stage;
			}
			LordToil_DanceRust lordToil_DanceVictory = new LordToil_DanceRust();
			lordToil_DanceVictory.useAvoidGrid = true;
			stateGraph.AddToil(lordToil_DanceVictory);
			Transition transition3 = new Transition(lordToil, lordToil_DanceVictory);
			transition3.AddTrigger(new Trigger_VictoryRust());
			stateGraph.AddTransition(transition3);
			Transition transition4 = new Transition(lordToil_DanceVictory, lordToil);
			transition4.AddTrigger(new Trigger_PawnHarmed());
			stateGraph.AddTransition(transition4);
			if (canLeave)
			{
				LordToil_ExitMapRust lordToil_ExitMap = new LordToil_ExitMapRust(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true);
				lordToil_ExitMap.useAvoidGrid = true;
				stateGraph.AddToil(lordToil_ExitMap);
				Transition transition5 = new Transition(lordToil_DanceVictory, lordToil_ExitMap);
				Trigger_TicksPassed trigger_TicksPassed = new Trigger_TicksPassed(3000);
				trigger_TicksPassed.WithFilter(new TriggerFilter_VictoryRust());
				transition5.AddTrigger(trigger_TicksPassed);
				transition5.AddPreAction(new TransitionAction_Message("MessageRaidersSatisfiedLeaving".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst(), "NAT_RustedArmy".Translate())));
				stateGraph.AddTransition(transition5);
			}

			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref stageLoc, "stageLoc");
			Scribe_Values.Look(ref fractionLostToAssault, "fractionLostToAssault", defaultValue: 0.05f);
			Scribe_Values.Look(ref waitForever, "waitForever", defaultValue: false);
			Scribe_Values.Look(ref canKidnap, "canKidnap", defaultValue: true);
			Scribe_Values.Look(ref canTimeoutOrFlee, "canTimeoutOrFlee", defaultValue: true);
			Scribe_Values.Look(ref canLeave, "canLeave", defaultValue: true);
			Scribe_Values.Look(ref breachers, "breaching", defaultValue: false);
			Scribe_Values.Look(ref canPickUpOpportunisticWeapons, "canPickUpOpportunisticWeapons", defaultValue: false);
		}
	}

	public class LordToil_ExitMapRust : LordToil_ExitMap
	{
		public override DutyDef ExitDuty => NATRADefOf.NAT_RustExitMap;

		public LordToil_ExitMapRust(LocomotionUrgency locomotion = LocomotionUrgency.None, bool canDig = false, bool interruptCurrentJob = false)
			: base(locomotion, canDig, interruptCurrentJob)
		{
		}

		public override void UpdateAllDuties()
		{
			foreach(Building b in lord.ownedBuildings.ToList())
			{
				if(b.TryGetComp<CompRustedTurretPawn>(out var comp) && !comp.destroyed)
				{
					comp.SpawnPawn(comp.parent.Position, comp.parent.Map);
				}
			}
			base.UpdateAllDuties();
		}
	}

	public class LordToil_AssaultColonyRust : LordToil
	{
		private bool attackDownedIfStarving;

		private bool canPickUpOpportunisticWeapons;

		public override bool ForceHighStoryDanger => true;

		public override bool AllowSatisfyLongNeeds => false;

		public LordToil_AssaultColonyRust(bool attackDownedIfStarving = false, bool canPickUpOpportunisticWeapons = false)
		{
			this.attackDownedIfStarving = attackDownedIfStarving;
			this.canPickUpOpportunisticWeapons = canPickUpOpportunisticWeapons;
		}

		public override void UpdateAllDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if (lord.ownedPawns[i].mindState != null)
				{
					lord.ownedPawns[i].mindState.duty = new PawnDuty(NATRADefOf.NAT_RustAssaultColony);
					lord.ownedPawns[i].mindState.duty.attackDownedIfStarving = attackDownedIfStarving;
					lord.ownedPawns[i].mindState.duty.pickupOpportunisticWeapon = canPickUpOpportunisticWeapons;
					lord.ownedPawns[i].TryGetComp<CompCanBeDormant>()?.WakeUp();
				}
			}
		}
	}

	public class LordToil_DanceRust : LordToil
	{
		public override bool ForceHighStoryDanger => true;

		public override bool AllowSatisfyLongNeeds => false;

		public LordToil_DanceRust()
		{
		}

		public override void UpdateAllDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if (lord.ownedPawns[i].mindState != null)
				{
					lord.ownedPawns[i].mindState.duty = new PawnDuty(NATRADefOf.NAT_RustDance);
					lord.ownedPawns[i].TryGetComp<CompCanBeDormant>()?.WakeUp();
				}
			}
		}
	}

	public class LordToil_StageRust : LordToil
	{
		public override IntVec3 FlagLoc => Data.stagingPoint;

		private LordToilData_Stage Data => (LordToilData_Stage)data;

		public override bool ForceHighStoryDanger => true;

		public LordToil_StageRust(IntVec3 stagingLoc)
		{
			data = new LordToilData_Stage();
			Data.stagingPoint = stagingLoc;
		}

		public override void UpdateAllDuties()
		{
			LordToilData_Stage lordToilData_Stage = Data;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				lord.ownedPawns[i].mindState.duty = new PawnDuty(NATRADefOf.NAT_RustDefend, lordToilData_Stage.stagingPoint);
				lord.ownedPawns[i].mindState.duty.radius = 28f;
			}
		}
	}

	public class LordJob_DefendVoidStructure : LordJob
	{
		private Thing structure;

		private float? wanderRadius;

		private float? defendRadius;

		private bool isCaravanSendable;

		private bool addFleeToil;

		private int ticksBeforeAttack;

		public override bool IsCaravanSendable => isCaravanSendable;

		public override bool AddFleeToil => addFleeToil;

		public LordJob_DefendVoidStructure()
		{
		}

		public LordJob_DefendVoidStructure(Thing structure, int ticksBeforeAttack, float? wanderRadius = null, float? defendRadius = null, bool isCaravanSendable = false, bool addFleeToil = true)
		{
			this.structure = structure;
			this.ticksBeforeAttack = ticksBeforeAttack;
			this.wanderRadius = wanderRadius;
			this.defendRadius = defendRadius;
			this.isCaravanSendable = isCaravanSendable;
			this.addFleeToil = addFleeToil;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			LordToil_DefendPoint lordToil_DefendStructure = (LordToil_DefendPoint)(stateGraph.StartingToil = new LordToil_DefendPoint(structure.Position, wanderRadius: wanderRadius, defendRadius: defendRadius));
			LordToil_AssaultColonyRust lordToil_AssaultColony = new LordToil_AssaultColonyRust(attackDownedIfStarving: true)
			{
				useAvoidGrid = true
			};
			stateGraph.AddToil(lordToil_AssaultColony);
			Transition transition = new Transition(lordToil_DefendStructure, lordToil_AssaultColony);
			transition.AddTrigger(new Trigger_FractionPawnsLost(0.1f));
			transition.AddTrigger(new Trigger_PawnHarmed(0.5f));
			transition.AddTrigger(new Trigger_TicksPassed(ticksBeforeAttack));
			transition.AddTrigger(new Trigger_OnClamor(ClamorDefOf.Ability));
			transition.AddTrigger(new Trigger_StructureActivated(structure));
			transition.AddPostAction(new TransitionAction_WakeAll());
			TaggedString taggedString = "MessageDefendersAttacking".Translate("NAT_RustedSoldiers".Translate(), "NAT_RustedArmy".Translate(), Faction.OfPlayer.def.pawnsPlural).CapitalizeFirst();
			transition.AddPreAction(new TransitionAction_Message(taggedString, MessageTypeDefOf.ThreatBig));
			stateGraph.AddTransition(transition);
			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_Deep.Look(ref structure, "structure");
			Scribe_Values.Look(ref wanderRadius, "wanderRadius");
			Scribe_Values.Look(ref defendRadius, "defendRadius");
			Scribe_Values.Look(ref isCaravanSendable, "isCaravanSendable", defaultValue: false);
			Scribe_Values.Look(ref addFleeToil, "addFleeToil", defaultValue: false);
		}
	}
	public class Trigger_TicksPassedWithoutHarm : Trigger_TicksPassed
	{
		public Trigger_TicksPassedWithoutHarm(int tickLimit)
			: base(tickLimit)
		{
		}

		public override bool ActivateOn(Lord lord, TriggerSignal signal)
		{
			if (Trigger_PawnHarmed.SignalIsHarm(signal))
			{
				base.Data.ticksPassed = 0;
			}
			return base.ActivateOn(lord, signal);
		}
	}



	public class TriggerData_StructureActivated : TriggerData
	{
		public Thing structure;

		public override void ExposeData()
		{
			Scribe_References.Look(ref structure, "structure", saveDestroyedThings: true);
		}
	}

	public class Trigger_StructureActivated : Trigger
	{
		protected TriggerData_StructureActivated Data => (TriggerData_StructureActivated)data;

		public Trigger_StructureActivated(Thing structure)
		{
			data = new TriggerData_StructureActivated();
			Data.structure = structure;
		}

		public override bool ActivateOn(Lord lord, TriggerSignal signal)
		{
			if (signal.type == TriggerSignalType.Tick)
			{
				if (data == null || !(data is TriggerData_StructureActivated))
				{
					return true;
				}
				TriggerData_StructureActivated triggerData_StructureActivated = Data;
				Thing structure = triggerData_StructureActivated.structure;
				if (!(structure is ThingWithComps s) || s.GetComp<CompVoidStructure>().Active)
				{
					return true;
				}
			}
			return false;
		}
	}

	public class Trigger_VictoryRust : Trigger
	{
		public override bool ActivateOn(Lord lord, TriggerSignal signal)
		{
			if (signal.type == TriggerSignalType.Tick && lord.ticksInToil % 500 == 0 && Victory(lord.Map))
			{
				return true;
			}
			return false;
		}

		public static bool Victory(Map map)
		{
			if(GenHostility.AnyHostileActiveThreatTo(map, Faction.OfEntities))
			{
				return false;
			}
			return true;
		}
	}

	public class TriggerFilter_VictoryRust : TriggerFilter
	{
		public override bool AllowActivation(Lord lord, TriggerSignal signal)
		{
			return Trigger_VictoryRust.Victory(lord.Map);
		}
	}

	public class LordToil_SleepRust : LordToil
	{
		public override void UpdateAllDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				p.mindState.duty = new PawnDuty(DutyDefOf.SleepForever);
				if (p.canBeDormant != null && p.canBeDormant.Awake)
				{
					p.canBeDormant.ToSleep();
				}
			}
		}
	}
}