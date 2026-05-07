using System.Threading;
using System.Threading.Tasks;
using Content.Server.NPC;
using Content.Shared.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared._Moffstation.Silicons.Bots;
using Content.Shared.Emag.Components;
using Content.Shared._Moffstation.NPC.Systems;

namespace Content.Server._Moffstation.NPC.HTN.PrimitiveTasks.Operators.Specific;

public sealed partial class PickNearbySpecificDamagedOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private RefillableMedibotSystem _refillableMedibot = default!;
    private PathfindingSystem _pathfinding = default!;
    private DamageableSystem _damageable = default!;
    private NPCRecentlyInjectedSystem _npcRecentlyInjected = default!;

    private EntityQuery<DamageableComponent> _damageQuery = default!;
    private EntityQuery<InjectableSolutionComponent> _injectQuery = default!;
    private EntityQuery<NPCRecentlyInjectedComponent> _recentlyInjectedQuery = default!;
    private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    private EntityQuery<EmaggedComponent> _emaggedQuery = default!;

    [DataField("rangeKey")] public string RangeKey = NPCBlackboard.MedibotInjectRange;

    /// <summary>
    /// Target entity to inject
    /// </summary>
    [DataField("targetKey", required: true)]
    public string TargetKey = string.Empty;

    /// <summary>
    /// Target entitycoordinates to move to.
    /// </summary>
    [DataField("targetMoveKey", required: true)]
    public string TargetMoveKey = string.Empty;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _refillableMedibot = sysManager.GetEntitySystem<RefillableMedibotSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _damageable = sysManager.GetEntitySystem<DamageableSystem>();
        _npcRecentlyInjected = sysManager.GetEntitySystem<NPCRecentlyInjectedSystem>();

        _damageQuery = _entManager.GetEntityQuery<DamageableComponent>();
        _injectQuery = _entManager.GetEntityQuery<InjectableSolutionComponent>();
        _recentlyInjectedQuery = _entManager.GetEntityQuery<NPCRecentlyInjectedComponent>();
        _mobStateQuery = _entManager.GetEntityQuery<MobStateComponent>();
        _emaggedQuery = _entManager.GetEntityQuery<EmaggedComponent>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<float>(RangeKey, out var range, _entManager))
            return (false, null);

        if (!_entManager.TryGetComponent<RefillableMedibotComponent>(owner, out var medibot))
            return (false, null);


        if (!blackboard.TryGetValue<IEnumerable<KeyValuePair<EntityUid, float>>>("TargetList", out var patients, _entManager))
            return (false, null);

        foreach (var (entity, _) in patients)
        {
            if (_mobStateQuery.TryGetComponent(entity, out var state) &&
                _injectQuery.HasComponent(entity) &&
                _damageQuery.TryGetComponent(entity, out var damage) &&
                _refillableMedibot.TryGetDamageType(medibot, out var damageType) &&
                (!_recentlyInjectedQuery.TryGetComponent(entity, out var npcRecentyInjected)
                || !_npcRecentlyInjected.WasInjectedFor(npcRecentyInjected, damageType)))
            {
                // No treating dead bodies
                if (!_refillableMedibot.CheckTreatableState(medibot, state.CurrentState))
                    continue;

                // Only go towards a target if the bot can actually help them or if the medibot is emagged
                // Note: Unlike the regular medibot, this does check for specific damage types
                // Should this be just calling CheckInjectable instead?
                if (!_refillableMedibot.TryGetDamageTypeProtoId(medibot, out var damageTypeProtoId)
                    || !_damageable.GetPositiveDamage((entity, damage)).DamageDict.ContainsKey((Robust.Shared.Prototypes.ProtoId<Shared.Damage.Prototypes.DamageTypePrototype>)damageTypeProtoId))
                    continue;


                //Needed to make sure it doesn't sometimes stop right outside its interaction range
                var pathRange = SharedInteractionSystem.InteractionRange - 1f;
                var path = await _pathfinding.GetPath(owner, entity, pathRange, cancelToken);

                if (path.Result == PathResult.NoPath)
                    continue;

                return (true, new Dictionary<string, object>()
                {
                    {TargetKey, entity},
                    {TargetMoveKey, _entManager.GetComponent<TransformComponent>(entity).Coordinates},
                    {NPCBlackboard.PathfindKey, path},
                });
            }
        }

        return (false, null);
    }
}
