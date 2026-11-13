using System;

namespace TransportManager.Save
{
    /// <summary>Une entrée du journal de notifications (sauvegardée).</summary>
    [Serializable]
    public class NotificationEntry
    {
        public long   timeTicks;   // heure UTC réelle de l'événement (même s'il s'est produit hors-ligne)
        public string kind;        // NotificationKind.*
        public string title;
        public string body;
        public bool   read;
    }

    /// <summary>Types de notifications (chaînes : robustes aux ajouts dans la sauvegarde).</summary>
    public static class NotificationKind
    {
        public const string Delivered   = "delivered";
        public const string Late        = "late";
        public const string Accident    = "accident";
        public const string Fatal       = "fatal";
        public const string Resigned    = "resigned";
        public const string LevelUp     = "levelup";
        public const string Maintenance = "maintenance";
        public const string Fuel        = "fuel";
        public const string Raise       = "raise";
    }
}
