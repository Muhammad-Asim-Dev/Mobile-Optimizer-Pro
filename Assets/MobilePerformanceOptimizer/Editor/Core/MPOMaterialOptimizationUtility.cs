using System;
using UnityEngine;

namespace MobilePerformanceOptimizer
{
    internal static class MPOMaterialOptimizationUtility
    {
        public static bool SupportsGpuInstancing(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            try
            {
                if (material.isVariant)
                    return false;
            }
            catch
            {
                return false;
            }

            Shader shader = material.shader;
            string name = shader.name ?? string.Empty;

            // Common Unity/URP shaders use the normal Enable GPU Instancing workflow.
            if (name == "Standard" ||
                name == "Standard (Specular setup)" ||
                name.StartsWith("Universal Render Pipeline/Lit", StringComparison.Ordinal) ||
                name.StartsWith("Universal Render Pipeline/Simple Lit", StringComparison.Ordinal) ||
                name.StartsWith("Universal Render Pipeline/Complex Lit", StringComparison.Ordinal) ||
                name.StartsWith("Universal Render Pipeline/Nature/", StringComparison.Ordinal))
                return true;

            // Custom shaders are only considered when they explicitly expose the instancing keyword.
            try
            {
                return shader.keywordSpace.FindKeyword("INSTANCING_ON").isValid;
            }
            catch
            {
                return false;
            }
        }
    }
}
