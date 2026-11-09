namespace DrawnUi.Net.Tests.Add3d;

/// <summary>The CC0 models of the Add3dGallery sample, copied next to the tests.</summary>
static class Add3dAssets
{
    static byte[] Read(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Add3d", name));

    /// <summary>"Dragon Evolved" by Quaternius: one skin, 8 animations. One array, so every test shares one read.</summary>
    public static readonly byte[] Dragon = Read("dragon.glb");

    public const string Idle = "CharacterArmature|Flying_Idle";
    public const string Punch = "CharacterArmature|Punch";
    public const string Death = "CharacterArmature|Death";
}
