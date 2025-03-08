using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Server.Chat.Systems;
using Content.Server.Chat.Managers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared.Chat;
using Robust.Shared.Network;

namespace Content.Server._ShibaStation.Objectives.Systems;

/// <summary>
/// Handles custom objectives that can be added and controlled by admins.
/// </summary>
public sealed class CustomObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CustomObjectiveComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<CustomObjectiveComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<CustomObjectiveComponent, GetCustomIssuerEvent>(OnGetCustomIssuer);
    }

    private void OnGetProgress(EntityUid uid, CustomObjectiveComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = comp.Progress;
    }

    private void OnAfterAssign(EntityUid uid, CustomObjectiveComponent comp, ref ObjectiveAfterAssignEvent args)
    {
        // Set the objective's title and description
        _metaData.SetEntityName(uid, comp.Title, args.Meta);
        _metaData.SetEntityDescription(uid, comp.Text, args.Meta);

        // Send notification to the player when objective is added
        if (args.Mind.Session != null && args.Mind.CurrentEntity != null)
        {
            NotifyPlayer(args.Mind.CurrentEntity.Value, args.Mind.Session.Channel, "custom-objective-received", ("issuer", comp.CustomIssuer));
        }
    }

    private void OnGetCustomIssuer(EntityUid uid, CustomObjectiveComponent comp, ref GetCustomIssuerEvent args)
    {
        args.CustomIssuer = comp.CustomIssuer;
    }

    /// <summary>
    /// Creates a new custom objective entity.
    /// </summary>
    public EntityUid CreateCustomObjective(string issuer, string title, string text, SpriteSpecifier? icon = null)
    {
        var objective = Spawn("AdminCustomObjective");

        // Get the custom objective component
        var customComp = Comp<CustomObjectiveComponent>(objective);
        customComp.Title = title;
        customComp.Text = text;
        customComp.Progress = 0f;
        customComp.CustomIssuer = issuer;

        // Set initial metadata
        _metaData.SetEntityName(objective, title);
        _metaData.SetEntityDescription(objective, text);

        // Set the icon if provided
        if (icon != null)
        {
            var objComp = EnsureComp<ObjectiveComponent>(objective);
            _objectives.SetIcon(objective, icon, objComp);
        }

        return objective;
    }

    /// <summary>
    /// Sets a custom objective's progress value.
    /// </summary>
    public void SetProgress(EntityUid uid, float progress, CustomObjectiveComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var oldProgress = comp.Progress;
        comp.Progress = Math.Clamp(progress, 0f, 1f);
    }

    /// <summary>
    /// Sends a notification to a player about an objective change
    /// </summary>
    public void NotifyPlayer(EntityUid playerEntity, INetChannel channel, string messageKey, params (string, object)[] args)
    {
        // Send chat message
        var message = Loc.GetString(messageKey, args);
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, playerEntity, false, channel);

        // Play notification sound
        _audio.PlayGlobal("/Audio/Misc/cryo_warning.ogg", Filter.Entities(playerEntity), false);
    }
}
