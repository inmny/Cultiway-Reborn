using System;

namespace Cultiway.LocaleKeys;

public class OverwriteComponentAttribute(string component) : Attribute
{
    public readonly string Component = component;
}