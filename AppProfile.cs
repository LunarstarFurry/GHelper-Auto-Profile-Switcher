namespace GHelperAutoProfileSwitcher
{
    public enum TargetMode
    {
        Silent,
        Balanced,
        Turbo
    }

    public class AppProfile
    {
        public string ProcessName { get; set; } = string.Empty;
        public TargetMode Mode { get; set; }
    }
}