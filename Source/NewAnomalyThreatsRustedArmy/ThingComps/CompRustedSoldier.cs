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
using System.Net.NetworkInformation;
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
	public class CompProperties_RustedSoldier : CompProperties
	{
		public InteractionDef interaction;

		public bool canInteract = false;

		public bool isHumanlike = true;

		public bool needSleep = true;

		public bool buffable = true;

		public int interactInterval = 10000;

		public bool canRecieveInteraction = true;

		public bool canEquipWeapons = true;

		public bool hasHead = false;

		public DrawData drawData = new DrawData();

		public float headSize = 1f;

		public List<string> headTags = new List<string>();

		public ThingDef skyfaller;

		public List<string> apparelTagsToAllow = new List<string>();

		public bool canWearApparel = true;

		public BodyTypeDef bodyType;

		public CompProperties_RustedSoldier()
		{
			compClass = typeof(CompRustedSoldier);
		}

        public override void ResolveReferences(ThingDef parentDef)
        {
            base.ResolveReferences(parentDef);
			if(bodyType == null)
            {
				bodyType = BodyTypeDefOf.Thin;
			}
			apparelTagsToAllow.Add("Gunlink");
			apparelTagsToAllow.Add("NAT_Rust_All");
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			if(req.Thing == null || !(req.Thing is RustedPawn rust))
			{
				yield break;
			}
			foreach (StatDef item in DefDatabase<StatDef>.AllDefs.Where((StatDef st) => st == StatDefOf.WorkSpeedGlobal || st == StatDefOf.Mass || st == StatDefOf.MoveSpeed || (st.category == StatCategoryDefOf.PawnCombat && !st.alwaysHide)))
			{
				if (!item.Worker.IsDisabledFor(rust))
				{
					float statValue = rust.GetStatValue(item);
					if (item.showOnDefaultValue || statValue != item.defaultBaseValue)
					{
						StatDrawEntry entry = new StatDrawEntry(item.category, item, statValue, req);
						entry.overridesHideStats = true;
						yield return entry;
					}
				}
				else
				{
					yield return new StatDrawEntry(item.category, item);
				}
			}
		}
    }
	public class CompRustedSoldier : ThingComp
	{
		private bool wantsRandomInteract;

		private int lastInteractionTime = -9999;

		public CompProperties_RustedSoldier Props => (CompProperties_RustedSoldier)props;

		public RustedPawn Rust => parent as RustedPawn;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref wantsRandomInteract, "wantsRandomInteract", defaultValue: false);
			Scribe_Values.Look(ref lastInteractionTime, "lastInteractionTime", -9999);
		}

		public override void CompTickInterval(int delta)
		{
			if (Props.canInteract)
			{
				if (!wantsRandomInteract)
				{
					if (Find.TickManager.TicksGame > lastInteractionTime + 320 && parent.IsHashIntervalTick(60, delta) && Rand.MTBEventOccurs(Props.interactInterval, 1f, 60f) && !TryInteractRandomly())
					{
						wantsRandomInteract = true;
					}
				}
				else if (parent.IsHashIntervalTick(91, delta) && TryInteractRandomly())
				{
					wantsRandomInteract = false;
				}
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (DebugSettings.ShowDevGizmos && parent.Faction != Faction.OfPlayerSilentFail)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: Make player",
					action = delegate
					{
						parent.SetFaction(Faction.OfPlayerSilentFail);
					}
				};
			}
		}

		private bool TryInteractRandomly()
		{
			if (Rust.Map != null && Rust.Faction != null && !Rust.Downed && !Rust.InAggroMentalState && Rust.Awake() && !Rust.IsBurning())
			{
				List<Pawn> pawns = new List<Pawn>();
				List<Pawn> collection = Rust.Map.mapPawns.SpawnedPawnsInFaction(Rust.Faction);
				pawns.AddRange(collection);
				pawns.Shuffle<Pawn>();
				for (int i = 0; i < pawns.Count; i++)
				{
					Pawn p = pawns[i];
					if (p != Rust && !p.Downed && !p.InAggroMentalState && p.Awake() && !p.IsBurning() && ((p is RustedPawn rust && rust.Comp?.Props?.canRecieveInteraction == true) || (p.RaceProps.Humanlike && Rand.Chance(0.2f))) && SocialInteractionUtility.IsGoodPositionForInteraction(Rust, p))
					{
						if (TryInteractWith(p, Props.interaction))
						{
							return true;
						}
					}
				}
			}
			return false;
		}
		public bool TryInteractWith(Pawn recipient, InteractionDef intDef)
		{
			Pawn pawn = this.parent as Pawn;
			List<RulePackDef> list = new List<RulePackDef>();
			string text;
			string str;
			LetterDef letterDef;
			LookTargets lookTargets;
			intDef.Worker.Interacted(pawn, recipient, list, out text, out str, out letterDef, out lookTargets);
			MoteMaker.MakeInteractionBubble(pawn, recipient, intDef.interactionMote, intDef.GetSymbol(pawn.Faction, null), intDef.GetSymbolColor(pawn.Faction));
			PlayLogEntry_Interaction playLogEntry_Interaction = new PlayLogEntry_Interaction(intDef, pawn, recipient, list);
			Find.PlayLog.Add(playLogEntry_Interaction);
			lastInteractionTime = Find.TickManager.TicksGame;
			return true;
		}

		public bool CanWearApparel(Apparel apparel)
        {
			if (!Props.canWearApparel)
			{
				return false;
			}
			if(apparel.def.apparel.LastLayer.IsUtilityLayer || apparel.def.apparel.layers[0] == ApparelLayerDefOf.Belt || apparel.def.apparel.tags.SharesElementWith(Props.apparelTagsToAllow))
            {
				return true;
            }
			return false;
        }

		private float headApparelOffset = 0f;

		private float bodyApparelOffset = 0f;

		public override List<PawnRenderNode> CompRenderNodes()
        {
			List<PawnRenderNode> list = new List<PawnRenderNode>();
			RustedPawn rust = Rust;
			if (rust.apparel == null || rust.apparel.WornApparelCount == 0)
			{
				return list;
			}
			headApparelOffset = 0f;
			bodyApparelOffset = 0f;
			foreach (Apparel item in rust.apparel.WornApparel)
			{
				try
				{
					list.Add(ProcessApparel(item));
				}
				catch (Exception arg)
				{
					Log.Error($"Exception setting up node for {item.def.defName} on {rust}: {arg}");
				}
			}
			return list;
		}

		private PawnRenderNode ProcessApparel(Apparel ap)
		{
			if (ap.def.apparel.HasDefinedGraphicProperties)
			{
				return null;
			}
			PawnRenderNodeProperties pawnRenderNodeProperties = null;
			DrawData drawData = ap.def.apparel.drawData;
			ApparelLayerDef lastLayer = ap.def.apparel.LastLayer;
			bool flag = lastLayer == ApparelLayerDefOf.Overhead || lastLayer == ApparelLayerDefOf.EyeCover;
			bool? flagOffset = null;
			if (ap.def.apparel.parentTagDef == PawnRenderNodeTagDefOf.ApparelHead)
			{
				flag = true;
				flagOffset = true;
			}
			else if (ap.def.apparel.parentTagDef == PawnRenderNodeTagDefOf.ApparelBody)
            {
				flag = false;
				flagOffset = false;
			}
			if (Rust.Head != null && flag)
			{
				pawnRenderNodeProperties = new PawnRenderNodeProperties
				{
					debugLabel = ap.def.defName,
					parentTagDef = PawnRenderNodeTagDefOf.ApparelHead,
					workerClass = typeof(PawnRenderNodeWorker_RustApparel_Head),
					baseLayer = 70f + headApparelOffset,
					drawData = drawData
				};
			}
			else
			{
				pawnRenderNodeProperties = new PawnRenderNodeProperties
				{
					debugLabel = ap.def.defName,
					parentTagDef = PawnRenderNodeTagDefOf.ApparelBody,
					workerClass = typeof(PawnRenderNodeWorker_RustApparel_Body),
					baseLayer = 20f + bodyApparelOffset,
					drawData = drawData
				};
				if (drawData == null && !ap.def.apparel.shellRenderedBehindHead)
				{
					if (lastLayer == ApparelLayerDefOf.Shell)
					{
						pawnRenderNodeProperties.drawData = DrawData.NewWithData(new DrawData.RotationalData(Rot4.North, 88f));
						pawnRenderNodeProperties.oppositeFacingLayerWhenFlipped = true;
					}
					else if (ap.RenderAsPack())
					{
						pawnRenderNodeProperties.drawData = DrawData.NewWithData(new DrawData.RotationalData(Rot4.North, 93f), new DrawData.RotationalData(Rot4.South, -3f));
						pawnRenderNodeProperties.oppositeFacingLayerWhenFlipped = true;
					}
				}
			}
            if (flagOffset == false)
            {
				bodyApparelOffset++;
			}
            else if (flagOffset == true)
			{
				headApparelOffset++;
			}
			pawnRenderNodeProperties.pawnType = PawnRenderNodeProperties.RenderNodePawnType.Any;
			return new PawnRenderNode_RustApparel(Rust, pawnRenderNodeProperties, Rust.Drawer.renderer.renderTree, ap, flag);
		}
	}
}