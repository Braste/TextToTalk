using Dalamud.Logging;
using TextToTalk.Backends;

namespace TextToTalk.Migrations
{
    public class Migration1_7 : IConfigurationMigration
    {
        public bool ShouldMigrate(PluginConfiguration config)
        {
            return !config.MigratedTo1_7;
        }

        public void Migrate(PluginConfiguration config)
        {
#pragma warning disable 618
            if (config.FirstTime)
            {
                config.IsInitialized = config.FirstTime;
            }
#pragma warning restore 618
            config.MigratedTo1_7 = true;
            PluginLog.Log("Configuration migrated to version 1.7");
        }
    }
}