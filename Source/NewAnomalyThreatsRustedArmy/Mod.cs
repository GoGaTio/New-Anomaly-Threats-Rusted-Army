using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace NAT
{
	public class SubModSettings_RustedArmy : SubModSettings
	{
		public static SubModSettings_RustedArmy Value;

		public SubModSettings_RustedArmy()
		{
			Value = this;
		}

		public bool showGearTabOnEnemies = false;

		public bool allowMechanitorRusts = false;

		public override void DoSettings(Rect inRect)
		{
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);
			listingStandard.CheckboxLabeled("NAT_Setting_ShowRustsGear".Translate(), ref showGearTabOnEnemies, "NAT_Setting_ShowRustsGear_Desc".Translate());
			bool flag = allowMechanitorRusts;
			listingStandard.CheckboxLabeled("NAT_Setting_AllowMechanitorRust".Translate(), ref flag, "NAT_Setting_AllowMechanitorRust_Desc".Translate());
			if(flag != allowMechanitorRusts)
			{
				Messages.Message(NewAnomalyThreatsUtility.NeedGameRestart, MessageTypeDefOf.SilentInput, false);
				allowMechanitorRusts = flag;
			}
			listingStandard.End();
		}

		public override string SettingsName => "Rusted Army";

		public override void ExposeData()
		{
			Scribe_Values.Look(ref showGearTabOnEnemies, "showGearTabOnEnemies", defaultValue: false);
			Scribe_Values.Look(ref allowMechanitorRusts, "allowMechanitorRusts", defaultValue: false);
		}
	}
}
