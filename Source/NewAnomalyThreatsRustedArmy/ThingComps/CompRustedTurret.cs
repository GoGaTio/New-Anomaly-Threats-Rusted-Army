using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace NAT
{
	public class CompProperties_RustedTurret : CompProperties
	{
		public ThingDef turretDef;

		public float angleOffset = -90;

		public bool autoAttack = true;

		public bool foamTurret = false;

		public int extraCooldownTicks = 0;

		public PawnRenderNodeProperties renderNodeProperty;

		[NoTranslate]
		public string saveKeysPrefix;

		public CompProperties_RustedTurret()
		{
			compClass = typeof(CompRustedTurret);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			Init(parentDef);
		}

		public void Init(ThingDef parentDef)
		{
			if (!typeof(PawnRenderNode_RustedTurret).IsAssignableFrom(renderNodeProperty.nodeClass))
			{
				renderNodeProperty.nodeClass = typeof(PawnRenderNode_RustedTurret);
			}
			if (!typeof(PawnRenderNodeWorker_RustedTurret).IsAssignableFrom(renderNodeProperty.workerClass))
			{
				renderNodeProperty.workerClass = typeof(PawnRenderNodeWorker_RustedTurret);
			}
			renderNodeProperty.pawnType = PawnRenderNodeProperties.RenderNodePawnType.Any;
			renderNodeProperty.overrideMeshSize = turretDef.graphicData.drawSize;
		}
	}
	public class CompRustedTurret : ThingComp, IAttackTargetSearcher, ITargetingSource
	{
		public bool targetForced;

		public Thing gun;

		protected int burstCooldownTicksLeft;

		protected int burstWarmupTicksLeft;

		public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

		protected bool fireAtWill = true;

		protected LocalTargetInfo lastAttackedTarget = LocalTargetInfo.Invalid;

		protected int lastAttackTargetTick;

		public float curRotation;

		private int ticksUntilIdleTurn;

		private int idleTurnTicksLeft;

		private bool idleTurnClockwise;

		public virtual TargetingParameters targetParams => AttackVerb.targetParams;

		public bool MultiSelect => AttackVerb.MultiSelect;

		public Thing Caster => parent;

		public Pawn CasterPawn => parent as Pawn;

		public Verb GetVerb => AttackVerb;

		public bool CasterIsPawn => CasterPawn != null;

		public bool IsMeleeAttack => false;

		public bool Targetable => true;

		public Texture2D UIIcon => ContentFinder<Texture2D>.Get("UI/Commands/Attack");

		public ITargetingSource DestinationSelector => AttackVerb.DestinationSelector;

		public bool HidePawnTooltips => false;

		public float CurRotation
		{
			get
			{
				return curRotation;
			}
			set
			{
				curRotation = value;
				if (curRotation > 360f)
				{
					curRotation -= 360f;
				}
				if (curRotation < 0f)
				{
					curRotation += 360f;
				}
			}
		}

		public Thing Thing => parent;

		public CompProperties_RustedTurret Props => (CompProperties_RustedTurret)props;

		public Verb CurrentEffectiveVerb => AttackVerb;

		public LocalTargetInfo LastAttackedTarget => lastAttackedTarget;

		public int LastAttackTargetTick => lastAttackTargetTick;

		public CompEquippable GunCompEq => gun.TryGetComp<CompEquippable>();

		public Verb AttackVerb => GunCompEq.PrimaryVerb;

		private bool WarmingUp => burstWarmupTicksLeft > 0;

		private bool CanShoot
		{
			get
			{
				if (parent is Pawn pawn)
				{
					if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Awake())
					{
						return false;
					}
					if (pawn.stances.stunner.Stunned)
					{
						return false;
					}
					if (TurretDestroyed)
					{
						return false;
					}
					if (pawn.Faction == Faction.OfPlayerSilentFail && !fireAtWill)
					{
						return false;
					}
				}
				CompCanBeDormant compCanBeDormant = parent.TryGetComp<CompCanBeDormant>();
				if (compCanBeDormant != null && !compCanBeDormant.Awake)
				{
					return false;
				}
				return true;
			}
		}

		public bool ShouldKeepTarget
		{
			get
			{
				if(currentTarget.ThingDestroyed || (!targetForced && currentTarget.Pawn?.Downed == true))
				{
					return false;
				}
				return true;
			}
		}

		public bool TurretDestroyed
		{
			get
			{
				if (parent is Pawn pawn && AttackVerb.verbProps.linkedBodyPartsGroup != null && AttackVerb.verbProps.ensureLinkedBodyPartsGroupAlwaysUsable && PawnCapacityUtility.CalculateNaturalPartsAverageEfficiency(pawn.health.hediffSet, AttackVerb.verbProps.linkedBodyPartsGroup) <= 0f)
				{
					return true;
				}
				return false;
			}
		}

		public virtual bool CanHitTarget(LocalTargetInfo target)
		{
			return AttackVerb.CanHitTarget(target);
		}

		public virtual bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
		{
			return CanHitTarget(target);
		}

		public void DrawHighlight(LocalTargetInfo target)
		{
			AttackVerb.DrawHighlight(target);
		}

		public void OnGUI(LocalTargetInfo target)
		{
			AttackVerb.OnGUI(target);
		}

		public void OrderForceTarget(LocalTargetInfo target)
		{
			targetForced = true;
			currentTarget = target;
			foreach(object o in Find.Selector.SelectedObjects)
			{
				if(o is Thing t && t.TryGetComp<CompRustedTurret>(out var comp))
				{
					comp.targetForced = true;
					comp.currentTarget = target;
				}
			}
		}

		public bool AutoAttack => Props.autoAttack;

		public override void PostPostMake()
		{
			base.PostPostMake();
			MakeGun();
		}

		private void MakeGun()
		{
			gun = ThingMaker.MakeThing(Props.turretDef);
			UpdateGunVerbs();
		}

		private void UpdateGunVerbs()
		{
			List<Verb> allVerbs = gun.TryGetComp<CompEquippable>().AllVerbs;
			for (int i = 0; i < allVerbs.Count; i++)
			{
				Verb verb = allVerbs[i];
				verb.caster = parent;
				verb.castCompleteCallback = delegate
				{
					burstCooldownTicksLeft = AttackVerb.verbProps.defaultCooldownTime.SecondsToTicks() + Props.extraCooldownTicks;
				};
			}
		}

		public override void CompTick()
		{
			if (!CanShoot)
			{
				return;
			}
			if (currentTarget.IsValid)
			{
				curRotation = (currentTarget.Cell.ToVector3Shifted() - parent.DrawPos).AngleFlat() + Props.angleOffset;
				ticksUntilIdleTurn = Rand.RangeInclusive(150, 350);
			}
			else if (ticksUntilIdleTurn > 0)
			{
				ticksUntilIdleTurn--;
				if (ticksUntilIdleTurn == 0)
				{
					if (Rand.Value < 0.5f)
					{
						idleTurnClockwise = true;
					}
					else
					{
						idleTurnClockwise = false;
					}
					idleTurnTicksLeft = 140;
				}
			}
			else
			{
				if (idleTurnClockwise)
				{
					CurRotation += 0.26f;
				}
				else
				{
					CurRotation -= 0.26f;
				}
				idleTurnTicksLeft--;
				if (idleTurnTicksLeft <= 0)
				{
					ticksUntilIdleTurn = Rand.RangeInclusive(150, 350);
				}
			}
			AttackVerb.VerbTick();
			if (AttackVerb.state == VerbState.Bursting)
			{
				return;
			}
			if (WarmingUp)
			{
				burstWarmupTicksLeft--;
				if (burstWarmupTicksLeft == 0)
				{
					AttackVerb.TryStartCastOn(currentTarget, surpriseAttack: false, canHitNonTargetPawns: true, preventFriendlyFire: false, nonInterruptingSelfCast: true);
					lastAttackTargetTick = Find.TickManager.TicksGame;
					lastAttackedTarget = currentTarget;
				}
				return;
			}
			if (burstCooldownTicksLeft > 0)
			{
				burstCooldownTicksLeft--;
			}
			if (burstCooldownTicksLeft <= 0 && parent.IsHashIntervalTick(10))
			{
				if (targetForced)
				{
					if (currentTarget.HasThing && (!currentTarget.Thing.Spawned || !parent.Spawned || currentTarget.Thing.Map != parent.Map))
					{
						targetForced = false;
						currentTarget = GetTarget();
					}
				}
				else
				{
					currentTarget = GetTarget();
				}
				if (currentTarget.IsValid)
				{
					burstWarmupTicksLeft = 1;
				}
				else
				{
					ResetCurrentTarget();
				}
			}
		}

		public override void PostDrawExtraSelectionOverlays()
		{
			if (targetForced && currentTarget.IsValid && !currentTarget.ThingDestroyed)
			{
				GenDraw.DrawLineBetween(parent.TrueCenter(), currentTarget.CenterVector3, Building_TurretGun.ForcedTargetLineMat);
			}
		}

		protected virtual LocalTargetInfo GetTarget()
		{
			if (Props.foamTurret)
			{
				int num = GenRadial.NumCellsInRadius(AttackVerb.EffectiveRange);
				for (int i = 0; i < num; i++)
				{
					IntVec3 intVec = parent.Position + GenRadial.RadialPattern[i];
					if (!GenSight.LineOfSight(parent.Position, intVec, parent.Map, skipFirstCell: true))
					{
						continue;
					}
					List<Thing> thingList = intVec.GetThingList(parent.Map);
					for (int j = 0; j < thingList.Count; j++)
					{
						if (thingList[j] is Fire || thingList[j].HasAttachment(ThingDefOf.Fire))
						{
							return thingList[j].Position;
						}
					}
				}
				return LocalTargetInfo.Invalid;
			}
			return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable);
		}

		private void ResetCurrentTarget()
		{
			currentTarget = LocalTargetInfo.Invalid;
			targetForced = false;
			burstWarmupTicksLeft = 0;
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
			if(parent.Faction != Faction.OfPlayer)
			{
				yield break;
			}
			Command_RustedTurretTarget command_VerbTarget = new Command_RustedTurretTarget();
			command_VerbTarget.defaultLabel = "CommandSetForceAttackTarget".Translate();
			command_VerbTarget.defaultDesc = "CommandSetForceAttackTargetDesc".Translate();
			command_VerbTarget.icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack");
			command_VerbTarget.verb = AttackVerb;
			command_VerbTarget.comp = this;
			command_VerbTarget.drawRadius = true;
			command_VerbTarget.requiresAvailableVerb = false;
			if (parent.Spawned)
			{
				float curWeatherMaxRangeCap = parent.Map.weatherManager.CurWeatherMaxRangeCap;
				if (curWeatherMaxRangeCap > 0f && curWeatherMaxRangeCap < AttackVerb.verbProps.minRange)
				{
					command_VerbTarget.Disable("CannotFire".Translate() + ": " + parent.Map.weatherManager.curWeather.LabelCap);
				}
			}
			yield return command_VerbTarget;
			if (targetForced)
			{
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "CommandStopForceAttack".Translate();
				command_Action2.defaultDesc = "CommandStopForceAttackDesc".Translate();
				command_Action2.icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt");
				command_Action2.action = delegate
				{
					ResetCurrentTarget();
					currentTarget = GetTarget();
					SoundDefOf.Tick_Low.PlayOneShotOnCamera();
				};
				command_Action2.hotKey = KeyBindingDefOf.Misc5;
				yield return command_Action2;
			}
			Command_Toggle command_Toggle = new Command_Toggle();
			command_Toggle.defaultLabel = "CommandHoldFire".Translate();
			command_Toggle.defaultDesc = "CommandHoldFireDesc".Translate();
			command_Toggle.icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire");
			command_Toggle.hotKey = KeyBindingDefOf.Misc6;
			command_Toggle.toggleAction = delegate
			{
				fireAtWill = !fireAtWill;
				if (!fireAtWill)
				{
					ResetCurrentTarget();
				}
			};
			command_Toggle.isActive = () => !fireAtWill;
			yield return command_Toggle;
		}

		public override List<PawnRenderNode> CompRenderNodes()
		{
			if (parent is Pawn pawn)
			{
				PawnRenderNode_RustedTurret pawnRenderNode = (PawnRenderNode_RustedTurret)Activator.CreateInstance(Props.renderNodeProperty.nodeClass, pawn, Props.renderNodeProperty, pawn.Drawer.renderer.renderTree);
				pawnRenderNode.turretComp = this;
				return new List<PawnRenderNode>() { pawnRenderNode };
			}
			return base.CompRenderNodes();
		}

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
		{
			if (Props.turretDef != null)
			{
				yield return new StatDrawEntry(StatCategoryDefOf.PawnCombat, "Turret".Translate(), Props.turretDef.LabelCap, "Stat_Thing_TurretDesc".Translate(), 5600, null, Gen.YieldSingle(new Dialog_InfoCard.Hyperlink(Props.turretDef)));
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref burstCooldownTicksLeft, Props.saveKeysPrefix + "_" + "burstCooldownTicksLeft", 0);
			Scribe_Values.Look(ref burstWarmupTicksLeft, Props.saveKeysPrefix + "_" + "burstWarmupTicksLeft", 0);
			Scribe_TargetInfo.Look(ref currentTarget, Props.saveKeysPrefix + "_" + "currentTarget");
			Scribe_Deep.Look(ref gun, Props.saveKeysPrefix + "_" + "gun");
			Scribe_Values.Look(ref fireAtWill, Props.saveKeysPrefix + "_" + "fireAtWill", defaultValue: true);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (gun == null)
				{
					Log.Error("CompTurrentGun had null gun after loading. Recreating.");
					MakeGun();
				}
				else
				{
					UpdateGunVerbs();
				}
			}
		}
	}

	public class Command_RustedTurretTarget : Command
	{
		public Verb verb;

		private List<Verb> groupedVerbs;

		public CompRustedTurret comp;

		public bool drawRadius = true;

		public bool requiresAvailableVerb = true;

		public override Color IconDrawColor
		{
			get
			{
				if (verb.EquipmentSource != null)
				{
					return verb.EquipmentSource.DrawColor;
				}
				return base.IconDrawColor;
			}
		}

		public override void DrawIcon(Rect rect, Material buttonMat, GizmoRenderParms parms)
		{
			base.DrawIcon(rect, buttonMat, parms);
		}

		public override void GizmoUpdateOnMouseover()
		{
			if (!drawRadius)
			{
				return;
			}
			verb.verbProps.DrawRadiusRing(verb.caster.Position, verb);
			if (groupedVerbs.NullOrEmpty())
			{
				return;
			}
			foreach (Verb groupedVerb in groupedVerbs)
			{
				groupedVerb.verbProps.DrawRadiusRing(groupedVerb.caster.Position, groupedVerb);
			}
		}

		public override bool GroupsWith(Gizmo other)
		{
			if (!(other is Command_RustedTurretTarget command_VerbTarget))
			{
				return false;
			}
			return base.GroupsWith(other);
		}

		public override void MergeWith(Gizmo other)
		{
			base.MergeWith(other);
			if (!(other is Command_RustedTurretTarget command_VerbTarget))
			{
				Log.ErrorOnce("Tried to merge Command_VerbTarget with unexpected type", 73406263);
				return;
			}
			if (groupedVerbs == null)
			{
				groupedVerbs = new List<Verb>();
			}
			groupedVerbs.Add(command_VerbTarget.verb);
			if (command_VerbTarget.groupedVerbs != null)
			{
				groupedVerbs.AddRange(command_VerbTarget.groupedVerbs);
			}
		}

		public override void ProcessInput(Event ev)
		{
			base.ProcessInput(ev);
			SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
			Targeter targeter = Find.Targeter;
			Find.Targeter.BeginTargeting(comp, null, allowNonSelectedTargetingSource: false, null, null, requiresAvailableVerb);
		}
	}
}
