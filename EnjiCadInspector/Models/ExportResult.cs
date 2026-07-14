using System.Collections.Generic;

namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Root payload serialized to drawing.json.
    /// </summary>
    public class ExportResult
    {
        public DrawingInfo Drawing { get; set; }

        public SummaryInfo Summary { get; set; }

        public List<LayerInfo> Layers { get; set; }

        public List<BlockInfo> Blocks { get; set; }

        public List<EntityInfo> Entities { get; set; }

        public ExportResult()
        {
            Layers = new List<LayerInfo>();
            Blocks = new List<BlockInfo>();
            Entities = new List<EntityInfo>();
            Summary = new SummaryInfo();
        }
    }
}
