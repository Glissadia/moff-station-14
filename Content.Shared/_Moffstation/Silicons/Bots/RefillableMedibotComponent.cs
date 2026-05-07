using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
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
    /// Damage type the bot will inject upon detecting.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype>? DamageType = null;

    /// <summary>
    /// Sound played after injecting a patient.
    /// </summary>
    [DataField("injectSound")]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    /// <summary>
    /// How many units to transfer per injection from the container to the mob.
    /// </summary>
    [DataField]
    public FixedPoint2 InjectionTransferAmount = 5;

    /// <summary>
    /// Whether or not the refillable medibot should inject less than the specified amount if the inserted container doesn't contain the full amount.
    /// </summary>
    [DataField]
    public bool AllowPartialInjections = true;

    /// <summary>
    /// The mob states that the refillable medibot can treat (only alive and crit, no treating dead bodies).
    /// </summary>
    [DataField]
    public HashSet<MobState> TreatableStates = new() { MobState.Alive, MobState.Critical };

    /// <summary>
    /// A list of currently running DoAfterIds.
    /// Current, duplicate injection DoAfters are blocked, so this list should only have one entry.
    /// </summary>
    [ViewVariables]
    public List<DoAfterId> InjectionDoAfterIds = [];
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
