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
}