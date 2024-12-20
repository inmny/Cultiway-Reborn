using System;

namespace Cultiway.Abstract;

public class AssetIdAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}