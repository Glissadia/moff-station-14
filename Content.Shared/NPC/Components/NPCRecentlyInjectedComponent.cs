using Content.Shared._Moffstation.NPC.Systems;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.NPC.Components
{
    /// Added when a medibot injects someone
    /// So they don't get injected again for at least a minute.
    [RegisterComponent, NetworkedComponent]
    [Access(typeof(NPCRecentlyInjectedSystem))] // Moffstation - Gave this an actual component system so it's using methods rather than direct references
    public sealed partial class NPCRecentlyInjectedComponent : Component
    {
        // Moffstation - Begin - Replace default behavior with a dictionary of 'treated' damage types, so that refillable medibots with different damage types can still treat at the same time.
        //[ViewVariables(VVAccess.ReadWrite), DataField("accumulator")]
        //public float Accumulator = 0f;

        [ViewVariables(VVAccess.ReadWrite), DataField("removeTime")]
        public TimeSpan RemoveTime = TimeSpan.FromMinutes(1);

        [ViewVariables, DataField]
        public Dictionary<DamageTypePrototype, float> TimeSinceTreatments = []; // Key is the addressed damage type, float is the time 'accumulator' for that treatment
        // Moffstation - End - Replace default behavior with a dictionary of 'treated' damage types, so that refillable medibots with different damage types can still treat at the same time.
    }
}
