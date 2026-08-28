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
	public class LordToil_EscortRust : LordToil
	{
		public Pawn escortee;

		public float followRadius = 7f;

		public override IntVec3 FlagLoc => escortee?.SpawnedOrAnyParentSpawned == true ? escortee.PositionHeld : base.FlagLoc;

		public LordToil_EscortRust(Pawn escortee, float followRadius = 7f)
		{
			this.escortee = escortee;
			this.followRadius = followRadius;
		}

		public override void UpdateAllDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				if(lord.ownedPawns[i] == escortee)
				{
					PawnDuty escorteeDuty = new PawnDuty(NATRADefOf.NAT_RustAssaultColony);
					escortee.mindState.duty = escorteeDuty;
					continue;
				}
				PawnDuty duty = new PawnDuty(DutyDefOf.Escort, escortee, followRadius);
				lord.ownedPawns[i].mindState.duty = duty;
			}
		}
	}
}