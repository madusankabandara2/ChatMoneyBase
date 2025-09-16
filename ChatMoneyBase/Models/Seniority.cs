namespace ChatMoneyBase.Models
{
    // Seniority levels and helper to get multiplier.
    public enum Seniority
    {
        Junior,
        Mid,
        Senior,
        TeamLead
    }

    public static class SeniorityHelper
    {
        // Multipliers from the task provided: junior 0.4, mid 0.6, senior 0.8, teamlead 0.5
        public static double GetMultiplier(Seniority s) => s switch
        {
            Seniority.Junior => 0.4,
            Seniority.Mid => 0.6,
            Seniority.Senior => 0.8,
            Seniority.TeamLead => 0.5,
            _ => 0.4
        };
    }
}
