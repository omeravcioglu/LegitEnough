namespace LegitEnough.Sim
{
    /// <summary>Name and place pools for generated customers. ASCII only, so every name is typeable.</summary>
    public static class NamePools
    {
        public static readonly string[] MaleGiven =
        {
            "Marcus", "Karim", "Tomasz", "Emre", "Farid", "Luca", "Dmitri", "Samuel", "Yusuf", "Andrei",
            "Hassan", "Mateo", "Jonas", "Kwame", "Rafael", "Omar", "Viktor", "Daniel", "Arash", "Kenji",
            "Mehmet", "Pavel", "Idris", "Bruno", "Nikolai", "Tariq", "Elias", "Joao", "Stefan", "Malik",
            "Ruben", "Anton", "Bilal", "Hugo", "Levan", "Darius", "Felix", "Amir", "Goran", "Theo",
            "Ibrahim", "Cyrus", "Lucien", "Adrian", "Sami", "Milan", "Oskar", "Reza", "Tobias", "Wendell",
        };

        public static readonly string[] FemaleGiven =
        {
            "Leila", "Aylin", "Hana", "Marta", "Nadia", "Sofia", "Amara", "Irina", "Yasmin", "Elena",
            "Zeynep", "Fatima", "Chloe", "Ana", "Mira", "Daria", "Selin", "Noor", "Camille", "Ines",
            "Olga", "Rania", "Lucia", "Ewa", "Shirin", "Adaeze", "Petra", "Lina", "Esra", "Greta",
            "Maryam", "Valentina", "Ayasha", "Jun", "Tamar", "Rosa", "Katya", "Salma", "Helene", "Nina",
            "Soraya", "Bianca", "Mei", "Ilse", "Farah", "Alma", "Vera", "Dilara", "Paulina", "Imani",
        };

        public static readonly string[] Surnames =
        {
            "Adeyemi", "Haddad", "Nowak", "Demir", "Karimi", "Rossi", "Volkov", "Mensah", "Petrovic", "Okafor",
            "Yilmaz", "Moreau", "Schmidt", "Kowalski", "Bakr", "Novak", "Ivanova", "Costa", "Tanaka", "Kim",
            "Aziz", "Lindqvist", "Ferreira", "Horvat", "Popescu", "Rahimi", "Celik", "Dubois", "Weber", "Esposito",
            "Mazur", "Nasser", "Oyelaran", "Brandt", "Sato", "Park", "Kaya", "Romano", "Lefevre", "Hoffmann",
            "Abboud", "Morales", "Bianchi", "Fischer", "Zielinski", "Toure", "Gashi", "Ozturk", "Varga", "Nkosi",
            "Mirza", "Laurent", "Krause", "Greco", "Sokolov", "Diallo", "Hosseini", "Arslan", "Castillo", "Wojcik",
        };

        public static readonly string[] BirthCities =
        {
            "LYON", "MARSEILLE", "ALGIERS", "TUNIS", "DAKAR", "ISTANBUL", "IZMIR", "ANKARA", "BERLIN", "HAMBURG",
            "KRAKOW", "LAGOS", "ACCRA", "CAIRO", "BEIRUT", "TEHRAN", "KABUL", "KARACHI", "MUMBAI", "DHAKA",
            "MANILA", "SEOUL", "OSAKA", "SAO PAULO", "BOGOTA", "LIMA", "KYIV", "TBILISI", "YEREVAN", "BAKU",
            "ODESSA", "BUCHAREST", "SOFIA", "TIRANA", "NAPLES", "PALERMO", "MANCHESTER", "GLASGOW", "CHICAGO", "DETROIT",
            "CASABLANCA", "AMMAN", "MINSK", "BELGRADE", "SARAJEVO", "PORTO", "VALENCIA", "ROTTERDAM", "LIEGE", "GDANSK",
        };

        /// <summary>Destination cities customers mention, by country id.</summary>
        public static string DestinationCity(string countryId, Rng rng)
        {
            string[] pool = countryId switch
            {
                Countries.France => new[] { "Lyon", "Marseille", "Paris", "Lille", "Toulouse" },
                Countries.Turkey => new[] { "Istanbul", "Izmir", "Ankara", "Antalya" },
                Countries.Germany => new[] { "Berlin", "Hamburg", "Munich", "Cologne" },
                Countries.Italy => new[] { "Milan", "Naples", "Rome", "Turin" },
                Countries.UnitedKingdom => new[] { "London", "Manchester", "Leeds" },
                Countries.UnitedStates => new[] { "Chicago", "New York", "Houston" },
                Countries.Japan => new[] { "Osaka", "Tokyo", "Nagoya" },
                Countries.SouthKorea => new[] { "Seoul", "Busan" },
                Countries.Emirates => new[] { "Dubai", "Abu Dhabi" },
                Countries.Iran => new[] { "Tehran", "Isfahan", "Tabriz" },
                _ => new[] { "the capital" },
            };
            return rng.Pick(pool);
        }
    }
}
