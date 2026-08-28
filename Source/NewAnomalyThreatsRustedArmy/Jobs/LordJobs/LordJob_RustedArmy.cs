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
}