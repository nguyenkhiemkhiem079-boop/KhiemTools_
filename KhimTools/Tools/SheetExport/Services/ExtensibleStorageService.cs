using System;
using System.Collections.Generic;
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

        private const string SnapshotFieldName = "SnapshotsJson";
        private const string NamingFieldName = "TemplatesJson";

        // ── Snapshots ────────────────────────────────────────────────────────
        public static List<RevisionSnapshot> LoadSnapshots(Document doc)
        {
            try
            {
                var schema = Schema.Lookup(SnapshotSchemaGuid);
                if (schema == null) return new List<RevisionSnapshot>();

                Element projInfo = doc.ProjectInformation;
                if (projInfo == null) return new List<RevisionSnapshot>();

                Entity entity = projInfo.GetEntity(schema);
                if (!entity.IsValid()) return new List<RevisionSnapshot>();

                string json = entity.Get<string>(schema.GetField(SnapshotFieldName));
                if (string.IsNullOrWhiteSpace(json)) return new List<RevisionSnapshot>();

                return JsonConvert.DeserializeObject<List<RevisionSnapshot>>(json) ?? new List<RevisionSnapshot>();
            }
            catch
            {
                return new List<RevisionSnapshot>();
            }
        }

        public static bool SaveSnapshots(Document doc, List<RevisionSnapshot> snapshots)
        {
            try
            {
                Schema schema = GetOrCreateSchema(SnapshotSchemaGuid, "KhimTools_SheetExport_RevisionSnapshots", SnapshotFieldName);
                Element projInfo = doc.ProjectInformation;
                if (projInfo == null) return false;

                string json = JsonConvert.SerializeObject(snapshots, Formatting.None);

                Entity entity = new Entity(schema);
                entity.Set(schema.GetField(SnapshotFieldName), json);
                projInfo.SetEntity(entity);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── Naming Templates ──────────────────────────────────────────────────
        public static List<NamingTemplate> LoadNamingTemplates(Document doc)
        {
            try
            {
                var schema = Schema.Lookup(NamingSchemaGuid);
                if (schema == null) return NamingTemplate.GetBuiltInTemplates();

                Element projInfo = doc.ProjectInformation;
                if (projInfo == null) return NamingTemplate.GetBuiltInTemplates();

                Entity entity = projInfo.GetEntity(schema);
                if (!entity.IsValid()) return NamingTemplate.GetBuiltInTemplates();

                string json = entity.Get<string>(schema.GetField(NamingFieldName));
                if (string.IsNullOrWhiteSpace(json)) return NamingTemplate.GetBuiltInTemplates();

                var templates = JsonConvert.DeserializeObject<List<NamingTemplate>>(json);
                return (templates != null && templates.Count > 0) ? templates : NamingTemplate.GetBuiltInTemplates();
            }
            catch
            {
                return NamingTemplate.GetBuiltInTemplates();
            }
        }

        public static bool SaveNamingTemplates(Document doc, List<NamingTemplate> templates)
        {
            try
            {
                Schema schema = GetOrCreateSchema(NamingSchemaGuid, "KhimTools_SheetExport_NamingTemplates", NamingFieldName);
                Element projInfo = doc.ProjectInformation;
                if (projInfo == null) return false;

                string json = JsonConvert.SerializeObject(templates, Formatting.None);

                Entity entity = new Entity(schema);
                entity.Set(schema.GetField(NamingFieldName), json);
                projInfo.SetEntity(entity);

                return true;
            }
            catch
            {
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
    }
}
