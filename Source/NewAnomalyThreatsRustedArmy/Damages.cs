using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using UnityEngine;
using System.Diagnostics;

namespace NAT
{
	public class DamageWorker_HateVaporize : DamageWorker_AddInjury
	{
		private const float VaporizeRadius = 2.9f;

		private static readonly FloatRange FireSizeRange = new FloatRange(0.4f, 0.8f);

		public override void ExplosionAffectCell(Explosion explosion, IntVec3 c, List<Thing> damagedThings, List<Thing> ignoredThings, bool canThrowMotes)
		{
			bool flag = c.DistanceTo(explosion.Position) <= 2.9f;
			c.GetFirstThing(explosion.Map, ThingDefOf.Filth_FireFoam)?.Destroy();
			base.ExplosionAffectCell(explosion, c, damagedThings, ignoredThings, canThrowMotes && flag);
			if (flag)
			{
				FleckMaker.ThrowSmoke(c.ToVector3Shifted(), explosion.Map, 2f);
			}
		}

		protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
		{
			if (cell.DistanceTo(explosion.Position) <= 2.9f)
			{
				base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
			}
		}

		public override void ExplosionStart(Explosion explosion, List<IntVec3> cellsToAffect)
		{
			base.ExplosionStart(explosion, cellsToAffect);
			Effecter effecter = NATRADefOf.NAT_HateVaporize_Heatwave.Spawn();
			effecter.Trigger(new TargetInfo(explosion.Position, explosion.Map), TargetInfo.Invalid);
			effecter.Cleanup();
		}
	}
}
