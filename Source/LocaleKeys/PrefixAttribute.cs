using System;

namespace Cultiway.LocaleKeys;

public class PrefixAttribute(string prefix) : Attribute
{
    public readonly string Prefix = prefix;
}