using System;

namespace PKHeX.Core;

/// <summary>
/// Generation 4 Trade Encounter with a fixed PID value, met location, and version.
/// </summary>
public sealed record EncounterTrade4RanchGift
    : IEncounterable, IEncounterMatch, IEncounterConvertible<PK4>, IFatefulEncounterReadOnly, IFixedTrainer, IMoveset
{
    public int Generation => 4;
    public EntityContext Context => EntityContext.Gen4;

    /// <summary>
    /// Fixed <see cref="PKM.PID"/> value the encounter must have.
    /// </summary>
    public readonly uint PID;

    public int MetLocation { private get; init; }
    public int Location => MetLocation;
    public Shiny Shiny => FatefulEncounter ? Shiny.Never : Shiny.FixedValue;
    public GameVersion Version { get; }
    public bool EggEncounter => false;
    public int EggLocation { get; init; }

    public Ball FixedBall { get; init; } = Ball.Poke;
    public bool IsShiny => false;
    public bool IsFixedTrainer => true;
    public byte LevelMin => Level;
    public byte LevelMax => Level;
    public ushort Species { get; }
    public byte Level { get; }

    public bool FatefulEncounter { get; }
    public required Moveset Moves { get; init; }
    public required ushort TID16 { get; init; }
    public required ushort SID16 { get; init; }
    private uint ID32 => (uint)(TID16 | (SID16 << 16));
    public required byte OTGender { get; init; }
    public required byte Gender { get; init; }
    public required AbilityPermission Ability { get; init; }
    public byte CurrentLevel { get; init; }
    public byte Form { get; init; }

    private static readonly string[] TrainerNames = { string.Empty, "ユカリ", "Hayley", "EULALIE", "GIULIA", "EUKALIA", string.Empty, "Eulalia" };

    private const string _name = "In-game Trade";
    public string Name => _name;
    public string LongName => _name;

    public EncounterTrade4RanchGift(uint pid, ushort species, byte level)
    {
        Version = GameVersion.D;
        PID = pid;
        Species = species;
        Level = level;
    }

    public EncounterTrade4RanchGift(ushort species, byte level)
    {
        Version = GameVersion.D;
        Species = species;
        Level = level;
        FatefulEncounter = true;
        MetLocation = 3000;
    }

    #region Generating

    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr);
    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria) => ConvertToPKM(tr, criteria);

    public PK4 ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr, EncounterCriteria.Unrestricted);

    public PK4 ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria)
    {
        var version = this.GetCompatibleVersion((GameVersion)tr.Game);
        int lang = (int)Language.GetSafeLanguage(Generation, (LanguageID)tr.Language, version);
        var actualLevel = CurrentLevel != default ? CurrentLevel : Level;
        var pk = new PK4
        {
            Species = Species,
            CurrentLevel = actualLevel,
            Met_Location = Location,
            Met_Level = Level,
            MetDate = EncounterDate.GetDateNDS(),
            Ball = (byte)FixedBall,

            ID32 = ID32,
            Version = (byte)version,
            Language = lang,
            OT_Gender = OTGender,
            OT_Name = TrainerNames[lang],

            OT_Friendship = PersonalTable.DP[Species, Form].BaseFriendship,

            IsNicknamed = true,
            Nickname = SpeciesName.GetSpeciesNameGeneration(Species, lang, Generation),

            HT_Name = tr.OT,
            HT_Gender = tr.Gender,
        };

        EncounterUtil1.SetEncounterMoves(pk, version, actualLevel);
        SetPINGA(pk, criteria);
        pk.ResetPartyStats();

        return pk;
    }

    private void SetPINGA(PKM pk, EncounterCriteria criteria)
    {
        var pid = FatefulEncounter ? Util.Rand32() : PID;
        pk.PID = pid;
        pk.Nature = (int)(pid % 25);
        pk.Gender = Gender;
        pk.RefreshAbility((int)(pid % 2));
        criteria.SetRandomIVs(pk);
    }

    #endregion

    #region Matching

    public bool IsTrainerMatch(PKM pk, ReadOnlySpan<char> trainer, int language) => (uint)language < TrainerNames.Length && trainer.SequenceEqual(TrainerNames[language]);

    public bool IsMatchExact(PKM pk, EvoCriteria evo)
    {
        if (pk.Met_Level != Level)
            return false;
        if (!IsMatchNatureGenderShiny(pk))
            return false;
        if (pk.ID32 != ID32)
            return false;
        if (evo.Form != Form && !FormInfo.IsFormChangeable(Species, Form, pk.Form, Context, pk.Context))
            return false;
        if (pk.OT_Gender != OTGender)
            return false;
        if (!IsMatchEggLocation(pk))
            return false;
        if (pk.IsEgg)
            return false;
        return true;
    }

    private bool IsMatchNatureGenderShiny(PKM pk)
    {
        if (pk.Gender != Gender)
            return false;
        if (FatefulEncounter)
            return !pk.IsShiny;
        return PID == pk.EncryptionConstant;
    }

    private bool IsMatchEggLocation(PKM pk)
    {
        var expect = EggLocation;
        if (pk is PB8)
            expect = Locations.Default8bNone;
        return pk.Egg_Location == expect;
    }

    public EncounterMatchRating GetMatchRating(PKM pk) => EncounterMatchRating.Match;

    #endregion
}
