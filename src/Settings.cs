namespace SiegeMoraleAdjuster
{
    public class Settings
    {
        private static Settings _instance;
        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Settings();
                }
                return _instance;
            }
        }

        public bool IsEnabled { get; set; }
        public float MoraleThreshold { get; set; }
        public float MoraleBoostRate { get; set; }

        private Settings()
        {
            IsEnabled = true;
            MoraleThreshold = 80f;
            MoraleBoostRate = 10f;
        }
    }
} 