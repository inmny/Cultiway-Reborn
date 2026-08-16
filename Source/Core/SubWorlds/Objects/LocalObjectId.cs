using System;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>标识一个 Runtime 内的稳定物理 Building；零值无效，Runtime 生命周期内不复用。</summary>
internal readonly struct LocalObjectId : IEquatable<LocalObjectId>, IComparable<LocalObjectId>
{
    internal LocalObjectId(int value)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }

    internal int Value { get; }
    internal bool IsValid => Value > 0;

    public bool Equals(LocalObjectId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is LocalObjectId other && Equals(other);
    public override int GetHashCode() => Value;
    public int CompareTo(LocalObjectId other) => Value.CompareTo(other.Value);
    public override string ToString() => Value.ToString();

    public static bool operator ==(LocalObjectId left, LocalObjectId right) => left.Equals(right);
    public static bool operator !=(LocalObjectId left, LocalObjectId right) => !left.Equals(right);
}
