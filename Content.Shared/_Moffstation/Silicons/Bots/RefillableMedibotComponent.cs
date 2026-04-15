using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Moffstation.Silicons.Bots;

/// <summary>
/// Used by the server for NPC refillable medibot injection.
/// Currently no clientside prediction done, only exists in shared for emag handling.
/// </summary>
[RegisterComponent]
[Access(typeof(RefillableMedibotSystem))]
public sealed partial class RefillableMedibotComponent : Component
{
    /// <summary>
    /// Treatments the bot will apply for each mob state.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<MobState, RefillableMedibotTreatment> Treatments = new();

    /// <summary>
    /// Sound played after injecting a patient.
    /// </summary>
    [DataField("injectSound")]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");
}

/// <summary>
/// An injection to treat the patient with.
/// </summary>
[DataDefinition]
public sealed partial class RefillableMedibotTreatment
{
    /// <summary>
    /// Reagent to inject into the patient.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent = string.Empty;

    /// <summary>
    /// How much of the reagent to inject.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Quantity;
}
