using Content.Shared._Moffstation.NPC.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared._Moffstation.Silicons.Bots;

/// <summary>
/// Handles emagging refillable medibots and provides api.
/// </summary>
public sealed class RefillableMedibotSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly NPCRecentlyInjectedSystem _npcRecentlyInjectedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmaggableRefillableMedibotComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<RefillableMedibotComponent, UserActivateInWorldEvent>(OnInteract);
        SubscribeLocalEvent<RefillableMedibotComponent, RefillableMedibotInjectDoAfterEvent>(OnInject);
        SubscribeLocalEvent<RefillableMedibotComponent, ItemSlotEjectAttemptEvent>(OnEject);
    }

    private void OnEmagged(EntityUid uid, EmaggableRefillableMedibotComponent comp, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(uid, EmagType.Interaction))
            return;

        if (!TryComp<RefillableMedibotComponent>(uid, out var medibot))
            return;

        // Emag behavior
        foreach (var (state, treatment) in comp.Replacements)
        {
            //medibot.Treatments[state] = treatment;
            continue;
        }

        args.Handled = true;
    }

    private void OnEject(Entity<RefillableMedibotComponent> medibot, ref ItemSlotEjectAttemptEvent args)
    {
        var idList = medibot.Comp.InjectionDoAfterIds.ToList();
        idList.ForEach(id =>
        {
            _doAfter.Cancel(id);
        });
        medibot.Comp.InjectionDoAfterIds.Clear();
    }

    private void OnInteract(Entity<RefillableMedibotComponent> medibot, ref UserActivateInWorldEvent args)
    {
        if (!CheckInjectable(medibot!, args.Target, true)
            || !TryGetContainedSolution(medibot!, out _, out var solution)
            || !CheckEnoughSolution(medibot!, solution))
            return;

        if (_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, 2f, new RefillableMedibotInjectDoAfterEvent(), args.User, args.Target)
        {
            BlockDuplicate = true,
            BreakOnMove = true,
        }, out var id))
            medibot.Comp.InjectionDoAfterIds.Add(id.Value);
    }

    private void OnInject(EntityUid uid, RefillableMedibotComponent comp, ref RefillableMedibotInjectDoAfterEvent args)
    {
        // This will be false if the doAfter was canceled due to movement or another reason besides ejecting the container.
        comp.InjectionDoAfterIds.Remove(args.DoAfter.Id);
        //if (!comp.InjectionDoAfterIds.Remove(args.DoAfter.Id))
        //{
        //    Log.Warning($"Attempted to remove a doAfter from InjectionDoAfterIds with invalid id ({args.DoAfter.Id}) on entity {ToPrettyString(uid)}.");
        //}
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
    //public bool TryGetTreatment(RefillableMedibotComponent comp, MobState state, [NotNullWhen(true)] out RefillableMedibotTreatment? treatment)
    //{
    //    return comp.Treatments.TryGetValue(state, out treatment);
    //}

    /// <summary>
    /// Check if the given state is one that the refillable medibot is set to treat (no treating dead bodies).
    /// </summary>
    public bool CheckTreatableState(Entity<RefillableMedibotComponent?> medibot, MobState state)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;
        return medibot.Comp.TreatableStates.Contains(state);
    }

    /// <summary>
    /// Check if the given state is one that the refillable medibot is set to treat (no treating dead bodies).
    /// </summary>
    public bool CheckTreatableState(RefillableMedibotComponent? medibot, MobState state)
    {
        if (medibot is null) return false;
        return medibot.TreatableStates.Contains(state);
    }

    /// <summary>
    /// Checks if the target can be injected.
    /// </summary>
    public bool CheckInjectable(Entity<RefillableMedibotComponent?> medibot, EntityUid target, bool manual = false)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;

        if (!TryGetDamageType(medibot, out var damageType)
            || !TryGetDamageTypeProtoId(medibot, out var damageTypeProtoId)) return false;
        if (TryComp<NPCRecentlyInjectedComponent>(target, out var npcRecentlyInjectedComponent) && _npcRecentlyInjectedSystem.WasInjectedFor(npcRecentlyInjectedComponent, damageType!)) // Checks for injected by medibot in the last minute
        {
            _popup.PopupClient(Loc.GetString("refillable-medibot-recently-injected-for-damage"), medibot, medibot);
            return false;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState)) return false;
        if (!TryComp<DamageableComponent>(target, out var damageable)) return false;
        if (!_solutionContainer.TryGetInjectableSolution(target, out _, out _)) return false;

        if (mobState.CurrentState != MobState.Alive && mobState.CurrentState != MobState.Critical) // Checks for dead
        {
            _popup.PopupClient(Loc.GetString("refillable-medibot-target-dead"), medibot, medibot);
            return false;
        }

        var damages = _damageable.GetPositiveDamage((target, damageable));
        if (!damages.AnyPositive() && !HasComp<EmaggedComponent>(medibot)) // Checks for any damage
        {
            _popup.PopupClient(Loc.GetString("refillable-medibot-target-healthy"), medibot, medibot);
            return false;
        }
        if (!damages.DamageDict.ContainsKey((ProtoId<DamageTypePrototype>)damageTypeProtoId!)) // Checks for the specified type of damage
        {
            _popup.PopupClient(Loc.GetString("refillable-medibot-wrong-damage"), medibot, medibot);
            return false;
        }

        //if (!TryGetTreatment(medibot.Comp, mobState.CurrentState, out var treatment) || !manual) return false;

        return true;
    }

    /// <summary>
    /// Checks if the given solution is enough to inject
    /// </summary>
    public bool CheckEnoughSolution(Entity<RefillableMedibotComponent?> medibot, Solution solution)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;

        if (solution.Volume == 0
            || !medibot.Comp.AllowPartialInjections && solution.Volume < medibot.Comp.InjectionTransferAmount)
        {
            _popup.PopupClient(Loc.GetString("refillable-medibot-not-enough-solution"), medibot, medibot);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if the refillable medibot contains a solution and gets it, null if no solution.
    /// </summary>
    public bool TryGetContainedSolution(Entity<RefillableMedibotComponent?> medibot, [NotNullWhen(true)] out Entity<SolutionComponent>? solutionComponent, [NotNullWhen(true)] out Solution? solution)
    {
        if (!TryComp<ItemSlotsComponent>(medibot, out var itemSlots))
        {
            solution = null;
            solutionComponent = null;
            return false;
        }

        var container = _itemSlots.GetItemOrNull(medibot, "containerSlot", itemSlots);

        if (container == null)
        {
            _popup.PopupClient(Loc.GetString("refillable-medibot-no-container"), medibot, medibot);
            solution = null;
            solutionComponent = null;
            return false;
        }

        if (!TryComp<SolutionContainerManagerComponent>(container, out var containerSolutionContainerManager)
            || !TryComp<FitsInDispenserComponent>(container, out var containerFitsInDispenser)
            || !_solutionContainer.TryGetFitsInDispenser((container.Value, containerFitsInDispenser, containerSolutionContainerManager), out var containerSolutionComponent, out var containerSolution)
            )
        {
            solution = null;
            solutionComponent = null;
            return false;
        }
        else
        {
            solution = containerSolution;
            solutionComponent = containerSolutionComponent;
        }
        return true;

    }

    /// <summary>
    /// Gets the refillable medibot's set damage type, returns false if null.
    /// </summary>
    public bool TryGetDamageType(Entity<RefillableMedibotComponent?> medibot, [NotNullWhen(true)] out DamageTypePrototype? damageType)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)
            || medibot.Comp.DamageType is null
            || !_prototypeManager.TryIndex(medibot.Comp.DamageType, out damageType))
        {
            damageType = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the refillable medibot's set damage type, returns false if null.
    /// </summary>
    public bool TryGetDamageType(RefillableMedibotComponent? medibot, [NotNullWhen(true)] out DamageTypePrototype? damageType)
    {
        if (medibot is null
            || medibot.DamageType is null
            || !_prototypeManager.TryIndex(medibot.DamageType, out damageType))
        {
            damageType = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the refillable medibot's set damage type ProtoId, returns false if null.
    /// </summary>
    public bool TryGetDamageTypeProtoId(Entity<RefillableMedibotComponent?> medibot, [NotNullWhen(true)] out ProtoId<DamageTypePrototype>? damageTypeProtoId)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)
            || medibot.Comp.DamageType is null)
        {
            damageTypeProtoId = null;
            return false;
        }

        damageTypeProtoId = medibot.Comp.DamageType;
        return true;
    }

    /// <summary>
    /// Gets the refillable medibot's set damage type ProtoId, returns false if null.
    /// </summary>
    public bool TryGetDamageTypeProtoId(RefillableMedibotComponent? medibot, [NotNullWhen(true)] out ProtoId<DamageTypePrototype>? damageTypeProtoId)
    {
        if (medibot is null
            || medibot.DamageType is null)
        {
            damageTypeProtoId = null;
            return false;
        }

        damageTypeProtoId = medibot.DamageType;
        return true;
    }

    /// <summary>
    /// Tries to inject the target.
    /// </summary>
    public bool TryInject(Entity<RefillableMedibotComponent?> medibot, EntityUid target)
    {
        if (!Resolve(medibot, ref medibot.Comp, false)) return false;

        if (!_interaction.InRangeUnobstructed(medibot.Owner, target)) return false;

        if (!TryComp<MobStateComponent>(target, out var mobState)) return false;
        if (!TryGetContainedSolution(medibot, out var solutionComponent, out var solution)) return false;
        //if (!TryGetTreatment(medibot.Comp, mobState.CurrentState, out var treatment)) return false;
        if (!_solutionContainer.TryGetInjectableSolution(target, out var injectableComponent, out var injectable)) return false;
        if (!CheckEnoughSolution(medibot, solution)) return false;

        //_solutionContainer.TryAddReagent(injectable.Value, treatment.Reagent, treatment.Quantity, out _);
        var amountToTransfer = FixedPoint2.Min(medibot.Comp.InjectionTransferAmount, injectable.AvailableVolume);
        var injection = _solutionContainer.SplitSolution(solutionComponent.Value, amountToTransfer);
        _solutionContainer.TryAddSolution(injectableComponent.Value, injection);

        if (!TryComp<NPCRecentlyInjectedComponent>(target, out var npcRecentlyInjectedComponent))
        {
            npcRecentlyInjectedComponent = AddComp<NPCRecentlyInjectedComponent>(target);
        }
        if (TryGetDamageType(medibot, out var damageType))
        {
            _npcRecentlyInjectedSystem.AddDamageTypeEntry(npcRecentlyInjectedComponent, damageType);
        }

        _popup.PopupEntity(Loc.GetString("injector-component-feel-prick-message"), target, target);
        _popup.PopupClient(Loc.GetString("refillable-medibot-target-injected"), medibot, medibot);

        _audio.PlayPredicted(medibot.Comp.InjectSound, medibot, medibot);

        return true;
    }
}

[Serializable, NetSerializable]
public sealed partial class RefillableMedibotInjectDoAfterEvent : SimpleDoAfterEvent { }
