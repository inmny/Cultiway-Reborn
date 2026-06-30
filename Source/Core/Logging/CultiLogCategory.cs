using System;

namespace Cultiway.Core.Logging;

[Flags]
public enum CultiLogCategory : long
{
    None = 0,
    General = 1L << 0,
    Combat = 1L << 1,
    Sect = 1L << 2,
    Cultivation = 1L << 3,
    Book = 1L << 4,
    Skill = 1L << 5,
    Pathfinding = 1L << 6,
    Item = 1L << 7,
    Train = 1L << 8,
    Geo = 1L << 9,
    AI = 1L << 10,
    UI = 1L << 11,
    Perf = 1L << 12,
    AIGC = 1L << 13,
    Error = 1L << 14,
    All = General | Combat | Sect | Cultivation | Book | Skill | Pathfinding | Item | Train | Geo | AI | UI | Perf |
          AIGC | Error
}
