using System;
using System.Collections.Generic;
using System.Linq;

namespace Nnrp.Core
{
    internal static class NnrpCapabilityValidation
    {
        public static T[] CopyValues<T>(IEnumerable<T> values, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return values.ToArray();
        }

        public static bool TryValidateEnumSet<TEnum>(IReadOnlyList<TEnum> values, string name, out string validationError)
            where TEnum : struct, Enum
        {
            if (values == null || values.Count == 0)
            {
                validationError = $"{name} must contain at least one value.";
                return false;
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                var value = values[valueIndex];
                if (!Enum.IsDefined(typeof(TEnum), value))
                {
                    validationError = $"{name} contains an unknown {typeof(TEnum).Name} value.";
                    return false;
                }

                for (var previousIndex = 0; previousIndex < valueIndex; previousIndex++)
                {
                    if (EqualityComparer<TEnum>.Default.Equals(values[previousIndex], value))
                    {
                        validationError = $"{name} contains duplicate values.";
                        return false;
                    }
                }
            }

            validationError = string.Empty;
            return true;
        }

        public static bool Contains<T>(IReadOnlyList<T> values, T value)
        {
            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                if (EqualityComparer<T>.Default.Equals(values[valueIndex], value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
