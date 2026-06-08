#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TransportManager.EditorTools
{
    /// <summary>
    /// Injecte les permissions iOS requises dans l'Info.plist à chaque build Xcode.
    ///
    /// ⚠️ Sans <c>NSPhotoLibraryUsageDescription</c>, l'app **crashe** à l'ouverture du sélecteur
    /// de photos (import du logo d'entreprise via UIImagePickerController), et Apple **rejette**
    /// l'app à la review. Ce script garantit la clé à chaque build, sans manip manuelle.
    /// </summary>
    public static class IosBuildPostProcess
    {
        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            // Lecture de la photothèque (choix du logo). Le picker ne fait que LIRE → seule
            // NSPhotoLibraryUsageDescription est nécessaire.
            plist.root.SetString(
                "NSPhotoLibraryUsageDescription",
                "Cette autorisation permet de choisir une photo de ta pellicule comme logo de ton entreprise.");

            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
#endif
