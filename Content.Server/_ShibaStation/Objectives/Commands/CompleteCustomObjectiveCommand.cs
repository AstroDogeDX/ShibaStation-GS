using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Objectives.Components;
using Content.Server._ShibaStation.Objectives.Systems;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._ShibaStation.Objectives.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CompleteCustomObjectiveCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public string Command => "customobjectivestatus";
    public string Description => "Sets a custom objective's progress value.";
    public string Help => "customobjectivestatus <username> <objective ID> [progress (0.0-1.0)]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Expected at least 2 arguments.");
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

        var progress = 1.0f; // Default to complete
        if (args.Length > 2 && float.TryParse(args[2], out var parsedProgress))
        {
            progress = Math.Clamp(parsedProgress, 0f, 1f);
        }

        _entityManager.System<CustomObjectiveSystem>().SetProgress(objectiveUid, progress);
        shell.WriteLine($"Set objective {objectiveUid} progress to {progress:P0}.");
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

        if (args.Length == 3)
        {
            return CompletionResult.FromHint("progress (0.0-1.0)");
        }

        return CompletionResult.Empty;
    }
}
