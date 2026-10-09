namespace LegitEnough.Sim
{
    /// <summary>
    /// Text for delayed consequences (design doc §38, §39). Outcome messages are written by
    /// the customer, so motivation and personality both shape them.
    /// </summary>
    public static class MessageTemplates
    {
        static string Pick(Rng rng, params string[] options) => options[rng.Range(0, options.Length)];

        public static string Subject(OutcomeKind kind, Rng rng) => kind switch
        {
            OutcomeKind.Success => Pick(rng, "it worked", "thank you", "made it", "(no subject)"),
            OutcomeKind.Failed => Pick(rng, "border", "you", "why", "(no subject)"),
            OutcomeKind.Missing => Pick(rng, "my brother", "have you seen", "please"),
            _ => "(no subject)",
        };

        public static string Success(CustomerProfile p, string city, Rng rng)
        {
            if (p.Has(Trait.Quiet) && rng.Chance(0.5f)) return "Thank you.";
            if (p.Primary == Trait.Funny && rng.Chance(0.6f))
                return Pick(rng, "Customs officer said I look younger in my photo. You're a genius.",
                    "Made it. Already ate three croissants. Unrelated, but thank you.");

            switch (p.Motivation)
            {
                case Motivation.FamilyReunion:
                    return Pick(rng, "It worked. I'm with my family. Thank you.",
                        "My daughter didn't recognize me at first. Then she did. Thank you, whoever you are.",
                        $"We are all together in {city}. I will never forget what you did.");
                case Motivation.Employment:
                    return Pick(rng, "Started the job on Monday. The boss doesn't ask questions. Thank you.",
                        "First paycheck. Sending money home this week. You did good work.");
                case Motivation.NewLife:
                    return Pick(rng, "New city, new apartment, new me. The passport was perfect.",
                        "I walked right through. Nobody looked twice. I can breathe again.");
                case Motivation.EscapingDebt:
                    return Pick(rng, "They won't find me here. Thank you.", "Made it. The people I owed can keep looking.");
                case Motivation.Relationship:
                    return Pick(rng, $"I made it to {city}. She said yes!! Thank you!!", "We're together now. Don't tell anyone, ok? :)");
                case Motivation.Criminal:
                    return Pick(rng, "Smooth. My associates were impressed. Expect some friends.", "Business went well. You'll hear from us again.");
                case Motivation.Business:
                    return Pick(rng, $"Meetings in {city} went well. Your work was adequate. I'll recommend you.", "Deal closed. Your document held up perfectly.");
                case Motivation.Political:
                    return Pick(rng, "I'm safe. I can write again. Thank you.", "Crossed last night. They can't touch me here.");
                case Motivation.PersonalSafety:
                    return Pick(rng, "I'm safe now. For the first time in a long time I slept through the night.", "He doesn't know where I am. Thank you. Really.");
                case Motivation.RunningAway:
                    return Pick(rng, "Made it. Don't ask where. Thanks.", "Gone. Thanks for the help.");
                case Motivation.Mysterious:
                    return Pick(rng, "Received. Satisfactory.", "The package has arrived safely. You understand.");
                default:
                    return Pick(rng, "Done. Thanks.", "It worked.");
            }
        }

        public static string Failure(CustomerProfile p, string city, int price, Rng rng)
        {
            if (p.Has(Trait.Quiet) && rng.Chance(0.5f)) return "It didn't work.";
            if (p.Primary == Trait.Aggressive && rng.Chance(0.6f)) return "You screwed me. I lost everything.";
            if (p.Primary == Trait.Funny && rng.Chance(0.5f))
                return "Fun fact: border holding cells have terrible wifi. Thanks for that.";

            switch (p.Motivation)
            {
                case Motivation.FamilyReunion:
                    return Pick(rng, "They stopped me at the airport. I never even saw the plane. Why did you do this to me?",
                        "I was so close. They took me into a room and asked about the passport. I lost everything.");
                case Motivation.Employment:
                    return Pick(rng, "They flagged the passport at the border. I lost the job and the money. You screwed me.",
                        $"Deported. Back where I started, minus {Labels.Money(price)}.");
                case Motivation.NewLife:
                    return "The officer looked at the photo for a long time. Then he called his supervisor. Thanks for nothing.";
                case Motivation.EscapingDebt:
                    return "They caught me at the border. Now I owe even more. This is on you.";
                case Motivation.Relationship:
                    return $"She waited at arrivals in {city} for six hours. I was in a holding cell. Great job.";
                case Motivation.Criminal:
                    return "Your work got two of my people detained. We will talk about this.";
                case Motivation.Business:
                    return "Disastrous. My associates were questioned for hours. Never again.";
                case Motivation.Political:
                    return "They arrested me at the checkpoint. Writing this from a borrowed phone. Please don't do this to anyone else.";
                case Motivation.PersonalSafety:
                    return "They sent me back. He knows where I am now.";
                case Motivation.RunningAway:
                    return "Caught. Thanks a lot.";
                case Motivation.Mysterious:
                    return "Unsatisfactory.";
                default:
                    return "Didn't work. You know what you did.";
            }
        }

        public static string Missing(CustomerProfile p, Rng rng) =>
            Pick(rng,
                $"{p.FirstName} came to your shop a few weeks ago. Nobody has heard from {(p.Identity.Sex == Sex.Male ? "him" : "her")} since. Do you know anything?",
                $"I'm looking for {p.FirstName}. Your number was in {(p.Identity.Sex == Sex.Male ? "his" : "her")} coat pocket. Please call me.");

        public static string MissingSender(CustomerProfile p, Rng rng) => Pick(rng, "Unknown number", $"Friend of {p.FirstName}", "(no name)");

        public const string WelcomeFrom = "R.";
        public const string WelcomeSubject = "the shop";
        public const string WelcomeBody =
            "Keys are under the mat. The rent is due every night, the printer jams, the camera lags. " +
            "Everything you need is in the HANDBOOK. Read it. Then read it again.\n\n" +
            "Photograph them, make the document, print it, bind it, hand it over, get paid. " +
            "Nobody needs it perfect. It needs to be legit enough.\n\nDon't get greedy. — R.";
    }
}
