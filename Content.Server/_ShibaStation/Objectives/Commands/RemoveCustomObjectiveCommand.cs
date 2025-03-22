using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Objectives.Components;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Console;
using Content.Server._ShibaStation.Objectives.Systems;

namespace Content.Server._ShibaStation.Objectives.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class RemoveCustomObjectiveCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command => "rmcustomobjective";
    public string Description => "Removes a custom objective.";
    public string Help => "rmcustomobjective <username> <objective ID>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Expected exactly 2 arguments.");
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var session))
        {
            shell.WriteError("Can't find the player.");
            return;
        }

        var minds = _entityManager.System<SharedMindSystem>();
        if (!minds.TryGetMind(session, out var mindId, out var mind))
        {
            shell.WriteError("Can't find the mind.");
            return;
        }

        if (!EntityUid.TryParse(args[1], out var objectiveUid))
        {
            shell.WriteError("Invalid objective ID.");
            return;
        }

        // Verify this objective belongs to the specified player
        if (!mind.Objectives.Contains(objectiveUid))
        {
            shell.WriteError("That objective ID does not belong to the specified player.");
            return;
        }

        if (!_entityManager.TryGetComponent<CustomObjectiveComponent>(objectiveUid, out var comp))
        {
            shell.WriteError("That objective ID does not correspond to a custom objective.");
            return;
        }

        // Send notification to the player before removing the objective
        if (mind.Session != null && mind.CurrentEntity != null)
        {
            var customSystem = _entityManager.System<CustomObjectiveSystem>();
            customSystem.NotifyPlayer(mind.CurrentEntity.Value, mind.Session.Channel, "custom-objective-removed");
        }

        // Remove the objective
        mind.Objectives.Remove(objectiveUid);
        _entityManager.DeleteEntity(objectiveUid);

        shell.WriteLine("Objective successfully removed!");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                CompletionHelper.SessionNames(),
                "username");
        }

        if (args.Length == 2)
        {
            // If we have a valid username, show their custom objectives
            if (_players.TryGetSessionByUsername(args[0], out var session))
            {
                var minds = _entityManager.System<SharedMindSystem>();
                if (minds.TryGetMind(session, out var mindId, out var mind))
                {
                    var options = new List<CompletionOption>();
                    foreach (var objective in mind.Objectives)
                    {
                        if (!_entityManager.TryGetComponent<CustomObjectiveComponent>(objective, out var comp))
                            continue;

                        var meta = _entityManager.GetComponent<MetaDataComponent>(objective);
                        options.Add(new CompletionOption(
                            objective.ToString(),
                            $"{meta.EntityName} ({(comp.Progress * 100):F0}%)"));
                    }
                    return CompletionResult.FromHintOptions(options, "objective ID");
                }
            }
        }

        return CompletionResult.Empty;
    }
}
