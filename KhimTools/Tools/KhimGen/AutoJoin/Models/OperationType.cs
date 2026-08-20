namespace KhimTools.SlabJoin.Models
{
    /// <summary>
    /// Identifies the type of geometry operation being performed on a pair of slabs.
    /// </summary>
    public enum OperationType
    {
        /// <summary>
        /// Join geometry operation (Element.JoinGeometry).
        /// </summary>
        Join,

        /// <summary>
        /// Unjoin geometry operation (Element.UnjoinGeometry).
        /// </summary>
        Unjoin
    }
}
