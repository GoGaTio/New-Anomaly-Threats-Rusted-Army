using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using System.Xml.Xsl;
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
	/*public class JobDriver_CarryShell : JobDriver
	{
		private Building_TurretGun Turret => (Building_TurretGun)job.GetTarget(TargetIndex.A).Thing;

		private Thing Hauling => job.GetTarget(TargetIndex.B).Thing;

		private static bool GunNeedsLoading(Building b)
		{
			if (!(b is Building_TurretGun building_TurretGun))
			{
				return false;
			}
			CompChangeableProjectile compChangeableProjectile = building_TurretGun.gun.TryGetComp<CompChangeableProjectile>();
			if (compChangeableProjectile == null || compChangeableProjectile.Loaded)
			{
				return false;
			}
			return true;
		}

		public static Thing FindAmmoForTurret(Pawn pawn, Building_TurretGun gun)
		{
			StorageSettings allowedShellsSettings = ((pawn.Faction?.IsPlayer == true) ? gun.gun.TryGetComp<CompChangeableProjectile>().allowedShellsSettings : null);
			return GenClosest.ClosestThingReachable(gun.Position, gun.Map, ThingRequest.ForGroup(ThingRequestGroup.Shell), PathEndMode.OnCell, TraverseParms.For(pawn), 40f, ShellValidator);
			bool ShellValidator(Thing t)
			{
				if (t.IsForbidden(pawn))
				{
					return false;
				}
				if (!pawn.CanReserve(t, 10, 1))
				{
					return false;
				}
				if (allowedShellsSettings != null && !allowedShellsSettings.AllowedToAccept(t))
				{
					return false;
				}
				if(gun is Building_RustedTurret r && !r.deadlifeAllowed && t.def.defName.Contains("Deadlife"))
				{
					return false;
				}
				return true;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			Toil loadIfNeeded = ToilMaker.MakeToil("MakeNewToils");
			loadIfNeeded.initAction = delegate
			{
				Pawn actor = loadIfNeeded.actor;
				Building obj = (Building)actor.CurJob.targetA.Thing;
				Building_TurretGun building_TurretGun = obj as Building_TurretGun;
				if (!GunNeedsLoading(obj))
				{
					actor.jobs.EndCurrentJob(JobCondition.Succeeded);
				}
				else
				{
					Thing thing = FindAmmoForTurret(pawn, building_TurretGun);
					if (thing == null)
					{
						if (actor.Faction == Faction.OfPlayerSilentFail)
						{
							Messages.Message("MessageOutOfNearbyShellsFor".Translate(actor.LabelShort, building_TurretGun.Label, actor.Named("PAWN"), building_TurretGun.Named("GUN")).CapitalizeFirst(), building_TurretGun, MessageTypeDefOf.NegativeEvent);
						}
						actor.jobs.EndCurrentJob(JobCondition.Incompletable);
					}
					actor.CurJob.targetB = thing;
					actor.CurJob.count = 1;
				}
			};
			yield return loadIfNeeded;
			yield return Toils_Reserve.Reserve(TargetIndex.B, 10, 1);
			yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.OnCell).FailOnSomeonePhysicallyInteracting(TargetIndex.B);
			yield return Toils_Haul.StartCarryThing(TargetIndex.B);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			Toil loadShell = ToilMaker.MakeToil("MakeNewToils");
			loadShell.initAction = delegate
			{
				Pawn actor = loadShell.actor;
				SoundDefOf.Artillery_ShellLoaded.PlayOneShot(new TargetInfo(Turret.Position, Turret.Map));
				Turret.gun.TryGetComp<CompChangeableProjectile>().LoadShell(Hauling.def, 1);
				actor.carryTracker.innerContainer.ClearAndDestroyContents();
				actor.jobs.EndCurrentJob(JobCondition.Succeeded);
			};
			yield return loadShell;
		}
	}*/

	public class JobDriver_UseItemByRust : JobDriver
	{
		private int useDuration = -1;

		private bool usingFromInventory;

		private bool targetsAnotherPawn;

		private Thing Item => job.GetTarget(TargetIndex.A).Thing;

		private RustedPawn Target => job.GetTarget(TargetIndex.B).Thing as RustedPawn;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref useDuration, "useDuration", 0);
			Scribe_Values.Look(ref targetsAnotherPawn, "targetsAnotherPawn", defaultValue: false);
			Scribe_Values.Look(ref usingFromInventory, "usingFromInventory", defaultValue: false);
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			useDuration = job.GetTarget(TargetIndex.A).Thing.TryGetComp<CompUsableByRust>().Props.useDuration;
			job.count = 1;
			usingFromInventory = pawn.inventory != null && pawn.inventory.Contains(Item);
			if (job.GetTarget(TargetIndex.B).Thing != null && job.GetTarget(TargetIndex.B).Thing is RustedPawn rust && rust != pawn as RustedPawn)
			{
				targetsAnotherPawn = true;
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (job.GetTarget(TargetIndex.B).Thing != null)
			{
				if (!pawn.Reserve(Target, job, 1, 1, null, errorOnFailed))
				{
					return false;
				}
			}
			else if (!pawn.Reserve(Item, job, 10, 1, null, errorOnFailed))
			{
				return false;
			}
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
			if (targetsAnotherPawn || pawn is RustedPawn)
			{
				foreach (Toil item in PrepareToUseToils())
				{
					yield return item;
				}
				Toil toil1 = (targetsAnotherPawn ? Toils_General.WaitWith(TargetIndex.B, useDuration, maintainPosture: true, maintainSleep: true) : Toils_General.Wait(useDuration, TargetIndex.A));
				toil1.WithProgressBarToilDelay(targetsAnotherPawn ? TargetIndex.B : TargetIndex.A);
				toil1.handlingFacing = true;
				toil1.tickAction = delegate
				{
					if (targetsAnotherPawn)
					{
						pawn.rotationTracker.FaceTarget(Target);
					}
				};
				yield return toil1;
				Toil use = ToilMaker.MakeToil("Use");
				use.initAction = delegate
				{
					CompUsableByRust comp = Item.TryGetComp<CompUsableByRust>();
					comp.UsedBy(targetsAnotherPawn ? Target : (pawn as RustedPawn));
				};
				use.defaultCompleteMode = ToilCompleteMode.Instant;
				yield return use;
			}
			else
			{
				this.FailOn(() => true);
			}
		}

		private IEnumerable<Toil> PrepareToUseToils()
		{
			if (usingFromInventory)
			{
				yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
			}
			else
			{
				yield return ReserveItem();
				yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
				Toil toil = ToilMaker.MakeToil("PickupItem");
				toil.initAction = delegate
				{
					Pawn actor = toil.actor;
					Job curJob = actor.jobs.curJob;
					Thing thing = Item;
					actor.carryTracker.TryStartCarry(thing, 1);
					if (thing != actor.carryTracker.CarriedThing && actor.Map.reservationManager.ReservedBy(thing, actor, curJob))
					{
						actor.Map.reservationManager.Release(thing, actor, curJob);
					}
					actor.jobs.curJob.targetA = actor.carryTracker.CarriedThing;
				};
				toil.defaultCompleteMode = ToilCompleteMode.Instant;
				yield return toil;
			}
			if (targetsAnotherPawn)
			{
				yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.B);
			}
		}

		private Toil ReserveItem()
		{
			Toil toil = ToilMaker.MakeToil("ReserveItem");
			toil.initAction = delegate
			{
				if (pawn.Faction != null)
				{
					Thing thing = job.GetTarget(TargetIndex.A).Thing;
					if (pawn.carryTracker.CarriedThing != thing)
					{
						if (!pawn.Reserve(thing, job, 10, 1))
						{
							Log.Error(string.Concat("NAT RustedPawn usable reservation for ", pawn, " on job ", this, " failed, because it could not register item from ", thing));
							pawn.jobs.EndCurrentJob(JobCondition.Errored);
						}
						job.count = 1;
					}
				}
			};
			toil.defaultCompleteMode = ToilCompleteMode.Instant;
			toil.atomicWithPrevious = true;
			return toil;
		}
	}

	public class JobDriver_DanceRust : JobDriver
	{
		private bool jumping;

		private int moveChangeInterval = 240;

		public int AgeTicks => Find.TickManager.TicksGame - startTick;

		public override Vector3 ForcedBodyOffset
		{
			get
			{
				float num = Mathf.Sin((float)AgeTicks / 60f * 16f);
				if (jumping)
				{
					float z = Mathf.Max(Mathf.Pow((num + 1f) * 0.5f, 2f) * 0.2f - 0.06f, 0f);
					return new Vector3(0f, 0f, z);
				}
				float num2 = Mathf.Sign(num);
				return new Vector3(EasingFunctions.EaseInOutQuad(Mathf.Abs(num) * 0.6f) * 0.09f * num2, 0f, 0f);
			}
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			pawn.Rotation = Rot4.Random;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			Toil toil = ToilMaker.MakeToil("MakeNewToils");
			jumping = Rand.Bool;
			toil.tickIntervalAction = delegate
			{
				if (AgeTicks % moveChangeInterval == 0)
				{
					jumping = !jumping;
				}
				if (AgeTicks % 120 == 0 && !jumping)
				{
					pawn.Rotation = Rot4.Random;
				}
			};
			toil.defaultCompleteMode = ToilCompleteMode.Never;
			toil.handlingFacing = true;
			yield return toil;
		}
	}

	public class JobDriver_TurretSetUp : JobDriver
	{
		public int AgeTicks => Find.TickManager.TicksGame - startTick;

		private IntVec3 Cell => job.GetTarget(TargetIndex.A).Cell;

		public CompRustedTurretPawn Comp => pawn.GetComp<CompRustedTurretPawn>();

		public override Vector3 ForcedBodyOffset
		{
			get
			{
				if(CurToilIndex == 1 && Comp.Props.buildingDef.Size.x == 2)
				{
					return Vector3.one * 0.5f * ((180f - (float)ticksLeftThisToil) / 180f);
				}
				return Vector3.zero;
			}
		}

		public override void Notify_Starting()
		{
			if(job.targetA == null)
			{
				job.SetTarget(TargetIndex.A, pawn.Position);
			}
			base.Notify_Starting();
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
			Toil toil1 = ToilMaker.MakeToil("Wait");
			toil1.initAction = delegate
			{
				toil1.actor.pather.StopDead();
			};
			toil1.defaultCompleteMode = ToilCompleteMode.Delay;
			toil1.defaultDuration = 180;
			toil1.WithProgressBarToilDelay(TargetIndex.None, 180);
			yield return toil1;
			Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
			toil2.initAction = delegate
			{
				if (!Comp.RectOccupied)
				{
					Log.Message(toil2.actor.ToString() + ":SpawnedTurret");
					Comp.SpawnTurret(Cell, toil2.actor.Map);
				}
			};
			toil2.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return toil2;
		}
	}
}