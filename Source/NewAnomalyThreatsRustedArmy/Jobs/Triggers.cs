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
}