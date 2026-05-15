using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.IO;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RimWorld.SketchGen;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Grammar;
using Verse.Noise;
using Verse.Profile;
using Verse.Sound;
using Verse.Steam;
using UnityEngine;
using System.Diagnostics;

namespace NAT
{
	[DefOf]
	public static class NATRADefOf
	{

		public static ThingDef NAT_RustedMassIncoming;

		public static ThingDef NAT_RustedTrooperIncoming;

		public static ThingDef NAT_RustedWall;

		public static ThingDef NAT_RustedDoor;

		public static ThingDef NAT_RustedDoor_Double;

		public static ThingDef NAT_RustedTurret_Mini;

		public static ThingDef NAT_RustedTurret_Auto;

		public static ThingDef NAT_RustedTurret_Sniper;

		public static ThingDef NAT_RustedTurret_Foam;

		public static ThingDef NAT_RustedBeacon_Reinforcements;

		public static ThingDef NAT_RustedChunkPawnIncoming;

		public static ThingDef NAT_RustedChunk1x1Incoming;

		public static ThingDef NAT_RustedChunk2x2Incoming;

		public static ThingDef NAT_RustedChunk3x3Incoming;

		public static ThingDef NAT_RustedCore;

		public static ThingDef NAT_RustedPallet;

		public static ThingDef NAT_RustedBroadcastDish;

		public static ThingDef NAT_RustedArmyBanner;

		public static ThingDef NAT_Mote_ArtOfWarPreCast;

		public static JobDef NAT_DanceRust;

		public static JobDef NAT_UseItemByRust;

		public static JobDef NAT_RustedTurretSetUp;

		public static PawnKindDef NAT_RustedMass;

		public static PawnKindDef NAT_RustedBannerman;

		public static PawnKindDef NAT_RustedSoldier;

		public static PawnKindDef NAT_RustedOfficer;

		public static PawnGroupKindDef NAT_RustedArmy;

		public static PawnGroupKindDef NAT_RustedArmyDefence;

		public static PawnGroupKindDef NAT_RustedArmyBarracks;

		public static PawnTableDef NAT_Rusts;

		public static PawnTableDef NAT_RustsWork;

		public static NeedDef NAT_RustRest;

		public static DutyDef NAT_RustAssaultColony;

		public static DutyDef NAT_RustDefend;

		public static DutyDef NAT_RustDance;

		public static DutyDef NAT_RustExitMap;

		public static StatDef NAT_CoreDropChance;

		public static StatDef NAT_ReinforcementsCooldown;

		public static LayoutRoomDef NAT_OutpostCorridor;

		public static LayoutRoomDef NAT_CitadelCorridor;

		[MayRequireOdyssey]
		public static OrbitalDebrisDef NAT_RustedDebris;

		public static PrefabDef NAT_RustedDish;

		public static PrefabDef NAT_RustedAutoTurretLabyrinth;

		public static EffecterDef NAT_HateVaporize_Heatwave;

		public static SoundDef NAT_World_RustedBannerCall;

		public static DamageDef NAT_RustedBomb;

		public static IncidentDef NAT_RustedArmySiege;

		public static StatCategoryDef NAT_Skills;

		public static ThinkTreeDef NAT_RustedTurret;

		public static TerrainDef NAT_RustedFloor;

		public static TerrainDef NAT_AncientCarpet;

		public static FleckDef NAT_RustedSmoke;
	}
}
