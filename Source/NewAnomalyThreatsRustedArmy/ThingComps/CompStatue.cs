using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Grammar;
using static Verse.HediffCompProperties_RandomizeSeverityPhases;

namespace NAT
{
	public class CompProperties_EntityStatue : CompProperties
	{
		public GraphicData statueBaseGraphic;

		public List<PawnGenOption> entityOptions = new List<PawnGenOption>();

		public float entityWeightNotDiscovered = 1f;

		public CompProperties_EntityStatue()
		{
			compClass = typeof(CompEntityStatue);
		}
	}
	public class CompEntityStatue : ThingComp, IThingHolder, IThingHolderWithDrawnPawn
	{
		private Pawn statuePawn;

		protected ThingOwner<Pawn> innerContainer;

		public CompProperties_EntityStatue Props => (CompProperties_EntityStatue)props;

		public float HeldPawnDrawPos_Y => parent.DrawPos.y + 0.03658537f;

		public float HeldPawnBodyAngle => Rot4.North.AsAngle;

		public PawnPosture HeldPawnPosture => PawnPosture.Standing;

		public Graphic StatueBaseGraphic => Props.statueBaseGraphic.GraphicColoredFor(parent);

		public Pawn InnerPawn
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
				if (value == null)
				{
					innerContainer.Clear();
					return;
				}
				if (innerContainer.Count > 0)
				{
					innerContainer.Clear();
				}
				innerContainer.TryAdd(value);
			}
		}

		public CompEntityStatue()
		{
			innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: true, LookMode.Reference, removeContentsIfDestroyed: false);
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public override void PostPostGeneratedForTrader(TraderKindDef trader, PlanetTile forTile, Faction forFaction)
		{
			InitEntity();
		}

		private void InitEntity()
		{
			PawnKindDef kind = null;
			if(Props.entityOptions.TryRandomElementByWeight((x) => x.selectionWeight * (Find.HiddenItemsManager.Hidden(x.kind.race) ? Props.entityWeightNotDiscovered : 1f), out var result))
			{
				kind = result.kind;
			}
			statuePawn = PawnGenerator.GeneratePawn(kind);
			statuePawn.Drawer.renderer.SetStatue(parent.Stuff);
			statuePawn.Drawer.renderer.SetAllGraphicsDirty();
			statuePawn.Drawer.renderer.EnsureGraphicsInitialized();
			Notify_ColorChanged();
			innerContainer.TryAddOrTransfer(statuePawn);
		}

		public override bool DontDrawParent()
		{
			return true;
		}

		public override void DrawAt(Vector3 drawPos, bool flip = false)
		{
			Vector3 loc = new Vector3(drawPos.x, drawPos.y - 0.03658537f, drawPos.z - 0.15f);
			StatueBaseGraphic.Draw(loc, flip ? parent.Rotation.Opposite : parent.Rotation, parent);
			if (statuePawn == null)
			{
				InitEntity();
			}
			statuePawn.Drawer.renderer.RenderPawnAt(drawPos, Rot4.South, neverAimWeapon: true);
		}

		public override void Notify_ColorChanged()
		{
			if (statuePawn != null)
			{
				statuePawn.Drawer.renderer.SetStatue(parent.Stuff);
				statuePawn.Drawer.renderer.SetStatuePaintColor(null);
				statuePawn.Drawer.renderer.SetAllGraphicsDirty();
				statuePawn.Drawer.renderer.EnsureGraphicsInitialized();
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
		}
	}
}
