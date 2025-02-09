using Content.Shared.Mind;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.Objectives.Components;

/// <summary>
/// Extends ObjectiveComponent to support custom issuers.
/// </summary>
[RegisterComponent]
public sealed partial class CustomObjectiveComponent : Component
{
    /// <summary>
    /// The custom issuer name for this objective.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string CustomIssuer = string.Empty;

    /// <summary>
    /// The title of this objective.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Title = string.Empty;

    /// <summary>
    /// The description text for this objective.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Text = string.Empty;

    /// <summary>
    /// The progress value of this objective (0.0 to 1.0).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Progress;

    /// <summary>
    /// The sound that plays for the player when this objective is added or removed.
    /// </summary>
    [DataField]
    public SoundSpecifier ObjectiveSound = new SoundPathSpecifier("/Audio/Misc/cryo_warning.ogg");
}
