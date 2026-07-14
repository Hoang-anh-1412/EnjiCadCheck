namespace EnjiCadInspector.Models
{
    /// <summary>
    /// Layer table record export model.
    /// </summary>
    public class LayerInfo
    {
        public string Name { get; set; }

        public string Color { get; set; }

        public string Linetype { get; set; }

        public bool IsLocked { get; set; }

        public bool IsFrozen { get; set; }

        public bool IsOff { get; set; }
    }
}
