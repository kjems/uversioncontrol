using System.Collections.Concurrent;
using Unity.Profiling;

namespace UVC
{
    public static class ProfilerScope
    {
        static readonly ConcurrentDictionary<string, ProfilerMarker> markerCache = new ConcurrentDictionary<string, ProfilerMarker>();
        public static ProfilerMarker.AutoScope Get(string name)
        {
            if (!markerCache.TryGetValue(name, out var marker))
            {
                marker = new ProfilerMarker(name);
                markerCache[name] = marker;
            }
            return marker.Auto();
        }
    }
}
