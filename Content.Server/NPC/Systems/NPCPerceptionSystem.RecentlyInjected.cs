using Content.Shared._Moffstation.NPC.Systems;
using Content.Shared.NPC.Components;
using System.Linq;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCPerceptionSystem
{
    [Dependency] private NPCRecentlyInjectedSystem _npcRecentlyInjectedSystem = default!; // Moffstation = References the new NPCRecentlyInjectedSystem

    /// <summary>
    /// Tracks targets recently injected by medibots.
    /// </summary>
    /// <param name="frameTime"></param>
    private void UpdateRecentlyInjected(float frameTime)
    {
        var query = EntityQueryEnumerator<NPCRecentlyInjectedComponent>();
        while (query.MoveNext(out var uid, out var entity))
        {
            // Moffstation - Begin - Replace default behavior with a dictionary of 'treated' damage types, so that refillable medibots with different damage types can still treat at the same time.
            _npcRecentlyInjectedSystem.AddTime(uid, frameTime);
            //entity.Accumulator += frameTime;
            //if (entity.Accumulator < entity.RemoveTime.TotalSeconds)
            //    continue;
            //entity.Accumulator = 0;

            //RemComp<NPCRecentlyInjectedComponent>(uid);

            if (_npcRecentlyInjectedSystem.GetEntryCount(uid) == 0) // If recent treatments are empty, remove the component so that regular medibots can inject.
                RemComp<NPCRecentlyInjectedComponent>(uid);
            // Moffstation - End - Replace default behavior with a dictionary of 'treated' damage types, so that refillable medibots with different damage types can still treat at the same time.
        }
    }
}
