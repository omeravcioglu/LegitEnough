namespace LegitEnough.Sim
{
    public enum Backdrop { White, LightGray, Blue, Red }

    public enum Expression { Neutral, Smile, Grin, Angry, Nervous, Tired, Surprised }

    public enum DocType { Passport, Visa }

    public enum DocCategory { TypeA, TypeB, TypeC, TypeD }

    public enum CoverColor { DarkRed, Navy, Black, Green, Brown }

    public enum DateFormat
    {
        /// <summary>14.03.1987</summary>
        DmyDots,
        /// <summary>14/03/1987</summary>
        DmySlashes,
        /// <summary>1987-03-14</summary>
        YmdDashes,
        /// <summary>03/14/1987</summary>
        MdySlashes,
        /// <summary>1987.03.14</summary>
        YmdDots,
        /// <summary>14 MAR 1987</summary>
        DMonY,
    }

    public enum Sex { Male, Female }

    /// <summary>Fields that can appear on a data page.</summary>
    public enum DocField
    {
        Surname,
        GivenNames,
        Sex,
        DateOfBirth,
        PlaceOfBirth,
        Height,
        EyeColor,
        DateOfIssue,
        DateOfExpiry,
        DocumentNumber,
    }

    public enum EyeColor { Brown, DarkBrown, Hazel, Green, Blue, Gray }

    public enum Trait
    {
        Friendly,
        Nervous,
        Aggressive,
        Quiet,
        Arrogant,
        Funny,
        Impatient,
        Cooperative,
        Suspicious,
        Exhausted,
        Drunk,
        Scared,
        Wealthy,
        Desperate,
    }

    public enum Motivation
    {
        FamilyReunion,
        Employment,
        NewLife,
        EscapingDebt,
        Relationship,
        Criminal,
        Business,
        Political,
        PersonalSafety,
        RunningAway,
        Mysterious,
        RefusesToExplain,
    }

    public enum OutcomeKind { Pending, Success, Failed, Unknown, Missing }
}
