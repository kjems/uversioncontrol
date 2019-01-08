using System.Collections.Generic;
using Unity.Profiling;

namespace UVC
{
    public static class ProfilerScope
    {
        static readonly Dictionary<string, ProfilerMarker> markerCache = new Dictionary<string, ProfilerMarker>();
        public static ProfilerMarker.AutoScope Get(string name)
        {
            if (!markerCache.TryGetValue(name, out var marker))
            {
                marker = new ProfilerMarker(name);
                markerCache.Add(name, marker);
            }
            return marker.Auto();
        }
    }
}
