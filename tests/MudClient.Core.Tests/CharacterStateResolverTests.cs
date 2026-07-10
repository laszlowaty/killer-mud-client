using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class CharacterStateResolverTests
{
    private readonly CharacterStateResolver _resolver = new();

    [Fact]
    public void Process_CharVitals_RaisesVitalsChangedWithAllFields()
    {
        CharacterVitalsUpdate? update = null;
        _resolver.VitalsChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Vitals",
            """{ "hp": 111, "max_hp": 112, "mv": 77, "max_mv": 100, "pos": "standing", "mem": 0, "name": "Ddsdsadsa", "sex": "M", "level": 1 }"""));

        Assert.NotNull(update);
        Assert.Equal(111, update!.Hp);
        Assert.Equal(112, update.MaxHp);
        Assert.Equal(77, update.Mv);
        Assert.Equal(100, update.MaxMv);
        Assert.Equal(0, update.Mem);
        Assert.Equal("Ddsdsadsa", update.Name);
        Assert.Equal("M", update.Sex);
        Assert.Equal(1, update.Level);
        Assert.Equal("standing", update.Position);
    }

    [Fact]
    public void Process_CharCondition_RaisesConditionChangedWithFlags()
    {
        CharacterConditionUpdate? update = null;
        _resolver.ConditionChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Condition",
            """{ "overweight": false, "position": "POS_STANDING", "drunk": false, "thirsty": true, "hungry": true, "sleepy": false, "smoking": false, "thighJab": false, "bleedingWound": false, "bleed": false, "halucinations": false }"""));

        Assert.NotNull(update);
        Assert.Equal("standing", update!.Position);
        Assert.True(update.Flags["thirsty"]);
        Assert.True(update.Flags["hungry"]);
        Assert.False(update.Flags["drunk"]);
        // Position is a string entry, not a flag.
        Assert.DoesNotContain("position", update.Flags.Keys);
    }

    [Fact]
    public void Process_PositionPrefix_IsNormalizedCaseInsensitively()
    {
        CharacterConditionUpdate? update = null;
        _resolver.ConditionChanged += u => update = u;

        _resolver.Process(new GmcpMessage("Char.Condition", """{ "position": "POS_FIGHTING" }"""));

        Assert.Equal("fighting", update!.Position);
    }

    [Fact]
    public void Process_MissingFields_AreNull()
    {
        CharacterVitalsUpdate? update = null;
        _resolver.VitalsChanged += u => update = u;

        _resolver.Process(new GmcpMessage("Char.Vitals", """{ "hp": 50 }"""));

        Assert.Equal(50, update!.Hp);
        Assert.Null(update.MaxHp);
        Assert.Null(update.Name);
        Assert.Null(update.Position);
    }

    [Fact]
    public void Process_CharVitals_NonIntegralNumber_ReturnsNull()
    {
        CharacterVitalsUpdate? update = null;
        _resolver.VitalsChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Vitals",
            """{ "hp": 1.5, "max_hp": 100 }"""));

        Assert.NotNull(update);
        Assert.Null(update!.Hp);        // 1.5 is not integral → null
        Assert.Equal(100, update.MaxHp); // 100 is integral → still parsed
    }

    [Fact]
    public void Process_CharVitals_OutOfRangeNumber_ReturnsNull()
    {
        CharacterVitalsUpdate? update = null;
        _resolver.VitalsChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Vitals",
            """{ "hp": 9999999999999, "max_hp": 100 }"""));

        Assert.NotNull(update);
        Assert.Null(update!.Hp);        // out of int32 range → null
        Assert.Equal(100, update.MaxHp);
    }

    [Theory]
    [InlineData("Room.People")]
    [InlineData("Room.Info")]
    public void Process_RoomPeople_RaisesPeopleChanged(string package)
    {
        IReadOnlyList<RoomPerson>? people = null;
        _resolver.PeopleChanged += p => people = p;

        _resolver.Process(new GmcpMessage(
            package,
            """[ {"name": "Ddsdsadsa", "is_fighting": false, "enemy": "   " },{"name": "Młody kapłan", "is_fighting": true, "enemy": "szczur" }]"""));

        Assert.NotNull(people);
        Assert.Equal(2, people!.Count);

        Assert.Equal("Ddsdsadsa", people[0].Name);
        Assert.False(people[0].IsFighting);
        Assert.Null(people[0].Enemy); // whitespace-padded "enemy" means none

        Assert.Equal("Młody kapłan", people[1].Name);
        Assert.True(people[1].IsFighting);
        Assert.Equal("szczur", people[1].Enemy);
    }

    [Fact]
    public void Process_RoomPeople_EmptyArray_RaisesEmptyList()
    {
        IReadOnlyList<RoomPerson>? people = null;
        _resolver.PeopleChanged += p => people = p;

        _resolver.Process(new GmcpMessage("Room.People", "[]"));

        Assert.NotNull(people);
        Assert.Empty(people!);
    }

    [Fact]
    public void Process_RoomInfo_ObjectPayload_DoesNotRaisePeopleChanged()
    {
        // Object-shaped Room.Info is room metadata (handled by the map resolver).
        var raised = false;
        _resolver.PeopleChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Room.Info", """{ "num": 123 }"""));

        Assert.False(raised);
    }

    [Theory]
    [InlineData("Room.Info", """{ "hp": 1 }""")]
    [InlineData("Char.Vitals", "nie-json")]
    [InlineData("Char.Vitals", "")]
    [InlineData("Char.Condition", "[1,2,3]")]
    public void Process_UnknownPackageOrMalformedJson_RaisesNothing(string package, string json)
    {
        var raised = false;
        _resolver.VitalsChanged += _ => raised = true;
        _resolver.ConditionChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage(package, json));

        Assert.False(raised);
    }

    // ====================================================================
    // Char.Affects parsing
    // ====================================================================

    [Fact]
    public void Process_CharAffects_HappyPath()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Błogosławieństwo","desc":"Zwiększa celność","negative":false,"ending":false,"extraValue":"10m"},{"name":"Zatrucie","desc":"Trucizna w organizmie","negative":true,"ending":false,"extraValue":"30s"}]"""));

        Assert.NotNull(affects);
        Assert.Equal(2, affects!.Count);

        // -- First affect: blessing --
        Assert.Equal("Błogosławieństwo", affects[0].Name);
        Assert.Equal("Zwiększa celność", affects[0].Description);
        Assert.False(affects[0].Negative);
        Assert.False(affects[0].Ending);
        Assert.Equal("10m", affects[0].ExtraValue);

        // -- Second affect: poison --
        Assert.Equal("Zatrucie", affects[1].Name);
        Assert.Equal("Trucizna w organizmie", affects[1].Description);
        Assert.True(affects[1].Negative);
        Assert.False(affects[1].Ending);
        Assert.Equal("30s", affects[1].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_EmptyArray_RaisesEmptyList()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage("Char.Affects", "[]"));

        Assert.NotNull(affects);
        Assert.Empty(affects!);
    }

    [Fact]
    public void Process_CharAffects_NonArray_Ignored()
    {
        var raised = false;
        _resolver.AffectsChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Char.Affects", """{"name":"test"}"""));

        Assert.False(raised);
    }

    [Fact]
    public void Process_CharAffects_MalformedJson_Ignored()
    {
        var raised = false;
        _resolver.AffectsChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Char.Affects", "nie-json"));

        Assert.False(raised);
    }

    [Fact]
    public void Process_CharAffects_SkipsNonObjectEntries()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[42,{"name":"Blessing","desc":"A blessing"},"string",false]"""));

        Assert.NotNull(affects);
        Assert.Single(affects!);
        Assert.Equal("Blessing", affects[0].Name);
    }

    [Fact]
    public void Process_CharAffects_SkipsMissingName()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"desc":"no name"},{"name":"Valid","desc":"ok"}]"""));

        Assert.NotNull(affects);
        Assert.Single(affects!);
        Assert.Equal("Valid", affects[0].Name);
    }

    [Fact]
    public void Process_CharAffects_SkipsEmptyName()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"","desc":"empty"},{"name":"Valid","desc":"ok"}]"""));

        Assert.NotNull(affects);
        Assert.Single(affects!);
        Assert.Equal("Valid", affects[0].Name);
    }

    [Fact]
    public void Process_CharAffects_SkipsWhitespaceName()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"   ","desc":"whitespace"},{"name":"Valid","desc":"ok"}]"""));

        Assert.NotNull(affects);
        Assert.Single(affects!);
        Assert.Equal("Valid", affects[0].Name);
    }

    [Fact]
    public void Process_CharAffects_DescMissing_FallbackEmpty()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"NoDesc"}]"""));

        Assert.NotNull(affects);
        Assert.Equal(string.Empty, affects![0].Description);
    }

    [Fact]
    public void Process_CharAffects_NegativeDefaultsFalse()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","negative":true},{"name":"Test2","desc":""}]"""));

        Assert.NotNull(affects);
        Assert.True(affects![0].Negative);
        Assert.False(affects[1].Negative);
    }

    [Fact]
    public void Process_CharAffects_EndingDefaultsFalse()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","ending":true},{"name":"Test2","desc":""}]"""));

        Assert.NotNull(affects);
        Assert.True(affects![0].Ending);
        Assert.False(affects[1].Ending);
    }

    [Fact]
    public void Process_CharAffects_BooleanNegativeOnlyWhenTrue()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        // negative: false should be treated as false (not defaulting to true)
        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","negative":false}]"""));

        Assert.NotNull(affects);
        Assert.False(affects![0].Negative);
    }

    [Fact]
    public void Process_CharAffects_BooleanEndingOnlyWhenTrue()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","ending":false}]"""));

        Assert.NotNull(affects);
        Assert.False(affects![0].Ending);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueString()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","extraValue":"30s"}]"""));

        Assert.NotNull(affects);
        Assert.Equal("30s", affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueNumber()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","extraValue":42}]"""));

        Assert.NotNull(affects);
        Assert.Equal("42", affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueBool()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","extraValue":true}]"""));

        Assert.NotNull(affects);
        Assert.Equal("true", affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueBoolFalse()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","extraValue":false}]"""));

        Assert.NotNull(affects);
        Assert.Equal("false", affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueNull_ReturnsNull()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","extraValue":null}]"""));

        Assert.NotNull(affects);
        Assert.Null(affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueMissing_ReturnsNull()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":""}]"""));

        Assert.NotNull(affects);
        Assert.Null(affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueObject_ReturnsNull()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","extraValue":{"nested":"val"}}]"""));

        Assert.NotNull(affects);
        Assert.Null(affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_ExtraValueArray_ReturnsNull()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","extraValue":[1,2,3]}]"""));

        Assert.NotNull(affects);
        Assert.Null(affects![0].ExtraValue);
    }

    [Fact]
    public void Process_CharAffects_NonBooleanNegative_DefaultsFalse()
    {
        // "negative" with a non-boolean value (string, number, etc.) should default to false
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","negative":"yes"}]"""));

        Assert.NotNull(affects);
        Assert.False(affects![0].Negative);
    }

    [Fact]
    public void Process_CharAffects_NonBooleanEnding_DefaultsFalse()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"","ending":"yes"}]"""));

        Assert.NotNull(affects);
        Assert.False(affects![0].Ending);
    }

    [Fact]
    public void Process_CharAffects_DescProvided()
    {
        IReadOnlyList<CharacterAffect>? affects = null;
        _resolver.AffectsChanged += a => affects = a;

        _resolver.Process(new GmcpMessage(
            "Char.Affects",
            """[{"name":"Test","desc":"  Opis z spacjami  "}]"""));

        Assert.NotNull(affects);
        Assert.Equal("  Opis z spacjami  ", affects![0].Description);
    }

    // ====================================================================
    // Char.Condition — all-false flags and position normalization
    // ====================================================================

    [Fact]
    public void Process_CharCondition_AllFalse_NoActiveFlags()
    {
        CharacterConditionUpdate? update = null;
        _resolver.ConditionChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Condition",
            """{ "overweight": false, "position": "POS_STOICCY", "drunk": false, "thirsty": false, "hungry": false, "sleepy": false, "smoking": false, "thighJab": false, "bleedingWound": false, "bleed": false, "halucinations": false }"""));

        Assert.NotNull(update);
        Assert.Equal("stoiccy", update!.Position);
        // All flags are false, so none should be active
        Assert.DoesNotContain(true, update.Flags.Values);
    }

    [Fact]
    public void Process_CharCondition_AllFalse_NoActiveFlags_Standing()
    {
        CharacterConditionUpdate? update = null;
        _resolver.ConditionChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Condition",
            """{ "overweight": false, "position": "POS_STANDING", "drunk": false, "thirsty": false, "hungry": false, "sleepy": false, "smoking": false, "thighJab": false, "bleedingWound": false, "bleed": false, "halucinations": false }"""));

        Assert.NotNull(update);
        Assert.Equal("standing", update!.Position);
        // All flags are false, so none should be active
        Assert.DoesNotContain(true, update.Flags.Values);
    }

    // ====================================================================
    // Char.Group parsing
    // ====================================================================

    [Fact]
    public void Process_CharGroup_RaisesGroupChanged()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety","pos":"standing","mem":0,"is_npc":false,"room":"Temple"}]}"""));

        Assert.NotNull(update);
        Assert.Equal("Hero", update!.Leader);
        Assert.Single(update.Members);
    }

    [Fact]
    public void Process_CharGroup_ParsesMemberFields()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Aragon","members":[{"name":"Aragon","hp":"zadnych sladow","mv":"wypoczety","pos":"POS_STANDING","mem":5,"is_npc":false,"room":"Throne Room"},{"name":"Gimli","hp":"ogromne rany","mv":"zmeczony","pos":"POS_SITTING","mem":0,"room":"Throne Room"},{"name":"szczur","hp":"zadrapania","mv":"zameczony","pos":"POS_FIGHTING","mem":1,"is_npc":true,"room":"Dungeon"}]}"""));

        Assert.NotNull(update);

        // -- Leader --
        Assert.Equal("Aragon", update!.Members[0].Name);
        Assert.True(update.Members[0].IsLeader);
        Assert.Equal("standing", update.Members[0].Position);
        Assert.Equal("zadnych sladow", update.Members[0].HpText);
        Assert.Equal(7, update.Members[0].HpScale);
        Assert.Equal("wypoczety", update.Members[0].MvText);
        Assert.Equal(4, update.Members[0].MvScale);
        Assert.Equal(5, update.Members[0].Mem);
        Assert.False(update.Members[0].IsNpc);
        Assert.Equal("Throne Room", update.Members[0].Room);

        // -- Non-leader player --
        Assert.Equal("Gimli", update.Members[1].Name);
        Assert.False(update.Members[1].IsLeader);
        Assert.Equal("sitting", update.Members[1].Position);
        Assert.Equal("ogromne rany", update.Members[1].HpText);
        Assert.Equal(2, update.Members[1].HpScale);
        Assert.Equal("zmeczony", update.Members[1].MvText);
        Assert.Equal(2, update.Members[1].MvScale);
        Assert.Equal(0, update.Members[1].Mem);
        Assert.False(update.Members[1].IsNpc);
        Assert.Equal("Throne Room", update.Members[1].Room);

        // -- NPC --
        Assert.Equal("szczur", update.Members[2].Name);
        Assert.False(update.Members[2].IsLeader);
        Assert.Equal("fighting", update.Members[2].Position);
        Assert.Equal("zadrapania", update.Members[2].HpText);
        Assert.Equal(6, update.Members[2].HpScale);
        Assert.Equal("zameczony", update.Members[2].MvText);
        Assert.Equal(0, update.Members[2].MvScale);
        Assert.True(update.Members[2].IsNpc);
        Assert.Equal("Dungeon", update.Members[2].Room);
    }

    [Fact]
    public void Process_CharGroup_NumericRoomValue()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety","room":6017}]}"""));

        Assert.NotNull(update);
        Assert.Equal("6017", update!.Members[0].Room);
    }

    [Fact]
    public void Process_CharGroup_MixedStringAndNumericRoomValues()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Aragon","members":[{"name":"Aragon","hp":"zadnych sladow","mv":"wypoczety","room":"Throne Room"},{"name":"Gimli","hp":"ogromne rany","mv":"zmeczony","room":6017}]}"""));

        Assert.NotNull(update);
        Assert.Equal("Throne Room", update!.Members[0].Room);
        Assert.Equal("6017", update.Members[1].Room);
    }

    [Fact]
    public void Process_CharGroup_LeaderDetectionIsCaseInsensitive()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"hero","hp":"zadnych sladow","mv":"wypoczety"},{"name":"Sidekick","hp":"zadnych sladow","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.True(update!.Members[0].IsLeader);
        Assert.False(update.Members[1].IsLeader);
    }

    [Fact]
    public void Process_CharGroup_LeaderTrimmed()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"  Hero  ","members":[{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Equal("Hero", update!.Leader);
        Assert.True(update.Members[0].IsLeader);
    }

    [Fact]
    public void Process_CharGroup_MemberNameTrimmed()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"  Hero  ","hp":"zadnych sladow","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Equal("Hero", update!.Members[0].Name);
        Assert.True(update.Members[0].IsLeader);
    }

    [Fact]
    public void Process_CharGroup_EmptyMemberName_Skipped()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"","hp":"zadnych sladow","mv":"wypoczety"},{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Single(update!.Members);
        Assert.Equal("Hero", update.Members[0].Name);
    }

    [Fact]
    public void Process_CharGroup_WhitespaceMemberName_Skipped()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"   ","hp":"zadnych sladow","mv":"wypoczety"},{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Single(update!.Members);
        Assert.Equal("Hero", update.Members[0].Name);
    }

    [Fact]
    public void Process_CharGroup_NonObjectMember_Skipped()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[42,{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Single(update!.Members);
        Assert.Equal("Hero", update.Members[0].Name);
    }

    [Fact]
    public void Process_CharGroup_MissingMembersField_DoesNotRaise()
    {
        var raised = false;
        _resolver.GroupChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero"}"""));

        Assert.False(raised);
    }

    [Fact]
    public void Process_CharGroup_MembersNotArray_DoesNotRaise()
    {
        var raised = false;
        _resolver.GroupChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":"not-an-array"}"""));

        Assert.False(raised);
    }

    [Fact]
    public void Process_CharGroup_NonObjectRoot_DoesNotRaise()
    {
        var raised = false;
        _resolver.GroupChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage("Char.Group", "[]"));

        Assert.False(raised);
    }

    [Theory]
    [InlineData("Char.Group", """{"leader":"","members":[]}""")]
    [InlineData("Char.Group", """{"leader":null,"members":[]}""")]
    [InlineData("Char.Group", """{"leader":"   ","members":[]}""")]
    [InlineData("Char.Group", """{"members":[]}""")]
    public void Process_CharGroup_NoValidLeader_RaisesWithNullLeaderAndEmptyMembers(string package, string json)
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(package, json));

        Assert.NotNull(update);
        Assert.Null(update!.Leader);
        Assert.Empty(update.Members);
    }

    [Fact]
    public void Process_CharGroup_NoLeaderWithMembers_RaisesWithAllIsLeaderFalse()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"members":[{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety"},{"name":"Gimli","hp":"ogromne rany","mv":"zmeczony"}]}"""));

        Assert.NotNull(update);
        Assert.Null(update!.Leader);
        Assert.Equal(2, update.Members.Count);
        Assert.False(update.Members[0].IsLeader);
        Assert.False(update.Members[1].IsLeader);
    }

    [Theory]
    [InlineData("Char.Group", "nie-json")]
    [InlineData("Char.Group", "")]
    public void Process_CharGroup_MalformedOrEmptyJson_DoesNotRaise(string package, string json)
    {
        var raised = false;
        _resolver.GroupChanged += _ => raised = true;

        _resolver.Process(new GmcpMessage(package, json));

        Assert.False(raised);
    }

    // ====================================================================
    // HP Polish text → numeric scale mapping (tested via Process)
    // ====================================================================

    [Theory]
    [InlineData("umiera", 0)]
    [InlineData("unieruchomiony", 0)]
    [InlineData("ledwo stoi", 1)]
    [InlineData("ogromne rany", 2)]
    [InlineData("ogromne uszkodzenia", 2)]
    [InlineData("ciezkie rany", 3)]
    [InlineData("ciężkie rany", 3)]
    [InlineData("ciezkie uszkodzenia", 3)]
    [InlineData("ciężkie uszkodzenia", 3)]
    [InlineData("srednie rany", 4)]
    [InlineData("średnie rany", 4)]
    [InlineData("srednie uszkodzenia", 4)]
    [InlineData("średnie uszkodzenia", 4)]
    [InlineData("lekkie rany", 5)]
    [InlineData("lekkie uszkodzenia", 5)]
    [InlineData("zadrapania", 6)]
    [InlineData("zadnych sladow", 7)]
    [InlineData("żadnych śladów", 7)]
    public void Process_CharGroup_HpTextMapping(string hpText, int expectedScale)
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            $$"""{"leader":"T","members":[{"name":"T","hp":"{{hpText}}","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Equal(expectedScale, update!.Members[0].HpScale);
        Assert.Equal(hpText, update.Members[0].HpText);
    }

    [Fact]
    public void Process_CharGroup_UnknownHpText_ReturnsNull()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"T","members":[{"name":"T","hp":"nieznany tekst","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Null(update!.Members[0].HpScale);
        Assert.Equal("nieznany tekst", update.Members[0].HpText);
    }

    [Fact]
    public void Process_CharGroup_MissingHpField_ReturnsEmptyTextAndNullScale()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"T","members":[{"name":"T","mv":"wypoczety"}]}"""));

        Assert.NotNull(update);
        Assert.Empty(update!.Members[0].HpText);
        Assert.Null(update.Members[0].HpScale);
    }

    // ====================================================================
    // MV Polish text → numeric scale mapping (tested via Process)
    // ====================================================================

    [Theory]
    [InlineData("zameczony", 0)]
    [InlineData("zamęczony", 0)]
    [InlineData("zameczona", 0)]
    [InlineData("zamęczona", 0)]
    [InlineData("bardzo zmeczony", 1)]
    [InlineData("bardzo zmęczony", 1)]
    [InlineData("bardzo zmeczona", 1)]
    [InlineData("bardzo zmęczona", 1)]
    [InlineData("zmeczony", 2)]
    [InlineData("zmęczony", 2)]
    [InlineData("zmeczona", 2)]
    [InlineData("zmęczona", 2)]
    [InlineData("lekko zmeczony", 3)]
    [InlineData("lekko zmęczony", 3)]
    [InlineData("lekko zmeczona", 3)]
    [InlineData("lekko zmęczona", 3)]
    [InlineData("wypoczety", 4)]
    [InlineData("wypoczęty", 4)]
    [InlineData("wypoczeta", 4)]
    [InlineData("wypoczęta", 4)]
    public void Process_CharGroup_MvTextMapping(string mvText, int expectedScale)
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            $$"""{"leader":"T","members":[{"name":"T","hp":"zadnych sladow","mv":"{{mvText}}"}]}"""));

        Assert.NotNull(update);
        Assert.Equal(expectedScale, update!.Members[0].MvScale);
        Assert.Equal(mvText, update.Members[0].MvText);
    }

    [Fact]
    public void Process_CharGroup_UnknownMvText_ReturnsNull()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"T","members":[{"name":"T","hp":"zadnych sladow","mv":"nieznany"}]}"""));

        Assert.NotNull(update);
        Assert.Null(update!.Members[0].MvScale);
        Assert.Equal("nieznany", update.Members[0].MvText);
    }

    [Fact]
    public void Process_CharGroup_MissingMvField_ReturnsEmptyTextAndNullScale()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"T","members":[{"name":"T","hp":"zadnych sladow"}]}"""));

        Assert.NotNull(update);
        Assert.Empty(update!.Members[0].MvText);
        Assert.Null(update.Members[0].MvScale);
    }

    // ====================================================================
    // Safe numeric parsing (GetInt / TryGetInt32)
    // ====================================================================

    [Fact]
    public void Process_CharGroup_MemberMem_NonIntegral_ReturnsNull()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety","mem":1.5}]}"""));

        Assert.NotNull(update);
        Assert.Null(update!.Members[0].Mem);
    }

    [Fact]
    public void Process_CharGroup_MemberMem_OutOfRange_ReturnsNull()
    {
        CharacterGroupUpdate? update = null;
        _resolver.GroupChanged += u => update = u;

        _resolver.Process(new GmcpMessage(
            "Char.Group",
            """{"leader":"Hero","members":[{"name":"Hero","hp":"zadnych sladow","mv":"wypoczety","mem":9999999999999}]}"""));

        Assert.NotNull(update);
        Assert.Null(update!.Members[0].Mem);
    }
}
