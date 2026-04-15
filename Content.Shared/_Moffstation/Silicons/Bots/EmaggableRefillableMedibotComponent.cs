using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Moffstation.Silicons.Bots;

/// <summary>
/// Replaces the medibot's meds with these when emagged. Could be poison, could be fun.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(RefillableMedibotSystem))]
public sealed partial class EmaggableRefillableMedibotComponent : Component
{
    /// <summary>
    /// Treatments to replace from the original set.
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<MobState, RefillableMedibotTreatment> Replacements = new();
}
