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
using static Verse.DamageWorker;

namespace NAT
{
	public class CompProperties_RustedShield : CompProperties_Armor
	{
		public int maxHealth;

		public int regenInterval;

		public int ticksToRestore;

		public SoundDef destroyedSound;

		public EffecterDef destroyedEffect;

		public PawnRenderNodeProperties renderProps;

		public List<StatModifier> statFactorsInactive = new List<StatModifier>();

		public List<StatModifier> statFactors = new List<StatModifier>();

		public CompProperties_RustedShield()
		{
			compClass = typeof(CompRustedShield);
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			return Enumerable.Empty<StatDrawEntry>();
		}
	}
	public class CompRustedShield : CompArmor
	{
		public float health = -1;

		public int ticksToRegen = -1;

		public int ticksSinceDestroyed = -1;

		public bool destroyed = false;

		public bool active = true;

		public CompProperties_RustedShield Props => (CompProperties_RustedShield)props;

		public RustedPawn Owner => parent as RustedPawn;

		public override void PostPostMake()
		{
			base.PostPostMake();
			if (health == -1 && !destroyed)
			{
				health = Props.maxHealth;
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			OverrideBodySize();
		}

		private void OverrideBodySize()
		{
			if (active && !destroyed)
			{
				Owner.bodySizeOverride = 1f;
			}
			else
			{
				Owner.bodySizeOverride = null;
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (!active || Owner == null)
			{
				return;
			}
			if (destroyed)
			{
				ticksSinceDestroyed++;
				if (Props.ticksToRestore <= ticksSinceDestroyed)
				{
					ticksSinceDestroyed = -1;
					health = Props.maxHealth;
					destroyed = false;
					Owner.Drawer.renderer.SetAllGraphicsDirty();
					OverrideBodySize();
				}
			}
			else if (Props.maxHealth > health)
			{
				ticksToRegen++;
				if (ticksToRegen >= Props.regenInterval)
				{
					ticksToRegen = 0;
					health = Mathf.Min(Props.maxHealth, health + 1);
				}
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo g in base.CompGetGizmosExtra())
			{
				yield return g;
			}
			if (!active)
			{
				yield break;
			}
			if (Find.Selector.SingleSelectedThing == parent)
			{
				yield return new RustedShieldGizmo(this);
			}
		}

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (!active || destroyed)
			{
				return;
			}
			base.PostPreApplyDamage(ref dinfo, out absorbed);
			if (dinfo.Def != DamageDefOf.EMP && dinfo.Def.harmsHealth)
			{
				absorbed = true;
				health -= dinfo.Amount;
				if (health <= 0)
				{
					Destroy();
				}
			}
		}

		public void Destroy(bool doEffect = true)
        {
			health = 0;
			ticksSinceDestroyed = 0;
			destroyed = true;
			Owner.Drawer.renderer.SetAllGraphicsDirty();
			if (doEffect && Owner.SpawnedOrAnyParentSpawned)
			{
				Props.destroyedSound?.PlayOneShot(Owner);
				Props.destroyedEffect.Spawn(Owner.PositionHeld, Owner.MapHeld);
			}
			OverrideBodySize();
		}

		public override float GetStatFactor(StatDef stat)
		{
			float num = 1f;
			if (!active || destroyed)
			{
				if (Props.statFactorsInactive != null)
				{
					num *= Props.statFactorsInactive.GetStatFactorFromList(stat);
				}
			}
			else
			{
				if (Props.statFactors != null)
				{
					num *= Props.statFactors.GetStatFactorFromList(stat);
				}
			}
			return num;
		}

		public override List<PawnRenderNode> CompRenderNodes()
		{
			List<PawnRenderNode> list = new List<PawnRenderNode>();
			if (active && !destroyed && Owner != null)
			{
				PawnRenderNodeProperties pawnRenderNodeProperties = Props.renderProps;
				PawnRenderNode pawnRenderNode = (PawnRenderNode)Activator.CreateInstance(Props.renderProps.nodeClass, Owner, pawnRenderNodeProperties, Owner.Drawer.renderer.renderTree);
				list.Add(pawnRenderNode);
			}
			return list;
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref destroyed, "destroyed", false);
			Scribe_Values.Look(ref active, "active", defaultValue: true);
			Scribe_Values.Look(ref health, "health", -1);
			Scribe_Values.Look(ref ticksToRegen, "ticksToRegen", -1);
			Scribe_Values.Look(ref ticksSinceDestroyed, "ticksSinceDestroyed", -1);
		}

		public override void Notify_SignalReceived(Signal signal)
		{
			base.Notify_SignalReceived(signal);
			if (active && signal.tag == "NAT_CreatedByPsychicRitual")
			{
				Destroy(false);
			}
		}
	}
}