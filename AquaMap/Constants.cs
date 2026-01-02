using System.IO;
using Microsoft.Maui.Storage;

namespace AquaMap
{
    public static class Constants
    {
        public const string DatabaseFilename = "aquamap.db";

        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
    }
}