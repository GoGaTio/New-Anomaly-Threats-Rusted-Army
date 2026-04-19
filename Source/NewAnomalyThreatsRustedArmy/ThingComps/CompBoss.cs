using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace NAT
{
	public class CompProperties_BossStages : CompProperties
	{
		public class BossStage
		{
			public BossStage() { }

			public List<StatModifier> statOffsets;

			public List<StatModifier> statFactors;
		}

		public List<BossStage> stages = new List<BossStage>();

		public CompProperties_BossStages()
		{
			compClass = typeof(CompBossStages);
		}
	}
	public class CompBossStages : ThingComp
	{
		public CompProperties_BossStages Props => (CompProperties_BossStages)props;

		public int currentBossStage;

		public override float GetStatFactor(StatDef stat)
		{
			return Props.stages[currentBossStage].statFactors.GetStatOffsetFromList(stat); ;
		}

		public override float GetStatOffset(StatDef stat)
		{
			return Props.stages[currentBossStage].statOffsets.GetStatOffsetFromList(stat);
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref currentBossStage, "currentBossStage", 0);
		}
	}
}
