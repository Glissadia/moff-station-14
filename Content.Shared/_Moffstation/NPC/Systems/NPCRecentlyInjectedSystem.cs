using Content.Shared.Damage.Prototypes;
using Content.Shared.NPC.Components;
using System.Linq;

namespace Content.Shared._Moffstation.NPC.Systems;

/// <summary>
///     Tracks recent medibot and refillable medibot treatments in a patient
/// </summary>
public sealed partial class NPCRecentlyInjectedSystem : EntitySystem
{
    [Dependency] private readonly EntityQuery<NPCRecentlyInjectedComponent> _recentlyInjectedQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    /// <summary>
    /// Gets a count of all damageType entires in the component
    /// </summary>
    public int GetEntryCount(Entity<NPCRecentlyInjectedComponent?> ent)
    {
        if (!_recentlyInjectedQuery.TryComp(ent, out var component)
            || component.TimeSinceTreatments is null) return 0;
        return component.TimeSinceTreatments.Count;
    }

    /// <summary>
    /// Get all damage type entries in the component
    /// </summary>
    public List<DamageTypePrototype> GetTreatedDamages(Entity<NPCRecentlyInjectedComponent?> ent)
    {
        if (!_recentlyInjectedQuery.TryComp(ent, out var component)) return [];
        return component.TimeSinceTreatments.Keys.ToList();
    }

    /// <summary>
    /// Checks if the NPCRecentlyInjectedComponent has an entry for the given damage type (if the entity was recently injected for that damage)
    /// </summary>
    public bool WasInjectedFor(Entity<NPCRecentlyInjectedComponent?> ent, DamageTypePrototype damageType)
    {
        if (!_recentlyInjectedQuery.TryComp(ent, out var component)) return false;
        return component.TimeSinceTreatments.TryGetValue(damageType, out _);
    }

    /// <summary>
    /// Adds the elapsed time to each treatment entry and removes it if it's past the expire time.
    /// </summary>
    public void AddTime(Entity<NPCRecentlyInjectedComponent?> ent, float frameDelta)
    {
        if (!_recentlyInjectedQuery.TryComp(ent, out var component)) return;
        foreach (var damageType in component.TimeSinceTreatments.Keys)
        {
            component.TimeSinceTreatments[damageType] += frameDelta;
            if (component.TimeSinceTreatments[damageType] >= component.RemoveTime.TotalSeconds)
                component.TimeSinceTreatments.Remove(damageType);
        }
    }

    /// <summary>
    /// Adds a new treated damageType entry. If (for whatever reason, this shouldn't happen, but just in case) the entry already exists, resets it to 0.
    /// </summary>
    public void AddDamageTypeEntry(NPCRecentlyInjectedComponent component, DamageTypePrototype damageType)
    {
        if (!component.TimeSinceTreatments.TryAdd(damageType, 0f)) // The dictionary tracks elapsed time, so this always starts at 0
            component.TimeSinceTreatments[damageType] = 0f;
    }
}
