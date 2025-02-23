using TaleWorlds.MountAndBlade;

namespace IronBloodSiege.Util
{
    public static class AgentPd
    {
        public static bool IsEnemy(this Agent agent)
        {
            Mission mission = Mission.Current;
            return ((mission != null) ? mission.PlayerTeam : null) != null && ((agent != null) ? agent.Team : null) != null && Mission.Current.PlayerTeam.Side != agent.Team.Side;
        }
    }
}
