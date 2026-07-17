using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using static System.Net.Mime.MediaTypeNames;

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

	public class Graphic_RustedMechanism : Graphic_Collection
	{
		public override Material MatSingle => subGraphics[0].MatSingle;

		public override Graphic GetColoredVersion(Shader newShader, Color newColor, Color newColorTwo)
		{
			return GraphicDatabase.Get<Graphic_RustedMechanism>(path, newShader, drawSize, newColor, newColorTwo, data);
		}

		public override Material MatAt(Rot4 rot, Thing thing = null)
		{
			if (thing == null)
			{
				return MatSingle;
			}
			return MatSingleFor(thing);
		}

		public override Material MatSingleFor(Thing thing)
		{
			if (thing == null)
			{
				return MatSingle;
			}
			return SubGraphicFor(thing).MatSingle;
		}

		public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
		{
			((thing != null) ? SubGraphicFor(thing) : subGraphics[0]).DrawWorker(loc, rot, thingDef, thing, extraRotation);
			if (base.ShadowGraphic != null)
			{
				base.ShadowGraphic.DrawWorker(loc, rot, thingDef, thing, extraRotation);
			}
		}

		public override void Print(SectionLayer layer, Thing thing, float extraRotation)
		{
			((thing != null) ? SubGraphicFor(thing) : subGraphics[0]).Print(layer, thing, extraRotation);
			if (base.ShadowGraphic != null && thing != null)
			{
				base.ShadowGraphic.Print(layer, thing, extraRotation);
			}
		}

		private Graphic SubGraphicFor(Thing thing)
		{
			if(thing is RustedMechanism item)
			{
				int num = Mathf.Min(Mathf.FloorToInt((float)subGraphics.Length * item.BioferritePercent), subGraphics.Length - 1);
				return subGraphics[num];
			}
			return subGraphics[0];
		}

		public override string ToString()
		{
			return "RustedMechanism(path=" + path + ", count=" + subGraphics.Length + ")";
		}
	}

	[PostDefLoadedNotify]
	public static class RustRestLabelAdjuster
	{
		public static void Notify_DefsLoaded()
		{
			NATRADefOf.NAT_RustRest.description = NeedDefOf.Rest.description;
			NATRADefOf.NAT_RustRest.label = NeedDefOf.Rest.label;
			List<DamageDef> damages = DefDatabase<DamageDef>.AllDefs.Where((x) => x.defName.Contains("Acid")).ToList();
			foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where((x) => typeof(RustedPawn).IsAssignableFrom(x.thingClass) || typeof(Building_RustedTurret).IsAssignableFrom(x.thingClass)))
			{
				if (def.damageMultipliers.NullOrEmpty())
				{
					def.damageMultipliers = new List<DamageMultiplier>();
				}
				foreach (DamageDef damage in damages)
				{
					def.damageMultipliers.Add(new DamageMultiplier() { damageDef = damage, multiplier = 0.25f });
				}
			}
		}
	}

	public class RustedMechanismActivityWorker : ActivityWorker_Outside
	{
		public override float GetChangeRatePerDay(ThingWithComps thing)
		{
			return base.GetChangeRatePerDay(thing) + thing.GetComp<CompRustedMechanism>().ActivityPerDay;
		}

		public override void GetSummary(ThingWithComps thing, StringBuilder sb)
		{
			float change = thing.GetComp<CompRustedMechanism>().ActivityPerDay;
			base.GetSummary(thing, sb);
			if (change >= 0.01f)
			{
				sb.Append(string.Format("\n - {0}: {1}", "NAT_BioferriteOnSurface".Translate(), change.ToStringPercent("0")));
			}
		}
	}

	/*public class NewAnomalyThreatsRustedArmySettings : ModSettings
    {

		public bool rustedSoldierName_Draft = true;
		public bool rustedSoldierName_NoDraft = true;
		public bool rustedSoldierWeaponChange = true;
		public bool rustedSoldierDeathNotification = true;
		public bool allowEndGameRaid = true;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref rustedSoldierName_Draft, "rustedSoldierName_Draft", true);
			Scribe_Values.Look(ref rustedSoldierName_NoDraft, "rustedSoldierName_Draft", true);
			Scribe_Values.Look(ref rustedSoldierWeaponChange, "rustedSoldierWeaponChange", true);
			Scribe_Values.Look(ref rustedSoldierDeathNotification, "rustedSoldierDeathNotification", true);
			Scribe_Values.Look(ref allowEndGameRaid, "allowEndGameRaid", true);
			base.ExposeData();
		}
	}

	public class NewAnomalyThreatsRustedArmyMod : Mod
	{

		NewAnomalyThreatsRustedArmySettings settings;

		public NewAnomalyThreatsRustedArmyMod(ModContentPack content) : base(content)
		{
			this.settings = GetSettings<NewAnomalyThreatsRustedArmySettings>();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);
			//listingStandard.CheckboxLabeled("NAT_Setting_NameDraft".Translate(), ref settings.rustedSoldierName_Draft, "NAT_Setting_NameDraft_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_NameNoDraft".Translate(), ref settings.rustedSoldierName_NoDraft, "NAT_Setting_NameNoDraft_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_WeaponChange".Translate(), ref settings.rustedSoldierWeaponChange, "NAT_Setting_WeaponChange_Desc".Translate());
			//listingStandard.CheckboxLabeled("NAT_Setting_DeathNotification".Translate(), ref settings.rustedSoldierDeathNotification, "NAT_Setting_DeathNotification_Desc".Translate());
			listingStandard.CheckboxLabeled("NAT_Setting_AllowRaid".Translate(), ref settings.allowEndGameRaid, "NAT_Setting_AllowRaid_Desc".Translate());
			listingStandard.End();
			base.DoSettingsWindowContents(inRect);
		}
		public override string SettingsCategory()
		{
			return "New Anomaly Threats: Rusted Army";
		}
	}*/
}
