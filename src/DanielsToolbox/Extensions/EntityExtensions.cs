using Microsoft.Xrm.Sdk;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DanielsToolbox.Extensions
{
    public static class EntityExtensions
    {
        public static TType GetAliasedValue<TType>(this Entity source, string attributeLogicalName)
            => source.TryGetAttributeValue<AliasedValue>(attributeLogicalName, out var aliasedValue) ? (TType)aliasedValue.Value : default;
    }
}
