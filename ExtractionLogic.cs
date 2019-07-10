namespace AirTickets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using ContentCollectorInterface;
    using DCT_Common_Functionality;
    using Infare.DataCollection.Common;
    using Infare.DataCollection.Common.TripFareObservationOM1;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ExtractionLogic
    {
        public InfareStandardFunctions ISF;

        private const string _UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/71.0.3578.80 Safari/537.36";
        private const int MaxDatasaves = 1000;

        private AirFareObservation _afo;

        private RobotInfo robotInfo;
        private SearchCriteria _sc;
        private Sector _outbound;
        private Sector _inbound;
        private string[] _outboundBookingClass;

        public ExtractionLogic(RobotInfo robotInfo)
        {
            this.robotInfo = robotInfo;

            this.ISF = InfareStandardFunctions.Instance;

            this._afo = new AirFareObservation(
                ISF.LogWriter,
                new BookingSiteInfo(robotInfo.BookingSiteId, "L¤"),
                robotInfo.Method);
        }

        public void CollectData(SearchCriteria searchCriteria)
        {
            _sc = searchCriteria;
            _afo.SearchCriteria = searchCriteria;
            _outbound = _afo.TripFare.Outbound;
            _inbound = _afo.TripFare.Inbound;
            Dictionaries.CheapestCarrier.Clear();

            int datasaveCounter = 0;

            Dictionaries.Saved.Clear();

            string flights = LoadFlightsSource();
            if (string.IsNullOrEmpty(flights))
            {
                return;
            }

            JArray allFlights = new JArray { };

            string[] jsonAreas = ISF.Regex.RegexMatchesToSingleArray(flights, Regexes.ExtractJsonAreas);

            foreach (string jsonArea in jsonAreas)
            {
                JToken jFlights = ParseLoadedSource(jsonArea);

                if (jFlights == null)
                {
                    continue;
                }

                JArray flightsArray = (JArray)jFlights.SelectToken("data") ?? new JArray { };

                if (!flightsArray.Any())
                {
                    continue;
                }

                allFlights.Merge(flightsArray);
            }

            if (allFlights.Any())
            {
                JArray sortedFlights = new JArray(allFlights.OrderBy(obj => ISF.Price.CPrice(obj["passengers"]["adults"]["price"]["total"]?.ToString())));

                foreach (JObject jsonFlight in sortedFlights)
                {
                    _afo.TripFare.ResetAllValues();
                    JToken[] outHomeLegs = jsonFlight.SelectToken("legs")?.ToArray();

                    if (outHomeLegs == null || outHomeLegs.Length < 1 || !TryGetPrices(jsonFlight) || !TrySetSectorData(outHomeLegs[0], _outbound))
                    {
                        continue;
                    }

                    List<string> outboundCarriers = _afo.TripFare.Outbound.Legs.Select(leg => leg.Carrier).ToList();

                    if (!ISF.DataValidation.AreCarriersValid(outboundCarriers, _sc))
                    {
                        continue;
                    }

                    if (_afo.TripType.Equals(TripType.RT))
                    {
                        if (outHomeLegs.Length < 2 || !TrySetSectorData(outHomeLegs[1], _inbound))
                        {
                            continue;
                        }

                        var outboundRoutes = _outbound.Legs.Select(x => new OriginDestination(x.Origin, x.Destination)).ToList();
                        var inboundRoutes = _inbound.Legs.Select(x => new OriginDestination(x.Origin, x.Destination)).ToList();
                        if (!ISF.DataValidation.AreRoutesValid(outboundRoutes, inboundRoutes, _sc))
                        {
                            continue;
                        }

                        int.TryParse(_outbound.Legs.Last().ArrivalDateTimeBpt, out int outArrivalBpt);
                        int.TryParse(_inbound.Legs.First().DepartureDateTimeBpt, out int inDepartureBpt);
                        if (outArrivalBpt == 0 || outArrivalBpt > inDepartureBpt)
                        {
                            continue;
                        }
                    }

                    if (!ValidateCxr())
                    {
                        continue;
                    }

                    if (!TrySaveData())
                    {
                        continue;
                    }

                    datasaveCounter++;
                    if (datasaveCounter > MaxDatasaves)
                    {
                        continue;
                    }
                }
            }
        }

        private string LoadFlightsSource()
        {
            string sourceFlights = string.Empty;

            if (!Dictionaries.Cabin.TryGetValue(_sc.Cabin, out string cabinTypeForSearch))
            {
                cabinTypeForSearch = "Y";
            }

            string postParams = "{\"dep\":null,\"arr\":null,\"obDate\":null,\"ibDate\":null,\"isRoundtrip\":null,\"passengersAdult\":\"1\",\"passengersChild\":\"0\",\"passengersInfant\":\"0\",\"directFlightsOnly\":\"0\",\"extendedDates\":\"0\",\"highlightID\":null,\"resultIdForHighlighting\":null}";

            JObject jsonForPost = JObject.Parse(postParams);

            jsonForPost["dep"] = _sc.Origin;
            jsonForPost["arr"] = _sc.Destination;
            jsonForPost["obDate"] = _sc.OutboundDate.ToString("yyyy-MM-dd");
            jsonForPost["ibDate"] = _sc.InboundDate.ToString("yyyy-MM-dd");
            jsonForPost["isRoundtrip"] = robotInfo.Method == TripType.RT ? "1" : "0";
            jsonForPost["class"] = cabinTypeForSearch;

            postParams = JsonConvert.SerializeObject(jsonForPost);

            ISF.Http.SetRequestHeader("Accept", "application/json, text/javascript, */*; q=0.01");
            ISF.Http.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            ISF.Http.SetRequestHeader("Host", Urls.DefaultBase);
            ISF.Http.SetRequestHeader("Referer", $"{Urls.Start}results");
            ISF.Http.SetRequestHeader("User-Agent", _UserAgent);
            ISF.Http.SetRequestHeader("X-Requested-With", "XMLHttpRequest");
            ISF.Http.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
            ISF.Http.SetRequestHeader("Origin", Urls.Start);
            ISF.Http.SetRequestHeader("Accept-Language", "da-DK,da;q=0.9,en-US;q=0.8,en;q=0.7");

            string sourcePreFlights = ISF.Http.ProxyFetch(FetchMethod.POST, Urls.Start + Urls.PreFlights, postParams, timeOutInSeconds: 180);

            if (!sourcePreFlights.Contains("OK"))
            {
                return null;
            }

            List<string> queryList = new List<string> { };

            queryList.Add("routes[0].departure=" + _sc.Origin);
            queryList.Add("routes[0].arrival=" + _sc.Destination);
            queryList.Add("routes[0].datetime=" + _sc.OutboundDate.ToString("yyyy-MM-dd") + "T00:00:00");

            if (_afo.TripType.Equals(TripType.RT))
            {
                queryList.Add("routes[1].departure=" + _sc.Destination);
                queryList.Add("routes[1].arrival=" + _sc.Origin);
                queryList.Add("routes[1].datetime=" + _sc.InboundDate.ToString("yyyy-MM-dd") + "T00:00:00");
            }

            queryList.Add("passengers[0].type=" + "adults");
            queryList.Add("passengers[0].count=" + "1");
            queryList.Add("passengers[1].type=children");
            queryList.Add("passengers[1].count=0");
            queryList.Add("passengers[2].type=infants");
            queryList.Add("passengers[2].count=0");
            queryList.Add("cabinClass=" + Dictionaries.Cabin[_sc.Cabin]);
            queryList.Add("carrier=");
            queryList.Add("directRoutes=" + (_sc.AllowedConnections.Any() ? "false" : "true"));
            queryList.Add("flexibleDates=false");
            queryList.Add("locale=" + (Dictionaries.Locales.ContainsKey(_sc.PointOfSale) ? Dictionaries.Locales[_sc.PointOfSale] : "el_GR"));
            queryList.Add("currency=" + (Dictionaries.Currencies.ContainsKey(_sc.PointOfSale) ? Dictionaries.Currencies[_sc.PointOfSale] : "EUR"));
            queryList.Add("market=" + (Dictionaries.Locales.ContainsKey(_sc.PointOfSale) ? _sc.PointOfSale.ToLower() : "gr"));
            queryList.Add("cache=true");

            string getParams = WebUtility.UrlEncode(string.Join("&", queryList.ToArray())).Replace("%3D", "=").Replace("%26", "&");

            for (int i = 0; i < 4; i++)
            {
                Thread.Sleep(4000);

                ISF.Http.SetRequestHeader("Host", "mule.ferryscanner.de");
                ISF.Http.SetRequestHeader("User-Agent", _UserAgent);
                ISF.Http.SetRequestHeader("Accept", "text/event-stream");
                ISF.Http.SetRequestHeader("Accept-Language", "da-DK,da;q=0.9,en-US;q=0.8,en;q=0.7");
                ISF.Http.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
                ISF.Http.SetRequestHeader("Origin", "https://www.airtickets.com");
                ISF.Http.SetRequestHeader("Connection", "keep-alive");
                ISF.Http.SetRequestHeader("Pragma", "no-cache");
                ISF.Http.SetRequestHeader("Cache-Control", "no-cache");
                ISF.Http.SetRequestHeader("Referer", $"{Urls.Start}results");

                sourceFlights = ISF.Http.ProxyFetch(FetchMethod.GET, Urls.SearchBase, getParams, encodeQuery: false, encodePostData: false, timeOutInSeconds: 180);

                if (!sourceFlights.Contains("Too Many Requests"))
                {
                    break;
                }
            }

            if (!sourceFlights.Contains(@"""data"":"))
            {
                return null;
            }

            return sourceFlights;
        }

        private bool TryGetPrices(JToken jsonFlight)
        {
            string rawBasePrice = jsonFlight.SelectToken("passengers.adults.price.faceValue")?.ToString();
            string rawTax = jsonFlight.SelectToken("passengers.adults.price.tax")?.ToString();
            string currency = jsonFlight.SelectToken("passengers.adults.price.currency")?.ToString();

            if (string.IsNullOrEmpty(rawBasePrice) || string.IsNullOrEmpty(rawTax) || string.IsNullOrEmpty(currency))
            {
                return false;
            }

            decimal? basePrice = ISF.Price.CPrice(rawBasePrice, ",");
            decimal? tax = ISF.Price.CPrice(rawTax, ",");

            _afo.TripFare.IsTaxIncluded = false;
            _afo.TripFare.Price = basePrice;
            _afo.TripFare.OriginalPrice = rawBasePrice;
            _afo.TripFare.Tax = tax;
            _afo.TripFare.OriginalTax = rawTax;

            _afo.TripFare.Currency = currency;

            return true;
        }

        private bool TrySetSectorData(JToken flight, Sector sector)
        {
            sector.ResetValues();

            List<JToken> legs = flight.SelectToken("segments")?.ToList();

            if (legs == null || legs.Count == 0)
            {
                return false;
            }

            foreach (JToken leg in legs)
            {
                string departureDateTime = leg.SelectToken("route.departure.datetime")?.ToString();
                string arrivalDateTime = leg.SelectToken("route.arrival.datetime")?.ToString();

                sector.Legs.Add(new Leg
                {
                    Origin = leg.SelectToken("route.departure.location")?.ToString(),
                    Destination = leg.SelectToken("route.arrival.location")?.ToString(),
                    Carrier = leg.SelectToken("transport.carriers.operating")?.ToString(),
                    FlightNumber = leg.SelectToken("transport.carriers.operating")?.ToString() + leg.SelectToken("transport.number")?.ToString(),
                    BookingClass = leg.SelectToken("services.bookingClass")?.ToString(),
                    FareFamily = Dictionaries.CabinClass.TryGetValue(leg.SelectToken("services.cabinClass")?.ToString(), out string fareFamily) ? fareFamily : string.Empty,
                    DepartureDateTime = departureDateTime,
                    DepartureDateTimeBpt = BptMethods.StringToBpt(departureDateTime, "dd-MM-yyyy HH:mm:ss").ToString(),
                    ArrivalDateTime = arrivalDateTime,
                    ArrivalDateTimeBpt = BptMethods.StringToBpt(arrivalDateTime, "dd-MM-yyyy HH:mm:ss").ToString(),
                });
            }

            // Flights with train/bus carriers are skipped. Approved by PH 20-08-2018. Done by ANI 20-08-2018.
            if (sector.Legs.Where(w => "04,07,10,11".Contains(w.Carrier)).Any())
            {
                return false;
            }

            List<OriginDestination> originDestinations = sector.Legs.Select(p => new OriginDestination(p.Origin, p.Destination)).ToList();
            return sector.TrySetValues() && ISF.DataValidation.AreConnectionsValid(originDestinations, _sc, _sc.WildCards.Contains("2cnx"));
        }

        private string GenerateCacheKey()
        {
            if (string.IsNullOrEmpty(_outbound.DepartureDateTime) || string.IsNullOrEmpty(_outbound.ArrivalDateTime) || string.IsNullOrEmpty(_outbound.FlightNumber))
            {
                return null;
            }

            string cacheKey = $"{_outbound.RouteForDic}|{_outbound.FlightNumber}|{_outbound.DepartureDateTime}|{_outbound.ArrivalDateTime}";

            if (robotInfo.Method == TripType.RT)
            {
                if (string.IsNullOrEmpty(_inbound.DepartureDateTime) || string.IsNullOrEmpty(_inbound.ArrivalDateTime) || string.IsNullOrEmpty(_inbound.FlightNumber))
                {
                    return null;
                }

                cacheKey += $"|{_inbound.RouteForDic}|{_inbound.FlightNumber}|{_inbound.DepartureDateTime}|{_inbound.ArrivalDateTime}|";
            }

            return cacheKey;
        }

        private void RemoveBookingClass()
        {
            _outboundBookingClass = (from leg in _outbound.Legs select leg.BookingClass).ToArray();

            foreach (Leg leg in _outbound.Legs)
            {
                leg.BookingClass = null;
            }

            _outbound.TrySetValues();

            if (robotInfo.Method == TripType.RT)
            {
                foreach (Leg leg in _inbound.Legs)
                {
                    leg.BookingClass = null;
                }

                _inbound.TrySetValues();
            }
        }

        private void RestoreOutboundBookingClass()
        {
            for (int i = 0; i < _outboundBookingClass.Length; i++)
            {
                _outbound.Legs[i].BookingClass = _outboundBookingClass[i];
            }

            _outbound.TrySetValues();
        }

        private bool TrySaveData()
        {
            string cacheForDatasave = GenerateCacheKey();

            if (cacheForDatasave == null || Dictionaries.Saved.ContainsKey(cacheForDatasave))
            {
                return false;
            }

            bool hasMissingBookingClass = false;
            if (robotInfo.Method == TripType.RT)
            {
                hasMissingBookingClass = _outbound.Legs.Exists(leg => (string.IsNullOrEmpty(leg.BookingClass) || leg.BookingClass.Length > 1)) ^ _inbound.Legs.Exists(leg => (string.IsNullOrEmpty(leg.BookingClass) || leg.BookingClass.Length > 1));
            }
            else
            {
                hasMissingBookingClass = _outbound.Legs.Exists(leg => leg.BookingClass.Length > 1);
            }

            if (hasMissingBookingClass)
            {
                RemoveBookingClass();
            }

            ConstructedFareBasis fb = ISF.FareBasis.ConstructFareBasis(_afo, true);

            _outbound.FareBasis = fb.OutboundFareBasis;

            if (robotInfo.Method == TripType.RT)
            {
                _inbound.FareBasis = fb.InboundFareBasis;
            }

            if (!_afo.TripFare.TrySetValues())
            {
                return false;
            }

            Dictionaries.Saved.Add(cacheForDatasave, _afo.TripFare.Price.ToCString());

            ISF.DataStore.Store(_afo);

            if (hasMissingBookingClass)
            {
                RestoreOutboundBookingClass();
            }

            return true;
        }

        private JObject ParseLoadedSource(string source)
        {
            JObject loadedJson;
            try
            {
                loadedJson = JObject.Parse(source);
            }
            catch (JsonReaderException)
            {
                return null;
            }

            return loadedJson;
        }

        private bool ValidateCxr()
        {
            if (!_sc.WildCards.Contains("CXR", StringComparer.InvariantCultureIgnoreCase))
            {
                return true;
            }

            string outboundCarrier = string.Empty;
            string inboundCarrier = string.Empty;
            string cxrKey = string.Empty;

            //If list contains more than 1 distinct carrier, outbound = MIX
            outboundCarrier = _outbound.Legs.Select(x => x.Carrier).Distinct().Count() > 1 ? "MIX" : _outbound.Legs.First().Carrier;

            if (outboundCarrier != "MIX" && !string.IsNullOrEmpty(_outbound.Connection))
            {
                outboundCarrier += "XXX";
            }

            cxrKey = outboundCarrier;

            if (_afo.TripType.Equals(TripType.RT) && outboundCarrier != "MIX")
            {
                inboundCarrier = _inbound.Legs.Select(x => x.Carrier).Distinct().Count() > 1 ? "MIX" : _inbound.Legs.First().Carrier;
                /**
                 1. If Inbound not MIX
                 2. IF inbound has CNX
                 3. IF inbound carrier == outbound carrier
                 */
                if (inboundCarrier != "MIX" &&
                    !string.IsNullOrEmpty(_inbound.Connection) &&
                    inboundCarrier.Equals(outboundCarrier.Substring(0, 2)))
                {
                    inboundCarrier += "XXX";
                }
                else if (!inboundCarrier.Equals(outboundCarrier.Substring(0, 2)))
                {
                    inboundCarrier = "MIX";
                }

                cxrKey += "#" + inboundCarrier;
            }

            if (Dictionaries.CheapestCarrier.ContainsKey(cxrKey))
            {
                return false;
            }

            Dictionaries.CheapestCarrier[cxrKey] = "SAVED";

            return true;
        }
    }
}
