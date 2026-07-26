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
using static HarmonyLib.Code;
using static System.Net.Mime.MediaTypeNames;

namespace NAT
{
	public class CompProperties_UsableByRust : CompProperties
	{
		public string jobReport = null;

		public int useDuration = 80;

		public HediffDef hediff;

		public bool replaceHediff = true;

		public BodyPartDef bodyPart = null;

		public int? duration;

		public float? severity;

		public float restOffset = 0f;

		public int unitOffset = 0;

		public bool destroyAfterUse = true;

		public EffecterDef useEffect;

		public bool combatEnhancing = false;

		public List<SkillGain> skillGains = new List<SkillGain>();

		public int maxSkillLevel = -1;

		public CompProperties_UsableByRust()
		{
			compClass = typeof(CompUsableByRust);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			if(hediff != null)
			{
				if (parentDef.descriptionHyperlinks.NullOrEmpty())
				{
					parentDef.descriptionHyperlinks = new List<DefHyperlink>();
				}
				parentDef.descriptionHyperlinks.Add(new DefHyperlink(hediff));
			}
		}

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
			foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
			{
				yield return item;
			}
			HediffCompProperties_Disappears comp = hediff?.CompProps<HediffCompProperties_Disappears>();
			if (comp != null)
			{
				yield return new StatDrawEntry(StatCategoryDefOf.CapacityEffects, "StatsReport_SerumDuration".Translate(), comp.disappearsAfterTicks.min.ToStringTicksToPeriod(), "StatsReport_SerumDuration_Desc".Translate(), 9000);
			}
			if (restOffset != 0f)
			{
				string text = ((restOffset > 0f) ? "+" : "-");
				yield return new StatDrawEntry(StatCategoryDefOf.CapacityEffects, NATRADefOf.NAT_RustRest.LabelCap, text + restOffset.ToStringPercent(), NATRADefOf.NAT_RustRest.description, 4050);
			}
			if (!skillGains.NullOrEmpty())
			{
				foreach (SkillGain s in skillGains)
				{
					yield return new StatDrawEntry(StatCategoryDefOf.CapacityEffects, s.skill.LabelCap, "+" + s.amount.ToString() + ", (" + "MaxValue".Translate(maxSkillLevel) + ")", s.skill.description, 4080);
				}
			}
			if(hediff != null && !hediff.stages.NullOrEmpty())
			{
				foreach (StatDrawEntry item in hediff.stages[0].SpecialDisplayStats())
				{
					yield return item;
				}
			}
		}
    }

	public class CompUsableByRust : ThingComp
	{
		public CompProperties_UsableByRust Props => (CompProperties_UsableByRust)props;

		public virtual string JobReport => Props.jobReport ?? "NAT_UseItem".Translate();

		public virtual AcceptanceReport CanBeUsedBy(RustedPawn rust)
		{
			if (!rust.Comp.Props.buffable)
			{
				return false;
			}
			if (Props.hediff != null)
			{
				if(Props.bodyPart != null && rust.RaceProps.body.GetPartsWithDef(Props.bodyPart).FirstOrFallback() == null)
				{
					return "InstallImplantNoBodyPart".Translate() + ": " + Props.bodyPart.LabelShort;
				}
				Hediff hediff = rust.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
				if(hediff != null)
				{
					if (Props.severity != null && (hediff.Severity >= hediff.def.maxSeverity || hediff.Severity >= hediff.def.stages.Last().minSeverity))
					{
						return "InstallImplantAlreadyMaxLevel".Translate();
					}
				}
			}
			if(!Props.skillGains.NullOrEmpty())
			{
				if(rust.skills == null)
				{
					return false;
				}
				bool flag = true;
				foreach(SkillGain item in Props.skillGains)
				{
					if(rust.skills.GetSkill(item.skill).Level < Props.maxSkillLevel)
					{
						flag = false;
					}
				}
				if (flag)
				{
					return false;
				}
			}
			return true;
		}

		public bool ShouldUseForCombat(RustedPawn r)
        {
            if (!Props.combatEnhancing)
            {
				return false;
            }
			if(Props.hediff != null && r.health.hediffSet.HasHediff(Props.hediff))
            {
				return false;
            }
			return true;
        }

		public virtual void UsedBy(RustedPawn rust)
		{
			if (!Props.skillGains.NullOrEmpty() && rust.skills != null)
			{
				foreach (SkillGain s in Props.skillGains)
				{
					SkillRecord skill = rust.skills.GetSkill(s.skill);
					if(skill.Level >= Props.maxSkillLevel)
					{
						continue;
					}
					skill.Level = Mathf.Min(skill.Level + s.amount, skill.Level);
				}
			}
			if (Props.restOffset > 0f && rust.restNeed != null)
            {
				rust.restNeed.CurLevel += Props.restOffset;
			}
			if (Props.unitOffset > 0 && rust.TryGetComp<CompRustedCommander>(out var comp) && (comp.units + Props.unitOffset) <= comp.Props.maxUnits)
			{
				comp.units += Props.unitOffset;
			}
			if (Props.hediff != null)
			{
				Hediff hediff = rust.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
				if(hediff == null)
				{
					hediff = rust.health.AddHediff(Props.hediff, Props.bodyPart == null ? null : rust.RaceProps.body.GetPartsWithDef(Props.bodyPart).FirstOrFallback());
					if (Props.severity != null)
					{
						hediff.Severity = Props.severity.Value;
					}
				}
				else
				{
					if (Props.severity != null)
					{
						if (hediff is Hediff_Level level)
						{
							level.ChangeLevel((int)Props.severity.Value);
						}
						else
						{
							if (Props.replaceHediff)
							{
								hediff.Severity = Props.severity.Value;
							}
							else
							{
								hediff.Severity += Props.severity.Value;
							}
						}
					}
				}
				if (Props.duration != null)
				{
					hediff.TryGetComp<HediffComp_Disappears>()?.SetDuration(Props.duration.Value);
				}
			}
			if(Props.useEffect != null && rust.SpawnedOrAnyParentSpawned)
            {
				Props.useEffect.SpawnAttached(rust.SpawnedParentOrMe, rust.MapHeld);
			}
            if (Props.destroyAfterUse)
            {
				if(parent.stackCount > 1)
                {
					parent.SplitOff(1).Destroy();
				}
                else
                {
					parent.Destroy();
				}
            }
		}
	}
}