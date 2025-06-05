using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Server._ShibaStation.Objectives.Systems;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Content.Shared.Objectives.Components;
using Robust.Shared.Utility;

namespace Content.Server._ShibaStation.Objectives.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class AddCustomObjectiveCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public string Command => "customobjective";
    public string Description => "Adds a custom objective to a player.";
    public string Help => "customobjective <username> <issuer> <title> <text> [icon entity]\nExample: customobjective username \"Devious Inc\" \"Steal Towels\" \"Steal 5 towels from the station\" Towel";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 4)
        {
            shell.WriteError("Expected at least 4 arguments: <username> <issuer> <title> <text> [icon entity]");
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

        var issuer = args[1];
        var title = args[2];
        var text = args[3];

        // Set the icon if provided
        SpriteSpecifier? icon = null;
        if (args.Length > 4)
        {
            var iconProto = args[4];
            if (!_prototype.HasIndex<EntityPrototype>(iconProto))
            {
                shell.WriteError($"Invalid entity prototype: {iconProto}");
                return;
            }
            icon = new SpriteSpecifier.EntityPrototype(iconProto);
        }

        var customSystem = _entityManager.System<CustomObjectiveSystem>();
        var objective = customSystem.CreateCustomObjective(issuer, title, text, icon);

        minds.AddObjective(mindId, mind, objective);
        shell.WriteLine($"Added custom objective '{title}' to {args[0]} under issuer '{issuer}'");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "<username>");
        if (args.Length == 2)
            return CompletionResult.FromHint("<issuer>");
        if (args.Length == 3)
            return CompletionResult.FromHint("<title>");
        if (args.Length == 4)
            return CompletionResult.FromHint("<text>");
        if (args.Length == 5)
            return CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<EntityPrototype>(), "<icon entity>");

        return CompletionResult.Empty;
    }
}
