using System;

namespace Cultiway.LocaleKeys;

public class PostfixAttribute(string postfix) : Attribute
{
    public readonly string Postfix = postfix;
}