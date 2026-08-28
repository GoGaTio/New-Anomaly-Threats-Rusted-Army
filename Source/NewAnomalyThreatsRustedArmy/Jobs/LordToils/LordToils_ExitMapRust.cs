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
					comp.SpawnPawn(comp.parent.Position, comp.parent.Map, false);
				}
			}
			base.UpdateAllDuties();
		}
	}

	public class LordToil_FleeRust : LordToil_ExitMapRust
	{
		public override DutyDef ExitDuty => NATRADefOf.NAT_RustFlee;

		public LordToil_FleeRust(LocomotionUrgency locomotion = LocomotionUrgency.None, bool canDig = false, bool interruptCurrentJob = false)
			: base(locomotion, canDig, interruptCurrentJob)
		{
		}

		public override void UpdateAllDuties()
		{
			base.UpdateAllDuties();
			foreach(Pawn p in lord.ownedPawns)
			{
				if(p?.mindState == null)
				{
					continue;
				}
				p.mindState.enemyTarget = null;
			}
		}
	}
}