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
	public class JobDriver_TurretSetUp : JobDriver
	{
		public int AgeTicks => Find.TickManager.TicksGame - startTick;

		private IntVec3 Cell => job.GetTarget(TargetIndex.A).Cell;

		public CompRustedTurretPawn Comp => pawn.GetComp<CompRustedTurretPawn>();

		public override Vector3 ForcedBodyOffset
		{
			get
			{
				if(CurToilIndex == 1 && Comp.Props.buildingDef.Size.x == 2)
				{
					return Vector3.one * 0.5f * ((180f - (float)ticksLeftThisToil) / 180f);
				}
				return Vector3.zero;
			}
		}

		public override void Notify_Starting()
		{
			if(job.targetA == null)
			{
				job.SetTarget(TargetIndex.A, pawn.Position);
			}
			base.Notify_Starting();
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
			Toil toil1 = ToilMaker.MakeToil("Wait");
			toil1.initAction = delegate
			{
				toil1.actor.pather.StopDead();
			};
			toil1.defaultCompleteMode = ToilCompleteMode.Delay;
			toil1.defaultDuration = 180;
			toil1.WithProgressBarToilDelay(TargetIndex.None, 180);
			yield return toil1;
			Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
			toil2.initAction = delegate
			{
				if (!Comp.RectOccupied)
				{
					Log.Message(toil2.actor.ToString() + ":SpawnedTurret");
					Comp.SpawnTurret(Cell, toil2.actor.Map);
				}
			};
			toil2.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil2;
		}
	}
}