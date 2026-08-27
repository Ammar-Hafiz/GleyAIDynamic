namespace Simmac.GleyAIDynamic
{
    internal static class MapRuntimeId
    {
        private const string CloneSuffix = "(Clone)";

        public static string Normalize(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId))
            {
                return string.Empty;
            }

            string normalized = mapId.Trim();

            if (normalized.EndsWith(CloneSuffix, System.StringComparison.Ordinal))
            {
                normalized = normalized
                    .Substring(0, normalized.Length - CloneSuffix.Length)
                    .TrimEnd();
            }

            return normalized;
        }
    }
}
