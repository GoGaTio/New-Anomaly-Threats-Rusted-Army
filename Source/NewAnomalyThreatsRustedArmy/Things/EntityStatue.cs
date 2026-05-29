using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace NAT
{
	/*public class EntityStatue : Building, IThingHolder
	{
		protected ThingOwner<Pawn> innerContainer;

		public Pawn InnerPawn
		{
			get
			{
				if (innerContainer.Count <= 0)
				{
					InitEntity();
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

		public EntityStatue()
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

		private void InitEntity()
		{
			if(Stuff == null)
			{
				this.SetStuffDirect(ThingDefOf.Steel);
			}
			PawnKindDef kind = null;
			if (Rand.Bool)
			{
				kind = PawnKindDefOf.Ghoul;
			}
			else
			{
				kind = PawnKindDefOf.Metalhorror;
			}
			InnerPawn = null;
			Pawn statuePawn = PawnGenerator.GeneratePawn(kind, Faction.OfEntities);
			statuePawn.Rotation = Rotation;
			statuePawn.Drawer.renderer.SetStatue(Stuff);
			statuePawn.Drawer.renderer.SetAllGraphicsDirty();
			statuePawn.Drawer.renderer.EnsureGraphicsInitialized();
			statuePawn.Drawer.Notify_DamageApplied
			InnerPawn = statuePawn;
			Notify_ColorChanged();
		}

		public override void Notify_ColorChanged()
		{
			base.Notify_ColorChanged();
			Pawn statuePawn = InnerPawn;
			if (statuePawn != null)
			{
				statuePawn.Drawer.renderer.SetStatue(Stuff);
				statuePawn.Drawer.renderer.SetStatuePaintColor(null);
				statuePawn.Drawer.renderer.SetAllGraphicsDirty();
				statuePawn.Drawer.renderer.EnsureGraphicsInitialized();
			}
		}

		public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
		{
			InnerPawn.DynamicDrawPhaseAt(phase, drawLoc.WithYOffset(InnerPawn.Drawer.SeededYOffset));
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
		}
	}*/
}
