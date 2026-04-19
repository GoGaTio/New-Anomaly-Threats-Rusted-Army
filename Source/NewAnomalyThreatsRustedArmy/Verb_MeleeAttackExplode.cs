using NAT;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace NAT
{
	public class Verb_MeleeAttackExplode : Verb_MeleeAttack
	{
		protected void Explode(ProjectileProperties props, LocalTargetInfo target)
		{
			Map map = Caster.Map;
			IntVec3 position = target.Cell;
			if (props.explosionEffect != null)
			{
				Effecter effecter = props.explosionEffect.Spawn();
				if (props.explosionEffectLifetimeTicks != 0)
				{
					map.effecterMaintainer.AddEffecterToMaintain(effecter, position, props.explosionEffectLifetimeTicks);
				}
				else
				{
					effecter.Trigger(new TargetInfo(position, map), new TargetInfo(position, map));
					effecter.Cleanup();
				}
			}
			float explosionRadius = props.explosionRadius;
			DamageDef damageDef = props.damageDef;
			Thing instigator = Caster;
			int damageAmount = props.GetDamageAmount(EquipmentSource);
			float armorPenetration = props.GetArmorPenetration(EquipmentSource);
			SoundDef soundExplode = props.soundExplode;
			ThingDef weapon = EquipmentSource.def;
			Thing thing = target.Thing;
			ThingDef postExplosionSpawnThingDef = props.postExplosionSpawnThingDef ?? (props.explosionSpawnsSingleFilth ? null : props.filth);
			ThingDef postExplosionSpawnThingDefWater = props.postExplosionSpawnThingDefWater;
			float postExplosionSpawnChance = props.postExplosionSpawnChance;
			int postExplosionSpawnThingCount = props.postExplosionSpawnThingCount;
			GasType? postExplosionGasType = props.postExplosionGasType;
			ThingDef preExplosionSpawnThingDef = props.preExplosionSpawnThingDef;
			float preExplosionSpawnChance = props.preExplosionSpawnChance;
			int preExplosionSpawnThingCount = props.preExplosionSpawnThingCount;
			bool applyDamageToExplosionCellsNeighbors = props.applyDamageToExplosionCellsNeighbors;
			float explosionChanceToStartFire = props.explosionChanceToStartFire;
			bool explosionDamageFalloff = props.explosionDamageFalloff;
			float? direction = Caster.DrawPos.AngleToFlat(target.CenterVector3);
			float expolosionPropagationSpeed = props.damageDef.expolosionPropagationSpeed;
			float screenShakeFactor = props.screenShakeFactor;
			bool doExplosionVFX = props.doExplosionVFX;
			ThingDef preExplosionSpawnSingleThingDef = props.preExplosionSpawnSingleThingDef;
			ThingDef postExplosionSpawnSingleThingDef = props.postExplosionSpawnSingleThingDef;
			GenExplosion.DoExplosion(position, map, explosionRadius, damageDef, instigator, damageAmount, armorPenetration, soundExplode, weapon, null, thing, postExplosionSpawnThingDef, postExplosionSpawnChance, postExplosionSpawnThingCount, postExplosionGasType, null, 255, applyDamageToExplosionCellsNeighbors, preExplosionSpawnThingDef, preExplosionSpawnChance, preExplosionSpawnThingCount, explosionChanceToStartFire, explosionDamageFalloff, direction, null, null, doExplosionVFX, expolosionPropagationSpeed, 0f, doSoundEffects: true, postExplosionSpawnThingDefWater, screenShakeFactor, null, null, postExplosionSpawnSingleThingDef, preExplosionSpawnSingleThingDef);
			if (props.explosionSpawnsSingleFilth && props.filth != null && props.filthCount.TrueMax > 0 && Rand.Chance(props.filthChance) && !position.Filled(map))
			{
				FilthMaker.TryMakeFilth(position, map, props.filth, props.filthCount.RandomInRange);
			}
		}
		protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
		{
			DamageWorker.DamageResult result = new DamageWorker.DamageResult();
			ThingDef source = base.EquipmentSource.def;
			ThingDef def = EquipmentSource.def.Verbs.FirstOrDefault(x => x.defaultProjectile != null)?.defaultProjectile;
			if (def != null)
			{
				Explode(def.projectile, target);
			}
			return result;
		}

		protected override bool TryCastShot()
		{
			if (base.TryCastShot())
			{
				if (burstShotsLeft <= 1)
				{
					SelfConsume();
				}
				return true;
			}
			if (burstShotsLeft < base.BurstShotCount)
			{
				SelfConsume();
			}
			return false;
		}

		private void SelfConsume()
		{
			if (base.EquipmentSource != null && !base.EquipmentSource.Destroyed)
			{
				base.EquipmentSource.Destroy();
			}
		}
	}
}
