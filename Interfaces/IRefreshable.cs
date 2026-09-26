namespace JDFixer.Interfaces
{
    /// <summary>
    /// A view that shows JDFixer's slider state and needs to re-read it when the user comes back to the
    /// main menu from a mode where the settings may have been changed.
    /// </summary>
    /// <remarks>
    /// Replaces the three <c>static Instance</c> fields the modifier UIs each kept, which meant the
    /// manager had to know about every concrete UI type by name.
    /// </remarks>
    internal interface IRefreshable
    {
        void Refresh();
    }
}
