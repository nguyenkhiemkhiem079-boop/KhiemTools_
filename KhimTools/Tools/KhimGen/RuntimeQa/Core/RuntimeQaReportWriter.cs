using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Core
{
    public static class RuntimeQaReportWriter
    {
        public static RuntimeQaReportPaths Write(QaRunResult run, string outputDirectory, bool preserveArtifacts = false)
        {
            if (run == null) throw new ArgumentNullException("run");
            string root = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Path.GetTempPath(), "KhimTools", "RuntimeQA", run.RunId)
                : outputDirectory;
            Directory.CreateDirectory(root);
            run.ReportDirectory = root;
            string jsonPath = Path.Combine(root, "runtime-qa.json");
            string textPath = Path.Combine(root, "runtime-qa.txt");
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(run, Formatting.Indented), Encoding.UTF8);
            File.WriteAllText(textPath, BuildSummary(run), Encoding.UTF8);
            if (!preserveArtifacts)
            {
                foreach (QaFixtureResult fixture in run.Fixtures)
                    foreach (string artifact in fixture.Artifacts.ToList())
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(artifact)) continue;
                            if (Directory.Exists(artifact)) Directory.Delete(artifact, true);
                            else if (File.Exists(artifact)) File.Delete(artifact);
                        }
                        catch { }
                    }
            }
            return new RuntimeQaReportPaths { Directory = root, JsonPath = jsonPath, TextPath = textPath };
        }

        public static string BuildSummary(QaRunResult run)
        {
            var sb = new StringBuilder();
            sb.AppendLine("K-TOOLS RUNTIME QA");
            sb.AppendLine();
            sb.AppendLine("PASS:     " + run.Passed);
            sb.AppendLine("FAIL:     " + run.Failed);
            sb.AppendLine("BLOCKED:  " + run.Blocked);
            sb.AppendLine("SKIPPED:  " + run.Skipped);
            sb.AppendLine("NOT_RUN:  " + run.NotRun);
            sb.AppendLine("CERTIFICATION: " + (run.CertificationStatus ?? "INCOMPLETE"));
            sb.AppendLine("MODEL CLEAN AFTER QA: " + (run.ModelRollbackVerified ? "YES" : "NO"));
            if (!string.IsNullOrWhiteSpace(run.ModelRollbackMessage))
                sb.AppendLine("MODEL ROLLBACK DETAILS: " + run.ModelRollbackMessage);
            sb.AppendLine();
            foreach (QaFixtureResult fixture in run.Fixtures)
                sb.AppendLine(string.Format("{0,-10} {1,-36} {2,-8} {3}ms", fixture.Status, fixture.Name, fixture.FixtureId, (long)fixture.Duration.TotalMilliseconds));
            return sb.ToString();
        }

        public sealed class RuntimeQaReportPaths
        {
            public string Directory { get; set; }
            public string JsonPath { get; set; }
            public string TextPath { get; set; }
        }
    }
}
