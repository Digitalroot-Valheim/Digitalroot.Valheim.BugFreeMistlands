using System.Diagnostics.CodeAnalysis;

namespace Digitalroot.Valheim.BugFreeMistlands
{
  [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
  public partial class Main
  {
    public const string Version = "0.0.1";
    public const string Name = "Bug Free Mistlands";
    public const string Guid = "digitalroot.mods.bugfreemistlands";
    public const string Namespace = $"{nameof(Digitalroot)}.{nameof(Valheim)}.{nameof(BugFreeMistlands)}";

    internal static class PluginConfigSection
    {
      internal const string General = nameof(General);
      internal const string Advanced = nameof(Advanced);
    }
  }
}
