using  PhoenixNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace  PhoenixNet
{
    public static class Presence
    {
        public static Dictionary<string, PresenceEntry> SyncState(
            Dictionary<string, PresenceEntry> currentState,
            Dictionary<string, PresenceEntry> newState,
            Action<string, PresenceEntry, PresenceEntry> onJoin = null,
            Action<string, PresenceEntry, PresenceEntry> onLeave = null)
        {
            var state = Clone(currentState);
            var joins = new Dictionary<string, PresenceEntry>();
            var leaves = new Dictionary<string, PresenceEntry>();

            foreach (var pair in state)
            {
                var key = pair.Key;
                var presence = pair.Value;
                if (!newState.ContainsKey(key))
                {
                    leaves[key] = presence;
                }
            }

            foreach (var pair in newState)
            {
                var key = pair.Key;
                var newPresence = pair.Value;
                if (state.TryGetValue(key, out var currentPresence))
                {
                    var newRefs = newPresence.Metas.Select(m => m.PhxRef).ToList();
                    var curRefs = currentPresence.Metas.Select(m => m.PhxRef).ToList();

                    var joinedMetas = newPresence.Metas.Where(m => !curRefs.Contains(m.PhxRef)).ToList();
                    var leftMetas = currentPresence.Metas.Where(m => !newRefs.Contains(m.PhxRef)).ToList();

                    if (joinedMetas.Count > 0)
                    {
                        joins[key] = new PresenceEntry
                        {
                            Metas = joinedMetas
                        };
                    }

                    if (leftMetas.Count > 0)
                    {
                        leaves[key] = Clone(currentPresence);
                        leaves[key].Metas = leftMetas;
                    }
                }
                else
                {
                    joins[key] = newPresence;
                }
            }

            return SyncDiff(state, joins, leaves, onJoin, onLeave);
        }

        public static Dictionary<string, PresenceEntry> SyncDiff(
            Dictionary<string, PresenceEntry> state,
            Dictionary<string, PresenceEntry> joins,
            Dictionary<string, PresenceEntry> leaves,
            Action<string, PresenceEntry, PresenceEntry> onJoin = null,
            Action<string, PresenceEntry, PresenceEntry> onLeave = null)
        {
            onJoin ??= (_, _, _) => { };
            onLeave ??= (_, _, _) => { };

            foreach (var pair in joins)
            {
                var key = pair.Key;
                var newPresence = pair.Value;
                var currentPresence = state.ContainsKey(key) ? state[key] : null;
                state[key] = newPresence;

                if (currentPresence != null)
                {
                    var metas = new List<PresenceMeta>();
                    metas.AddRange(currentPresence.Metas);
                    metas.AddRange(newPresence.Metas);
                    state[key].Metas = metas;
                }

                onJoin(key, currentPresence, newPresence);
            }

            foreach (var pair in leaves)
            {
                var key = pair.Key;
                var leftPresence = pair.Value;
                if (!state.ContainsKey(key)) continue;
                var currentPresence = state[key];

                var refsToRemove = leftPresence.Metas.Select(m => m.PhxRef).ToList();
                currentPresence.Metas = currentPresence.Metas
                    .Where(p => !refsToRemove.Contains(p.PhxRef))
                    .ToList();

                onLeave(key, currentPresence, leftPresence);

                if (currentPresence.Metas.Count == 0)
                {
                    state.Remove(key);
                }
            }

            return state;
        }

        public static List<T> List<T>(
            Dictionary<string, PresenceEntry> presences,
            Func<string, PresenceEntry, T> chooser = null)
        {
            chooser ??= (key, presence) => (T)(object)presence;

            return presences.Select(kv => chooser(kv.Key, kv.Value)).ToList();
        }

        private static T Clone<T>(T obj)
        {
            if (obj == null) return default;
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}