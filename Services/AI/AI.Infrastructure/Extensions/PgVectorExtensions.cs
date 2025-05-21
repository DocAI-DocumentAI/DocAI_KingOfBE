using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI.Infrastructure.Extensions
{
    public static class PgVectorExtensions
    {
        public static PropertyBuilder<float[]> UseVector(
          this PropertyBuilder<float[]> propertyBuilder,
          int dimensions)
        {
            return propertyBuilder
                .HasColumnType($"vector({dimensions})")
                .HasAnnotation("Dimensions", dimensions);
        }

        public static IndexBuilder HasVectorMethod(
            this IndexBuilder indexBuilder,
            string method)
        {
            return indexBuilder.HasMethod(method);
        }

        public static IndexBuilder HasVectorOperators(
            this IndexBuilder indexBuilder,
            string operators)
        {
            return indexBuilder.HasOperators(operators);
        }
    }
}
