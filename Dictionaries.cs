namespace AirTickets
{
    using System.Collections.Generic;

    internal static class Dictionaries
    {
        public static Dictionary<string, string> Cabin = new Dictionary<string, string>
        {
            ["E"] = "Y",
            ["P"] = "W",
            ["B"] = "C",
            ["F"] = "F",
        };

        public static Dictionary<string, string> CabinClass = new Dictionary<string, string>
        {
            ["Y"] = "Economy",
            ["W"] = "Economy Premium",
            ["C"] = "Business",
            ["F"] = "First",
        };

        public static Dictionary<string, string> Saved = new Dictionary<string, string> { };

        public static Dictionary<string, string> CheapestCarrier = new Dictionary<string, string>();

        public static Dictionary<int, string> LocalizedBaseUrls = new Dictionary<int, string>
        {
            [1293] = "https://travel.airtickets.com/", // albania
            [1294] = "https://travel.airtickets.com/", // australia
            [1295] = "https://travel.airtickets.com/", // france
            [1296] = "https://travel.airtickets.com/", // germany
            [1297] = "https://travel.airtickets.com/", // greece
            [1298] = "https://travel.airtickets.com/", // italy
            [1299] = "https://travel.airtickets.com/", // poland
            [1300] = "https://travel.airtickets.com/", // romania
            [1301] = "https://travel.airtickets.com/", // russia
            [1302] = "https://travel.airtickets.com/", // turkey
            [1303] = "https://travel.airtickets.com/", // ukraine
            [1285] = "https://travel.airtickets.com/", // united kingdom
            [1304] = "https://travel.airtickets.com/", // usa
        };

        public static Dictionary<string, string> Locales = new Dictionary<string, string>
        {
            //["AL"] = "sq_AL", // albania
            //["AU"] = "en_AU", // australia
            //["FR"] = "fr_FR", // france
            //["DE"] = "de_DE", // germany
            ["GR"] = "el_GR", // greece
            //["IT"] = "it_IT", // italy
            //["PL"] = "pl_PL", // poland
            //["RO"] = "ro_Ro", // romania
            //["RU"] = "ru_RU", // russia
            //["TR"] = "tr_TR", // turkey
            //["UA"] = "uk_UA", // ukraine
            //["UK"] = "en_GB", // united kingdom
            ["US"] = "en_US", // usa
        };

        public static Dictionary<string, string> Currencies = new Dictionary<string, string>
        {
            //["AL"] = "ALL", // albania
            //["AU"] = "AUD", // australia
            //["FR"] = "EUR", // france
            //["DE"] = "EUR", // germany
            ["GR"] = "EUR", // greece
            //["IT"] = "EUR", // italy
            //["PL"] = "PLN", // poland
            //["RO"] = "RON", // romania
            //["RU"] = "RUB", // russia
            //["TR"] = "TRY", // turkey
            //["UA"] = "UAH", // ukraine
            //["UK"] = "GBP", // united kingdom
            ["US"] = "USD", // usa
        };
    }
}
