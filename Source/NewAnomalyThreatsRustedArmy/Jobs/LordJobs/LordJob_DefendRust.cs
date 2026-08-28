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
}