using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using LudeonTK;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NAT
{
	public class CompProperties_RustedMechanism : CompProperties_Interactable
	{
		public SimpleCurve activityPerDayFromBioferrite;

		public ThingDef mainGunDef;

		public ThingDef moteDef;

		public float printOffsetTowards;

		public int printCooldownTicks;

		public int printWarmupTicks;

		public EffecterDef printEffecter;

		public EffecterDef printingEffecter;

		public List<PawnGenOption> options = new List<PawnGenOption>();

		public List<PawnGenOption> optionsPlayer = new List<PawnGenOption>();

		public List<Vector3> printOffsets = new List<Vector3>();

		public CompProperties_RustedMechanism()
		{
			compClass = typeof(CompRustedMechanism);
		}

		public override void ResolveReferences(ThingDef parentDef)
		{
			base.ResolveReferences(parentDef);
			onCooldownString = "Cooldown".Translate();
		}
	}
	public class CompRustedMechanism : CompInteractable, IActivity, IRoofCollapseAlert
	{
		private CompActivity activityInt;

		public CompActivity CompActivity => activityInt ?? (activityInt = parent.TryGetComp<CompActivity>());

		private CompStudyUnlocks studyComp;

		private CompStudyUnlocks StudyComp => studyComp ?? (studyComp = parent.GetComp<CompStudyUnlocks>());

		public new CompProperties_RustedMechanism Props => (CompProperties_RustedMechanism)props;

		protected override string ActivateOptionLabel => "Activate".Translate();

		public RustedMechanism Parent => (RustedMechanism)parent;

		public float ActivityPerDay => Parent.active ? 0f : Props.activityPerDayFromBioferrite.Evaluate(bioferritePercent);

		public float bioferritePercent;

		public bool passive;

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref bioferritePercent, "bioferritePercent", 0);
			Scribe_Values.Look(ref passive, "passive");
		}

		protected override void OnInteracted(Pawn caster)
		{
			base.OnInteracted(caster);
			Parent.workingPrinter = new IntRange(0, Parent.printers.Count - 1).RandomInRange;
			Parent.printers[Parent.workingPrinter].StartPrinting();
			Parent.printers[Parent.workingPrinter].currentKind = Props.optionsPlayer.RandomElementByWeight((x) => x.selectionWeight).kind;
			Parent.printers[Parent.workingPrinter].creatingForPlayer = true;
		}

		private static bool IsValidCell(IntVec3 cell, Map map)
		{
			if (cell.InBounds(map))
			{
				return cell.Walkable(map);
			}
			return false;
		}

		public override void CompTick()
		{
			if (Parent.active)
			{
				return;
			}
			base.CompTick();
		}

		public RoofCollapseResponse Notify_OnBeforeRoofCollapse()
		{
			if (RCellFinder.TryFindRandomCellNearWith(parent.Position, (IntVec3 c) => IsValidCell(c, parent.MapHeld), parent.MapHeld, out var result, 10))
			{
				SkipUtility.SkipTo(parent, result, parent.MapHeld);
				CompActivity.AdjustActivity(0.5f);
			}
			return RoofCollapseResponse.RemoveThing;
		}

		public void OnActivityActivated()
		{
			if(Parent.Faction != Faction.OfEntities)
			{
				Parent.SetFaction(Faction.OfEntities);
			}
			Parent.Activate();
		}

		public void OnPassive()
		{
			Parent.active = false;
		}

		public bool ShouldGoPassive()
		{
			return false;
		}

		public bool CanBeSuppressed()
		{
			return !Parent.active;
		}

		public bool CanActivate()
		{
			return !Parent.active;
		}

		public string ActivityTooltipExtra()
		{
			return null;
		}

		public override AcceptanceReport CanInteract(Pawn activateBy = null, bool checkOptionalItems = true)
		{
			if (!StudyComp.Completed || Parent.active)
			{
				return false;
			}
			return base.CanInteract(activateBy, checkOptionalItems);
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			if (!StudyComp.Completed || Parent.active)
			{
				yield break;
			}
			foreach (Gizmo item in base.CompGetGizmosExtra())
			{
				yield return item;
			}
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			if (!StudyComp.Completed || Parent.active)
			{
				yield break;
			}
			foreach (FloatMenuOption item in base.CompFloatMenuOptions(selPawn))
			{
				yield return item;
			}
		}
	}
}