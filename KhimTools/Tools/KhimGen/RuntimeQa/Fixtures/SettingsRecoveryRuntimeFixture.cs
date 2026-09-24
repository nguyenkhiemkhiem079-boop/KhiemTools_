using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.SheetExport.Models;
using KhimTools.SheetExport.Services;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Exercises document-owned settings inside RuntimeQaFixtureBase's rollback-only group.</summary>
    public sealed class SettingsRecoveryRuntimeFixture : RuntimeQaFixtureBase
    {
        private static readonly Guid SnapshotSchemaId = new Guid("F48C8B1C-62E8-4B52-87C1-6F56499B9A12");
        private static readonly Guid SnapshotBackupSchemaId = new Guid("A9E77DC5-A201-4AE5-9E45-63CC118B8C32");
        private static readonly Guid NamingSchemaId = new Guid("A7B69C18-912E-4D29-A351-789E23B81C92");
        private static readonly Guid NamingBackupSchemaId = new Guid("B4D90B73-321D-4D78-9341-A3ED0F5817C8");
        private string _originalSnapshotPayload;
        private string _originalSnapshotBackupPayload;
        private string _originalNamingPayload;
        private string _originalNamingBackupPayload;

        public override string Id => "SETTINGS-EXTSTORAGE-RECOVERY";
        public override string Name => "Settings Extensible Storage Recovery";
        public override string Suite => "SETTINGS";
        public override string Description => "Round-trip project settings, recover corrupted primary JSON, and refuse overwrite when both copies are corrupt; the fixture is rollback-only.";
        public override bool IsCritical => true;

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            _originalSnapshotPayload = ReadPayload(context.Document, SnapshotSchemaId, "SnapshotsJson");
            _originalSnapshotBackupPayload = ReadPayload(context.Document, SnapshotBackupSchemaId, "BackupJson");
            _originalNamingPayload = ReadPayload(context.Document, NamingSchemaId, "TemplatesJson");
            _originalNamingBackupPayload = ReadPayload(context.Document, NamingBackupSchemaId, "BackupJson");
            string token = context.RunId;
            var firstNaming = new List<NamingTemplate>
            {
                new NamingTemplate { Name = "QA-A-" + token, Expression = "{SheetNumber}", RegexPattern = ".*" }
            };
            var secondNaming = new List<NamingTemplate>
            {
                new NamingTemplate { Name = "QA-B-" + token, Expression = "{SheetName}", RegexPattern = ".*" }
            };
            bool namingFirstSaved = ExtensibleStorageService.SaveNamingTemplates(context.Document, firstNaming);
            bool namingSecondSaved = ExtensibleStorageService.SaveNamingTemplates(context.Document, secondNaming);
            Check(result, "SET_NAMING_ROUNDTRIP", "Document naming preset saves", namingFirstSaved && namingSecondSaved &&
                ExtensibleStorageService.LoadNamingTemplates(context.Document).Exists(x => x.Name == secondNaming[0].Name),
                "Latest naming preset round-trips", namingSecondSaved.ToString(), "All writes remain inside the fixture's transaction group.", QaSeverity.CRITICAL);

            bool namingPrimaryCorrupted = TryReplacePayload(context.Document, NamingSchemaId, "TemplatesJson", "{corrupt");
            bool namingRecovered = ExtensibleStorageService.LoadNamingTemplates(context.Document).Exists(x => x.Name == firstNaming[0].Name);
            Check(result, "SET_NAMING_RECOVERY", "Corrupt naming primary recovers prior valid payload", namingPrimaryCorrupted && namingRecovered,
                firstNaming[0].Name, namingRecovered ? "Recovered prior valid preset" : "No valid recovery found",
                "The previous valid document payload is retained in its separate backup schema.", QaSeverity.CRITICAL);

            bool namingBackupCorrupted = TryReplacePayload(context.Document, NamingBackupSchemaId, "BackupJson", "{corrupt-backup");
            bool refusedOverwrite = !ExtensibleStorageService.SaveNamingTemplates(context.Document,
                new List<NamingTemplate> { new NamingTemplate { Name = "QA-C-" + token, Expression = "{SheetNumber}", RegexPattern = ".*" } });
            bool primaryStillCorrupted = ReadPayload(context.Document, NamingSchemaId, "TemplatesJson") == "{corrupt";
            Check(result, "SET_NAMING_PRESERVE", "Do not overwrite when primary and backup are corrupt",
                namingBackupCorrupted && refusedOverwrite && primaryStillCorrupted,
                "Save refused and corrupt source bytes preserved", (refusedOverwrite && primaryStillCorrupted).ToString(),
                "Source model remains unchanged after fixture rollback.", QaSeverity.CRITICAL);

            var firstSnapshot = Snapshot("QA-A-" + token);
            var secondSnapshot = Snapshot("QA-B-" + token);
            bool snapshotFirstSaved = ExtensibleStorageService.SaveSnapshots(context.Document, new List<RevisionSnapshot> { firstSnapshot });
            bool snapshotSecondSaved = ExtensibleStorageService.SaveSnapshots(context.Document, new List<RevisionSnapshot> { secondSnapshot });
            Check(result, "SET_SNAPSHOT_ROUNDTRIP", "Document revision snapshot saves", snapshotFirstSaved && snapshotSecondSaved &&
                ExtensibleStorageService.LoadSnapshots(context.Document).Exists(x => x.ExportId == secondSnapshot.ExportId),
                "Latest revision snapshot round-trips", snapshotSecondSaved.ToString(), "Snapshot data is test-only and rolled back with the fixture.", QaSeverity.CRITICAL);

            bool snapshotPrimaryCorrupted = TryReplacePayload(context.Document, SnapshotSchemaId, "SnapshotsJson", "{corrupt");
            bool snapshotRecovered = ExtensibleStorageService.LoadSnapshots(context.Document).Exists(x => x.ExportId == firstSnapshot.ExportId);
            Check(result, "SET_SNAPSHOT_RECOVERY", "Corrupt snapshot primary recovers prior valid payload", snapshotPrimaryCorrupted && snapshotRecovered,
                firstSnapshot.ExportId, snapshotRecovered ? "Recovered prior valid snapshot" : "No valid recovery found",
                "The previous valid document payload is retained in its separate backup schema.", QaSeverity.CRITICAL);
        }

        protected override bool VerifyAdditionalRollbackState(RuntimeQaContext context, out string message)
        {
            bool restored = string.Equals(_originalSnapshotPayload, ReadPayload(context.Document, SnapshotSchemaId, "SnapshotsJson"), StringComparison.Ordinal) &&
                string.Equals(_originalSnapshotBackupPayload, ReadPayload(context.Document, SnapshotBackupSchemaId, "BackupJson"), StringComparison.Ordinal) &&
                string.Equals(_originalNamingPayload, ReadPayload(context.Document, NamingSchemaId, "TemplatesJson"), StringComparison.Ordinal) &&
                string.Equals(_originalNamingBackupPayload, ReadPayload(context.Document, NamingBackupSchemaId, "BackupJson"), StringComparison.Ordinal);
            message = "Document Extensible Storage primaries and backups restored exactly: " + restored + ".";
            return restored;
        }

        private static RevisionSnapshot Snapshot(string id) => new RevisionSnapshot
        {
            ExportId = id,
            ExportTime = DateTime.UtcNow,
            ExportedBy = "K-TOOLS Runtime QA",
            IssueSetName = "Recovery fixture",
            Items = new List<SheetSnapshotItem>()
        };

        private static bool TryReplacePayload(Document doc, Guid schemaId, string fieldName, string value)
        {
            Schema schema = Schema.Lookup(schemaId);
            if (schema == null || doc.ProjectInformation == null) return false;
            Entity entity = doc.ProjectInformation.GetEntity(schema);
            if (!entity.IsValid()) return false;
            entity.Set(schema.GetField(fieldName), value);
            using (var transaction = new Transaction(doc, "K-TOOLS Runtime QA - Corrupt Settings Fixture"))
            {
                if (transaction.Start() != TransactionStatus.Started) return false;
                doc.ProjectInformation.SetEntity(entity);
                return transaction.Commit() == TransactionStatus.Committed;
            }
        }

        private static string ReadPayload(Document doc, Guid schemaId, string fieldName)
        {
            Schema schema = Schema.Lookup(schemaId);
            if (schema == null || doc.ProjectInformation == null) return null;
            Entity entity = doc.ProjectInformation.GetEntity(schema);
            return entity.IsValid() ? entity.Get<string>(schema.GetField(fieldName)) : null;
        }
    }
}
