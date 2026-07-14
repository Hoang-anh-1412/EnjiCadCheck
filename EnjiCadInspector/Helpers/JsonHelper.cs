using System;
using System.IO;
using System.Text;
using EnjiCadInspector.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EnjiCadInspector.Helpers
{
    /// <summary>
    /// Newtonsoft.Json helpers for writing drawing.json.
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        /// <summary>
        /// Serializes the export payload to an indented camelCase JSON string.
        /// </summary>
        public static string Serialize(ExportResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return JsonConvert.SerializeObject(result, Settings);
        }

        /// <summary>
        /// Writes UTF-8 JSON (no BOM) to the given path. Creates parent directory if needed.
        /// </summary>
        public static void WriteToFile(ExportResult result, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Output path is empty.", nameof(filePath));
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = Serialize(result);
            File.WriteAllText(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        /// <summary>
        /// Resolves drawing.json beside the DWG. Falls back to Desktop when the DWG is unsaved.
        /// </summary>
        public static string ResolveOutputPath(string dwgPath)
        {
            if (!string.IsNullOrWhiteSpace(dwgPath))
            {
                try
                {
                    var directory = Path.GetDirectoryName(dwgPath);
                    if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    {
                        return Path.Combine(directory, "drawing.json");
                    }
                }
                catch (Exception)
                {
                    // Fall through to Desktop.
                }
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "drawing.json");
        }

        /// <summary>
        /// Deserializes a drawing.json file into <see cref="ExportResult"/>.
        /// </summary>
        public static ExportResult Deserialize(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Input path is empty.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("JSON file not found.", filePath);
            }

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            var result = JsonConvert.DeserializeObject<ExportResult>(json, Settings);
            if (result == null)
            {
                throw new InvalidOperationException("JSON deserialized to null.");
            }

            if (result.Layers == null)
            {
                result.Layers = new System.Collections.Generic.List<LayerInfo>();
            }

            if (result.Blocks == null)
            {
                result.Blocks = new System.Collections.Generic.List<BlockInfo>();
            }

            if (result.Entities == null)
            {
                result.Entities = new System.Collections.Generic.List<EntityInfo>();
            }

            if (result.Summary == null)
            {
                result.Summary = new SummaryInfo();
            }

            return result;
        }
    }
}
