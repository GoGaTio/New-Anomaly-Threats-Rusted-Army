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
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Messaging;
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
	public class RustedPawnExtention : DefModExtension
	{
		public bool defaultDraftable = true;

		public bool scenarioAvailable = true;

		public bool sendDeathLetter = true;

		public bool nonPlayer = false;
	}

	public class RustedPawn : Pawn
	{
		public Need_RustRest restNeed;

		private CompRustedSoldier comp;

		public float? bodySizeOverride;

		public int stunAdaptationTicksLeft = -1;

		public CompRustedSoldier Comp
		{
			get
			{
				if (comp == null)
				{
					comp = this.GetComp<CompRustedSoldier>();
				}
				return comp;
			}
		}

		private CompRustedCommander commander;

		public CompRustedCommander Commander
		{
			get
			{
				if (commander == null)
				{
					commander = this.GetComp<CompRustedCommander>();
				}
				return commander;
			}
		}

		private CompAttachBase attach;

		public CompAttachBase Attach
		{
			get
			{
				if (attach == null)
				{
					attach = this.GetComp<CompAttachBase>();
				}
				return attach;
			}
		}

		private CompRustedWorker worker;

		public CompRustedWorker Worker
		{
			get
			{
				if (worker == null)
				{
					worker = this.GetComp<CompRustedWorker>();
				}
				return worker;
			}
		}

		public override CellRect? CustomRectForSelector => base.CustomRectForSelector;

		public bool HasHead
		{
			get
			{
				if(Comp?.Props?.hasHead == true && Head != null)
                {
					return health.hediffSet.GetNotMissingParts().Any((BodyPartRecord x) => x.def.defName == "NAT_RustedHead");
				}
				return false;
			}
		}

		private RustHeadDef head;

		public RustHeadDef Head
		{
			get
			{
				return head;
			}
			set
			{
				if (head == value)
				{
					return;
				}
				head = value;
				this.Drawer.renderer.SetAllGraphicsDirty();
			}
		}
		public bool Draftable
		{
			get
			{
				return Controllable;
			}
		}

		public bool EverControllable
		{
			get
			{
				return Faction == Faction.OfPlayerSilentFail;
			}
		}

		public bool Controllable
		{
			get
			{
				if(restNeed?.exhausted == true)
				{
					return false;
				}
                if (!EverControllable)
                {
					return false;
                }
				if (!Spawned)
				{
					return false;
				}
				return !Downed;
			}
		}

        public override void PostPostMake()
        {
            base.PostPostMake();
			if (Comp?.Props?.hasHead == true)
			{
				head = DefDatabase<RustHeadDef>.AllDefs.RandomElementByWeight((RustHeadDef x) => Comp.Props.headTags.Contains(x.tag) ? x.selectionWeight : 0);
			}
		}

		public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
		{
			if (dinfo.Def.causeStun)
			{
				int duration = Mathf.RoundToInt((dinfo.Def.constantStunDurationTicks ?? (dinfo.Amount * 30f)) * 2.5f);
				if (kindDef.isBoss)
				{
					duration *= 2;
				}
				if (stunAdaptationTicksLeft > 0)
				{
					stunAdaptationTicksLeft += duration;
					absorbed = true;
					if (dinfo.Def.displayAdaptedTextMote)
					{
						MoteMaker.ThrowText(new Vector3((float)Position.x + 1f, Position.y, (float)Position.z + 1f), text: dinfo.Def.adaptedText ?? ((string)"Adapted".Translate()), map: Map, color: Color.white);
					}
					return;
				}
				stunAdaptationTicksLeft = Mathf.Max(duration, 600);
			}
			if(dinfo.Tool == null)
			{
				if (dinfo.Def.isExplosive)
				{
					dinfo.SetAmount(dinfo.Amount * 0.5f);
				}
				else if(dinfo.Def == DamageDefOf.Blunt)
				{
					dinfo.SetAmount(dinfo.Amount * 0.25f);
				}
			}
			else
			{
				float factor = 0.75f;
				if(dinfo.Instigator is Pawn p)
				{
					factor *= Mathf.Min(p.BodySize, 1);
				}
				dinfo.SetAmount(dinfo.Amount * factor);
			}
			base.PreApplyDamage(ref dinfo, out absorbed);
		}

        public override void DrawGUIOverlay()
		{
			base.DrawGUIOverlay();
            if (!Spawned || Map.fogGrid.IsFogged(Position) || WorldComponent_GravshipController.CutsceneInProgress)
            {
				return;
            }
			if (Name != null && Name.IsValid)
			{
				Vector2 pos = GenMapUI.LabelDrawPosFor(this, -0.7f);
				GenMapUI.DrawPawnLabel(this, pos);
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (!respawningAfterLoad)
			{
				Hediff hediff = health.hediffSet.GetFirstHediffOfDef(NATRADefOf.NAT_RustedRegeneration);
				if(hediff == null)
				{
					float? severity = kindDef.startingHediffs.FirstOrDefault((x) => x.def == NATRADefOf.NAT_RustedRegeneration)?.severity;
					if(severity != null)
					{
						hediff = health.AddHediff(NATRADefOf.NAT_RustedRegeneration);
						hediff.Severity = severity.Value;
					}
				}
			}
		}

		protected override void TickInterval(int delta)
		{
			base.TickInterval(delta);
			if(stunAdaptationTicksLeft > 0)
			{
				stunAdaptationTicksLeft -= delta;
			}
		}

		public static AcceptanceReport CanEquip(ThingWithComps equipment, RustedPawn rust)
		{
			string cantReason;
			if (equipment.def.IsRangedWeapon && rust.WorkTagIsDisabled(WorkTags.Shooting))
			{
				return "IsIncapableOfShootingLower".Translate(rust);
			}
			else if (!rust.CanReach(equipment, PathEndMode.ClosestTouch, Danger.Deadly))
			{
				return "NoPath".Translate().CapitalizeFirst();
			}
			else if (!rust.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				return "Incapable".Translate().CapitalizeFirst();
			}
			else if (equipment.IsBurning())
			{
				return "BurningLower".Translate();
			}
			else if (rust.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanEquip(equipment, rust))
			{
				return "QuestRelated".Translate().CapitalizeFirst();
			}
			else if (!EquipmentUtility.CanEquip(equipment, rust, out cantReason, checkBonded: false))
			{
				return cantReason.CapitalizeFirst();
			}
			return true;
		}

		private IEnumerable<IReloadableComp> GetReloadablesUsingAmmo(Pawn pawn, Thing clickedThing)
		{
			if (pawn.equipment?.PrimaryEq != null && pawn.equipment.PrimaryEq is IReloadableComp reloadableComp && clickedThing.def == reloadableComp.AmmoDef)
			{
				yield return reloadableComp;
			}
			if (pawn.apparel == null)
			{
				yield break;
			}
			foreach (Apparel item in pawn.apparel.WornApparel)
			{
				IReloadableComp reloadableComp2 = item.TryGetComp<CompApparelReloadable>();
				if (reloadableComp2 != null && clickedThing.def == reloadableComp2.AmmoDef)
				{
					yield return reloadableComp2;
				}
			}
		}

		public IEnumerable<Gizmo> GetDraftedGizmos()
		{
			if (drafter.ShowDraftGizmo)
			{
				Command_Toggle command_Toggle = new Command_Toggle
				{
					hotKey = KeyBindingDefOf.Command_ColonistDraft,
					isActive = () => drafter.Drafted,
					toggleAction = delegate
					{
						drafter.Drafted = !drafter.Drafted;
						PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Drafting, KnowledgeAmount.SpecificInteraction);
						if (drafter.Drafted)
						{
							LessonAutoActivator.TeachOpportunity(ConceptDefOf.QueueOrders, OpportunityType.GoodToKnow);
						}
					},
					defaultDesc = "CommandToggleDraftDesc".Translate(),
					icon = TexCommand.Draft,
					turnOnSound = SoundDefOf.DraftOn,
					turnOffSound = SoundDefOf.DraftOff,
					groupKeyIgnoreContent = 81729172,
					defaultLabel = (Drafted ? "CommandUndraftLabel" : "CommandDraftLabel").Translate()
				};
				if (this.Downed)
				{
					command_Toggle.Disable("IsIncapped".Translate(this.LabelShort, this));
				}
				command_Toggle.tutorTag = ((!Drafted) ? "Draft" : "Undraft");
				yield return command_Toggle;
			}
			if (Drafted && this.equipment.Primary != null && equipment.Primary.def.IsRangedWeapon)
			{
				yield return new Command_Toggle
				{
					hotKey = KeyBindingDefOf.Misc6,
					isActive = () => drafter.FireAtWill,
					toggleAction = delegate
					{
						drafter.FireAtWill = !drafter.FireAtWill;
					},
					icon = TexCommand.FireAtWill,
					defaultLabel = "CommandFireAtWillLabel".Translate(),
					defaultDesc = "CommandFireAtWillDesc".Translate(),
					tutorTag = "FireAtWillToggle"
				};
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos())
			{
				yield return g;
			}
            if (!Spawned)
            {
				yield break;
            }
			bool flag = restNeed?.exhausted == true;
			if (Faction == Faction.OfPlayer && Draftable)
			{
				AcceptanceReport allowsDrafting = this.GetLord()?.AllowsDrafting(this) ?? ((AcceptanceReport)true);
				if (drafter != null)
				{
					foreach (Gizmo gizmo2 in GetDraftedGizmos())
					{
						if (!allowsDrafting && !gizmo2.Disabled)
						{
							gizmo2.Disabled = true;
							gizmo2.disabledReason = allowsDrafting.Reason;
						}
						else if (flag)
						{
							gizmo2.Disable("IsIncapped".Translate(this.LabelShort, this));
						}
						yield return gizmo2;
					}
				}
                if (!flag)
                {
					foreach (Gizmo attackGizmo in PawnAttackGizmoUtility.GetAttackGizmos(this))
					{
						if (!allowsDrafting && !attackGizmo.Disabled)
						{
							attackGizmo.Disabled = true;
							attackGizmo.disabledReason = allowsDrafting.Reason;
						}
						yield return attackGizmo;
					}
					if (Drafted)
					{
						List<Thing> usables = new List<Thing>();
						List<ThingWithComps> sidearms = new List<ThingWithComps>();
						foreach (Thing item in inventory.innerContainer)
						{
							if (item.TryGetComp<CompUsableByRust>() != null)
							{
								usables.Add(item);
							}
							else if (item.TryGetComp<CompRustedSidearm>() != null && !sidearms.Any(x => x.def == item.def))
							{
								sidearms.Add(item as ThingWithComps);
							}
						}
						ThingWithComps primary = this.equipment.Primary;
						if(primary != null && primary.HasComp<CompRustedSidearm>())
						{
							ThingWithComps oldPrimary = inventory.innerContainer.FirstOrDefault(x => x.def.IsWeapon && !x.HasComp<CompRustedSidearm>()) as ThingWithComps;
							if (oldPrimary != null)
							{
								sidearms.Add(oldPrimary);
							}
						}
						if (sidearms.Count > 0)
						{
							foreach(ThingWithComps sidearm in sidearms)
							{
								if (primary.def == sidearm.def)
								{
									continue;
								}
								Command_Action command_Action = new Command_Action();
								command_Action.defaultLabel = "Equip".Translate(sidearm.LabelCapNoCount);
								command_Action.defaultDesc = sidearm.LabelCapNoCount + ": " + sidearm.def.description.CapitalizeFirst();
								command_Action.icon = sidearm.def.uiIcon;
								command_Action.iconAngle = sidearm.def.uiIconAngle;
								command_Action.iconOffset = sidearm.def.uiIconOffset;
								command_Action.action = delegate
								{
									if (primary != null)
									{
										equipment.Remove(primary);
										//stances.CancelBusyStanceSoft();
										primary.Notify_Unequipped(this);
										inventory.TryAddAndUnforbid(primary);
									}
									ThingWithComps secondary;
									if (sidearm.stackCount > 1)
									{
										secondary = sidearm.SplitOff(1) as ThingWithComps;
									}
									else
									{
										secondary = sidearm;
										inventory.innerContainer.Remove(sidearm);
									}
									equipment.AddEquipment(secondary);
									//jobs.EndCurrentJob(JobCondition.InterruptForced);
								};
								yield return command_Action;
							}
						}
						if (Find.Selector.SingleSelectedThing == this && usables.Count > 0)
						{
							Thing activator = usables[0];
							if (usables.Count == 1)
							{
								Command_Action command_Action = new Command_Action();
								command_Action.defaultLabel = activator.TryGetComp<CompUsableByRust>().JobReport.Formatted(activator.LabelShort);
								command_Action.defaultDesc = activator.LabelCapNoCount + ": " + activator.def.description.CapitalizeFirst();
								command_Action.icon = activator.def.uiIcon;
								command_Action.iconAngle = activator.def.uiIconAngle;
								command_Action.iconOffset = activator.def.uiIconOffset;
								command_Action.iconDrawScale = activator.def.uiIconScale;
								command_Action.action = delegate
								{
									jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATRADefOf.NAT_UseItemByRust, activator), JobTag.Misc);
								};
								yield return command_Action;
							}
							else
							{
								Command_Action command_Action2 = new Command_Action();
								command_Action2.defaultLabel = "NAT_TakeUsable".Translate();
								command_Action2.defaultDesc = "NAT_TakeUsableDesc".Translate();
								command_Action2.icon = activator.def.uiIcon;
								command_Action2.iconAngle = activator.def.uiIconAngle;
								command_Action2.iconOffset = activator.def.uiIconOffset;
								command_Action2.iconDrawScale = activator.def.uiIconScale;
								command_Action2.action = delegate
								{
									List<FloatMenuOption> list = new List<FloatMenuOption>();
									foreach (Thing usable in usables)
									{
										string label = usable.TryGetComp<CompUsableByRust>().JobReport.Formatted(usable.LabelShort);
										list.Add(new FloatMenuOption(label, delegate
										{
											jobs.TryTakeOrderedJob(JobMaker.MakeJob(NATRADefOf.NAT_UseItemByRust, usable), JobTag.Misc);
										}));
									}
									Find.WindowStack.Add(new FloatMenu(list));
								};
								yield return command_Action2;
							}
						}
					}
				}
			}
			if(abilities == null || abilities.AllAbilitiesForReading.NullOrEmpty() || !Spawned)
            {
				yield break;
            }
			foreach (Ability a in abilities.AllAbilitiesForReading)
			{
				if (EverControllable && !DebugSettings.ShowDevGizmos)
				{
					bool visibleSecondary = (Drafted || a.def.displayGizmoWhileUndrafted) && a.GizmosVisible();
					if (visibleSecondary)
					{
						foreach (Command gizmo in a.GetGizmos())
						{
							if (flag)
							{
								gizmo.Disable();
							}
							yield return gizmo;
						}
					}
				}
                if (!flag)
                {
					foreach (Gizmo item in a.GetGizmosExtra())
					{
						yield return item;
					}
				}
			}
		}
		public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
		{
			IntVec3 pos = PositionHeld;
			Map map = MapHeld;
			bool isPlayer = EverControllable;
			float chance = Comp.Props.isHumanlike ? this.GetStatValue(NATRADefOf.NAT_CoreDropChance) : 0f;
			if (Faction != Faction.OfPlayerSilentFail)
			{
				
			}
			if(apparel != null)
			{
				foreach (Apparel ap in apparel.WornApparel.ToList())
				{
					if (ap.HitPoints < 35f)
					{
						apparel.Remove(ap);
						ap.Destroy();
					}
					else
					{
						ap.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 35f));
						apparel.TryDrop(ap);
					}
				}
			}
			Caravan caravan = this.GetCaravan();
			base.Kill(dinfo, exactCulprit);
			RustedCore core = null;
			if (((map != null && pos.IsValid) || caravan != null) && Rand.Chance(chance))
            {
				core = (RustedCore)ThingMaker.MakeThing(NATRADefOf.NAT_RustedCore);
				core.Rust = this;
				if (this.Discarded)
				{
					Log.Warning("New Anomaly Threats - " + LabelCap + " was discarded after core creation, fixing");
					ForceSetStateToUnspawned();
					DecrementMapIndex();
				}
				if (caravan == null)
                {
					GenPlace.TryPlaceThing(core, pos, map, ThingPlaceMode.Near);
				}
                else
                {
					caravan.AddPawnOrItem(core, false);
				}
            }
			if (isPlayer)
			{
				TaggedString diedLetterText = HealthUtility.GetDiedLetterText(this, dinfo, exactCulprit);
				LookTargets targets = null;
				if(core != null)
                {
					if (caravan == null)
					{
						targets = core;
					}
                    else
                    {
						targets = caravan;
					}
				}
                else if(pos.IsValid)
                {
					targets = new LookTargets(pos, map);
				}
				Find.LetterStack.ReceiveLetter("Death".Translate() + ": " + (Name.IsValid ? Name.ToStringFull : LabelCap), diedLetterText, LetterDefOf.Death, targets);
			}
		}

		public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
		{
			base.PostApplyDamage(dinfo, totalDamageDealt);
			if (dinfo.Def.makesBlood && totalDamageDealt > 0f && Rand.Chance(0.5f))
			{
				health.DropBloodFilth();
			}
			if(Attach?.attachments != null && Attach.attachments.Count != 0)
			{
				if(Attach.attachments.Count == 1)
				{
					if (Attach.attachments[0].def != ThingDefOf.Fire)
					{
						Attach.attachments[0].Destroy();
					}
				}
				else
				{
					for (int i = Attach.attachments.Count - 1; i > 0; i--)
					{
						Attach.attachments[i].Destroy();
					}
				}
			}
		}

		public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
			if (EverControllable && Spawned)
			{
				pather.curPath?.DrawPath(this);
				jobs.DrawLinesBetweenTargets();
			}
		}

		public override void SetFaction(Faction newFaction, Pawn recruiter = null)
		{
			base.SetFaction(newFaction, recruiter);
			if (Name == null && newFaction == Faction.OfPlayer)
			{
				Name = PawnBioAndNameGenerator.GeneratePawnName(this, NameStyle.Numeric);
			}
		}

        public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Defs.Look(ref head, "head");
			Scribe_Values.Look(ref stunAdaptationTicksLeft, "stunAdaptationTicksLeft");
			Scribe_Values.Look(ref bodySizeOverride, "bodySizeOverride");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && head == null && Comp?.Props?.hasHead == true)
            {
				head = DefDatabase<RustHeadDef>.AllDefs.RandomElementByWeight((RustHeadDef x) => Comp.Props.headTags.Contains(x.tag) ? x.selectionWeight : 0);
			}
		}
	}
}