using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Newtonsoft.Json;
using KhimTools.SheetExport.Models;

namespace KhimTools.SheetExport.Services
{
    public static class ExtensibleStorageService
    {
        private static readonly Guid SnapshotSchemaGuid = new Guid("F48C8B1C-62E8-4B52-87C1-6F56499B9A12");
        private static readonly Guid NamingSchemaGuid = new Guid("A7B69C18-912E-4D29-A351-789E23B81C92");
        private static readonly Guid SnapshotBackupSchemaGuid = new Guid("A9E77DC5-A201-4AE5-9E45-63CC118B8C32");
        private static readonly Guid NamingBackupSchemaGuid = new Guid("B4D90B73-321D-4D78-9341-A3ED0F5817C8");

        private const string SnapshotFieldName = "SnapshotsJson";
        private const string NamingFieldName = "TemplatesJson";

        // ── Snapshots ────────────────────────────────────────────────────────
        public static List<RevisionSnapshot> LoadSnapshots(Document doc)
        {
            try
            {
                string json = ReadLatestValidPayload(doc, SnapshotSchemaGuid, SnapshotBackupSchemaGuid,
                    SnapshotFieldName, "BackupJson", ParseSnapshots, IsValidSnapshots);
                return string.IsNullOrWhiteSpace(json) ? new List<RevisionSnapshot>() : ParseSnapshots(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][SheetExport] revision snapshot load failed: " + ex);
                return new List<RevisionSnapshot>();
            }
        }

        public static bool SaveSnapshots(Document doc, List<RevisionSnapshot> snapshots)
        {
            if (doc == null || !IsValidSnapshots(snapshots)) return false;
            try
            {
                string json = JsonConvert.SerializeObject(snapshots, Formatting.None);
                return SaveWithBackup(doc, SnapshotSchemaGuid, "KhimTools_SheetExport_RevisionSnapshots",
                    SnapshotBackupSchemaGuid, "KhimTools_SheetExport_RevisionSnapshotsBackup",
                    SnapshotFieldName, "BackupJson", json, ParseSnapshots, IsValidSnapshots,
                    "Save KhimTools Revision Snapshots");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][SheetExport] revision snapshot save failed: " + ex);
                return false;
            }
        }

