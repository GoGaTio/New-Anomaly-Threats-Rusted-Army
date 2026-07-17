using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
using static UnityEngine.GraphicsBuffer;

namespace NAT
{
	public class RustedMechanism : MovableEntity, IAttackTargetSearcher
	{
		public class MechanismPrinter : IExposable
		{
			public RustedMechanism parent;

			private MoteDualAttached warmupMote;

			public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

			public int cooldownTicksLeft;

			public int warmupTicksLeft;

			public PawnKindDef currentKind;

			public Vector3 moteDrawOffset;

			public Effecter effecter;

			public bool creatingForPlayer;

			private static readonly SimpleCurve ScaleCurve = new SimpleCurve
		{
			new CurvePoint(1f, 0f),
			new CurvePoint(0f, 4f)
		};

			public void Tick()
			{
				if (warmupTicksLeft > 0)
				{
					warmupTicksLeft--;
					if (currentKind == null)
					{
						currentKind = parent.Comp.Props.options.RandomElementByWeight((x) => x.selectionWeight).kind;
					}
					if (warmupTicksLeft > 0)
					{
						if(effecter == null)
						{
							effecter = parent.Comp.Props.printingEffecter.Spawn(currentTarget.Cell, parent.Map);
						}
						effecter.scale = ScaleCurve.Evaluate((float)warmupTicksLeft / (float)parent.Comp.Props.printWarmupTicks);
						effecter.EffectTick(currentTarget.ToTargetInfo(parent.Map), currentTarget.ToTargetInfo(parent.Map));
						Vector3 vector = moteDrawOffset;
						Vector3 offset = (currentTarget.CenterVector3 - (parent.DrawPos + vector)).normalized;
						vector += offset * parent.Comp.Props.printOffsetTowards;
						if (warmupMote == null || warmupMote.Destroyed)
						{
							warmupMote = MoteMaker.MakeInteractionOverlay(parent.Comp.Props.moteDef, parent, currentTarget.ToTargetInfo(parent.Map), vector, offset * 0.5f);
						}
						else
						{
							warmupMote.Maintain();
							warmupMote.UpdateTargets(parent, currentTarget.ToTargetInfo(parent.Map), vector, offset * 0.5f);
						}
					}
					else
					{
						PrintRust();
					}
				}
				else
				{
					if (cooldownTicksLeft > 0)
					{
						cooldownTicksLeft--;
					}
					if (cooldownTicksLeft <= 0)
					{
						StartPrinting();
					}
				}
			}

			public void StartPrinting()
			{
				Map map = parent.Map;
				if (RCellFinder.TryFindRandomCellNearWith(parent.Position, (c) => c.Standable(map) && c.DistanceTo(parent.Position) > 2.9f && !parent.printers.Any((x) => x.currentTarget.Cell == c) && map.reachability.CanReach(parent.Position, c, PathEndMode.OnCell, TraverseMode.NoPassClosedDoorsOrWater), map, out var cell, 5, 15))
				{
					warmupTicksLeft = parent.Comp.Props.printWarmupTicks;
					currentTarget = cell;
					currentKind = parent.Comp.Props.options.RandomElementByWeight((x)=>x.selectionWeight).kind;
					effecter = null;
					warmupMote = null;
					creatingForPlayer = false;
				}
			}

			public void PrintRust()
			{
				if (creatingForPlayer)
				{
					PrintPlayerRust();
				}
				else
				{
					Pawn pawn = PawnGenerator.GeneratePawn(currentKind, Faction.OfEntities);
					pawn.ageTracker.AgeBiologicalTicks = 0;
					pawn.ageTracker.AgeChronologicalTicks = 0;
					pawn.inventory.DestroyAll();
					GenSpawn.Spawn(pawn, currentTarget.Cell, parent.Map, WipeMode.VanishOrMoveAside);
					Lord lord = parent.Map.lordManager.lords.FirstOrDefault((x) => x.LordJob is LordJob_RustedArmy job && job.canLeave == false);
					if (lord == null)
					{
						lord = LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_RustedArmy(parent.Position, -1, false, false, false, true), parent.Map);
					}
					parent.Comp.Props.printEffecter.Spawn(pawn, pawn.Map).Cleanup();
					lord.AddPawn(pawn);
				}
				cooldownTicksLeft = parent.Comp.Props.printCooldownTicks;
				currentTarget = LocalTargetInfo.Invalid;
				parent.bioferrite -= currentKind.combatPower * 0.1f;
				currentKind = null;
			}

			public void PrintPlayerRust()
			{
				Pawn pawn = PawnGenerator.GeneratePawn(currentKind, Faction.OfPlayer);
				pawn.equipment.DestroyAllEquipment();
				pawn.inventory.DestroyAll();
				pawn.apparel.DestroyAll();
				pawn.ageTracker.AgeBiologicalTicks = 0;
				pawn.ageTracker.AgeChronologicalTicks = 0;
				pawn.GetComp<CompRustedShield>()?.Destroy(false);
				pawn.GetComp<CompRustedCommander>()?.Reset();
				GenSpawn.Spawn(pawn, currentTarget.Cell, parent.Map, WipeMode.VanishOrMoveAside);
				parent.Comp.Props.printEffecter.Spawn(pawn, pawn.Map).Cleanup();
				creatingForPlayer = false;
				parent.workingPrinter = -1;
			}

