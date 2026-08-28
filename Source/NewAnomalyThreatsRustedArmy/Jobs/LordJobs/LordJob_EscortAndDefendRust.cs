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
}