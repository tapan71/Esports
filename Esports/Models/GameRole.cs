namespace Esports.Models
{
    public class GameRole
    {
        public int Id { get; set; }
        public string RoleName { get; set; }   // e.g. "Top", "Jungle", "Mid", "ADC", "Support"

        public int GameId { get; set; }
        public Game Game { get; set; }
    }
}
