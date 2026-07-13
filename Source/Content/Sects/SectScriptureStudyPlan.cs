namespace Cultiway.Content.Sects;

public readonly struct SectScriptureStudyPlan
{
    public SectScriptureStudyPlan(Book book, int cost, float score, int candidateCount)
    {
        Book = book;
        Cost = cost;
        Score = score;
        CandidateCount = candidateCount;
    }

    public Book Book { get; }
    public int Cost { get; }
    public float Score { get; }
    public int CandidateCount { get; }
}
