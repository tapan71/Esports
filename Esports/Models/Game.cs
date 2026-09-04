namespace Esports.Models
{
    public class Game
    {
        public int Id { get; set; }
        public string Name { get; set; }   // e.g. "League of Legends"

        public ICollection<Team> Teams { get; set; }
        public ICollection<GameRole> GameRoles { get; set; }
    }
}
