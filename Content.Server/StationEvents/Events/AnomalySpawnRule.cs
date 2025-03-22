using System.Linq;
using Content.Server.Anomaly;
using Content.Server.Spawners.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.EntityTable;
using Robust.Shared.Prototypes;

﻿using Content.Shared.GameTicking.Components;

namespace Content.Server.StationEvents.Events;

public sealed class AnomalySpawnRule : StationEventSystem<AnomalySpawnRuleComponent>
{
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly EntityTableSystem _entityTable = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    protected override void Added(EntityUid uid, AnomalySpawnRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        var str = Loc.GetString("anomaly-spawn-event-announcement",
            ("sighting", Loc.GetString($"anomaly-spawn-sighting-{RobustRandom.Next(1, 6)}")));
        stationEvent.StartAnnouncement = str;

        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, AnomalySpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!TryComp<StationDataComponent>(chosenStation, out var stationData))
            return;

        var grid = StationSystem.GetLargestGrid(stationData);

        if (grid is null)
            return;

        var amountToSpawn = 1;
        for (var i = 0; i < amountToSpawn; i++)
        {

            // ShibaStation - Picks a specific anomaly instead of using the default SpawnerPrototype,
            // allowing for anomaly type logging in SpawnOnRandomGridLocation.
            var chosenAnomaly = ChooseAnomaly(component.AnomalySpawnerPrototype);

            if (!string.IsNullOrEmpty(chosenAnomaly))
            {
                _anomaly.SpawnOnRandomGridLocation(grid.Value, chosenAnomaly, true);
            }

        }

    }

    // ShibaStation - Selects a specific anomaly to spawn based on the given prototype, resolving any nested spawners.
    private string? ChooseAnomaly(string prototypeId)
    {
        // Attempt to fetch the primary anomaly based on the provided prototype ID.
        var chosenAnomaly = GetAnomalySpawn(prototypeId);

        // If it picks another random spawner, we also have to refrence it's table and pick one.
        if (chosenAnomaly == "RandomAnomalyInjectorSpawner")
        {
            chosenAnomaly = GetAnomalySpawn("RandomAnomalyInjectorSpawner");
        }

        if (chosenAnomaly == "RandomRockAnomalySpawner")
        {
            chosenAnomaly = GetAnomalySpawn("RandomRockAnomalySpawner");
        }

        return chosenAnomaly;
    }

    // ShibaStation - Retrieves an anomaly to spawn from the given entity table prototype using defined weights.
    private string? GetAnomalySpawn(string prototypeId)
    {
        var proto = _prototypeManager.Index<EntityPrototype>(prototypeId);
        if (!proto.TryGetComponent<EntityTableSpawnerComponent>(out var anomalies, EntityManager.ComponentFactory))
        {
            Log.Warning($"Prototype '{prototypeId}' does not contain an EntityTableSpawnerComponent. Returning default spawner prototype");
            return "RandomAnomalySpawner";
        }

        // Chooses a spawn using weights from the given AnomalySpawnerPrototype's weights.
        var spawns = _entityTable.GetSpawns(anomalies.Table);

        // Ensures only one result is used from the EntityTable.
        return spawns.FirstOrDefault();
    }
}
