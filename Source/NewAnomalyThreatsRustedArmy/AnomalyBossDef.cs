using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Noise;

namespace NAT
{
	public class AnomalyBoss_Rust : AnomalyBoss_Pawn
	{
		public override bool TryArriveInt(List<Pawn> list)
		{
			return RustedArmyUtility.TryArriveRusts(list, Map, true);
		}

		public override void GenerateLord(List<Pawn> list, Map map)
		{
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_EscortAndDefendRust(list[0]), map, list);
		}
	}
}
