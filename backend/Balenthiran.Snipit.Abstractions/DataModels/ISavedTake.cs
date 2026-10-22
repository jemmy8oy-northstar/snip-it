namespace Balenthiran.Snipit.Abstractions.DataModels;

/// <summary>A take the creator chose to save to snip-it's server (BEH-EDITION-3).</summary>
public interface ISavedTake
{
    /// <summary>The file name the phone gave the take, e.g. <c>take-1729612345.mov</c>. Its id within a production.</summary>
    string Name { get; }

    long Bytes { get; }

    DateTime SavedAt { get; }
}
