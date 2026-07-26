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
	public class CompProperties_RustedShield : CompProperties
	{
		public int maxHealth;

		public int regenInterval;

		public int ticksToRestore;

		public SoundDef destroyedSound;

		public EffecterDef destroyedEffect;

		public PawnRenderNodeProperties renderProps;

		public List<StatModifier> statFactorsInactive = new List<StatModifier>();

		public List<StatModifier> statFactors = new List<StatModifier>();

		public FloatRange effectorOffsetRange = new FloatRange(-0.4f, 0.4f);

		public bool combatExtendedArmor = false;

		public CompProperties_RustedShield()
		{
			compClass = typeof(CompRustedShield);
		}
	}
	public class CompRustedShield : ThingComp
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

		private int lastDamageCheckTick = -99999;

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (!active || destroyed || !dinfo.Def.ExternalViolenceFor(parent))
			{
				return;
			}
			Pawn pawn = Owner;
			bool spawnedOrAnyParentSpawned = pawn.SpawnedOrAnyParentSpawned;
			if (spawnedOrAnyParentSpawned && pawn.jobs != null)
			{
				Job job = pawn.CurJob;
				if(job != null && dinfo.Def.canInterruptJobs && !job.playerForced && Find.TickManager.TicksGame >= lastDamageCheckTick + 180)
				{
					Thing instigator = dinfo.Instigator;
					if (job.def.checkOverrideOnDamage == CheckJobOverrideOnDamageMode.Always || (job.def.checkOverrideOnDamage == CheckJobOverrideOnDamageMode.OnlyIfInstigatorNotJobTarget && !job.AnyTargetIs(instigator)))
					{
						lastDamageCheckTick = Find.TickManager.TicksGame;
						pawn.jobs?.CheckForJobOverride();
					}
				}
			}
			if (dinfo.Def.armorCategory != null)
			{
				StatDef armorRatingStat = dinfo.Def.armorCategory.armorRatingStat;
				float armorPenetration = dinfo.ArmorPenetrationInt;
				float armorRating = parent.GetStatValue(armorRatingStat);
				bool diminished = false;
				if (Props.combatExtendedArmor)
				{
					if (armorPenetration < armorRating)
					{
						absorbed = true;
					}
				}
				else
				{
					float num = Mathf.Max(armorRating - armorPenetration, 0f);
					float value = Rand.Value;
					float num2 = num * 0.5f;
					float num3 = num;
					if (value < num2)
					{
						absorbed = true;
					}
					else if (value < num3)
					{
						dinfo.SetAmount(GenMath.RoundRandom(dinfo.Amount / 2f));
						diminished = true;
					}
				}
				if (spawnedOrAnyParentSpawned)
				{
					if (absorbed || diminished)
					{
						EffecterDef effecterDef = (absorbed ? (dinfo.Def.canUseDeflectMetalEffect ? ((dinfo.Def != DamageDefOf.Bullet) ? EffecterDefOf.Deflect_Metal : EffecterDefOf.Deflect_Metal_Bullet) : ((dinfo.Def != DamageDefOf.Bullet) ? EffecterDefOf.Deflect_General : EffecterDefOf.Deflect_General_Bullet)) : EffecterDefOf.DamageDiminished_Metal);
						if (pawn.health.deflectionEffecter == null || pawn.health.deflectionEffecter.def != effecterDef)
						{
							if (pawn.health.deflectionEffecter != null)
							{
								pawn.health.deflectionEffecter.Cleanup();
								pawn.health.deflectionEffecter = null;
							}
							pawn.health.deflectionEffecter = effecterDef.Spawn();
						}
						TargetInfo targetInfo = new TargetInfo(pawn.Position, pawn.MapHeld);
						Effecter deflectionEffecter = pawn.health.deflectionEffecter;
						Thing instigator = dinfo.Instigator;
						deflectionEffecter.Trigger(targetInfo, (instigator != null) ? ((TargetInfo)instigator) : targetInfo);
						if (absorbed)
						{
							pawn.Drawer.Notify_DamageDeflected(dinfo);
							return;
						}
					}
					else
					{
						LifeStageUtility.PlayNearestLifestageSound(pawn, (LifeStageAge lifeStage) => lifeStage.soundWounded, null, null, 0.7f);
						pawn.Drawer.Notify_DamageApplied(dinfo);
						EffecterDef damageEffecter = pawn.RaceProps.FleshType.damageEffecter;
						if (damageEffecter != null)
						{
							if (pawn.health.woundedEffecter != null && pawn.health.woundedEffecter.def != damageEffecter)
							{
								pawn.health.woundedEffecter.Cleanup();
							}
							pawn.health.woundedEffecter = damageEffecter.Spawn();
							pawn.health.woundedEffecter.Trigger(pawn, dinfo.Instigator ?? pawn);
						}
						if (dinfo.Def.damageEffecter != null)
						{
							Effecter effecter = dinfo.Def.damageEffecter.Spawn();
							effecter.Trigger(pawn, pawn);
							effecter.Cleanup();
						}
					}
				}
			}
			if (dinfo.Def != DamageDefOf.EMP && dinfo.Def.harmsHealth)
			{
				absorbed = true;
				float damage = dinfo.Amount * pawn.GetStatValue(StatDefOf.IncomingDamageFactor);
				health -= damage;
				pawn.records.AddTo(RecordDefOf.DamageTaken, damage);
				if (dinfo.Instigator is Pawn pawn2)
				{
					pawn2.records.AddTo(RecordDefOf.DamageDealt, damage);
				}
				pawn.mindState.Notify_DamageTaken(dinfo);
				pawn.GetLord()?.Notify_PawnDamaged(pawn, dinfo);
				if (health <= 0)
				{
					Destroy();
				}
				if (dinfo.Def.makesBlood && Rand.Chance(0.5f))
				{
					pawn.health.DropBloodFilth();
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