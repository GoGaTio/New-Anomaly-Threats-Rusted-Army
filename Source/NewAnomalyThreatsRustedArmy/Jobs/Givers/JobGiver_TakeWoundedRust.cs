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

namespace NAT
{
	public class JobGiver_TakeWoundedRust : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if(pawn.carryTracker.CarriedThing != null)
			{
				return null;
			}
			Pawn pawn2 = ReachableWounded(pawn);
			if (pawn2 == null)
			{
				return null;
			}
			Job job = JobMaker.MakeJob(JobDefOf.CarryDownedPawnDrafted);
			job.targetA = pawn2;
			job.count = 1;
			return job;
		}

		public static Pawn ReachableWounded(Pawn searcher)
		{
			List<Pawn> list = searcher.Map.mapPawns.SpawnedPawnsInFaction(searcher.Faction);
			for (int i = 0; i < list.Count; i++)
			{
				Pawn pawn = list[i];
				if (pawn is RustedPawn rust && pawn.Downed && searcher.CanReserveAndReach(pawn, PathEndMode.OnCell, Danger.Deadly))
				{
					return pawn;
				}
			}
			return null;
		}
	}
}