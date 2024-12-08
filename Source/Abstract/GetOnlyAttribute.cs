using System;

namespace Cultiway.Abstract;

public class GetOnlyAttribute : Attribute
{
    public GetOnlyAttribute(string id)
    {
        SourceID = id;
    }

    public GetOnlyAttribute()
    {
    }

    public string SourceID { get; }
}