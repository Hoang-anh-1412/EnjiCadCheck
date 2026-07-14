namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Counters reported after IMPORT_DWG_JSON finishes.
    /// </summary>
    public class ImportStats
    {
        public int Created { get; set; }

        public int Skipped { get; set; }

        public int Errors { get; set; }

        public int LayersCreated { get; set; }
    }
}
