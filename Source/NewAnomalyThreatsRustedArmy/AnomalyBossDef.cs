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
	public class AnomalyBossDef_Rust : AnomalyBossDef
	{
		public override void ArriveInt(List<Pawn> list, AnomalyBossManager.AnomalyBoss boss)
		{
			RustedArmyUtility.RustsArrive(list, boss.Map, true);
		}

		public override void GenerateLord(List<Pawn> list, AnomalyBossManager.AnomalyBoss boss)
		{
			LordMaker.MakeNewLord(Faction.OfEntities, new LordJob_EscortAndDefendRust(list[0]), boss.Map, list);
		}
	}
}
