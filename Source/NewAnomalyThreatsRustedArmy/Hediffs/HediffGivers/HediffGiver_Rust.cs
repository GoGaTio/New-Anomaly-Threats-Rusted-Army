using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace NAT
{
	public class HediffGiver_Rust : HediffGiver
	{
		public List<HediffDef> hediffs = new List<HediffDef>();

		public override bool OnHediffAdded(Pawn pawn, Hediff hediff)
		{
			if (hediff.def.lethalSeverity > 0 || hediffs.Contains(hediff.def))
			{
				pawn.health.RemoveHediff(hediff);
			}
			return false;
		}
	}
}