			public void ExposeData()
			{
				Scribe_Values.Look(ref cooldownTicksLeft, "cooldownTicksLeft");
				Scribe_Values.Look(ref warmupTicksLeft, "warmupTicksLeft");
				Scribe_TargetInfo.Look(ref currentTarget, "currentTarget");
				Scribe_Defs.Look(ref currentKind, "currentKind");
			}
		}

		private CompRustedMechanism compInt;

		public CompRustedMechanism Comp => compInt ?? (compInt = this.GetComp<CompRustedMechanism>());

		public override float TargetPriorityFactor => 1.5f;

		public override LocalTargetInfo TargetCurrentlyAimingAt => currentTarget;

		public override bool CanBeMoved => !active;

		public override bool ThreatDisabled(IAttackTargetSearcher disabledFor)
		{
			return !active;
		}

		public Verb CurrentEffectiveVerb => GunCompEq.PrimaryVerb;

		public CompEquippable GunCompEq => gun.TryGetComp<CompEquippable>();

		public LocalTargetInfo LastAttackedTarget => lastAttackedTarget;

		public int LastAttackTargetTick => lastAttackTargetTick;

		protected float BurstCooldownTime => CurrentEffectiveVerb.verbProps.defaultCooldownTime;

		protected float BurstWarmupTime => CurrentEffectiveVerb.verbProps.warmupTime;

		Thing IAttackTargetSearcher.Thing => this;

		public float BioferritePercent => bioferrite / Comp.Props.maxBioferrite;

		public bool active = false;

		public LocalTargetInfo currentTarget = LocalTargetInfo.Invalid;

		private LocalTargetInfo lastAttackedTarget;

		private int lastAttackTargetTick;

		public Thing gun;

		public int burstCooldownTicksLeft;

		public int burstWarmupTicksLeft;

		public int workingPrinter = -1;

		public float bioferrite;

