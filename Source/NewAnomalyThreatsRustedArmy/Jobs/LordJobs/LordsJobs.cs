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