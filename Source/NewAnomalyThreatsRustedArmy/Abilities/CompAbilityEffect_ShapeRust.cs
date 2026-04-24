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
using Verse.AI.Group;
using Verse.Noise;

namespace NAT
{
	public class CompProperties_AbilityShapeRust : CompProperties_AbilityEffect
	{
		public int bioferriteCount;

		public float connectRadius;

		public List<PawnKindDefCount> kinds = new List<PawnKindDefCount>();

		public CompProperties_AbilityShapeRust()
		{
			compClass = typeof(CompAbilityEffect_ShapeRust);
		}
	}
	public class CompAbilityEffect_ShapeRust : CompAbilityEffect
	{
		public static Color DustColor = new Color(0.55f, 0.55f, 0.55f, 3f);

		private List<Thing> foundChunksTemp;

		private int lastChunkUpdateFrame;

		public new CompProperties_AbilityShapeRust Props => (CompProperties_AbilityShapeRust)props;


		public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
		{
			base.Apply(target, dest);
			Map map = parent.pawn.Map;
			List<Thing> list = FindClosestBioferrite(target).ToList();
			if (list.Sum((Thing x) => x.stackCount) >= Props.bioferriteCount)
			{
				PsychicRitualDef_AdditionalOfferings.RemoveItem(list, Props.bioferriteCount);
				Pawn pawn = PawnGenerator.GeneratePawn(Props.kinds.RandomElementByWeight((PawnKindDefCount x) => x.count).kindDef, parent.pawn.Faction);
				pawn.equipment.DestroyAllEquipment();
				pawn.inventory.DestroyAll();
				pawn.apparel.DestroyAll();
				pawn.ageTracker.AgeBiologicalTicks = 0;
				pawn.ageTracker.AgeChronologicalTicks = 0;
				pawn.Notify_SignalReceived(new Signal("NAT_CreatedByPsychicRitual", (1f).Named("QUALITY")));
				GenSpawn.Spawn(pawn, target.Cell, map);
				EffecterDefOf.PsychicRitual_Complete.SpawnMaintained(target.Cell, map);
			}
			else
			{
				parent.ResetCooldown();
			}
		}

		public override IEnumerable<PreCastAction> GetPreCastActions()
		{
			yield return new PreCastAction
			{
				action = delegate (LocalTargetInfo t, LocalTargetInfo d)
				{
					foreach (Thing item in FindClosestBioferrite(t))
					{
						FleckMaker.Static(item.TrueCenter(), parent.pawn.Map, FleckDefOf.PsycastSkipFlashEntry, 0.72f);
					}
				},
				ticksAwayFromCast = 5
			};
		}

		private IEnumerable<Thing> FindClosestBioferrite(LocalTargetInfo target)
		{
			if (lastChunkUpdateFrame == Time.frameCount && foundChunksTemp != null)
			{
				return foundChunksTemp;
			}
			if (foundChunksTemp == null)
			{
				foundChunksTemp = new List<Thing>();
			}
			foundChunksTemp.Clear();
			IntVec3 cell = target.Cell;
			if (!cell.IsValid || !cell.InBounds(parent.pawn.Map))
			{
				return foundChunksTemp;
			}
			int range = Mathf.CeilToInt(Props.connectRadius / 2) + 1;
			CellRect rect = new CellRect(cell.x - range, cell.z - range, range * 2, range * 2).ClipInsideMap(parent.pawn.Map);
			foreach (IntVec3 c in rect.Cells)
			{
				Thing t = c.GetFirstThing(parent.pawn.Map, ThingDefOf.Bioferrite);
				if (t != null && c.DistanceTo(cell) < Props.connectRadius)
				{
					foundChunksTemp.Add(t);
				}
			}
			lastChunkUpdateFrame = Time.frameCount;
			return foundChunksTemp;
		}

		public override void DrawEffectPreview(LocalTargetInfo target)
		{
			foreach (Thing item in FindClosestBioferrite(target))
			{
				GenDraw.DrawLineBetween(item.TrueCenter(), target.CenterVector3);
				GenDraw.DrawTargetHighlight(item);
			}
			GenDraw.DrawRadiusRing(target.Cell, Props.connectRadius);
		}

		public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
		{
			if (!target.Cell.IsValid)
			{
				return false;
			}
			if (!target.Cell.Standable(parent.pawn.Map))
			{
				return false;
			}
			if (target.Cell.Filled(parent.pawn.Map))
			{
				return false;
			}
			if (FindClosestBioferrite(target).Sum((Thing t) => t.stackCount) < Props.bioferriteCount)
			{
				if (throwMessages)
				{
					Messages.Message("CannotUseAbility".Translate(parent.def.label) + ": " + "AbilityNotEnoughFreeSpace".Translate(), parent.pawn, MessageTypeDefOf.RejectInput, historical: false);
				}
				return false;
			}
			return base.Valid(target, throwMessages);
		}

		public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
		{
			if (!target.Cell.IsValid)
			{
				return false;
			}
			if (!target.Cell.Standable(parent.pawn.Map))
			{
				return false;
			}
			if (target.Cell.Filled(parent.pawn.Map))
			{
				return false;
			}
			if (FindClosestBioferrite(target).Sum((Thing t) => t.stackCount) < Props.bioferriteCount)
			{
				return false;
			}
			return base.CanApplyOn(target, dest);
		}

		public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
		{

			int count = FindClosestBioferrite(target).Sum((Thing t) => t.stackCount);
			if (target.IsValid && count < Props.bioferriteCount)
			{
				return "AbilityNoChunkToSkip".Translate() + "(" + count.ToString() + "/" + Props.bioferriteCount.ToString() + ")";
			}
			return base.ExtraLabelMouseAttachment(target);
		}
	}
}
