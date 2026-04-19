using LudeonTK;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using static RimWorld.FleshTypeDef;
using static System.Collections.Specialized.BitVector32;

namespace NAT
{
	public class PsychicRitualDef_CreateRust : PsychicRitualDef_CreatePawn
	{
		public override PsychicRitualCandidatePool FindCandidatePool()
		{
			PsychicRitualCandidatePool pool = base.FindCandidatePool();
			List<Pawn> list = pool.AllCandidatePawns;
			if(Faction.OfPlayerSilentFail != null)
			{
				list.AddRange(Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayerSilentFail).Where((Pawn p) => p is RustedPawn rust && rust.Controllable));
			}
			return new PsychicRitualCandidatePool(list, pool.NonAssignablePawns);
		}
	}
}