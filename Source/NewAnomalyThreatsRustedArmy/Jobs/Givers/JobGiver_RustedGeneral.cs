using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace NAT
{
	public class JobGiver_RustedGeneral : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.Map == null)
			{
				return null;
			}
			if (pawn.CurJob?.ability?.def != null)
			{
				return null;
			}
			if (pawn.Faction?.IsPlayer != false || !(pawn is RustedPawn rust && rust.Awake()))
			{
				return null;
			}
			Ability artOfWar = pawn.abilities?.GetAbility(NATRADefOf.NAT_ArtOfWar);
			if (artOfWar != null && !artOfWar.OnCooldown && ShouldUseArtOfWar(rust))
			{
				return artOfWar.GetJob(pawn, pawn);
			}
			return null;
		}

		private bool ShouldUseArtOfWar(RustedPawn rust)
		{
			if (rust.lord?.lastPawnHarmTick < 0)
			{
				return false;
			}
			List<Pawn> pawns = rust.Map.mapPawns.SpawnedPawnsInFaction(rust.Faction);
			if (pawns.NullOrEmpty())
			{
				return false;
			}
			pawns.RemoveWhere(x => !x.Position.InHorDistOf(rust.Position, 65f));
			if (pawns.Count < 5)
			{
				return false;
			}
			if (rust.mindState?.enemyTarget != null)
			{
				float distanceEnemy = rust.mindState.enemyTarget.Position.DistanceTo(rust.Position);
				if (distanceEnemy > 30f && distanceEnemy <= 65f)
				{
					return true;
				}
			}
			List<Thing> list = rust.Map.listerThings.ThingsInGroup(ThingRequestGroup.AttackTarget).Where(ValidateEnemy).ToList();
			bool ValidateEnemy(Thing t)
			{
				float distance = t.Position.DistanceTo(rust.Position);
				if (distance < 20f || distance > 65f)
				{
					return false;
				}
				if (!t.HostileTo(rust))
				{
					return false;
				}
				if ((t as IAttackTarget).ThreatDisabled(null))
				{
					return false;
				}
				return true;
			}
			if (list.NullOrEmpty())
			{
				return false;
			}
			return true;
		}
	}
}
