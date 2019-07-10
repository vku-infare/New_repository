namespace AirTickets
{
    using System.Text.RegularExpressions;

    internal static class Regexes
    {
        public static RegexOptions RegexOptionsCompiled = RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

        public static Regex JsonSearchId = new Regex(@"\u0022ID\u0022\s*\:\s*\u0022\s*([^\u0022]*)\u0022", RegexOptionsCompiled);
        public static Regex ExtractJsonAreas = new Regex(@"event\:message\s*data\:(.*?\u007d)\s*(?=event)", RegexOptionsCompiled);
    }
}
