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
	public class DamageWorker_RustedBomb : DamageWorker_AddInjury
	{
		protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
		{
			if(t == explosion.instigator)
			{
				return;
			}
			base.ExplosionDamageThing(explosion, t, damagedThings, ignoredThings, cell);
		}
		protected override void ExplosionVisualEffectCenter(Explosion explosion)
		{
			for (int i = 0; i < 4; i++)
			{
				ThrowSmoke(explosion.Position.ToVector3Shifted() + Gen.RandomHorizontalVector(explosion.radius * 0.7f), explosion.Map, explosion.radius * 0.6f);
			}
			if (def.explosionCenterFleck != null)
			{
				FleckMaker.Static(explosion.Position.ToVector3Shifted(), explosion.Map, def.explosionCenterFleck);
			}
			else if (def.explosionCenterMote != null)
			{
				MoteMaker.MakeStaticMote(explosion.Position.ToVector3Shifted(), explosion.Map, def.explosionCenterMote);
			}
			if (def.explosionCenterEffecter != null)
			{
				def.explosionCenterEffecter.Spawn(explosion.Position, explosion.Map, Vector3.zero);
			}
			if (def.explosionInteriorMote == null && def.explosionInteriorFleck == null && def.explosionInteriorEffecter == null)
			{
				return;
			}
			int num = Mathf.RoundToInt(Mathf.PI * explosion.radius * explosion.radius / 6f * def.explosionInteriorCellCountMultiplier);
			for (int j = 0; j < num; j++)
			{
				Vector3 vector = Gen.RandomHorizontalVector(explosion.radius * def.explosionInteriorCellDistanceMultiplier);
				if (def.explosionInteriorEffecter != null)
				{
					Vector3 vect = explosion.Position.ToVector3Shifted() + vector;
					def.explosionInteriorEffecter.Spawn(explosion.Position, vect.ToIntVec3(), explosion.Map);
				}
				else if (def.explosionInteriorFleck != null)
				{
					FleckMaker.ThrowExplosionInterior(explosion.Position.ToVector3Shifted() + vector, explosion.Map, def.explosionInteriorFleck);
				}
				else
				{
					MoteMaker.ThrowExplosionInteriorMote(explosion.Position.ToVector3Shifted() + vector, explosion.Map, def.explosionInteriorMote);
				}
			}
		}

		public static void ThrowSmoke(Vector3 loc, Map map, float size)
		{
			if (loc.ShouldSpawnMotesAt(map))
			{
				FleckCreationData dataStatic = FleckMaker.GetDataStatic(loc, map, NATRADefOf.NAT_RustedSmoke, Rand.Range(1.5f, 2.5f) * size);
				dataStatic.rotationRate = Rand.Range(-30f, 30f);
				dataStatic.velocityAngle = Rand.Range(30, 40);
				dataStatic.velocitySpeed = Rand.Range(0.5f, 0.7f);
				map.flecks.CreateFleck(dataStatic);
			}
		}
	}
	public class DamageWorker_HateVaporize : DamageWorker_RustedBomb
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
				DamageWorker_RustedBomb.ThrowSmoke(c.ToVector3Shifted(), explosion.Map, 2f);
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
