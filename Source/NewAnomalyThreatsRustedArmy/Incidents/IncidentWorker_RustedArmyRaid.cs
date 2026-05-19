using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using RimWorld.Utility;
using LudeonTK;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NAT
{
	public class IncidentWorker_RustedArmyRaid : IncidentWorker
	{
		public static readonly SimpleCurve PointsFromPoints = new SimpleCurve
		{
			new CurvePoint(1000f, 900f),
			new CurvePoint(10000f, 11000f)
		};

		private static readonly SimpleCurve GroupsChanceFromPoints = new SimpleCurve
		{
			new CurvePoint(0f, 0f),
			new CurvePoint(1000f, 0f),
			new CurvePoint(2000f, 0.2f),
			new CurvePoint(10000f, 0.9f)
		};
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			Map map = (Map)parms.target;
			if (!map.TileInfo.OnSurface || Rand.Chance(0.3f))
			{
				RustedArmyUtility.ExecuteRaid(map, PointsFromPoints.Evaluate(parms.points), 1, false, true, null, null, true, Rand.Chance(0.5f));
				return true;
			}
			RustedArmyUtility.ExecuteRaid(map, PointsFromPoints.Evaluate(parms.points), Rand.Chance(GroupsChanceFromPoints.Evaluate(parms.points)) ? new IntRange(2, 3).RandomInRange : 1, Rand.Chance(0.2f));
			return true;
		}
	}
}