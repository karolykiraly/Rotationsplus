using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RotationsPlus.Common.Domain;

namespace RotationsPlus.Common.Data;

public static class SoftDeleteModelBuilderExtensions
{
    /// <summary>
    /// Adds a global query filter (<c>!IsDeleted</c>) to every mapped entity implementing
    /// <see cref="ISoftDeletable"/>, so soft-deleted rows are invisible to ordinary queries.
    /// Call from <c>OnModelCreating</c> after entity configurations are applied.
    /// </summary>
    public static void ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = Expression.Not(isDeleted);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(notDeleted, parameter));
        }
    }
}
