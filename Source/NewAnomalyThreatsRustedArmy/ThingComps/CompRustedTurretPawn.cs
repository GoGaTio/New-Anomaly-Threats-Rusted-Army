using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
using static HarmonyLib.Code;

namespace NAT
{
	public class CompProperties_RustedTurretPawn : CompProperties
	{
		public PawnKindDef kindDef;

		public ThingDef buildingDef;

		public int hitPoints;

		public int ticksPerHeal;

		public bool combatExtendedArmor;

		public float offsetTop;

		public float offsetBottom;

		public float offsetSide;

		public CompProperties_RustedTurretPawn()
		{
			compClass = typeof(CompRustedTurretPawn);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			if (kindDef != null)
			{
				CompProperties_RustedTurretPawn comp = kindDef.race.GetCompProperties<CompProperties_RustedTurretPawn>();
				comp.buildingDef = parentDef;
				kindDef.combatPower = parentDef.building.combatPower;
				kindDef.label = parentDef.label;
				kindDef.race.label = parentDef.label;
				kindDef.race.description = parentDef.description;
				comp.hitPoints = parentDef.BaseMaxHitPoints;
				comp.ticksPerHeal = parentDef.GetCompProperties<CompProperties_SelfhealHitpoints>()?.ticksPerHeal ?? 1000;
				CompProperties_RustedTurret turret = kindDef.race.GetCompProperties<CompProperties_RustedTurret>();
				turret.turretDef = parentDef.building.turretGunDef;
				turret.foamTurret = typeof(Building_RustedTurretFoam).IsAssignableFrom(parentDef.thingClass);
				turret.Init(kindDef.race);
				kindDef.race.uiIconPath = parentDef.uiIconPath;
				kindDef.race.uiIcon = parentDef.uiIcon;
				kindDef.race.killedLeavingsRanges = parentDef.killedLeavingsRanges.ListFullCopy();
			}
		}
	}
	public class CompRustedTurretPawn : ThingComp, IThingHolder
	{
		public CompProperties_RustedTurretPawn Props => (CompProperties_RustedTurretPawn)props;

		public RustedPawn Rust
		{
			get
			{
				if (innerContainer.Count <= 0)
				{
					return null;
				}
				return innerContainer[0];
			}
			set
			{
				innerContainer.Clear();
				if (value == null)
				{
					return;
				}
				innerContainer.TryAdd(value);
			}
		}

		private CompRustedTurret turret;

		public CompRustedTurret Turret
		{
			get
			{
				if (turret == null)
				{
					turret = parent.GetComp<CompRustedTurret>();
				}
				return turret;
			}
		}

