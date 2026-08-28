using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
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
	public class CompProperties_RustedTurretPawn : CompProperties_Armor
	{
		public PawnKindDef kindDef;

		public ThingDef buildingDef;

		public int hitPoints;

		public int ticksPerHeal;

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
				parentDef.description += "\n" + "NAT_RustedTurretDesc".Translate(parentDef.label);
				kindDef.race.description = parentDef.description;
				comp.hitPoints = parentDef.BaseMaxHitPoints;
				comp.ticksPerHeal = parentDef.GetCompProperties<CompProperties_SelfhealHitpoints>()?.ticksPerHeal ?? 1000;
				parentDef.building.turretBurstCooldownTime = parentDef.building.turretGunDef.Verbs[0].defaultCooldownTime;
				parentDef.building.turretBurstWarmupTime = new FloatRange(parentDef.building.turretGunDef.Verbs[0].warmupTime);
				CompProperties_Turret turret = kindDef.race.GetCompProperties<CompProperties_Turret>();
				turret.turretDef = parentDef.building.turretGunDef;
				turret.foamTurret = typeof(Building_RustedTurretFoam).IsAssignableFrom(parentDef.thingClass);
				turret.Init(kindDef.race);
				kindDef.race.uiIconPath = parentDef.uiIconPath;
				kindDef.race.uiIcon = parentDef.uiIcon;
				kindDef.race.killedLeavingsRanges = parentDef.killedLeavingsRanges.ListFullCopy();
			}
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			return base.SpecialDisplayStats(req);
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

		private CompTurret turret;

		public CompTurret Turret
		{
			get
			{
				if (turret == null)
				{
					turret = parent.GetComp<CompTurret>();
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
				command_Action.defaultLabel = "NAT_CommandSetUp".Translate();
				command_Action.defaultDesc = "NAT_CommandSetUpDesc".Translate();
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
				command_Action.defaultLabel = "NAT_CommandSetOff".Translate();
				command_Action.defaultDesc = "NAT_CommandSetOffDesc".Translate();
				command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/NAT_RustedTurret_SetOff");
				command_Action.action = delegate
				{
					SpawnPawn(parent.Position, parent.Map);
				};
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "NAT_CommandReSetUp".Translate();
				command_Action2.defaultDesc = "NAT_CommandReSetUpDesc".Translate();
				command_Action2.icon = ContentFinder<Texture2D>.Get("UI/Commands/NAT_RustedTurret_ReSetUp");
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
			Log.Message(building.ToString() + ":SpawnedTurretBuilding");
		}

		public Pawn SpawnPawn(IntVec3 pos, Map map, bool updateDuties = true)
		{
			if (destroyed)
			{
				return null;
			}
			destroyed = true;
			bool selected = Find.Selector.IsSelected(parent);
			Pawn pawn = Rust;
			CompRustedTurretPawn comp = pawn.GetComp<CompRustedTurretPawn>();
			comp.destroyed = false;
			comp.health = parent.HitPoints;
			pawn.ageTracker.AgeBiologicalTicks += ageTicks;
			pawn.ageTracker.AgeChronologicalTicks += ageTicks;
			Lord lord = ((Building_TurretGun)parent).GetLord();
			lord?.ownedBuildings?.Remove(parent as Building_TurretGun);
			if (lord != null && lord.ownedPawns?.Contains(pawn) == false)
			{
				if (updateDuties)
				{
					lord.AddPawn(pawn);
				}
				else
				{
					lord.AddPawns(Gen.YieldSingle(pawn), false);
				}
			}
			pawn.GetComp<CompTurret>().CurRotation = ((Building_TurretGun)parent).Top.CurRotation + pawn.GetComp<CompTurret>().Props.angleOffset;
			parent.Destroy();
			GenSpawn.Spawn(pawn, pos, map, WipeMode.VanishOrMoveAside);
			if (selected)
			{
				Find.Selector.Select(pawn, false, false);
			}
			Log.Message(pawn.ToString() + ":SpawnedTurretPawn");
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
			else if(parent.Spawned)
			{
				ageTicks++;
				if (ageTicks > 300 && parent is Building_RustedTurret turret && !turret.CurrentTarget.IsValid)
				{
					string name = turret.GetLord()?.CurLordToil?.GetType().Name;
					if (name != null && (name.Contains("ExitMap") || (!name.Contains("Defend") && !name.Contains("Stage"))))
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
			base.PostPreApplyDamage(ref dinfo, out absorbed);
			if (dinfo.Def != DamageDefOf.EMP && dinfo.Def.harmsHealth)
			{
				absorbed = true;
				health -= Mathf.RoundToInt(dinfo.Amount);
				if (health <= 0)
				{
					health = 0;
					destroyed = true;
					parent.Kill(dinfo);
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
