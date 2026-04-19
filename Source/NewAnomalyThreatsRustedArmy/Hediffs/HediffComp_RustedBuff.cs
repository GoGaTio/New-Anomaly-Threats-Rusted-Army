using NAT;
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
	public class HediffCompProperties_RustedBuff : HediffCompProperties
	{
		public int disappearsAfter = 300;

		public int checkInterval = 10;

		public HediffCompProperties_RustedBuff()
		{
			compClass = typeof(HediffComp_RustedBuff);
		}
	}
	public class HediffComp_RustedBuff : HediffComp
	{
		private Dictionary<Thing, int> affecters = new Dictionary<Thing, int>();

		public HediffCompProperties_RustedBuff Props => (HediffCompProperties_RustedBuff)props;

		public override bool CompShouldRemove
		{
			get
			{
				if (base.CompShouldRemove)
				{
					return true;
				}
				if (!parent.pawn.SpawnedOrAnyParentSpawned)
				{
					return true;
				}
				if (affecters.NullOrEmpty())
				{
					return true;
				}
				return false;
			}
		}

		public override string CompLabelInBracketsExtra => parent.Severity > 1 ? ("x" + affecters.Count) : null;

		public void AffectTick(Thing thing)
		{
			if (affecters.ContainsKey(thing))
			{
				affecters[thing] = Props.disappearsAfter;
			}
			else
			{
				affecters.Add(thing, Props.disappearsAfter);
			}
		}

		public override void CompPostTick(ref float severityAdjustment)
		{
			if (parent.pawn.IsHashIntervalTick(Props.checkInterval))
			{
				foreach (Thing t in affecters.Keys.ToList())
				{
					affecters[t] -= Props.checkInterval;
					if(affecters[t] <= 0 || t.Destroyed)
					{
						affecters.Remove(t);
					}
				}
				parent.Severity = affecters.Count;
			}
		}

		private List<Thing> affectersKeys;

		private List<int> affectersValues;

		public override void CompExposeData()
		{
			base.CompExposeData();
			if(Scribe.mode == LoadSaveMode.Saving)
			{
				foreach (Thing t in affecters.Keys.ToList())
				{
					if (affecters[t] <= 0 || t.Destroyed)
					{
						affecters.Remove(t);
					}
				}
			}
			Scribe_Collections.Look(ref affecters, "affecters", LookMode.Reference, LookMode.Value, ref affectersKeys, ref affectersValues);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				affecters.RemoveAll((x) => x.Key == null);
			}
		}

		public override string CompDebugString()
		{
			string s = "affecters:";
			foreach(var item in affecters)
			{
				s += "\n" + item.Key.LabelCap + " - " + item.Value;
			}
			return s;
		}
	}
}
