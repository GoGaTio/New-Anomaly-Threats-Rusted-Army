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
using System.Security.Cryptography;
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
	public class CompProperties_AbilityArtOfWar : CompProperties_AbilityEffect
	{
		public CompProperties_AbilityArtOfWar()
		{
			compClass = typeof(CompAbilityEffect_ArtOfWar);
		}
	}
	public class CompAbilityEffect_ArtOfWar : CompAbilityEffect
	{
		public class ArtOfWarTarget : IExposable
		{
			public Pawn pawn;

			public void ExposeData()
			{
				Scribe_References.Look(ref pawn, "pawn");
			}
		}
		public new CompProperties_AbilityArtOfWar Props => (CompProperties_AbilityArtOfWar)props;

		public List<ArtOfWarTarget> targets = new List<ArtOfWarTarget>();

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
		}

		public override void CompTick()
		{
			base.CompTick();
		}
    }
}