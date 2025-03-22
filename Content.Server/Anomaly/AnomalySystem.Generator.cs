using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Anomaly.Components;
using Content.Server.Chat.Managers;
using Content.Server.Power.EntitySystems;
using Content.Server.Station.Components;
using Content.Shared.Anomaly;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Materials;
using Content.Shared.Physics;
using Content.Shared.Power;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server.Anomaly;

/// <summary>
/// This handles anomalous vessel as well as
/// the calculations for how many points they
/// should produce.
/// </summary>
public sealed partial class AnomalySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

    private void InitializeGenerator()
    {
        SubscribeLocalEvent<AnomalyGeneratorComponent, BoundUIOpenedEvent>(OnGeneratorBUIOpened);
        SubscribeLocalEvent<AnomalyGeneratorComponent, MaterialAmountChangedEvent>(OnGeneratorMaterialAmountChanged);
        SubscribeLocalEvent<AnomalyGeneratorComponent, AnomalyGeneratorGenerateButtonPressedEvent>(OnGenerateButtonPressed);
        SubscribeLocalEvent<AnomalyGeneratorComponent, PowerChangedEvent>(OnGeneratorPowerChanged);
        SubscribeLocalEvent<GeneratingAnomalyGeneratorComponent, ComponentStartup>(OnGeneratingStartup);
    }

    private void OnGeneratorPowerChanged(EntityUid uid, AnomalyGeneratorComponent component, ref PowerChangedEvent args)
    {
        _ambient.SetAmbience(uid, args.Powered);
    }

    private void OnGeneratorBUIOpened(EntityUid uid, AnomalyGeneratorComponent component, BoundUIOpenedEvent args)
    {
        UpdateGeneratorUi(uid, component);
    }

    private void OnGeneratorMaterialAmountChanged(EntityUid uid, AnomalyGeneratorComponent component, ref MaterialAmountChangedEvent args)
    {
        UpdateGeneratorUi(uid, component);
    }

    private void OnGenerateButtonPressed(EntityUid uid, AnomalyGeneratorComponent component, AnomalyGeneratorGenerateButtonPressedEvent args)
    {
        TryGeneratorCreateAnomaly(uid, component);
    }

    public void UpdateGeneratorUi(EntityUid uid, AnomalyGeneratorComponent component)
    {
        var materialAmount = _material.GetMaterialAmount(uid, component.RequiredMaterial);

        var state = new AnomalyGeneratorUserInterfaceState(component.CooldownEndTime, materialAmount, component.MaterialPerAnomaly);
        _ui.SetUiState(uid, AnomalyGeneratorUiKey.Key, state);
    }

    public void TryGeneratorCreateAnomaly(EntityUid uid, AnomalyGeneratorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!this.IsPowered(uid, EntityManager))
            return;

        if (Timing.CurTime < component.CooldownEndTime)
            return;

        if (!_material.TryChangeMaterialAmount(uid, component.RequiredMaterial, -component.MaterialPerAnomaly))
            return;

        var generating = EnsureComp<GeneratingAnomalyGeneratorComponent>(uid);
        generating.EndTime = Timing.CurTime + component.GenerationLength;
        generating.AudioStream = Audio.PlayPvs(component.GeneratingSound, uid, AudioParams.Default.WithLoop(true))?.Entity;
        component.CooldownEndTime = Timing.CurTime + component.CooldownLength;
        UpdateGeneratorUi(uid, component);
    }

    // ShibaStation - Added logSpawn parameter to allow logging whenever desired.
    public void SpawnOnRandomGridLocation(EntityUid grid, string toSpawn, bool logSpawn = false)
    {
        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var xform = Transform(grid);

        var targetCoords = xform.Coordinates;
        var gridBounds = gridComp.LocalAABB.Scale(_configuration.GetCVar(CCVars.AnomalyGenerationGridBoundsScale));

        for (var i = 0; i < 25; i++)
        {
            var randomX = Random.Next((int) gridBounds.Left, (int) gridBounds.Right);
            var randomY = Random.Next((int) gridBounds.Bottom, (int)gridBounds.Top);

            var tile = new Vector2i(randomX, randomY);

            // no air-blocked areas.
            if (_atmosphere.IsTileSpace(grid, xform.MapUid, tile) ||
                _atmosphere.IsTileAirBlocked(grid, tile, mapGridComp: gridComp))
            {
                continue;
            }

            // don't spawn inside of solid objects
            var physQuery = GetEntityQuery<PhysicsComponent>();
            var valid = true;

            // TODO: This should be using static lookup.
            foreach (var ent in _mapSystem.GetAnchoredEntities(grid, gridComp, tile))
            {
                if (!physQuery.TryGetComponent(ent, out var body))
                    continue;
                if (body.BodyType != BodyType.Static ||
                    !body.Hard ||
                    (body.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }
            if (!valid)
                continue;


            var pos = _mapSystem.GridTileToLocal(grid, gridComp, tile);


            // ShibaStation - Applies a slight offset to avoid exact grid alignment, except for predefined spawner prototypes.
            // Refer to predefined spawner prototypes.
            if (toSpawn != "!AnomalySpawnerPrototype")
            {
                var offset = 0.15f;
                var xOffset = Random.NextFloat(-offset, offset);
                var yOffset = Random.NextFloat(-offset, offset);

                pos = pos.Offset(new Vector2(xOffset, yOffset));
            }

            var mapPos = _transform.ToMapCoordinates(pos);
            // don't spawn in AntiAnomalyZones
            var antiAnomalyZonesQueue = AllEntityQuery<AntiAnomalyZoneComponent, TransformComponent>();
            while (antiAnomalyZonesQueue.MoveNext(out _, out var zone, out var antiXform))
            {
                if (antiXform.MapID != mapPos.MapId)
                    continue;

                var antiCoordinates = _transform.GetWorldPosition(antiXform);

                var delta = antiCoordinates - mapPos.Position;
                if (delta.LengthSquared() < zone.ZoneRadius * zone.ZoneRadius)
                {
                    valid = false;
                    break;
                }
            }
            if (!valid)
                continue;

            targetCoords = pos;
            break;
        }

        if (logSpawn) // ShibaStation
        {
            LogSpawnDetails(toSpawn, targetCoords);
        }

        Spawn(toSpawn, targetCoords);
    }

    // ShibaStation
    private void LogSpawnDetails(string prototype, EntityCoordinates coordinates)
    {
        var mapCords = _transform.ToMapCoordinates(coordinates);
        var x = (int)mapCords.X;
        var y = (int)mapCords.Y;
        var mapId = mapCords.MapId;

        _adminLogger.Add(LogType.EventRan, LogImpact.High, $"{prototype} spawned at ({x},{y}) on map {mapId}.");
        _chat.SendAdminAnnouncement($"{prototype} spawned at ({x},{y})");
    }

    private void OnGeneratingStartup(EntityUid uid, GeneratingAnomalyGeneratorComponent component, ComponentStartup args)
    {
        Appearance.SetData(uid, AnomalyGeneratorVisuals.Generating, true);
    }

    private void OnGeneratingFinished(EntityUid uid, AnomalyGeneratorComponent component)
    {
        var xform = Transform(uid);

        if (_station.GetStationInMap(xform.MapID) is not { } station ||
            !TryComp<StationDataComponent>(station, out var data) ||
            _station.GetLargestGrid(data) is not { } grid)
        {
            if (xform.GridUid == null)
                return;
            grid = xform.GridUid.Value;
        }

        SpawnOnRandomGridLocation(grid, component.SpawnerPrototype);
        RemComp<GeneratingAnomalyGeneratorComponent>(uid);
        Appearance.SetData(uid, AnomalyGeneratorVisuals.Generating, false);
        Audio.PlayPvs(component.GeneratingFinishedSound, uid);

        var message = Loc.GetString("anomaly-generator-announcement");
        _radio.SendRadioMessage(uid, message, _prototype.Index<RadioChannelPrototype>(component.ScienceChannel), uid);
    }

    private void UpdateGenerator()
    {
        var query = EntityQueryEnumerator<GeneratingAnomalyGeneratorComponent, AnomalyGeneratorComponent>();
        while (query.MoveNext(out var ent, out var active, out var gen))
        {
            if (Timing.CurTime < active.EndTime)
                continue;

            active.AudioStream = _audio.Stop(active.AudioStream);
            OnGeneratingFinished(ent, gen);
        }
    }
}
