namespace Server
{
    public class UsedSkillMsg
    {
        public ushort shipId;

        public bool success { get; set; }

        public ushort origin { get; set; }

        public string weaponId { get; set; }

        public long timestamp { get; set; }
    }
}