        // ── Naming Templates ──────────────────────────────────────────────────
        public static List<NamingTemplate> LoadNamingTemplates(Document doc)
        {
            try
            {
                string json = ReadLatestValidPayload(doc, NamingSchemaGuid, NamingBackupSchemaGuid,
                    NamingFieldName, "BackupJson", ParseNamingTemplates, IsValidNamingTemplates);
                if (string.IsNullOrWhiteSpace(json)) return NamingTemplate.GetBuiltInTemplates();
                var templates = ParseNamingTemplates(json);
                return (templates != null && templates.Count > 0) ? templates : NamingTemplate.GetBuiltInTemplates();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][SheetExport] naming template load failed: " + ex);
                return NamingTemplate.GetBuiltInTemplates();
            }
        }

        public static bool SaveNamingTemplates(Document doc, List<NamingTemplate> templates)
        {
            if (doc == null || !IsValidNamingTemplates(templates)) return false;
            try
            {
                string json = JsonConvert.SerializeObject(templates, Formatting.None);
                return SaveWithBackup(doc, NamingSchemaGuid, "KhimTools_SheetExport_NamingTemplates",
                    NamingBackupSchemaGuid, "KhimTools_SheetExport_NamingTemplatesBackup",
                    NamingFieldName, "BackupJson", json, ParseNamingTemplates, IsValidNamingTemplates,
                    "Save KhimTools Naming Templates");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][SheetExport] naming template save failed: " + ex);
                return false;
            }
        }

        private static Schema GetOrCreateSchema(Guid schemaGuid, string schemaName, string fieldName)
        {
            Schema schema = Schema.Lookup(schemaGuid);
            if (schema != null) return schema;

            SchemaBuilder builder = new SchemaBuilder(schemaGuid);
            builder.SetSchemaName(schemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(fieldName, typeof(string));

            return builder.Finish();
        }

        private static string ReadLatestValidPayload<T>(Document doc, Guid primarySchemaId, Guid backupSchemaId,
            string primaryField, string backupField, Func<string, List<T>> parse,
            Func<List<T>, bool> validate) where T : class
        {
            if (doc?.ProjectInformation == null) return null;
            string primary = ReadRawPayload(doc.ProjectInformation, primarySchemaId, primaryField);
            if (IsValidPayload(primary, parse, validate)) return primary;
            string backup = ReadRawPayload(doc.ProjectInformation, backupSchemaId, backupField);
            return IsValidPayload(backup, parse, validate) ? backup : null;
        }

        private static bool SaveWithBackup<T>(Document doc, Guid primarySchemaId, string primarySchemaName,
            Guid backupSchemaId, string backupSchemaName, string primaryField, string backupField,
            string json, Func<string, List<T>> parse, Func<List<T>, bool> validate, string transactionName) where T : class
        {
            Element projectInfo = doc.ProjectInformation;
            if (projectInfo == null || !IsValidPayload(json, parse, validate)) return false;

            string current = ReadRawPayload(projectInfo, primarySchemaId, primaryField);
            string existingBackup = ReadRawPayload(projectInfo, backupSchemaId, backupField);
            string previousValid = IsValidPayload(current, parse, validate) ? current :
                IsValidPayload(existingBackup, parse, validate) ? existingBackup : null;
            // Never replace the only extant record when both it and its recovery copy are malformed.
            if (!string.IsNullOrWhiteSpace(current) && previousValid == null) return false;

            Schema primarySchema = GetOrCreateSchema(primarySchemaId, primarySchemaName, primaryField);
            Schema backupSchema = GetOrCreateSchema(backupSchemaId, backupSchemaName, backupField);
            Entity backupEntity = projectInfo.GetEntity(backupSchema);
            if (!backupEntity.IsValid()) backupEntity = new Entity(backupSchema);
            if (!string.IsNullOrWhiteSpace(previousValid))
                backupEntity.Set(backupSchema.GetField(backupField), previousValid);

            Entity primaryEntity = projectInfo.GetEntity(primarySchema);
            if (!primaryEntity.IsValid()) primaryEntity = new Entity(primarySchema);
            primaryEntity.Set(primarySchema.GetField(primaryField), json);

            Action persist = () =>
            {
                if (!string.IsNullOrWhiteSpace(previousValid)) projectInfo.SetEntity(backupEntity);
                projectInfo.SetEntity(primaryEntity);
            };
            if (doc.IsModifiable)
            {
                persist();
                return true;
            }

            using (var transaction = new Transaction(doc, transactionName))
            {
                if (transaction.Start() != TransactionStatus.Started) return false;
                persist();
                return transaction.Commit() == TransactionStatus.Committed;
            }
        }

        private static string ReadRawPayload(Element element, Guid schemaId, string fieldName)
        {
            Schema schema = Schema.Lookup(schemaId);
            if (schema == null) return null;
            Entity entity = element.GetEntity(schema);
            if (!entity.IsValid()) return null;
            return entity.Get<string>(schema.GetField(fieldName));
        }

        private static bool IsValidPayload<T>(string json, Func<string, List<T>> parse,
            Func<List<T>, bool> validate) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { return validate(parse(json)); }
            catch { return false; }
        }

        private static List<RevisionSnapshot> ParseSnapshots(string json) =>
            JsonConvert.DeserializeObject<List<RevisionSnapshot>>(json,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 });

        private static List<NamingTemplate> ParseNamingTemplates(string json) =>
            JsonConvert.DeserializeObject<List<NamingTemplate>>(json,
                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 32 });

        private static bool IsValidSnapshots(List<RevisionSnapshot> snapshots)
        {
            return snapshots != null && snapshots.Count <= 50000 && snapshots.All(snapshot => snapshot != null &&
                !string.IsNullOrWhiteSpace(snapshot.ExportId) && snapshot.ExportId.Length <= 200 &&
                snapshot.Items != null && snapshot.Items.Count <= 10000 &&
                snapshot.Items.All(item => item != null && item.SheetUniqueId != null && item.SheetUniqueId.Length <= 500 &&
                    item.SheetNumber != null && item.SheetNumber.Length <= 200 && item.ExportFileName != null && item.ExportFileName.Length <= 2000));
        }

        private static bool IsValidNamingTemplates(List<NamingTemplate> templates)
        {
            return templates != null && templates.Count <= 500 && templates.All(template => template != null &&
                !string.IsNullOrWhiteSpace(template.Name) && template.Name.Length <= 200 &&
                !string.IsNullOrWhiteSpace(template.Expression) && template.Expression.Length <= 4000 &&
                !string.IsNullOrWhiteSpace(template.RegexPattern) && template.RegexPattern.Length <= 4000);
        }
    }
}
