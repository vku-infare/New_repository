namespace AirTickets
{
    internal static class Urls
    {
        public static string Start = @"https://www.airtickets.com/";
        public static string DefaultBase = @"https://www.airtickets.com/results/search-query";
        public static string SearchBase = @"https://mule.ferryscanner.de/api/v1/search-async?";
        public static string Airport = @"api/v1/flights/autocomplete/airports";
        public static string PreFlights = @"results/search-query";
        public static string Flights = @"api/v1/flights/results"; // Same url as second PreFlights
    }
}
