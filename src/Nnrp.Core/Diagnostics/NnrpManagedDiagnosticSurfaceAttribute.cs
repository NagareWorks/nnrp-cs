using System;

namespace Nnrp.Core
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class NnrpManagedDiagnosticSurfaceAttribute : Attribute
    {
        public NnrpManagedDiagnosticSurfaceAttribute(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("Reason must not be null or empty.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}
