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
	public class JobDriver_DanceRust : JobDriver
	{
		private bool jumping;

		private int moveChangeInterval = 240;

		public int AgeTicks => Find.TickManager.TicksGame - startTick;

		public override Vector3 ForcedBodyOffset
		{
			get
			{
				float num = Mathf.Sin((float)AgeTicks / 60f * 16f);
				if (jumping)
				{
					float z = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
					return new Vector3(0f, 0f, z);
				}
				float num2 = Mathf.Sign(num);
				return new Vector3(EasingFunctions.EaseInOutQuad(Mathf.Abs(num) * 0.6f) * 0.09f * num2, 0f, 0f);
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			pawn.Rotation = Rot4.Random;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
			jumping = Rand.Bool;
			toil.tickIntervalAction = delegate
			{
				if (AgeTicks % moveChangeInterval == 0)
				{
					jumping = !jumping;
				}
				if (AgeTicks % 120 == 0 && !jumping)
				{
					pawn.Rotation = Rot4.Random;
				}
			};
			toil.defaultCompleteMode = ToilCompleteMode.Never;
			toil.handlingFacing = true;
			yield return toil;
		}
	}
}