		public List<MechanismPrinter> printers = new List<MechanismPrinter>();

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref bioferrite, "bioferrite");
			Scribe_Values.Look(ref burstCooldownTicksLeft, "burstCooldownTicksLeft");
			Scribe_Values.Look(ref burstWarmupTicksLeft, "burstWarmupTicksLeft");
			Scribe_TargetInfo.Look(ref currentTarget, "currentTarget");
			Scribe_TargetInfo.Look(ref lastAttackedTarget, "lastAttackedTarget");
			Scribe_Values.Look(ref lastAttackTargetTick, "lastAttackTargetTick");
			Scribe_Values.Look(ref active, "active");
			Scribe_Deep.Look(ref gun, "gun");
			Scribe_Collections.Look(ref printers, "printers", lookMode: LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				try
				{
					for (int i = 0; i < Comp.Props.printOffsets.Count; i++)
					{
						printers[i].moteDrawOffset = Comp.Props.printOffsets[i];
						printers[i].parent = this;
					}
				}
				catch(Exception ex)
				{
					Log.Warning(this.ToString() + " caught an exception on printers loading, regenerating:" + ex);
					MakePrinters();
				}
				if (gun == null)
				{
					Log.Error("Turret had null gun after loading. Recreating.");
					MakeGun();
				}
				else
				{
					UpdateGunVerbs();
				}
			}
		}

		public void Deactivate()
		{
			Comp.CompActivity.EnterPassiveState();
			Comp.CompActivity.SetActivity(0);
			bioferrite = -100;
		}

		public void Activate()
		{
			if(bioferrite <= 0)
			{
				bioferrite = 100;
			}
			active = true;
			burstWarmupTicksLeft = 0;
			burstCooldownTicksLeft = BurstCooldownTime.SecondsToTicks();
			for (int i = 0; i < printers.Count; i++)
			{
				printers[i].cooldownTicksLeft = Mathf.RoundToInt((float)Comp.Props.printCooldownTicks * Rand.Value);
				printers[i].warmupTicksLeft = 0;
			}
		}

		public void MakePrinters()
		{
			printers.Clear();
			printers = new List<MechanismPrinter>();
			for (int i = 0; i < Comp.Props.printOffsets.Count; i++)
			{
				MechanismPrinter printer = new MechanismPrinter();
				printer.moteDrawOffset = Comp.Props.printOffsets[i];
				printer.parent = this;
				printer.cooldownTicksLeft = Comp.Props.printCooldownTicks;
				printers.Add(printer);
			}
		}

		public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			if(bioferrite <= 0)
			{
				absorbed = true;
				return;
			}
			base.PreApplyDamage(ref dinfo, out absorbed);
		}

		public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			base.PostApplyDamage(dinfo, totalDamageDealt);
			bioferrite -= dinfo.Amount;
			if(bioferrite <= 0)
			{
				Deactivate();
			}
		}

		public override string GetInspectString()
		{
			string s = base.GetInspectString();
			if (s.NullOrEmpty())
			{
				s = string.Empty;
			}
			else
			{
				s += "\n";
			}
			s += "NAT_BioferriteOnSurface".Translate() + ": " + Mathf.Max(bioferrite, 0).ToStringByStyle(ToStringStyle.Integer);
			return s;
		}

		public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
		{
			base.DeSpawn(mode);
			ResetCurrentTarget();
		}

		public override void PostPostMake()
		{
			base.PostPostMake();
			MakeGun();
			MakePrinters();
			burstCooldownTicksLeft = BurstCooldownTime.SecondsToTicks();
			SetFaction(Faction.OfEntities);
		}

		public override void DrawExtraSelectionOverlays()
		{
			base.DrawExtraSelectionOverlays();
			if (!active)
			{
				return;
			}
			float effectiveRange = CurrentEffectiveVerb.EffectiveRange;
			float num = CurrentEffectiveVerb.verbProps.EffectiveMinRange(allowAdjacentShot: true);
			if (num < effectiveRange)
			{
				if (effectiveRange < 90f)
				{
					GenDraw.DrawRadiusRing(base.Position, effectiveRange);
				}
				if (num < 90f && num > 0.1f)
				{
					GenDraw.DrawRadiusRing(base.Position, num);
				}
			}
			if (burstWarmupTicksLeft > 0)
			{
				int degreesWide = (int)((float)burstWarmupTicksLeft * 0.5f);
				GenDraw.DrawAimPie(this, currentTarget, degreesWide, (float)def.size.x * 0.5f);
			}
			else if(burstCooldownTicksLeft > 0)
			{
				GenDraw.DrawCooldownCircle(DrawPos + new Vector3(0f, 0.2f, 0f), Mathf.Min(0.5f, (float)burstCooldownTicksLeft * 0.002f));
			}
		}

		protected override void Tick()
		{
			base.Tick();
			if (!Spawned)
			{
				return;
			}
			if (active)
			{
				if (bioferrite <= 0)
				{
					Deactivate();
					return;
				}
				for (int i = 0; i < printers.Count; i++)
				{
					printers[i].Tick();
				}
				GunCompEq.verbTracker.VerbsTick();
				if (CurrentEffectiveVerb.state == VerbState.Bursting)
				{
					return;
				}
				if (burstWarmupTicksLeft > 0)
				{
					burstWarmupTicksLeft--;
					if (burstWarmupTicksLeft <= 0)
					{
						BeginBurst();
					}
				}
				else
				{
					if (burstCooldownTicksLeft > 0)
					{
						burstCooldownTicksLeft--;
					}
					if (burstCooldownTicksLeft <= 0 && this.IsHashIntervalTick(15))
					{
						TryStartShootSomething(canBeginBurstImmediately: true);
					}
				}
			}
			else
			{
				if (workingPrinter >= 0)
				{
					printers[workingPrinter].Tick();
				}
				ResetCurrentTarget();
			}
		}

		public void TryStartShootSomething(bool canBeginBurstImmediately)
		{
			if (!base.Spawned || !CurrentEffectiveVerb.Available())
			{
				ResetCurrentTarget();
				return;
			}
			currentTarget = TryFindNewTarget();
			if (currentTarget.IsValid)
			{
				burstWarmupTicksLeft = BurstWarmupTime.SecondsToTicks();
			}
			else
			{
				ResetCurrentTarget();
			}
		}

		public LocalTargetInfo TryFindNewTarget()
		{
			TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable | TargetScanFlags.NeedLOSToAll;
			return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(this, targetScanFlags);
		}

		protected virtual void BeginBurst()
		{
			CurrentEffectiveVerb.TryStartCastOn(currentTarget);
			lastAttackTargetTick = Find.TickManager.TicksGame;
			lastAttackedTarget = currentTarget;
		}

		protected void BurstComplete()
		{
			burstCooldownTicksLeft = BurstCooldownTime.SecondsToTicks();
		}

		private void ResetCurrentTarget()
		{
			currentTarget = LocalTargetInfo.Invalid;
			burstWarmupTicksLeft = 0;
		}

		public void MakeGun()
		{
			gun = ThingMaker.MakeThing(Comp.Props.mainGunDef);
			UpdateGunVerbs();
		}

		private void UpdateGunVerbs()
		{
			List<Verb> allVerbs = gun.TryGetComp<CompEquippable>().AllVerbs;
			for (int i = 0; i < allVerbs.Count; i++)
			{
				Verb verb = allVerbs[i];
				verb.caster = this;
				verb.castCompleteCallback = BurstComplete;
			}
		}
	}
}
