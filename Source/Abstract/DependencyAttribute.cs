using System;
using System.Collections.ObjectModel;

namespace Cultiway.Abstract;

public class DependencyAttribute(params Type[] types) : Attribute
{
    public ReadOnlyCollection<Type> Types { get; } = Array.AsReadOnly(types);
}