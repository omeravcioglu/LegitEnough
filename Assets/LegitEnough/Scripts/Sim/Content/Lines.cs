namespace LegitEnough.Sim
{
    /// <summary>
    /// Everything customers say. Lines are picked by personality and motivation so the same
    /// mechanics feel different from person to person (design doc §9, §10, §52).
    /// </summary>
    public static class Lines
    {
        static string Pick(Rng rng, params string[] options) => options[rng.Range(0, options.Length)];

        // ------------------------------------------------------------------ arrival & request

        public static string Greeting(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Friendly => Pick(rng, "Hi there! Are you... the passport person?", "Hello! Nice place. Very... cozy."),
            Trait.Nervous => Pick(rng, "Is this... the place? Somebody said you could help.", "Hi. Sorry. Is the door supposed to lock?"),
            Trait.Aggressive => Pick(rng, "You the one who does papers? Good. I'm in a hurry.", "Don't waste my time. You make passports?"),
            Trait.Quiet => Pick(rng, "...Hello.", "Hi."),
            Trait.Arrogant => Pick(rng, "I was told you're the best on this side of town. We'll see.", "So this is the famous shop. Smaller than I imagined."),
            Trait.Funny => Pick(rng, "Hi! I'd like one new identity, extra crispy.", "Is this where I sign up for the witness protection program? Kidding. Mostly."),
            Trait.Impatient => Pick(rng, "Hi, yes, hello, I need something fast.", "Okay. Hi. Let's do this quickly."),
            Trait.Cooperative => Pick(rng, "Good afternoon. Tell me what you need from me.", "Hello. I brought everything I could think of."),
            Trait.Suspicious => Pick(rng, "Nobody else is here, right?", "Before anything: who else knows about this place?"),
            Trait.Exhausted => Pick(rng, "Hey. Long week. Long year, actually.", "Hi. Sorry, I haven't slept."),
            Trait.Drunk => Pick(rng, "Heyyy. Is this the... the paper place?", "Hello! I'm totally fine. Hello."),
            Trait.Scared => Pick(rng, "Please. Nobody followed me. I need help.", "Can we... can we talk quietly?"),
            Trait.Wealthy => Pick(rng, "Good evening. Discretion is valued, I trust?", "My assistant found you. Let's be efficient."),
            Trait.Desperate => Pick(rng, "Please. You're my last option.", "I heard you can help people like me."),
            _ => "Hello.",
        };

        public static string Request(CustomerProfile p, JobRequest r, CountrySpec c, Rng rng)
        {
            string doc = r.DocType == DocType.Passport ? "passport" : "visa";
            return p.Primary switch
            {
                Trait.Wealthy => $"I require a {c.Adjective} {doc}. Properly done.",
                Trait.Quiet => $"{c.Adjective} {doc}.",
                Trait.Aggressive => $"{c.Adjective} {doc}. Make it good.",
                Trait.Funny => $"One {c.Adjective} {doc}, please. Hold the questions.",
                _ => Pick(rng, $"I need a {c.Adjective} {doc}.", $"I need to get to {c.Name}. A {c.Adjective} {doc}."),
            };
        }

        public static string Motivation(CustomerProfile p, JobRequest r, Rng rng)
        {
            string city = r.DestinationCity;
            if (p.Has(Trait.Quiet) && rng.Chance(0.6f)) return null;
            return p.Motivation switch
            {
                Sim.Motivation.FamilyReunion => Pick(rng, $"My wife and kids are in {city}. I haven't seen them in two years.", $"My mother is in {city}. She's sick. I have to get there."),
                Sim.Motivation.Employment => Pick(rng, $"There's a job waiting in {city}. Restaurant kitchen. Good money.", $"A cousin got me work in {city}. Construction."),
                Sim.Motivation.NewLife => Pick(rng, "I want to start over. Somewhere nobody knows me.", "New country, new life. That's the plan."),
                Sim.Motivation.EscapingDebt => Pick(rng, "I owe some people money. The wrong people.", "Let's say I need to be very far away by the end of the month."),
                Sim.Motivation.Relationship => Pick(rng, $"There's someone in {city}. We met online. It's real, I swear.", $"My fiance is in {city}. They won't give me a visa."),
                Sim.Motivation.Criminal => Pick(rng, "Business. The kind you don't ask about.", "Let's just say I have associates abroad."),
                Sim.Motivation.Business => Pick(rng, $"I have meetings in {city}. My own passport is... complicated.", "Investors. Contracts. Paperwork, you understand."),
                Sim.Motivation.Political => Pick(rng, "I wrote some things. Now I can't stay here.", "They know my name at the ministry. That's bad."),
                Sim.Motivation.PersonalSafety => Pick(rng, "There's a man who won't stop looking for me.", "I need to disappear. For my safety."),
                Sim.Motivation.RunningAway => Pick(rng, "I just need to leave. Today, if possible.", "Don't ask. Please."),
                Sim.Motivation.Mysterious => Pick(rng, "The reason is not important.", "Let's call it... a long holiday."),
                Sim.Motivation.RefusesToExplain => Pick(rng, "Why I need it is my business.", "No questions. That's the deal, right?"),
                _ => null,
            };
        }

        public static string Offer(CustomerProfile p, int amount, bool urgent, Rng rng)
        {
            string money = Labels.Money(amount);
            string urgency = urgent ? " And I need it TODAY." : "";
            return p.Primary switch
            {
                Trait.Wealthy => $"I'll pay {money}. That should be generous.{urgency}",
                Trait.Desperate => $"I have {money}. It's everything I have.{urgency}",
                Trait.Aggressive => $"{money}. Take it.{urgency}",
                Trait.Funny => $"I can offer {money} and my eternal gratitude.{urgency}",
                Trait.Drunk => $"I got... {money}. Somewhere. Yes.{urgency}",
                _ => Pick(rng, $"I can pay {money}.{urgency}", $"Here's what I've got: {money}.{urgency}"),
            };
        }

        public static string Accepted(CustomerProfile p, int amount, Rng rng) =>
            p.Has(Trait.Arrogant) ? $"{Labels.Money(amount)}. Fine." : Pick(rng, $"{Labels.Money(amount)}. Deal.", "Okay. Deal.", "Alright. Thank you.");

        public static string Countered(CustomerProfile p, int amount, Rng rng) => p.Primary switch
        {
            Trait.Aggressive => $"{Labels.Money(amount)}. That's it. Don't push me.",
            Trait.Desperate => $"Please... {Labels.Money(amount)}. That's really all I can do.",
            Trait.Wealthy => $"Let's not be vulgar. {Labels.Money(amount)}.",
            _ => Pick(rng, $"{Labels.Money(amount)}. That's my final offer.", $"I can do {Labels.Money(amount)}. Final."),
        };

        public static string WalkedOut(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Aggressive => "Forget it. Thief.",
            Trait.Arrogant => "I've wasted enough time here.",
            Trait.Desperate => "I... I can't. I'm sorry.",
            _ => Pick(rng, "No. That's too much. Forget it.", "I'll find someone else."),
        };

        public static string Declined(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Desperate => "Please... okay. Okay.",
            Trait.Aggressive => "Your loss.",
            Trait.Funny => "Rejected by a forger. New low.",
            _ => Pick(rng, "Oh. Okay.", "Fine. I'll go somewhere else."),
        };

        public static string Handover(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Cooperative => "I wrote everything down. Neatly, I hope.",
            Trait.Drunk => "Here's my... stuff. Some of it.",
            Trait.Suspicious => "Here. Burn this when you're done.",
            _ => Pick(rng, "Everything you need is in here.", "Here. My details.", "It's all on the sheet."),
        };

        // ------------------------------------------------------------------ photo

        public static string SitDown(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Drunk) ? "Ooh, a little chair." : Pick(rng, "Where do you want me?", "Like this?", "Do I smile?");

        public static string AfterPhoto(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Impatient) ? "Finally. How long now?" : Pick(rng, "So... how long does this take?", "Is that it?");

        public static string WillWait(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Nervous) ? "I'll wait. Over there. Away from the window." : Pick(rng, "I'll wait over there.", "I'll sit. Take your time. Not too much time.");

        public static string WillReturn(CustomerProfile p, GameTime at, int today, Rng rng) =>
            $"I'll come back {Calendar.Relative(at.Day, today).ToLowerInvariant()} at {at.Clock}.";

        public static string LookAck(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Drunk => "Like thish?",
            Trait.Arrogant => "Obviously.",
            Trait.Funny => "Smoldering. Got it.",
            _ => Pick(rng, "Like this?", "Okay.", "Sorry."),
        };

        public static string LookRefuse(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Aggressive) ? "I AM looking!" : Pick(rng, "What?", "Which camera?", "Huh?");

        public static string FaceAck(CustomerProfile p, Expression was, Rng rng)
        {
            if (was == Expression.Smile || was == Expression.Grin)
                return p.Has(Trait.Funny) ? "This IS my serious face." : Pick(rng, "Sorry! I always do that.", "Right. No smiling. Got it.");
            if (was == Expression.Angry) return "...Fine.";
            if (was == Expression.Nervous) return "Relaxed. I'm relaxed.";
            return "Okay.";
        }

        public static string FaceRefuse(CustomerProfile p, Rng rng) => Pick(rng, "I'm trying!", "My face does that.");

        public static string ExpressionRelapse(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Funny) ? Pick(rng, "Sorry. Thought of something funny.", "Heh. Sorry.") : Pick(rng, "Oops.", "Sorry, sorry.", null);

        public static string GlanceExcuse(CustomerProfile p, Rng rng) => Pick(rng, "Did you hear that?", "Is someone outside?", "Sorry.");

        public static string Fidget(CustomerProfile p, Rng rng) => Pick(rng, "Are we done?", "My back hurts.", "Is it taking a picture right now?");

        public static string Manhandled(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Aggressive) ? "Hands off." : Pick(rng, "Ow.", "Careful with the neck!", "Gently, please.");

        public static string GlassesOff(CustomerProfile p, Rng rng) => Pick(rng, "I can't see a thing without them.", "Okay. You're very blurry now.");

        public static string PhotoImpatient(CustomerProfile p, int level, Rng rng) => level <= 1
            ? Pick(rng, "Are we done yet?", "How many pictures do you need?")
            : Pick(rng, "Seriously. Just take the picture.", "I'm getting a headache.");

        // ------------------------------------------------------------------ waiting

        public static string WaitingComplaint(CustomerProfile p, int level, Rng rng) => level <= 1
            ? Pick(rng, "How much longer?", "Is it almost ready?")
            : p.Has(Trait.Aggressive) ? "I've been sitting here forever." : Pick(rng, "I can't wait much longer.", "Please, I need to go soon.");

        public static string LeavesAngry(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Aggressive => "Forget it. I'm telling everyone about this place.",
            Trait.Desperate => "I can't wait anymore. I'm sorry.",
            _ => Pick(rng, "Forget it. I'm going somewhere else.", "This is taking too long. I'm leaving."),
        };

        public static string ClosingTime(CustomerProfile p, Rng rng) => Pick(rng, "You're closing? I'll come back tomorrow.", "Tomorrow, then. First thing.");

        // ------------------------------------------------------------------ pickup & delivery

        public static string PickupGreeting(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Impatient) ? "I'm here. Is it ready?" : Pick(rng, "Hi again. I'm here for my passport.", "It's me. Is it ready?");

        public static string NotReady(CustomerProfile p, Rng rng) =>
            p.Has(Trait.Aggressive) ? "It's not ready?! We agreed!" : Pick(rng, "It's not ready?", "Oh. I thought it would be done.");

        public static string Inspecting(CustomerProfile p, Rng rng) => Pick(rng, "Let me see...", "Hmm.", "Okay, let's see it.");

        public static string Complaint(CustomerProfile p, Finding f, CountrySpec docCountry, Rng rng)
        {
            switch (f.Issue)
            {
                case Issue.WrongPerson: return "That's... not me. Who is this?";
                case Issue.NoPhoto: return "There's no photo in it!";
                case Issue.WrongCountry: return $"Why does this say {docCountry?.Name.ToUpperInvariant() ?? "THAT"}?";
                case Issue.EyesClosed: return Pick(rng, "My eyes are closed in this!", "I look dead in this photo.");
                case Issue.HeadCutOff: return "Where's the top of my head?";
                case Issue.Blurry: return "Why is the photo blurry?";
                case Issue.Smudged: return "There's ink all over it.";
                case Issue.LookingAway: return "I'm looking at the wall in this.";
                case Issue.WrongExpression: return "I look... weird in this.";
                case Issue.WrongBackdrop: return "Is the background supposed to be that color?";
                case Issue.WrongCover: return "Shouldn't the cover be a different color?";
                case Issue.MissingField: return "Some of this is blank.";
                case Issue.Misspelled:
                case Issue.WrongValue:
                    if (f.Field == DocField.Surname || f.Field == DocField.GivenNames)
                        return Pick(rng, "That's not how you spell my name.", "That's not my name.");
                    if (f.Field == DocField.DateOfBirth) return "That's not my birthday.";
                    if (f.Field == DocField.PlaceOfBirth) return "I wasn't born there.";
                    return "Some of this is wrong.";
                default: return "Something about this looks off.";
            }
        }

        public static string Satisfied(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Arrogant => "Acceptable.",
            Trait.Funny => "Wow. I look like a real person.",
            Trait.Drunk => "Thass me! Hello me!",
            Trait.Suspicious => "...It'll do.",
            _ => Pick(rng, "Looks... real. Looks very real.", "This is perfect.", "Wow. Okay."),
        };

        public static string Persuaded(CustomerProfile p, Rng rng) => Pick(rng, "...Fine. I hope you're right.", "If you say so.");

        public static string NotPersuaded(CustomerProfile p, Rng rng) => Pick(rng, "No. Fix it.", "I'm not paying for this.");

        public static string Pay(CustomerProfile p, int amount, bool reduced, Rng rng) => reduced
            ? $"{Labels.Money(amount)}. That's what it's worth."
            : p.Has(Trait.Wealthy) ? "As agreed. Keep the change." : Pick(rng, "Here. As agreed.", "Here's the money.");

        public static string Farewell(CustomerProfile p, Rng rng) => p.Primary switch
        {
            Trait.Friendly => "Thank you! Really. Thank you.",
            Trait.Nervous => "If anyone asks, I was never here.",
            Trait.Aggressive => "We're done here.",
            Trait.Quiet => "...Thanks.",
            Trait.Arrogant => "Try to keep the standard up.",
            Trait.Funny => "If this works, I'm naming my next cat after you.",
            Trait.Impatient => "Bye.",
            Trait.Cooperative => "Thank you for everything.",
            Trait.Suspicious => "Forget my face.",
            Trait.Exhausted => "Finally. Sleep.",
            Trait.Drunk => "You're my best friend.",
            Trait.Scared => "Thank you. I won't forget this.",
            Trait.Wealthy => "You may hear from my associates.",
            Trait.Desperate => "You saved my life. I mean it.",
            _ => "Thanks.",
        };

        // ------------------------------------------------------------------ player lines

        public static class Player
        {
            public static string Accept(int amount) => $"Deal. {Labels.Money(amount)}.";
            public static string Ask(int amount) => $"Make it {Labels.Money(amount)}.";
            public static string AskBig(int amount) => $"{Labels.Money(amount)}. Take it or leave it.";
            public const string Decline = "Can't help you.";
            public static string DontDo(string country) => $"I don't do {country}. Not yet.";
            public static string NotTaking(string country) => $"Not taking {country} jobs right now.";
            public static string AcceptFinal(int amount) => $"Fine. {Labels.Money(amount)}.";
            public const string NoDeal = "No deal.";
            public const string TakePhoto = "Let's take your photo.";
            public const string WaitBench = "Have a seat. I'll call you.";
            public const string BackToCounter = "Come back to the counter.";
            public const string FixIt = "I'll fix it.";
            public const string TrustMe = "It's fine. Trust me.";
            public const string GetOut = "Then get out.";
            public const string GiveMeAnHour = "Give me an hour.";
            public const string ComeBackTomorrow = "Come back tomorrow.";
            public const string CantDoIt = "I can't do it. Sorry.";
            public const string AlmostDone = "Almost done.";
        }
    }
}
