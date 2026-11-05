using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.HttpAuth.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {

        public PluginConfiguration()
        {
            EnablePlugin = false;
            EnableSafetyBreaker = true;
            BreakerTripped = false;
            UserHeader = "X-Forwarded-User";
            AllowedFolderIds = Array.Empty<string>();
        }

        public bool EnablePlugin { get; set; }
        public bool EnableSafetyBreaker { get; set; }
        public bool BreakerTripped { get; set; }
        public string UserHeader { get; set; }

        // Library (virtual folder) item ids that newly created users get access to. Users are never created as admins.
        public string[] AllowedFolderIds { get; set; }

    }
}
