// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
{
    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public class ForeignKeyIndexConvention :
        IForeignKeyAddedConvention,
        IForeignKeyRemovedConvention,
        IForeignKeyPropertiesChangedConvention,
        IForeignKeyUniquenessChangedConvention,
        IKeyAddedConvention,
        IKeyRemovedConvention,
        IBaseTypeChangedConvention,
        IIndexAddedConvention,
        IIndexRemovedConvention,
        IIndexUniquenessChangedConvention,
        IModelBuiltConvention
    {
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public ForeignKeyIndexConvention([NotNull] IDiagnosticsLogger<DbLoggerCategory.Model> logger)
        {
            Logger = logger;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected virtual IDiagnosticsLogger<DbLoggerCategory.Model> Logger { get; }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalRelationshipBuilder Apply(InternalRelationshipBuilder relationshipBuilder)
        {
            var foreignKey = relationshipBuilder.Metadata;
            CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);

            return relationshipBuilder;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual void Apply(InternalEntityTypeBuilder entityTypeBuilder, ForeignKey foreignKey)
        {
            OnForeignKeyRemoved(foreignKey.DeclaringEntityType, foreignKey.Properties);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalRelationshipBuilder Apply(
            InternalRelationshipBuilder relationshipBuilder,
            IReadOnlyList<Property> oldDependentProperties,
            Key oldPrincipalKey)
        {
            var foreignKey = relationshipBuilder.Metadata;
            if (!foreignKey.Properties.SequenceEqual(oldDependentProperties))
            {
                OnForeignKeyRemoved(foreignKey.DeclaringEntityType, oldDependentProperties);
                if (relationshipBuilder.Metadata.Builder != null)
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
            }

            return relationshipBuilder;
        }

        private static void OnForeignKeyRemoved(EntityType declaringType, IReadOnlyList<Property> foreignKeyProperties)
        {
            var index = declaringType.FindIndex(foreignKeyProperties);
            if (index == null)
            {
                return;
            }

            var otherForeignKeys = declaringType.FindForeignKeys(foreignKeyProperties).ToList();
            if (otherForeignKeys.Count != 0)
            {
                if (index.IsUnique
                    && otherForeignKeys.All(fk => !fk.IsUnique))
                {
                    index.Builder.IsUnique(false, ConfigurationSource.Convention);
                }

                return;
            }

            index.DeclaringEntityType.Builder.RemoveIndex(index, ConfigurationSource.Convention);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalKeyBuilder Apply(InternalKeyBuilder keyBuilder)
        {
            var key = keyBuilder.Metadata;
            foreach (var index in key.DeclaringEntityType.GetDerivedIndexesInclusive()
                .Where(i => AreIndexedBy(i.Properties, i.IsUnique, key.Properties, true)).ToList())
            {
                RemoveIndex(index);
            }

            return keyBuilder;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual void Apply(InternalEntityTypeBuilder entityTypeBuilder, Key key)
        {
            foreach (var otherForeignKey in key.DeclaringEntityType.GetDerivedForeignKeysInclusive()
                .Where(fk => AreIndexedBy(fk.Properties, fk.IsUnique, key.Properties, coveringIndexUniqueness: true)))
            {
                CreateIndex(otherForeignKey.Properties, otherForeignKey.IsUnique, otherForeignKey.DeclaringEntityType.Builder);
            }
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual bool Apply(InternalEntityTypeBuilder entityTypeBuilder, EntityType oldBaseType)
        {
            var baseType = entityTypeBuilder.Metadata.BaseType;
            var baseKeys = baseType?.GetKeys().ToList();
            var baseIndexes = baseType?.GetIndexes().ToList();
            foreach (var foreignKey in entityTypeBuilder.Metadata.GetDerivedForeignKeysInclusive())
            {
                var index = foreignKey.DeclaringEntityType.FindIndex(foreignKey.Properties);
                if (index == null)
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
                else if (baseType != null)
                {
                    var coveringKey = baseKeys.FirstOrDefault(
                        k => AreIndexedBy(foreignKey.Properties, foreignKey.IsUnique, k.Properties, coveringIndexUniqueness: true));
                    if (coveringKey != null)
                    {
                        RemoveIndex(index);
                    }
                    else
                    {
                        var coveringIndex = baseIndexes.FirstOrDefault(
                            i => AreIndexedBy(foreignKey.Properties, foreignKey.IsUnique, i.Properties, i.IsUnique));
                        if (coveringIndex != null)
                        {
                            RemoveIndex(index);
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalIndexBuilder Apply(InternalIndexBuilder indexBuilder)
        {
            var index = indexBuilder.Metadata;
            foreach (var otherIndex in index.DeclaringEntityType.GetDerivedIndexesInclusive()
                .Where(i => i != index && AreIndexedBy(i.Properties, i.IsUnique, index.Properties, index.IsUnique)).ToList())
            {
                RemoveIndex(otherIndex);
            }

            return indexBuilder;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual void Apply(InternalEntityTypeBuilder entityTypeBuilder, Index index)
        {
            foreach (var foreignKey in index.DeclaringEntityType.GetDerivedForeignKeysInclusive()
                .Where(fk => AreIndexedBy(fk.Properties, fk.IsUnique, index.Properties, index.IsUnique)))
            {
                CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
            }
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        InternalRelationshipBuilder IForeignKeyUniquenessChangedConvention.Apply(InternalRelationshipBuilder relationshipBuilder)
        {
            var foreignKey = relationshipBuilder.Metadata;
            var index = foreignKey.DeclaringEntityType.FindIndex(foreignKey.Properties);
            if (index == null)
            {
                if (foreignKey.IsUnique)
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
            }
            else
            {
                if (!foreignKey.IsUnique)
                {
                    var coveringKey = foreignKey.DeclaringEntityType.GetKeys()
                        .FirstOrDefault(k => AreIndexedBy(foreignKey.Properties, false, k.Properties, coveringIndexUniqueness: true));
                    if (coveringKey != null)
                    {
                        RemoveIndex(index);
                        return relationshipBuilder;
                    }

                    var coveringIndex = foreignKey.DeclaringEntityType.GetIndexes()
                        .FirstOrDefault(i => AreIndexedBy(foreignKey.Properties, false, i.Properties, i.IsUnique));
                    if (coveringIndex != null)
                    {
                        RemoveIndex(index);
                        return relationshipBuilder;
                    }
                }

                index.Builder.IsUnique(foreignKey.IsUnique, ConfigurationSource.Convention);
            }

            return relationshipBuilder;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        bool IIndexUniquenessChangedConvention.Apply(InternalIndexBuilder indexBuilder)
        {
            var index = indexBuilder.Metadata;
            if (index.IsUnique)
            {
                foreach (var otherIndex in index.DeclaringEntityType.GetDerivedIndexesInclusive()
                    .Where(i => i != index && AreIndexedBy(i.Properties, i.IsUnique, index.Properties, coveringIndexUniqueness: true)).ToList())
                {
                    RemoveIndex(otherIndex);
                }
            }
            else
            {
                foreach (var foreignKey in index.DeclaringEntityType.GetDerivedForeignKeysInclusive()
                    .Where(fk => fk.IsUnique && AreIndexedBy(fk.Properties, fk.IsUnique, index.Properties, coveringIndexUniqueness: true)))
                {
                    CreateIndex(foreignKey.Properties, foreignKey.IsUnique, foreignKey.DeclaringEntityType.Builder);
                }
            }

            return true;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected virtual Index CreateIndex(
            [NotNull] IReadOnlyList<Property> properties, bool unique, [NotNull] InternalEntityTypeBuilder entityTypeBuilder)
        {
            foreach (var key in entityTypeBuilder.Metadata.GetKeys())
            {
                if (AreIndexedBy(properties, unique, key.Properties, coveringIndexUniqueness: true))
                {
                    return null;
                }
            }

            foreach (var existingIndex in entityTypeBuilder.Metadata.GetIndexes())
            {
                if (AreIndexedBy(properties, unique, existingIndex.Properties, existingIndex.IsUnique))
                {
                    return null;
                }
            }

            var indexBuilder = entityTypeBuilder.HasIndex(properties, ConfigurationSource.Convention);
            if (unique)
            {
                indexBuilder?.IsUnique(true, ConfigurationSource.Convention);
            }

            return indexBuilder?.Metadata;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected virtual bool AreIndexedBy(
            [NotNull] IReadOnlyList<Property> properties,
            bool unique,
            [NotNull] IReadOnlyList<Property> coveringIndexProperties,
            bool coveringIndexUniqueness)
            => (!unique && coveringIndexProperties.Select(p => p.Name).StartsWith(properties.Select(p => p.Name)))
               || (unique && coveringIndexUniqueness && coveringIndexProperties.SequenceEqual(properties));

        private static void RemoveIndex(Index index)
            => index.DeclaringEntityType.Builder.RemoveIndex(index, ConfigurationSource.Convention);

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual InternalModelBuilder Apply(InternalModelBuilder modelBuilder)
        {
            var definition = CoreResources.LogRedundantIndexRemoved(Logger);
            if (definition.GetLogBehavior(Logger) == WarningBehavior.Ignore
                && !Logger.DiagnosticSource.IsEnabled(definition.EventId.Name))
            {
                return modelBuilder;
            }

            foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
            {
                foreach (var declaredForeignKey in entityType.GetDeclaredForeignKeys())
                {
                    foreach (var key in entityType.GetKeys())
                    {
                        if (AreIndexedBy(declaredForeignKey.Properties, declaredForeignKey.IsUnique, key.Properties, coveringIndexUniqueness: true))
                        {
                            if (declaredForeignKey.Properties.Count != key.Properties.Count)
                            {
                                Logger.RedundantIndexRemoved(declaredForeignKey.Properties, key.Properties);
                            }
                        }
                    }

                    foreach (var existingIndex in entityType.GetIndexes())
                    {
                        if (AreIndexedBy(declaredForeignKey.Properties, declaredForeignKey.IsUnique, existingIndex.Properties, existingIndex.IsUnique))
                        {
                            if (declaredForeignKey.Properties.Count != existingIndex.Properties.Count)
                            {
                                Logger.RedundantIndexRemoved(declaredForeignKey.Properties, existingIndex.Properties);
                            }
                        }
                    }
                }
            }

            return modelBuilder;
        }
    }
}
