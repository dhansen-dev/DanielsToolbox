using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

namespace DanielsToolbox.Models
{
    public class SymbolEqualityComparer : EqualityComparer<Symbol>
    {
        public int RandomValue { get; } = new Random().Next(0, 999999);
        public static SymbolEqualityComparer Create = new();

        public override bool Equals(Symbol x, Symbol y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (x is null || y is null)
                return false;

            return x.Name == y.Name;
        }

        public override int GetHashCode([DisallowNull] Symbol symbol)
            => symbol?.Name?.GetHashCode() ?? 0;
    }
}
