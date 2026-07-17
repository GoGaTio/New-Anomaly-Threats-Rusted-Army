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
using static System.Collections.Specialized.BitVector32;

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

	public class LordJob_RustedArmy : LordJob
	{
		private bool canKidnap = true;

		private bool canTimeoutOrFlee = true;

		private IntVec3 stageLoc;

		public bool canLeave = true;

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
			if (canLeave)
			{
				LordToil_DanceRust lordToil_DanceVictory = new LordToil_DanceRust();
				lordToil_DanceVictory.useAvoidGrid = true;
				stateGraph.AddToil(lordToil_DanceVictory);
				Transition transition3 = new Transition(lordToil, lordToil_DanceVictory);
				transition3.AddTrigger(new Trigger_VictoryRust());
				stateGraph.AddTransition(transition3);
				Transition transition4 = new Transition(lordToil_DanceVictory, lordToil);
				transition4.AddTrigger(new Trigger_PawnHarmed());
				stateGraph.AddTransition(transition4);
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

	public class LordJob_EscortAndDefendRust : LordJob
	{
		public Pawn escortee;

		private bool canLeave = true;

		public override bool GuiltyOnDowned => true;

		public LordJob_EscortAndDefendRust()
		{
		}

		public LordJob_EscortAndDefendRust(Pawn escortee)
		{
			//this.leaveIfEscorteeDestroyed = leaveIfEscorteeDestroyed;
			this.escortee = escortee;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();
			List<LordToil> list = new List<LordToil>();

			LordToil_StageRust lordToil_Stage = new LordToil_StageRust(escortee.PositionHeld);
			stateGraph.AddToil(lordToil_Stage);
			stateGraph.StartingToil = lordToil_Stage;
			
			LordToil_EscortRust lordToil_Escort = new LordToil_EscortRust(escortee, 10f);
			stateGraph.AddToil(lordToil_Escort);
			LordToil_AssaultColonyRust lordToil_Assault = new LordToil_AssaultColonyRust(false, false);
			stateGraph.AddToil(lordToil_Assault);
			LordToil_FleeRust lordToil_Flee = new LordToil_FleeRust(LocomotionUrgency.Jog, true, true);
			stateGraph.AddToil(lordToil_Flee);
			list.Add(lordToil_Escort);
			list.Add(lordToil_Assault);

			Transition transition1 = new Transition(lordToil_Stage, lordToil_Escort);
			transition1.AddTrigger(new Trigger_TicksPassed(1200));
			transition1.AddTrigger(new Trigger_PawnHarmed());
			transition1.AddPostAction(new TransitionAction_WakeAll());
			stateGraph.AddTransition(transition1);

			Transition transition2 = new Transition(lordToil_Escort, lordToil_Assault);
			transition2.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: false));
			transition2.AddTrigger(new Trigger_Custom((TriggerSignal signal) => ((signal.type == TriggerSignalType.BuildingDamaged || signal.type == TriggerSignalType.BuildingLost) && signal.thing is Building b && b.GetLord() == lord)));
			stateGraph.AddTransition(transition2);

			Transition transition3 = new Transition(lordToil_Assault, lordToil_Escort);
			transition3.AddTrigger(new Trigger_TicksPassedWithoutHarm(2500));
			stateGraph.AddTransition(transition3);

			Transition transition4 = new Transition(lordToil_Stage, lordToil_Flee);
			transition4.AddSources(list);
			transition4.AddTrigger(new Trigger_Custom((TriggerSignal signal) => signal.type == TriggerSignalType.Tick && escortee.Dead && Find.TickManager.TicksGame - escortee.TickDeSpawned > 10));
			transition4.AddPreAction(new TransitionAction_Message("MessageFightersFleeing".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst(), "NAT_RustedArmy".Translate())));
			stateGraph.AddTransition(transition4);

			if (canLeave)
			{
				LordToil_ExitMapRust lordToil_ExitMap = new LordToil_ExitMapRust(LocomotionUrgency.Jog, canDig: false, interruptCurrentJob: true) { useAvoidGrid = true };
				stateGraph.AddToil(lordToil_ExitMap);
				Transition transition5 = new Transition(lordToil_Stage, lordToil_ExitMap);
				transition5.AddSources(list);
				transition5.AddTrigger(new Trigger_TicksPassedWithoutHarm(5000).WithFilter(new TriggerFilter_VictoryRust()));
				transition5.AddPreAction(new TransitionAction_Message("MessageRaidersSatisfiedLeaving".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst(), "NAT_RustedArmy".Translate())));
				stateGraph.AddTransition(transition5);
			}

			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_References.Look(ref escortee, "escortee");
			//Scribe_Values.Look(ref leaveIfEscorteeDestroyed, "leaveIfEscorteeDestroyed");
			Scribe_Values.Look(ref canLeave, "canLeave", defaultValue: true);
		}
	}

	public class LordJob_SiegeRust : LordJob
	{
		public Thing besiegeWeapon;

		public int aggroTicks = -1;

		public bool leaveIfWeaponDestroyed = true;

		public override bool GuiltyOnDowned => true;

		public LordJob_SiegeRust()
		{
		}

		public LordJob_SiegeRust(Thing besiegeWeapon, int aggroTicks = -1, bool leaveIfWeaponDestroyed = true)
		{
			this.besiegeWeapon = besiegeWeapon;
			this.leaveIfWeaponDestroyed = leaveIfWeaponDestroyed;
			this.aggroTicks = aggroTicks;
		}

		public override StateGraph CreateGraph()
		{
			StateGraph stateGraph = new StateGraph();

			LordToil_StageRust lordToil_Stage = new LordToil_StageRust(besiegeWeapon.PositionHeld);
			stateGraph.AddToil(lordToil_Stage);
			stateGraph.StartingToil = lordToil_Stage;

			LordToil_AssaultColonyRust lordToil_Assault = new LordToil_AssaultColonyRust(false, false);
			stateGraph.AddToil(lordToil_Assault);

			LordToil_AssaultColonyRust lordToil_AssaultPermanent = new LordToil_AssaultColonyRust(false, false);
			stateGraph.AddToil(lordToil_AssaultPermanent);

			LordToil_FleeRust lordToil_Flee = new LordToil_FleeRust(LocomotionUrgency.Jog, true, true);
			stateGraph.AddToil(lordToil_Flee);

			if(aggroTicks > 0)
			{
				Transition transition2 = new Transition(lordToil_Stage, lordToil_Assault);
				transition2.AddTrigger(new Trigger_PawnHarmed(1f, requireInstigatorWithFaction: false));
				transition2.AddTrigger(new Trigger_Custom((TriggerSignal signal) => ((signal.type == TriggerSignalType.BuildingDamaged || signal.type == TriggerSignalType.BuildingLost) && signal.thing is Building b && b.GetLord() == lord)));
				stateGraph.AddTransition(transition2);

				Transition transition3 = new Transition(lordToil_Assault, lordToil_Stage);
				transition3.AddTrigger(new Trigger_TicksPassedWithoutHarm(aggroTicks));
				stateGraph.AddTransition(transition3);
			}

			if (leaveIfWeaponDestroyed)
			{
				Transition transition4 = new Transition(lordToil_Assault, lordToil_Flee);
				transition4.AddSources(Gen.YieldSingle(lordToil_Stage));
				transition4.AddTrigger(new Trigger_Custom((TriggerSignal signal) => signal.type == TriggerSignalType.Tick && besiegeWeapon.DestroyedOrNull() && Find.TickManager.TicksGame - besiegeWeapon.TickDeSpawned > 10));
				transition4.AddPreAction(new TransitionAction_Message("MessageFightersFleeing".Translate("NAT_RustedSoldiers".Translate().CapitalizeFirst(), "NAT_RustedArmy".Translate())));
				stateGraph.AddTransition(transition4);
			}
			else
			{
				Transition transition4 = new Transition(lordToil_Stage, lordToil_AssaultPermanent);
				transition4.AddSources(Gen.YieldSingle(lordToil_Assault));
				transition4.AddTrigger(new Trigger_Custom((TriggerSignal signal) => signal.type == TriggerSignalType.Tick && besiegeWeapon.DestroyedOrNull() && Find.TickManager.TicksGame - besiegeWeapon.TickDeSpawned > 10));
				transition4.AddPreAction(new TransitionAction_Message("MessageDefendersAttacking".Translate("NAT_RustedSoldiers".Translate(), "NAT_RustedArmy".Translate(), Faction.OfPlayer.def.pawnsPlural).CapitalizeFirst()));
				stateGraph.AddTransition(transition4);
			}
			return stateGraph;
		}

		public override void ExposeData()
		{
			Scribe_References.Look(ref besiegeWeapon, "besiegeWeapon");
			Scribe_Values.Look(ref aggroTicks, "aggroTicks");
			Scribe_Values.Look(ref leaveIfWeaponDestroyed, "leaveIfWeaponDestroyed", defaultValue: true);
		}
	}
}