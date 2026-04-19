using Content.Shared._Moffstation.NPC.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.EntityConditions;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Silicons.Bots;

/// <summary>
/// Handles emagging medibots and provides api.
/// </summary>
public sealed class MedibotSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly NPCRecentlyInjectedSystem _npcRecentlyInjectedSystem = default!; // Moffstation - Use new entity system for NPCRecentlyInjected component.
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // Moffstation - Use prototype manager so treatment damage types can be programatically determined.
    [Dependency] private readonly SharedEntityConditionsSystem _entityConditionsSystem = default!; // Moffstation - Be able to parse entity conditions to determine how a reagent is being applied.

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmaggableMedibotComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<MedibotComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<MedibotComponent, MedibotInjectDoAfterEvent>(OnInject);
    }

    private void OnEmagged(EntityUid uid, EmaggableMedibotComponent comp, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        if (!TryComp<MedibotComponent>(uid, out var medibot))
            return;

        foreach (var (state, treatment) in comp.Replacements)
        {
            medibot.Treatments[state] = treatment;
        }

        args.Handled = true;
    }

    private void OnInteract(Entity<MedibotComponent> medibot, ref UserActivateInWorldEvent args)
    {
        if (!CheckInjectable(medibot!, args.Target, true)) return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, 2f, new MedibotInjectDoAfterEvent(), args.User, args.Target)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
        });
    }

    private void OnInject(EntityUid uid, MedibotComponent comp, ref MedibotInjectDoAfterEvent args)
    {
        if (args.Cancelled) return;

        if (args.Target is { } target)
            TryInject(uid, target);
    }

    /// <summary>
    /// Get a treatment for a given mob state.
    /// </summary>
    /// <remarks>
    /// This only exists because allowing other execute would allow modifying the dictionary, and Read access does not cover TryGetValue.
    /// </remarks>
    public bool TryGetTreatment(MedibotComponent comp, MobState state, [NotNullWhen(true)] out MedibotTreatment? treatment)
    {
        return comp.Treatments.TryGetValue(state, out treatment);
    }

    /// <summary>
    /// Checks if the target can be injected.
    /// </summary>
    public bool CheckInjectable(Entity<MedibotComponent?> medibot, EntityUid target, bool manual = false)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;

        if (HasComp<NPCRecentlyInjectedComponent>(target))
        {
            _popup.PopupClient(Loc.GetString("medibot-recently-injected"), medibot, medibot);
            return false;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState)) return false;
        if (!TryComp<DamageableComponent>(target, out var damageable)) return false;
        if (!_solutionContainer.TryGetInjectableSolution(target, out _, out _)) return false;

        if (mobState.CurrentState != MobState.Alive && mobState.CurrentState != MobState.Critical)
        {
            _popup.PopupClient(Loc.GetString("medibot-target-dead"), medibot, medibot);
            return false;
        }

        var total = _damageable.GetTotalDamage((target, damageable));
        if (total == 0 && !HasComp<EmaggedComponent>(medibot))
        {
            _popup.PopupClient(Loc.GetString("medibot-target-healthy"), medibot, medibot);
            return false;
        }

        if (!TryGetTreatment(medibot.Comp, mobState.CurrentState, out var treatment) || !manual) return false;

        return true;
    }

    /// <summary>
    /// Tries to inject the target.
    /// </summary>
    public bool TryInject(Entity<MedibotComponent?> medibot, EntityUid target)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;

        if (!_interaction.InRangeUnobstructed(medibot.Owner, target)) return false;

        if (!TryComp<MobStateComponent>(target, out var mobState)) return false;
        if (!TryGetTreatment(medibot.Comp, mobState.CurrentState, out var treatment)) return false;
        if (!_solutionContainer.TryGetInjectableSolution(target, out var injectable, out _)) return false;

        _solutionContainer.TryAddReagent(injectable.Value, treatment.Reagent, treatment.Quantity, out _);

        // Moffstation - Begin - Moved logic for adding NPCRecentlyInjectedComponent to medibot entity system rather than HTN in order to support treated damage type tracking
        if (!TryComp<NPCRecentlyInjectedComponent>(target, out var npcRecentlyInjectedComponent))
        {
            npcRecentlyInjectedComponent = AddComp<NPCRecentlyInjectedComponent>(target);
        }
        if (_prototypeManager.Resolve(treatment.Reagent, out var reagentPrototype)
            && reagentPrototype.Metabolisms is not null)
            foreach (var effectEntry in reagentPrototype.Metabolisms.Metabolisms.Values)
            {
                foreach (var effect in effectEntry.Effects)
                {
                    if (effect.Conditions is null || !_entityConditionsSystem.TryConditions(target, effect.Conditions)) continue;
                    Log.Debug($"Effect: {effect.ToString()}");
                }
            }
        
        //_npcRecentlyInjectedSystem.AddDamageTypeEntry(npcRecentlyInjectedComponent, medibot.Comp.DamageType);
        // Moffstation - End - Moved logic for adding NPCRecentlyInjectedComponent to medibot entity system rather than HTN in order to support treated damage type tracking

        _popup.PopupEntity(Loc.GetString("injector-component-feel-prick-message"), target, target);
        _popup.PopupClient(Loc.GetString("medibot-target-injected"), medibot, medibot);

        _audio.PlayPredicted(medibot.Comp.InjectSound, medibot, medibot);

        return true;
    }
}

[Serializable, NetSerializable]
public sealed partial class MedibotInjectDoAfterEvent : SimpleDoAfterEvent { }
