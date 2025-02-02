namespace PKHeX.Core;

/// <summary>
/// Encounter Slot found in <see cref="GameVersion.XY"/>.
/// </summary>
public sealed record EncounterSlot6XY(EncounterArea6XY Parent, ushort Species, byte Form, byte LevelMin, byte LevelMax)
    : IEncounterable, IEncounterMatch, IEncounterConvertible<PK6>, IEncounterFormRandom, IFlawlessIVCount
{
    public int Generation => 6;
    public EntityContext Context => EntityContext.Gen6;
    public bool EggEncounter => false;
    public Ball FixedBall => Ball.None;
    public Shiny Shiny => Shiny.Random;
    public bool IsShiny => false;
    public int EggLocation => 0;
    public bool IsRandomUnspecificForm => Form >= EncounterUtil1.FormDynamic;

    public byte FlawlessIVCount
    {
        get
        {
            var pi = PersonalTable.XY.GetFormEntry(Species, Form);
            return pi.EggGroup1 == 15 ? (byte)3 : IsFriendSafari ? (byte)2 : (byte)0;
        }
    }

    public string Name => $"Wild Encounter ({Version})";
    public string LongName => $"{Name} {Type.ToString().Replace('_', ' ')}";
    public GameVersion Version => Parent.Version;
    public int Location => Parent.Location;
    public SlotType Type => Parent.Type;

    public bool Pressure { get; init; }
    public bool IsFriendSafari => Type == SlotType.FriendSafari;
    public bool IsHorde => Type == SlotType.Horde;

    public EncounterSlot6XY CreatePressureFormCopy(byte form) => this with {Form = form, Pressure = true};

    private HiddenAbilityPermission IsHiddenAbilitySlot() => IsHorde || IsFriendSafari ? HiddenAbilityPermission.Possible : HiddenAbilityPermission.Never;

    public AbilityPermission Ability => IsHiddenAbilitySlot() switch
    {
        HiddenAbilityPermission.Never => AbilityPermission.Any12,
        HiddenAbilityPermission.Always => AbilityPermission.OnlyHidden,
        _ => AbilityPermission.Any12H,
    };
    private bool IsDeferredHiddenAbility(bool IsHidden) => IsHiddenAbilitySlot() switch
    {
        HiddenAbilityPermission.Never => IsHidden,
        HiddenAbilityPermission.Always => !IsHidden,
        _ => false,
    };

    #region Generating
    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria) => ConvertToPKM(tr, criteria);
    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr);
    public PK6 ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr, EncounterCriteria.Unrestricted);

    public PK6 ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria)
    {
        int lang = (int)Language.GetSafeLanguage(Generation, (LanguageID)tr.Language);
        var version = Version != GameVersion.XY ? Version : GameVersion.XY.Contains(tr.Game) ? (GameVersion)tr.Game : GameVersion.X;
        var form = GetWildForm(Form);
        var pk = new PK6
        {
            Species = Species,
            Form = form,
            CurrentLevel = LevelMin,
            Met_Location = Location,
            Met_Level = LevelMin,
            Ball = (byte)Ball.Poke,
            MetDate = EncounterDate.GetDate3DS(),

            Version = (byte)version,
            Language = lang,
            OT_Name = tr.OT,
            OT_Gender = tr.Gender,
            ID32 = tr.ID32,
            Nickname = SpeciesName.GetSpeciesNameGeneration(Species, lang, Generation),
            OT_Friendship = PersonalTable.XY[Species, form].BaseFriendship,
        };
        if (tr is IRegionOrigin r)
            r.CopyRegionOrigin(pk);
        else
            pk.SetDefaultRegionOrigins(lang);

        SetPINGA(pk, criteria);
        EncounterUtil1.SetEncounterMoves(pk, Version, LevelMin);
        pk.SetRandomMemory6();
        pk.ResetPartyStats();
        return pk;
    }

    private byte GetWildForm(byte form)
    {
        if (form != EncounterUtil1.FormRandom)
            return form;
        // flagged as totally random
        return (byte)Util.Rand.Next(PersonalTable.XY[Species].FormCount);
    }

    private void SetPINGA(PK6 pk, EncounterCriteria criteria)
    {
        var pi = PersonalTable.XY.GetFormEntry(Species, Form);
        pk.PID = Util.Rand32();
        pk.EncryptionConstant = Util.Rand32();
        pk.Nature = (int)criteria.GetNature(Nature.Random);
        pk.Gender = criteria.GetGender(-1, pi);
        pk.RefreshAbility(criteria.GetAbilityFromNumber(Ability));
        pk.SetRandomIVs(FlawlessIVCount);
    }

    #endregion

    #region Matching

    public bool IsMatchExact(PKM pk, EvoCriteria evo)
    {
        if (!this.IsLevelWithinRange(pk.Met_Level))
            return false;

        if (Form != evo.Form && !IsRandomUnspecificForm && Species is not ((int)Core.Species.Burmy or (int)Core.Species.Furfrou))
            return false;

        return true;
    }

    public EncounterMatchRating GetMatchRating(PKM pk)
    {
        bool isHidden = pk.AbilityNumber == 4;
        if (isHidden && this.IsPartialMatchHidden(pk.Species, Species))
            return EncounterMatchRating.PartialMatch;
        if (IsDeferredHiddenAbility(isHidden))
            return EncounterMatchRating.Deferred;
        return EncounterMatchRating.Match;
    }
    #endregion
}
