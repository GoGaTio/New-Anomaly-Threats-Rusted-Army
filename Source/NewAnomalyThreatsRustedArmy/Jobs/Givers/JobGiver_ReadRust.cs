using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NAT
{
	public class JobGiver_ReadRust : ThinkNode_JobGiver
	{
		private List<Thing> tmpCandidates = new List<Thing>();

		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.kindDef.isBoss && pawn is RustedPawn rust && rust.preventReadingTill < Find.TickManager.TicksGame && BookUtility.CanReadEver(rust))
			{
				if (TryGetRandomBookToRead(pawn, out var book))
				{
					rust.preventReadingTill = Find.TickManager.TicksGame + 30000;
					return JobMaker.MakeJob(JobDefOf.Reading, book);
				}
				rust.preventReadingTill = Find.TickManager.TicksGame + 10000;
			}
			return null;
		}

		private bool TryGetRandomBookToRead(Pawn pawn, out Book book)
		{
			book = null;
			tmpCandidates.Clear();
			tmpCandidates.AddRange(from thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Book)
								   where IsValidBook(thing, pawn)
								   select thing);
			tmpCandidates.AddRange(from thing in pawn.Map.listerThings.GetThingsOfType<Building_Bookcase>().SelectMany((Building_Bookcase x) => x.HeldBooks)
								   where IsValidBook(thing, pawn)
								   select thing);
			if (tmpCandidates.Empty())
			{
				return false;
			}
			book = (Book)(tmpCandidates.RandomElement());
			tmpCandidates.Clear();
			return true;
		}

		private static bool IsValidBook(Thing thing, Pawn pawn)
		{
			if (thing is Book && !thing.IsForbiddenHeld(pawn) && pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.None) && !thing.Fogged())// && thing.IsPoliticallyProper(pawn))
			{
				return true;
			}
			return false;
		}
	}
}