		public CompRustedTurretPawn()
		{
			innerContainer = new ThingOwner<RustedPawn>(this, oneStackOnly: true, LookMode.Deep, removeContentsIfDestroyed: false);
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		protected ThingOwner<RustedPawn> innerContainer;

		private int lastDamageCheckTick = -99999;

		public int health = -1;

		public int ticksToRegen = -1;

		public long ageTicks = 0;

		public bool destroyed = false;

		public CellRect PlaceRect => new CellRect(parent.Position.x, parent.Position.z, Props.buildingDef.Size.x, Props.buildingDef.Size.z);

		public bool RectOccupied => IsRectOccupied(PlaceRect);

		public bool IsRectOccupied(CellRect rect)
		{
			foreach (IntVec3 c in rect)
			{
				if (!c.Standable(parent.Map) || !c.GetAffordances(parent.Map).Contains(TerrainAffordanceDefOf.Light))
				{
					return true;
				}
				foreach (Thing t in c.GetThingList(parent.Map))
				{
					if (t.def.IsEdifice() || t.def.Fillage != FillCategory.None)
					{
						return true;
					}
				}
			}
			return false;
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			if (!respawningAfterLoad && parent is Pawn pawn)
			{
				if (Props.buildingDef.entityCodexEntry == null)
				{
					Find.HiddenItemsManager.SetDiscovered(Props.buildingDef);
				}
				else
				{
					Find.EntityCodex.SetDiscovered(new List<EntityCodexEntryDef>() { Props.buildingDef.entityCodexEntry }, Props.buildingDef, pawn);
				}
			}
			if (parent is Building && Rust == null)
			{
				Rust = PawnGenerator.GeneratePawn(Props.kindDef, parent.Faction) as RustedPawn;
			}
		}

		public override string CompInspectStringExtra()
		{
			if (parent is Pawn)
			{
				return base.CompInspectStringExtra();
			}
			return "AgeIndicator".Translate(((float)Rust.ageTracker.AgeBiologicalTicks / 3600000f).ToStringApproxAge());
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			if ((parent.Faction != Faction.OfPlayer && !DebugSettings.ShowDevGizmos) || !parent.Spawned)
			{
				yield break;
			}
			Command_Action command_Action = new Command_Action();
			if (parent is Pawn pawn)
			{
				command_Action.defaultLabel = "CommandStopForceAttack".Translate();
				command_Action.defaultDesc = "CommandStopForceAttackDesc".Translate();
				command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/NAT_RustedTurret_SetUp");
				command_Action.action = delegate
				{
					if (pawn.jobs != null && pawn.CurJobDef != NATRADefOf.NAT_RustedTurretSetUp && !RectOccupied)
					{
						pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATRADefOf.NAT_RustedTurretSetUp), JobTag.Misc);
					}
				};
				command_Action.onHover = delegate
				{
					if (destroyed)
					{
						return;
					}
					foreach (Pawn p in Find.Selector.SelectedPawns)
					{
						if (p.TryGetComp<CompRustedTurretPawn>(out var comp))
						{
							if (!comp.destroyed)
							{
								GenDraw.DrawFieldEdges(comp.PlaceRect.Cells.ToList(), comp.RectOccupied ? Color.red : Color.white);
							}
						}
					}
				};
			}
			else
			{
				command_Action.defaultLabel = "CommandStopForceAttack".Translate();
				command_Action.defaultDesc = "CommandStopForceAttackDesc".Translate();
				command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/NAT_RustedTurret_SetOff");
				command_Action.action = delegate
				{
					SpawnPawn(parent.Position, parent.Map);
				};
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "MU_InsertMech".Translate() + "...";
				command_Action2.defaultDesc = "MU_InsertMech_Desc".Translate();
				command_Action2.icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire");
				command_Action2.groupable = false;
				command_Action2.action = delegate
				{
					Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), delegate (LocalTargetInfo target)
					{
						SpawnPawn(parent.Position, parent.Map).jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATRADefOf.NAT_RustedTurretSetUp, target), JobTag.Misc);
					}, delegate (LocalTargetInfo targ)
					{
						CellRect cellRect = new CellRect(targ.Cell.x, targ.Cell.z, parent.def.Size.x, parent.def.Size.z);
						bool flag = false;
						Vector3 vec = cellRect.CenterVector3;
						if (!cellRect.InBounds(parent.Map))
						{
							cellRect.ClipInsideMap(parent.Map);
							flag = true;
						}
						if (flag || IsRectOccupied(cellRect))
						{
							GenDraw.DrawFieldEdges(cellRect.ToList(), Color.red);
							GenDraw.DrawLineBetween(vec, parent.TrueCenter(), SimpleColor.Red);
						}
						else
						{
							GenDraw.DrawFieldEdges(cellRect.ToList(), Color.white);
							GenDraw.DrawLineBetween(vec, parent.TrueCenter(), SimpleColor.White);
						}
					}, (t) => t.Cell.InBounds(parent.Map) && !IsRectOccupied(new CellRect(t.Cell.x, t.Cell.z, parent.def.Size.x, parent.def.Size.z)));
				};
				yield return command_Action2;
			}
			yield return command_Action;
		}

		public void SpawnTurret(IntVec3 pos, Map map)
		{
			destroyed = true;
			bool selected = Find.Selector.IsSelected(parent);
			Building_RustedTurret building = ThingMaker.MakeThing(Props.buildingDef) as Building_RustedTurret;
			building.HitPoints = health;
			building.SetFaction(parent.Faction);
			Lord lord = (parent as Pawn).GetLord();
			if (lord != null)
			{
				lord.AddBuilding(building);
			}
			building.SetTarget(Turret.currentTarget, Turret.targetForced);
			parent.DeSpawn();
			building.GetComp<CompRustedTurretPawn>().Rust = (parent as RustedPawn);
			GenSpawn.Spawn(building, pos, map, WipeMode.VanishOrMoveAside);
			building.Top.CurRotation = Turret.CurRotation - Turret.Props.angleOffset;
			if (selected)
			{
				Find.Selector.Select(building, false, false);
			}
		}

		public Pawn SpawnPawn(IntVec3 pos, Map map)
		{
			destroyed = true;
			bool selected = Find.Selector.IsSelected(parent);
			Pawn pawn = Rust;
			CompRustedTurretPawn comp = pawn.GetComp<CompRustedTurretPawn>();
			comp.destroyed = false;
			comp.health = parent.HitPoints;
			pawn.ageTracker.AgeBiologicalTicks += ageTicks;
			pawn.ageTracker.AgeChronologicalTicks += ageTicks;
			Lord lord = ((Building_TurretGun)parent).GetLord();
			if (lord != null && !lord.ownedPawns.Contains(pawn))
			{
				lord.AddPawn(pawn);
			}
			pawn.GetComp<CompRustedTurret>().CurRotation = ((Building_TurretGun)parent).Top.CurRotation + pawn.GetComp<CompRustedTurret>().Props.angleOffset;
			parent.Destroy();
			GenSpawn.Spawn(pawn, pos, map, WipeMode.VanishOrMoveAside);
			if (selected)
			{
				Find.Selector.Select(pawn, false, false);
			}
			return pawn;
		}

		public override void PostPostMake()
		{
			base.PostPostMake();
			if (health == -1 && !destroyed)
			{
				health = Props.hitPoints;
			}
		}

		public override void CompTick()
		{
			base.CompTick();
			if (parent is Pawn)
			{
				if (Props.hitPoints > health)
				{
					ticksToRegen++;
					if (ticksToRegen >= Props.ticksPerHeal)
					{
						ticksToRegen = 0;
						health++;
					}
				}
			}
			else
			{
				ageTicks++;
				if (parent.IsHashIntervalTick(60) && parent is Building_RustedTurret turret && !turret.CurrentTarget.IsValid)
				{
					string name = turret.GetLord()?.CurLordToil?.GetType().Name;
					if (name != null && !name.Contains("Defend") && !name.Contains("Stage"))
					{
						SpawnPawn(parent.Position, parent.Map);
					}
				}
			}
		}

		public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (destroyed)
			{
				return;
			}
			Pawn pawn = parent as Pawn;
			if (pawn == null)
			{
				Thing instigator = dinfo.Instigator;
				if (instigator != null && instigator.Map == parent.Map && parent is Building_RustedTurret turret && !turret.CurrentTarget.IsValid)
				{
					SpawnPawn(parent.Position, parent.Map);
				}
				return;
			}
			if (!dinfo.Def.ExternalViolenceFor(parent))
			{
				return;
			}
			bool spawnedOrAnyParentSpawned = pawn.SpawnedOrAnyParentSpawned;
			if (spawnedOrAnyParentSpawned && pawn.jobs != null)
			{
				Job job = pawn.CurJob;
				if (job != null && dinfo.Def.canInterruptJobs && !job.playerForced && Find.TickManager.TicksGame >= lastDamageCheckTick + 180)
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
				health -= Mathf.RoundToInt(dinfo.Amount * pawn.GetStatValue(StatDefOf.IncomingDamageFactor));
				pawn.mindState.Notify_DamageTaken(dinfo);
				pawn.GetLord()?.Notify_PawnDamaged(pawn, dinfo);
				if (health <= 0)
				{
					health = 0;
					destroyed = true;
					parent.Kill(dinfo);
				}
				else
				{
					if (dinfo.Def.makesBlood && Rand.Chance(0.5f))
					{
						pawn.health.DropBloodFilth();
					}
				}
			}
		}

		public override void PostExposeData()
		{
			Scribe_Values.Look(ref health, "health");
			Scribe_Values.Look(ref ticksToRegen, "ticksToRegen");
			Scribe_Values.Look(ref ageTicks, "ageTicks", defaultValue: 0);
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
			if (innerContainer == null)
			{
				innerContainer = new ThingOwner<RustedPawn>(this, oneStackOnly: true, LookMode.Deep, removeContentsIfDestroyed: false);
			}
			if (Scribe.mode == LoadSaveMode.PostLoadInit && innerContainer.removeContentsIfDestroyed)
			{
				innerContainer.removeContentsIfDestroyed = false;
			}
		}

		public override List<PawnRenderNode> CompRenderNodes()
		{
			List<PawnRenderNode> list = new List<PawnRenderNode>();
			if (parent is RustedPawn rust)
			{
				for (int i = 0; i < 4; i++)
				{
					PawnRenderNodeProperties pawnRenderNodeProperties = new PawnRenderNodeProperties()
					{
						texPath = "Things/Pawn/NAT_RustedTurretLeg",
						flipGraphic = i % 2 == 0,
						drawSize = Vector2.one,
						pawnType = PawnRenderNodeProperties.RenderNodePawnType.Any,
						overrideMeshSize = Vector2.one,
						debugLabel = "Leg-" + i,
						parentTagDef = PawnRenderNodeTagDefOf.Body,
						workerClass = typeof(PawnRenderNodeWorker_RustedTurretLeg)
					};
					pawnRenderNodeProperties.drawData = DrawData.NewWithData(new DrawData.RotationalData(null, -10) { offset = new Vector3((i % 2 == 0) ? Props.offsetSide : -Props.offsetSide, 0, (i > 1) ? Props.offsetBottom : Props.offsetTop) });
					PawnRenderNode pawnRenderNode = (PawnRenderNode)Activator.CreateInstance(typeof(PawnRenderNode), rust, pawnRenderNodeProperties, rust.Drawer.renderer.renderTree);
					list.Add(pawnRenderNode);
				}
			}
			return list;
		}
	}


}
