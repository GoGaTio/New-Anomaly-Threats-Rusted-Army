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
	public enum CastLocation
	{
		OnTarget,
		Turret,
		BehindTarget,
		NearCaster
	}

	[Flags]
	public enum CastCondition
	{
		None,
		EnemyNearby,
		OverwhelmingEnemies,
		Attacking
	}

	public class CastParms
	{
		public CastCondition conditions;

		public CastLocation location;

		public float weight = 1f;

		public Ability ability;

		public CastParms()
        {

        }

		public CastParms LinkWithAbility(Ability ability)
        {
			CastParms p = new CastParms();
			p.ability = ability;
			p.conditions = this.conditions;
			p.location = this.location;
			p.weight = this.weight;
			return p;
		}
	}
	public class CompProperties_AbilityRustedCommander : CompProperties_AbilityEffect
	{
		public int cost;

		public List<CastParms> castParms = new List<CastParms>();

		public CompProperties_AbilityRustedCommander()
		{
			compClass = typeof(CompAbilityEffect_RustedCommander);
		}
	}
	public class CompAbilityEffect_RustedCommander : CompAbilityEffect
	{
		public new CompProperties_AbilityRustedCommander Props => (CompProperties_AbilityRustedCommander)props;

		public CompRustedCommander Comp => parent.pawn.GetComp<CompRustedCommander>();

		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
			Comp.units -= Props.cost;
			if(parent.def.groupDef != null)
			{
				int cooldown = Mathf.Max(1, Mathf.RoundToInt(parent.pawn.GetStatValue(NATRADefOf.NAT_ReinforcementsCooldown))) * 60;
				foreach (Ability item in parent.pawn.abilities.AllAbilitiesForReading)
				{
					item.Notify_GroupStartedCooldown(parent.def.groupDef, cooldown);
				}
			}
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (Comp == null || Comp.units < Props.cost)
            {
				reason = "NAT_NoUnits".Translate();
				return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool CanCast => Comp.units >= Props.cost && base.CanCast;

        public IEnumerable<CastParms> CastParms()
        {
			foreach(CastParms p in Props.castParms)
            {
				yield return p.LinkWithAbility(parent);
            }
        }
    }
}