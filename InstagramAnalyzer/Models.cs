namespace InstagramAnalyzer.InstagramAnalyzer;

public enum RelationshipKind
{
    DoesNotFollowBack,
    FollowsYouOnly,
    Mutual
}

public sealed record InstagramProfile(string Username, string ProfileUrl);

public sealed record RelationshipRow(string Username, RelationshipKind Kind, string ProfileUrl);
