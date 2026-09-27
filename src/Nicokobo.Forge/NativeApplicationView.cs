namespace Nicokobo.Forge;

public enum NativeApplicationStatus { Staged, Applied, Conflict, Failed }

public sealed record NativeApplicationView(string OwnerId, string ContentId,
    string Kind, NativeApplicationStatus Status, string Reason);
