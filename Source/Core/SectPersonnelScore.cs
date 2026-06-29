namespace Cultiway.Core;

public readonly struct SectPersonnelScore
{
    public SectPersonnelScore(int realm, int tenure, int contribution)
    {
        Realm = realm;
        Tenure = tenure;
        Contribution = contribution;
    }

    public int Realm { get; }
    public int Tenure { get; }
    public int Contribution { get; }
    public int Total => Realm + Tenure + Contribution;
}
