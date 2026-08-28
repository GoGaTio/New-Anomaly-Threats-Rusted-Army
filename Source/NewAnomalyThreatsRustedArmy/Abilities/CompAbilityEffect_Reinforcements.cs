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
	public class CompProperties_AbilityReinforcements : CompProperties_AbilityEffect
	{
		public float missRadius;

		public List<PawnKindDefCount> kindDefs = new List<PawnKindDefCount>();

		public List<ThingDefCountClass> thingDefs = new List<ThingDefCountClass>();

		public CompProperties_AbilityReinforcements()
		{
			compClass = typeof(CompAbilityEffect_Reinforcements);
		}
	}
	public class CompAbilityEffect_Reinforcements : CompAbilityEffect
	{
		public new CompProperties_AbilityReinforcements Props => (CompProperties_AbilityReinforcements)props;

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			Map map = parent.pawn.Map;
			IntVec3 cell = target.Cell;
			IntVec3 dropCell = cell;
			List<Thing> list = new List<Thing>();
			List<Skyfaller_RustedChunk> skyfallers = new List<Skyfaller_RustedChunk>();
			Faction faction = parent.pawn.Faction ?? Faction.OfEntities;
			Lord lord = parent.pawn.GetLord();
			foreach (PawnKindDefCount p in Props.kindDefs)
            {
				ThingDef skyfaller = RustedArmyUtility.GetSkyfaller(p.kindDef.race);
				for (int i = 0; i < p.count; i++)
				{
					Pawn pawn = PawnGenerator.GeneratePawn(p.kindDef, faction);
					if(lord != null)
                    {
						lord.AddPawn(pawn);
                    }
					list.Add(pawn);
					if(!CellFinder.TryFindRandomCellNear(target.Cell, map, GenMath.RoundRandom(Props.missRadius), (c)=>c.GetRoof(map)?.isThickRoof != true, out dropCell))
					{
						dropCell = cell;
					}
					Skyfaller_RustedChunk chunk = SkyfallerMaker.SpawnSkyfaller(skyfaller, pawn, dropCell, map) as Skyfaller_RustedChunk;
					chunk.faction = faction;
					skyfallers.Add(chunk);
				}
			}
			foreach (ThingDefCountClass t in Props.thingDefs)
			{
				ThingDef skyfaller = RustedArmyUtility.GetBaseSkyfaller(t.thingDef);
				for (int i = 0; i < t.count; i++)
				{
					Thing thing = ThingMaker.MakeThing(t.thingDef);
					thing.SetFaction(faction);
					list.Add(thing);
					if (lord != null && thing is Building b)
					{
						lord.AddBuilding(b);
					}
					if (!CellFinder.TryFindRandomCellNear(target.Cell, map, GenMath.RoundRandom(Props.missRadius), (c) => c.GetRoof(map)?.isThickRoof != true, out dropCell))
					{
						dropCell = cell;
					}
					Skyfaller_RustedChunk chunk = SkyfallerMaker.SpawnSkyfaller(skyfaller, thing, dropCell, map) as Skyfaller_RustedChunk;
					chunk.faction = faction;
					skyfallers.Add(chunk);
				}
			}
			foreach(Skyfaller_RustedChunk unit in skyfallers)
            {
				unit.frendlies = list;
			}
			Messages.Message("NAT_DropRequested".Translate(parent.pawn.LabelCap), list, MessageTypeDefOf.ThreatBig);
			base.Apply(target, dest);
		}

		public override void DrawEffectPreview(LocalTargetInfo target)
		{
			GenDraw.DrawRadiusRing(target.Cell, Props.missRadius);
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			IntVec3 cell = target.Cell;
			if (!cell.IsValid)
			{
				return false;
			}
			if (!cell.Standable(parent.pawn.Map))
			{
				return false;
			}
			if (cell.Filled(parent.pawn.Map))
			{
				return false;
			}
			if (cell.GetRoof(parent.pawn.Map)?.isThickRoof == true)
			{
				return false;
			}
			return base.Valid(target, throwMessages);
		}

		public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
		{
			return Valid(target, false);
		}

		public override bool GizmoDisabled(out string reason)
		{
			if (parent.pawn.Map.generatorDef.isUnderground)
			{
				reason = "CannotUseReason_PocketMap".Translate(parent.pawn.MapHeld.generatorDef.label);
				return true;
			}
			return base.GizmoDisabled(out reason);
		}
	}
}