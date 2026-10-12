using Autodesk.Revit.DB.Structure;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// Centralizes Rebar API differences across Revit 2021-2027.
    /// Keep version conditionals here rather than in business services.
    /// </summary>
    public static class RebarApiCompatibility
    {
        public static void SetPreferredConstraint(
            RebarConstraintsManager manager,
            RebarConstrainedHandle handle,
            RebarConstraint constraint)
        {
#if REVIT2025_OR_GREATER
            // Revit 2025 introduced SetPreferredConstraint(RebarConstraint) and deprecated
            // SetPreferredConstraintForHandle. Revit 2026+ may remove the old overload.
            manager.SetPreferredConstraint(constraint);
#else
            manager.SetPreferredConstraintForHandle(handle, constraint);
#endif
        }

        public static void SetShapeDrivenVaryingEnabled(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            bool enabled)
        {
#if REVIT2025_OR_GREATER
            using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
            {
                accessor.UseRebarConstraintsToProduceVaryingBars = enabled;
            }
#else
            rebar.DistributionType = enabled
                ? DistributionType.VaryingLength
                : DistributionType.Uniform;
#endif
        }

        public static string ReadVaryingMode(Autodesk.Revit.DB.Structure.Rebar rebar)
        {
#if REVIT2025_OR_GREATER
            try
            {
                using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
                    return accessor.UseRebarConstraintsToProduceVaryingBars ? "ConstraintsVarying" : "Uniform";
            }
            catch
            {
                return string.Empty;
            }
#else
            try { return rebar.DistributionType.ToString(); }
            catch { return string.Empty; }
#endif
        }
    }
}
