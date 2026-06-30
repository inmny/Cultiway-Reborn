using System;

namespace Cultiway.Core.Logging;

public sealed class CultiLogArgs
{
    public static readonly CultiLogArgs Empty = new(Array.Empty<CultiLogArg>());

    public readonly CultiLogArg[] Items;

    private CultiLogArgs(CultiLogArg[] items)
    {
        Items = items ?? Array.Empty<CultiLogArg>();
    }

    public static CultiLogArgsBuilder Create(int capacity = 4)
    {
        return new CultiLogArgsBuilder(capacity);
    }

    public sealed class CultiLogArgsBuilder
    {
        private CultiLogArg[] _items;
        private int _count;

        internal CultiLogArgsBuilder(int capacity)
        {
            _items = new CultiLogArg[Math.Max(1, capacity)];
        }

        public CultiLogArgsBuilder Str(string key, string value)
        {
            Add(CultiLogArg.Str(key, value));
            return this;
        }

        public CultiLogArgsBuilder Int(string key, long value)
        {
            Add(CultiLogArg.Int(key, value));
            return this;
        }

        public CultiLogArgsBuilder Float(string key, double value)
        {
            Add(CultiLogArg.Float(key, value));
            return this;
        }

        public CultiLogArgsBuilder Bool(string key, bool value)
        {
            Add(CultiLogArg.Bool(key, value));
            return this;
        }

        public CultiLogArgsBuilder Null(string key)
        {
            Add(CultiLogArg.Null(key));
            return this;
        }

        private void Add(CultiLogArg arg)
        {
            if (_count == _items.Length)
            {
                Array.Resize(ref _items, _items.Length * 2);
            }

            _items[_count++] = arg;
        }

        public CultiLogArgs Build()
        {
            if (_count == 0) return Empty;

            var result = new CultiLogArg[_count];
            Array.Copy(_items, result, _count);
            return new CultiLogArgs(result);
        }
    }
}
