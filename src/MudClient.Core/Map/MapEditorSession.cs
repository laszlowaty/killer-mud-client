using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MudClient.Core.Map;

public sealed record MapEditorCommandDecision(bool Allow, string Command, string? Message = null);

/// <summary>
/// Correlates a manually sent movement command with the next complete Room.Info
/// snapshot and applies a small, reversible edit to an immutable map document.
/// </summary>
public sealed class MapEditorSession
{
    private static readonly IReadOnlyDictionary<string, string> DirectionAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["n"] = "n", ["north"] = "n",
            ["ne"] = "ne", ["northeast"] = "ne",
            ["e"] = "e", ["east"] = "e",
            ["se"] = "se", ["southeast"] = "se",
            ["s"] = "s", ["south"] = "s",
            ["sw"] = "sw", ["southwest"] = "sw",
            ["w"] = "w", ["west"] = "w",
            ["nw"] = "nw", ["northwest"] = "nw",
            ["u"] = "u", ["up"] = "u",
            ["d"] = "d", ["down"] = "d",
        };

    private static readonly IReadOnlyDictionary<string, string> FullDirections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["n"] = "north", ["ne"] = "northeast", ["e"] = "east", ["se"] = "southeast",
            ["s"] = "south", ["sw"] = "southwest", ["w"] = "west", ["nw"] = "northwest",
            ["u"] = "up", ["d"] = "down",
        };

    private static readonly IReadOnlyDictionary<string, string> OppositeDirections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["n"] = "s", ["ne"] = "sw", ["e"] = "w", ["se"] = "nw", ["s"] = "n",
            ["sw"] = "ne", ["w"] = "e", ["nw"] = "se", ["u"] = "d", ["d"] = "u",
        };

    private readonly Stack<MapDocument> _undo = new();
    private readonly Stack<MapDocument> _redo = new();
    private MapDocument _savedDocument;
    private PendingMovement? _pendingMovement;
    private MappingConflict? _conflict;
    private RoomSnapshot? _lastSnapshot;
    private int? _nextNewRoomAreaId;
    private int? _targetAreaId;
    private int? _mappingStartRoomId;

    public MapEditorSession(
        MapDocument document,
        IEnumerable<MapDocument>? undoHistory = null,
        bool isDirty = false)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _savedDocument = Document;
        if (undoHistory is not null)
        {
            foreach (var snapshot in undoHistory)
            {
                _undo.Push(snapshot);
            }
        }

        IsDirty = isDirty;
        if (isDirty)
        {
            Status = "Odzyskano niezapisane zmiany mapy.";
        }
    }

    public MapDocument Document { get; private set; }

    public bool IsMapping { get; private set; }

    public bool IsDirty { get; private set; }

    public bool IsAwaitingRoomInfo => _pendingMovement is not null;

    public bool HasConflict => _conflict is not null;

    public bool HasTargetArea => _targetAreaId.HasValue;

    public bool MoveKnownRoomsToTargetArea { get; private set; }

    public int Step { get; private set; } = 2;

    public string Status { get; private set; } = "Edytor mapy jest gotowy.";

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public IReadOnlyList<MapDocument> GetUndoHistory(int maximumEntries = 5)
    {
        if (maximumEntries <= 0)
        {
            return [];
        }

        return _undo.Reverse().TakeLast(maximumEntries).ToArray();
    }

    public bool Start(string? currentVnum)
    {
        if (string.IsNullOrWhiteSpace(currentVnum))
        {
            Status = "Nie można rozpocząć: brak bieżącego vnum z GMCP.";
            return false;
        }

        var currentRoom = FindRoomByVnum(currentVnum);
        var createdStartingRoom = false;
        if (currentRoom is null)
        {
            if (_targetAreaId is not { } targetAreaId ||
                Document.Areas.All(area => area.Id != targetAreaId))
            {
                Status = "Nie można rozpocząć: bieżącego vnum nie ma na mapie i nie wybrano obszaru docelowego.";
                return false;
            }

            if (_lastSnapshot is not { } snapshot ||
                !string.Equals(snapshot.Vnum, currentVnum, StringComparison.Ordinal))
            {
                Status = $"Nie można rozpocząć od nowego vnum {currentVnum}: brak aktualnego pełnego Room.Info.";
                return false;
            }

            var before = Document;
            currentRoom = CreateStartingRoom(snapshot, targetAreaId);
            Document = AddRoom(Document, currentRoom);
            RecordUndo(before);
            _nextNewRoomAreaId = null;
            IsDirty = true;
            createdStartingRoom = true;
        }

        IsMapping = true;
        _mappingStartRoomId = currentRoom.Id;
        _pendingMovement = null;
        _conflict = null;
        Status = createdStartingRoom
            ? $"Utworzono pokój startowy vnum {currentVnum} w obszarze {GetAreaName(currentRoom.AreaId)}. Mapowanie aktywne."
            : $"Mapowanie aktywne od vnum {currentVnum}.";
        return true;
    }

    public void Stop(string? reason = null)
    {
        IsMapping = false;
        _mappingStartRoomId = null;
        _pendingMovement = null;
        _conflict = null;
        Status = !string.IsNullOrWhiteSpace(reason)
            ? reason
            : IsDirty
                ? "Mapowanie zatrzymane. Mapa ma niezapisane zmiany."
                : "Mapowanie zatrzymane.";
    }

    public bool SetStep(int step)
    {
        if (step is < 1 or > 20)
        {
            Status = "Odstęp musi być liczbą od 1 do 20.";
            return false;
        }

        Step = step;
        Status = $"Odstęp nowych pokoi: {step}.";
        return true;
    }

    public bool CreateArea(string name)
    {
        var trimmed = name.Trim();
        if (IsMapping)
        {
            Status = "Najpierw zatrzymaj mapowanie.";
            return false;
        }

        if (trimmed.Length == 0)
        {
            Status = "Podaj nazwę nowego obszaru.";
            return false;
        }

        if (Document.Areas.Any(area => string.Equals(area.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            Status = $"Obszar „{trimmed}” już istnieje.";
            return false;
        }

        var before = Document;
        var areaId = Document.Areas.Select(area => area.Id).DefaultIfEmpty(0).Max() + 1;
        Document = new MapDocument
        {
            AnonymousAreaName = Document.AnonymousAreaName,
            Areas =
            [
                .. Document.Areas,
                new MapArea { Id = areaId, Name = trimmed, Rooms = [], Labels = [] },
            ],
        };
        RecordUndo(before);
        _nextNewRoomAreaId = areaId;
        _targetAreaId = areaId;
        IsDirty = true;
        Status = $"Utworzono obszar „{trimmed}”. Pierwszy nowy pokój trafi do niego.";
        return true;
    }

    public bool SetMoveKnownRoomsToTargetArea(bool enabled, int? targetAreaId = null)
    {
        var effectiveTargetAreaId = targetAreaId ?? _targetAreaId;
        if (enabled && effectiveTargetAreaId is null)
        {
            Status = "Najpierw wybierz obszar docelowy.";
            return false;
        }

        if (enabled && Document.Areas.All(area => area.Id != effectiveTargetAreaId))
        {
            _targetAreaId = null;
            Status = "Docelowy obszar już nie istnieje.";
            return false;
        }

        if (enabled)
        {
            _targetAreaId = effectiveTargetAreaId;
        }
        MoveKnownRoomsToTargetArea = enabled;
        Status = enabled
            ? $"Istniejące pokoje będą przenoszone do obszaru {GetAreaName(_targetAreaId!.Value)}. Pokój wejściowy pozostanie w starej krainie."
            : "Istniejące pokoje pozostaną w swoich dotychczasowych obszarach.";
        return true;
    }

    public bool SetCurrentRoomSymbol(string symbol)
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return false;
        }

        var value = symbol.Trim();
        if (value == "!!")
        {
            value = "‼";
        }

        if (value is "-1" or "clear")
        {
            value = string.Empty;
        }

        if (string.Equals(room.Symbol ?? string.Empty, value, StringComparison.Ordinal))
        {
            Status = "Pokój ma już taki symbol.";
            return false;
        }

        RecordUndo(Document);
        Document = ReplaceRoom(Document, CloneRoomWithSymbol(room, value.Length == 0 ? null : value));
        IsDirty = true;
        Status = value.Length == 0
            ? $"Usunięto symbol pokoju {room.Vnum}."
            : $"Ustawiono symbol „{value}” w pokoju {room.Vnum}.";
        return true;
    }

    public bool ForgetCurrentRoom()
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return false;
        }

        var userData = CloneUserData(room.UserData);
        userData.Remove("vnum");
        userData.Remove("sector");
        RecordUndo(Document);
        Document = ReplaceRoom(
            Document,
            CloneRoomWithSymbol(CloneRoom(room, userData: userData), null));
        IsDirty = true;
        IsMapping = false;
        _pendingMovement = null;
        Status = $"Pokój {room.Id} zapomniał vnum, sektor i symbol. Mapowanie zatrzymano.";
        return true;
    }

    public bool AddLabel(string text)
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return false;
        }

        var labelText = text.Trim();
        if (labelText.Length == 0)
        {
            Status = "Podaj tekst etykiety.";
            return false;
        }

        var fontSize = 16d;
        if (labelText.StartsWith("###", StringComparison.Ordinal))
        {
            fontSize = 38;
            labelText = labelText[3..].TrimStart();
        }
        else if (labelText.StartsWith("##", StringComparison.Ordinal))
        {
            fontSize = 30;
            labelText = labelText[2..].TrimStart();
        }
        else if (labelText.StartsWith('#'))
        {
            fontSize = 22;
            labelText = labelText[1..].TrimStart();
        }

        if (labelText.EndsWith('!'))
        {
            labelText = "☠ " + labelText[..^1].TrimEnd();
        }

        if (labelText.Length == 0)
        {
            Status = "Podaj tekst etykiety po znaczniku rozmiaru.";
            return false;
        }

        var area = Document.Areas.First(item => item.Id == room.AreaId);
        var labelId = Document.Areas.SelectMany(item => item.Labels).Select(label => label.Id).DefaultIfEmpty(-1).Max() + 1;
        var label = new MapLabel
        {
            Id = labelId,
            AreaId = area.Id,
            Text = labelText,
            Coordinates = new MapCoordinates(
                room.Coordinates.X + 0.5,
                room.Coordinates.Y + 1.3,
                room.Coordinates.Z),
            FontSize = fontSize,
            ShowOnTop = true,
        };

        RecordUndo(Document);
        Document = ReplaceArea(Document, new MapArea
        {
            Id = area.Id,
            Name = area.Name,
            Rooms = area.Rooms,
            Labels = [.. area.Labels, label],
        });
        IsDirty = true;
        Status = $"Dodano etykietę „{labelText}”.";
        return true;
    }

    public bool SetCurrentRoomName(string name)
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return false;
        }

        var value = name.Trim();
        if (value.Length == 0 || string.Equals(room.Name, value, StringComparison.Ordinal))
        {
            Status = value.Length == 0 ? "Podaj nazwę pokoju." : "Pokój ma już taką nazwę.";
            return false;
        }

        RecordUndo(Document);
        Document = ReplaceRoom(Document, CloneRoom(room, name: value));
        IsDirty = true;
        Status = $"Zmieniono nazwę pokoju {room.Vnum} na „{value}”.";
        return true;
    }

    public bool SetCurrentRoomSector(string sector)
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return false;
        }

        var value = sector.Trim();
        var clear = value is "-1" or "clear";
        var userData = CloneUserData(room.UserData);
        var changed = clear
            ? userData.Remove("sector")
            : !string.Equals(room.Sector, Normalize(value), StringComparison.OrdinalIgnoreCase);
        if (!changed || (!clear && value.Length == 0))
        {
            Status = "Sektor pokoju nie wymaga zmiany.";
            return false;
        }

        if (!clear)
        {
            userData["sector"] = JsonSerializer.SerializeToElement(Normalize(value));
        }

        RecordUndo(Document);
        Document = ReplaceRoom(Document, CloneRoom(room, userData: userData));
        IsDirty = true;
        Status = clear ? "Usunięto sektor pokoju." : $"Ustawiono sektor pokoju na „{Normalize(value)}”.";
        return true;
    }

    public bool SetCurrentRoomWeight(double weight)
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return false;
        }

        if (!double.IsFinite(weight) || weight is <= 0 or > 1000)
        {
            Status = "Waga pokoju musi być liczbą większą od 0 i nie większą niż 1000.";
            return false;
        }

        if (room.Weight == weight)
        {
            Status = "Pokój ma już taką wagę.";
            return false;
        }

        RecordUndo(Document);
        Document = ReplaceRoom(Document, CloneRoom(room, weight: weight));
        IsDirty = true;
        Status = $"Ustawiono wagę pokoju na {weight.ToString(CultureInfo.InvariantCulture)}.";
        return true;
    }

    public bool MoveCurrentRoom(MapCoordinates coordinates)
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return false;
        }

        if (room.Coordinates == coordinates)
        {
            Status = "Pokój ma już takie współrzędne.";
            return false;
        }

        if (Document.Areas.SelectMany(area => area.Rooms).Any(candidate =>
                candidate.Id != room.Id && candidate.AreaId == room.AreaId && candidate.Coordinates == coordinates))
        {
            Status = $"Współrzędne {coordinates.X},{coordinates.Y},{coordinates.Z} są już zajęte w tym obszarze.";
            return false;
        }

        RecordUndo(Document);
        Document = ReplaceRoom(Document, CloneRoom(room, coordinates: coordinates));
        IsDirty = true;
        Status = $"Przeniesiono pokój na {coordinates.X},{coordinates.Y},{coordinates.Z}.";
        return true;
    }

    public IReadOnlyList<MapLabel> ShowCurrentAreaLabels()
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return [];
        }

        var labels = Document.Areas.First(area => area.Id == room.AreaId).Labels;
        Status = labels.Count == 0
            ? "Bieżący obszar nie ma etykiet tekstowych."
            : "Etykiety: " + string.Join(", ", labels.Take(12).Select(label => $"{label.Id}=„{label.Text}”")) +
              (labels.Count > 12 ? $" (+{labels.Count - 12} kolejnych)" : string.Empty);
        return labels;
    }

    public bool SetLabelText(int id, string text)
    {
        var area = Document.Areas.FirstOrDefault(candidate => candidate.Labels.Any(label => label.Id == id));
        var label = area?.Labels.FirstOrDefault(candidate => candidate.Id == id);
        var value = text.Trim();
        if (area is null || label is null)
        {
            Status = $"Nie znaleziono etykiety {id}.";
            return false;
        }

        if (value.Length == 0 || string.Equals(label.Text, value, StringComparison.Ordinal))
        {
            Status = value.Length == 0 ? "Podaj nowy tekst etykiety." : "Etykieta ma już taki tekst.";
            return false;
        }

        var replacement = new MapLabel
        {
            Id = label.Id,
            AreaId = label.AreaId,
            Text = value,
            Coordinates = label.Coordinates,
            FontSize = label.FontSize,
            ShowOnTop = label.ShowOnTop,
        };
        RecordUndo(Document);
        Document = ReplaceArea(Document, new MapArea
        {
            Id = area.Id,
            Name = area.Name,
            Rooms = area.Rooms,
            Labels = area.Labels.Select(candidate => candidate.Id == id ? replacement : candidate).ToArray(),
        });
        IsDirty = true;
        Status = $"Zmieniono tekst etykiety {id}.";
        return true;
    }

    public bool RemoveLabel(int id)
    {
        var area = Document.Areas.FirstOrDefault(candidate => candidate.Labels.Any(label => label.Id == id));
        if (area is null)
        {
            Status = $"Nie znaleziono etykiety {id}.";
            return false;
        }

        RecordUndo(Document);
        Document = ReplaceArea(Document, new MapArea
        {
            Id = area.Id,
            Name = area.Name,
            Rooms = area.Rooms,
            Labels = area.Labels.Where(label => label.Id != id).ToArray(),
        });
        IsDirty = true;
        Status = $"Usunięto etykietę {id}.";
        return true;
    }

    public MapEditorCommandDecision PrepareSpecialMovement(string direction, string command)
    {
        if (!IsMapping)
        {
            return new MapEditorCommandDecision(false, command, "Najpierw włącz mapowanie.");
        }

        if (_pendingMovement is not null)
        {
            return new MapEditorCommandDecision(false, command, "Mapper nadal czeka na Room.Info.");
        }

        var canonicalDirection = CanonicalShort(direction);
        var trimmedCommand = command.Trim();
        if (canonicalDirection is null || trimmedCommand.Length == 0)
        {
            return new MapEditorCommandDecision(false, command, "Użycie: /map special <kierunek> <komenda>.");
        }

        if (GetCurrentRoom() is not { } origin)
        {
            return new MapEditorCommandDecision(false, command, "Brak bieżącego pokoju na mapie.");
        }

        _pendingMovement = new PendingMovement(
            origin.Id,
            origin.Vnum!,
            canonicalDirection,
            trimmedCommand,
            HasDoor: false,
            IsClosed: false);
        Status = $"Specjalne wyjście {canonicalDirection}: wysyłanie „{trimmedCommand}”.";
        return new MapEditorCommandDecision(true, trimmedCommand);
    }

    public bool RemoveSpecialExit(string direction)
    {
        if (GetCurrentRoom() is not { } room || CanonicalShort(direction) is not { } canonicalDirection)
        {
            Status = "Brak bieżącego pokoju albo niepoprawny kierunek.";
            return false;
        }

        var userData = CloneUserData(room.UserData);
        var targetId = FindTargetInMetadata(userData.GetValueOrDefault(canonicalDirection));
        var removedMetadata = userData.Remove(canonicalDirection);
        var exits = room.Exits.Where(exit =>
                targetId.HasValue
                    ? exit.ExitId != targetId.Value
                    : CanonicalShort(exit.Name ?? string.Empty) != canonicalDirection)
            .ToArray();
        if (!removedMetadata && exits.Length == room.Exits.Count)
        {
            Status = $"Brak specjalnego wyjścia {canonicalDirection}.";
            return false;
        }

        RecordUndo(Document);
        Document = ReplaceRoom(Document, CloneRoom(room, exits: exits, userData: userData));
        IsDirty = true;
        Status = $"Usunięto specjalne wyjście {canonicalDirection}.";
        return true;
    }

    public void CancelPendingMovement(string message)
    {
        _pendingMovement = null;
        Status = message;
    }

    public bool ResolveConflictKeepMap()
    {
        if (_conflict is null)
        {
            Status = "Brak konfliktu do rozwiązania.";
            return false;
        }

        _conflict = null;
        Status = "Zachowano istniejące połączenie mapy.";
        return true;
    }

    public bool ResolveConflictUseGmcp()
    {
        if (_conflict is not { } conflict)
        {
            Status = "Brak konfliktu do rozwiązania.";
            return false;
        }

        var before = Document;
        var origin = FindRoomById(conflict.Movement.OriginRoomId);
        if (origin is null)
        {
            _conflict = null;
            Status = "Pokój początkowy konfliktu zniknął z mapy.";
            return false;
        }

        var oldTargetId = FindMappedTarget(
            origin,
            conflict.Movement.Direction,
            conflict.Movement.Command);
        Document = ReplaceRoom(
            Document,
            Disconnect(origin, conflict.Movement.Direction, conflict.Movement.Command));

        if (oldTargetId is { } mappedTargetId &&
            FindRoomById(mappedTargetId) is { } oldTarget &&
            OppositeDirections.GetValueOrDefault(conflict.Movement.Direction) is { } opposite)
        {
            Document = ReplaceRoom(Document, DisconnectFromTarget(oldTarget, opposite, origin.Id));
        }

        _conflict = null;
        return ApplyMovement(conflict.Movement, conflict.Snapshot, before);
    }

    public IReadOnlyList<string> Validate()
    {
        var rooms = Document.Areas.SelectMany(area => area.Rooms).ToArray();
        var roomIds = rooms.Select(room => room.Id).ToHashSet();
        var issues = new List<string>();
        foreach (var duplicate in rooms.Where(room => room.Vnum is not null).GroupBy(room => room.Vnum!)
                     .Where(group => group.Count() > 1))
        {
            issues.Add($"Vnum {duplicate.Key} występuje {duplicate.Count()} razy.");
        }

        foreach (var room in rooms)
        {
            foreach (var exit in room.Exits)
            {
                if (!roomIds.Contains(exit.ExitId))
                {
                    issues.Add($"Pokój {room.Id}: wyjście prowadzi do brakującego pokoju {exit.ExitId}.");
                }
                else if (exit.ExitId == room.Id)
                {
                    issues.Add($"Pokój {room.Id}: zapętlone wyjście.");
                }
            }
        }

        Status = issues.Count == 0
            ? "Kontrola mapy nie wykryła błędów."
            : $"Kontrola mapy: {issues.Count} problemów. {string.Join(" ", issues.Take(3))}";
        return issues;
    }

    public void ShowCurrentRoomInfo()
    {
        if (GetCurrentRoom() is not { } room)
        {
            Status = "Brak bieżącego pokoju na mapie.";
            return;
        }

        var area = Document.Areas.First(item => item.Id == room.AreaId);
        Status = $"Pokój id={room.Id}, vnum={room.Vnum}, obszar={area.Name}, " +
                 $"xyz={room.Coordinates.X},{room.Coordinates.Y},{room.Coordinates.Z}, wyjścia={room.Exits.Count}.";
    }

    public MapEditorCommandDecision PrepareManualCommand(string command)
    {
        if (!IsMapping)
        {
            return new MapEditorCommandDecision(true, command);
        }

        if (_pendingMovement is not null)
        {
            return new MapEditorCommandDecision(
                false,
                command,
                "Mapper czeka na Room.Info po poprzedniej komendzie ruchu.");
        }

        var trimmed = command.Trim();
        if (IsSafeNonMovementCommand(trimmed))
        {
            return new MapEditorCommandDecision(true, command);
        }

        var snapshot = _lastSnapshot;
        if (snapshot is null || FindRoomByVnum(snapshot.Vnum) is not { } origin)
        {
            return new MapEditorCommandDecision(false, command, "Brak aktualnego Room.Info lub pokoju początkowego na mapie.");
        }

        var normalizedCommand = Normalize(trimmed);
        var exit = snapshot.Exits.FirstOrDefault(candidate => ExitMatches(candidate, normalizedCommand));
        if (exit is null)
        {
            var available = string.Join(", ", snapshot.Exits.Select(item => item.Command));
            return new MapEditorCommandDecision(
                false,
                command,
                snapshot.Exits.Count == 0
                    ? "Room.Info nie zawiera dostępnych wyjść."
                    : $"Komenda jest zablokowana w trybie mapowania. Wyjścia: {available}.");
        }

        var direction = CanonicalShort(exit.Direction);
        if (direction is null)
        {
            return new MapEditorCommandDecision(false, command, $"Nieobsługiwany kierunek wyjścia: {exit.Direction}.");
        }

        _pendingMovement = new PendingMovement(
            origin.Id,
            origin.Vnum!,
            direction,
            exit.Command.Trim(),
            exit.HasDoor,
            exit.IsClosed);
        Status = $"Wysłano „{trimmed}”; oczekiwanie na Room.Info.";
        return new MapEditorCommandDecision(true, command);
    }

    public bool ProcessSnapshot(RoomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var previousSnapshot = _lastSnapshot;
        _lastSnapshot = snapshot;

        if (!IsMapping)
        {
            return false;
        }

        if (_pendingMovement is null)
        {
            if (previousSnapshot is not null &&
                !string.Equals(previousSnapshot.Vnum, snapshot.Vnum, StringComparison.Ordinal))
            {
                if (FindRoomByVnum(snapshot.Vnum) is null)
                {
                    IsMapping = false;
                    Status = $"Wykryto teleport z vnum {previousSnapshot.Vnum} do nieznanego vnum {snapshot.Vnum}. Mapowanie zatrzymano.";
                    return false;
                }

                var metadataChanged = UpdateKnownRoomMetadata(snapshot);
                Status = $"Wykryto teleport z vnum {previousSnapshot.Vnum} do {snapshot.Vnum}. Mapper zsynchronizował nowy punkt startowy.";
                return metadataChanged;
            }

            return UpdateKnownRoomMetadata(snapshot);
        }

        var pending = _pendingMovement;
        _pendingMovement = null;

        if (string.Equals(snapshot.Vnum, pending.OriginVnum, StringComparison.Ordinal))
        {
            var metadataChanged = UpdateKnownRoomMetadata(snapshot);
            Status = $"Ruch „{pending.Command}” nie zmienił pokoju.";
            return metadataChanged;
        }

        var origin = FindRoomById(pending.OriginRoomId);
        if (origin is null)
        {
            Status = "Pokój początkowy zniknął z dokumentu mapy.";
            return false;
        }

        var target = FindRoomByVnum(snapshot.Vnum);
        var existingTargetId = FindMappedTarget(origin, pending.Direction, pending.Command);
        if (existingTargetId is not null && target is not null && existingTargetId != target.Id)
        {
            _conflict = new MappingConflict(pending, snapshot);
            Status = $"Konflikt: wyjście {pending.Direction} prowadzi na mapie do {existingTargetId}, a GMCP do vnum {snapshot.Vnum}.";
            return false;
        }

        if (existingTargetId is not null && target is null)
        {
            _conflict = new MappingConflict(pending, snapshot);
            Status = $"Konflikt: wyjście {pending.Direction} ma już cel {existingTargetId}, ale vnum {snapshot.Vnum} nie istnieje na mapie.";
            return false;
        }

        return ApplyMovement(pending, snapshot, Document);
    }

    private bool ApplyMovement(PendingMovement pending, RoomSnapshot snapshot, MapDocument before)
    {
        var origin = FindRoomById(pending.OriginRoomId);
        if (origin is null)
        {
            Status = "Pokój początkowy zniknął z dokumentu mapy.";
            return false;
        }

        var target = FindRoomByVnum(snapshot.Vnum);
        var movedKnownRoom = false;
        if (target is null)
        {
            target = CreateRoom(origin, snapshot, pending.Direction);
            Document = AddRoom(Document, target);
        }
        else
        {
            if (ShouldMoveKnownRoomToTargetArea(target))
            {
                var targetAreaId = _targetAreaId!.Value;
                var coordinates = origin.AreaId == targetAreaId
                    ? Offset(origin.Coordinates, pending.Direction, Step)
                    : new MapCoordinates(0, 0, 0);
                target = CloneRoom(target, areaId: targetAreaId, coordinates: coordinates);
                Document = MoveRoomToArea(Document, target);
                _nextNewRoomAreaId = null;
                movedKnownRoom = true;
            }

            Document = ReplaceRoom(Document, BuildRoomWithMetadata(target, snapshot));
            target = FindRoomById(target.Id)!;
        }

        origin = FindRoomById(origin.Id)!;
        Document = ReplaceRoom(Document, Connect(origin, target, pending.Direction, pending.Command, pending.HasDoor));

        var opposite = OppositeDirections[pending.Direction];
        var reverseSnapshotExit = snapshot.Exits.FirstOrDefault(exit => CanonicalShort(exit.Direction) == opposite);
        if (reverseSnapshotExit is not null)
        {
            target = FindRoomById(target.Id)!;
            origin = FindRoomById(origin.Id)!;
            var reverseExistingTarget = FindMappedTarget(target, opposite, reverseSnapshotExit.Command);
            if (reverseExistingTarget is null || reverseExistingTarget == origin.Id)
            {
                Document = ReplaceRoom(
                    Document,
                    Connect(target, origin, opposite, reverseSnapshotExit.Command, reverseSnapshotExit.HasDoor));
            }
        }

        RecordUndo(before);
        IsDirty = true;
        Status = movedKnownRoom
            ? $"Przeniesiono vnum {snapshot.Vnum} do obszaru {GetAreaName(target.AreaId)} i zmapowano przejście ({pending.Command})."
            : $"Zmapowano {origin.Vnum} → {snapshot.Vnum} ({pending.Command}).";
        return true;
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            Status = "Brak zmian do cofnięcia.";
            return false;
        }

        _redo.Push(Document);
        Document = _undo.Pop();
        _nextNewRoomAreaId = null;
        ClearMissingTargetArea();
        IsDirty = true;
        _pendingMovement = null;
        _conflict = null;
        Status = "Cofnięto ostatnią zmianę mapy.";
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            Status = "Brak cofniętej zmiany do ponowienia.";
            return false;
        }

        _undo.Push(Document);
        Document = _redo.Pop();
        _nextNewRoomAreaId = null;
        ClearMissingTargetArea();
        IsDirty = true;
        _pendingMovement = null;
        _conflict = null;
        Status = "Ponowiono ostatnią cofniętą zmianę mapy.";
        return true;
    }

    public void MarkSaved()
    {
        _savedDocument = Document;
        IsDirty = false;
        Status = "Mapa została zapisana.";
    }

    public bool CancelChanges()
    {
        if (!IsDirty && _pendingMovement is null && _conflict is null)
        {
            Status = "Brak niezapisanych zmian mapy do anulowania.";
            return false;
        }

        Document = _savedDocument;
        _undo.Clear();
        _redo.Clear();
        _nextNewRoomAreaId = null;
        _pendingMovement = null;
        _conflict = null;
        ClearMissingTargetArea();
        IsDirty = false;
        Status = "Anulowano wszystkie niezapisane zmiany mapy.";
        return true;
    }

    private bool UpdateKnownRoomMetadata(RoomSnapshot snapshot)
    {
        var room = FindRoomByVnum(snapshot.Vnum);
        if (room is null)
        {
            Status = $"Vnum {snapshot.Vnum} nie istnieje na mapie; użyj wyjścia z istniejącego pokoju, aby go utworzyć.";
            return false;
        }

        var updated = BuildRoomWithMetadata(room, snapshot);
        if (RoomMetadataEquals(room, updated))
        {
            Status = $"Pokój {snapshot.Vnum} jest zgodny z GMCP.";
            return false;
        }

        RecordUndo(Document);
        Document = ReplaceRoom(Document, updated);
        IsDirty = true;
        Status = $"Uaktualniono dane pokoju {snapshot.Vnum}.";
        return true;
    }

    private MapRoom CreateRoom(MapRoom origin, RoomSnapshot snapshot, string direction)
    {
        var areaId = _nextNewRoomAreaId ?? origin.AreaId;
        var coordinates = _nextNewRoomAreaId.HasValue
            ? new MapCoordinates(0, 0, 0)
            : Offset(origin.Coordinates, direction, Step);
        _nextNewRoomAreaId = null;
        var userData = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["vnum"] = JsonSerializer.SerializeToElement(snapshot.Vnum),
        };
        if (!string.IsNullOrWhiteSpace(snapshot.Sector))
        {
            userData["sector"] = JsonSerializer.SerializeToElement(Normalize(snapshot.Sector));
        }

        return new MapRoom
        {
            Id = Document.Areas.SelectMany(area => area.Rooms).Select(room => room.Id).DefaultIfEmpty().Max() + 1,
            AreaId = areaId,
            Name = snapshot.Name,
            Coordinates = coordinates,
            Weight = 1,
            Exits = [],
            UserData = userData,
        };
    }

    private MapRoom CreateStartingRoom(RoomSnapshot snapshot, int areaId)
    {
        var coordinates = new MapCoordinates(0, 0, 0);
        var occupied = Document.Areas
            .First(area => area.Id == areaId)
            .Rooms
            .Select(room => room.Coordinates)
            .ToHashSet();
        while (occupied.Contains(coordinates))
        {
            coordinates = coordinates with { X = coordinates.X + Step };
        }

        var userData = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["vnum"] = JsonSerializer.SerializeToElement(snapshot.Vnum),
        };
        if (!string.IsNullOrWhiteSpace(snapshot.Sector))
        {
            userData["sector"] = JsonSerializer.SerializeToElement(Normalize(snapshot.Sector));
        }

        return new MapRoom
        {
            Id = Document.Areas.SelectMany(area => area.Rooms).Select(room => room.Id).DefaultIfEmpty().Max() + 1,
            AreaId = areaId,
            Name = snapshot.Name,
            Coordinates = coordinates,
            Weight = 1,
            Exits = [],
            UserData = userData,
        };
    }

    private static MapRoom BuildRoomWithMetadata(MapRoom room, RoomSnapshot snapshot)
    {
        var userData = CloneUserData(room.UserData);
        userData["vnum"] = JsonSerializer.SerializeToElement(snapshot.Vnum);
        if (!string.IsNullOrWhiteSpace(snapshot.Sector))
        {
            userData["sector"] = JsonSerializer.SerializeToElement(Normalize(snapshot.Sector));
        }

        return CloneRoom(room, name: snapshot.Name ?? room.Name, userData: userData);
    }

    private static MapRoom Connect(MapRoom from, MapRoom to, string direction, string command, bool hasDoor)
    {
        var exits = from.Exits.ToList();
        var currentTarget = FindMappedTarget(from, direction, command);
        if (currentTarget is null)
        {
            var normalizedCommand = Normalize(command);
            var storedCommand = CanonicalShort(normalizedCommand) is null
                ? normalizedCommand
                : FullDirections[direction];
            exits.Add(new MapExit
            {
                ExitId = to.Id,
                Name = storedCommand,
                Door = hasDoor ? "closed" : null,
            });
        }

        var userData = CloneUserData(from.UserData);
        var metadata = JsonSerializer.Serialize(new { command = Normalize(command), id = to.Id });
        userData[direction] = JsonSerializer.SerializeToElement(metadata);
        return CloneRoom(from, exits: exits, userData: userData);
    }

    private static int? FindMappedTarget(MapRoom room, string direction, string command)
    {
        if (room.UserData is not null && room.UserData.TryGetValue(direction, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var metadata = JsonDocument.Parse(value.GetString()!);
                if (metadata.RootElement.TryGetProperty("id", out var id) && id.TryGetInt32(out var parsed))
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // Malformed legacy metadata is ignored and the regular exit list remains usable.
            }
        }

        var normalizedCommand = Normalize(command);
        return room.Exits.FirstOrDefault(exit =>
            Normalize(exit.Name ?? string.Empty) == normalizedCommand ||
            CanonicalShort(exit.Name ?? string.Empty) == direction)?.ExitId;
    }

    private static int? FindTargetInMetadata(JsonElement metadataElement)
    {
        if (metadataElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            using var metadata = JsonDocument.Parse(metadataElement.GetString()!);
            return metadata.RootElement.TryGetProperty("id", out var id) && id.TryGetInt32(out var parsed)
                ? parsed
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MapRoom Disconnect(MapRoom room, string direction, string command)
    {
        var targetId = FindMappedTarget(room, direction, command);
        var normalizedCommand = Normalize(command);
        var exits = room.Exits.Where(exit =>
                targetId.HasValue
                    ? exit.ExitId != targetId.Value
                    : Normalize(exit.Name ?? string.Empty) != normalizedCommand &&
                      CanonicalShort(exit.Name ?? string.Empty) != direction)
            .ToArray();
        var userData = CloneUserData(room.UserData);
        userData.Remove(direction);
        return CloneRoom(room, exits: exits, userData: userData);
    }

    private static MapRoom DisconnectFromTarget(MapRoom room, string direction, int targetId)
    {
        var exits = room.Exits.Where(exit => exit.ExitId != targetId).ToArray();
        var userData = CloneUserData(room.UserData);
        if (FindTargetInMetadata(userData.GetValueOrDefault(direction)) == targetId)
        {
            userData.Remove(direction);
        }

        return CloneRoom(room, exits: exits, userData: userData);
    }

    private static MapCoordinates Offset(MapCoordinates origin, string direction, int step) => direction switch
    {
        "n" => origin with { Y = origin.Y + step },
        "ne" => new MapCoordinates(origin.X + step, origin.Y + step, origin.Z),
        "e" => origin with { X = origin.X + step },
        "se" => new MapCoordinates(origin.X + step, origin.Y - step, origin.Z),
        "s" => origin with { Y = origin.Y - step },
        "sw" => new MapCoordinates(origin.X - step, origin.Y - step, origin.Z),
        "w" => origin with { X = origin.X - step },
        "nw" => new MapCoordinates(origin.X - step, origin.Y + step, origin.Z),
        "u" => origin with { Z = origin.Z + 1 },
        "d" => origin with { Z = origin.Z - 1 },
        _ => origin,
    };

    private MapRoom? FindRoomByVnum(string vnum) =>
        Document.Areas.SelectMany(area => area.Rooms)
            .FirstOrDefault(room => string.Equals(room.Vnum, vnum, StringComparison.Ordinal));

    private MapRoom? FindRoomById(int id) =>
        Document.Areas.SelectMany(area => area.Rooms).FirstOrDefault(room => room.Id == id);

    private MapRoom? GetCurrentRoom() =>
        _lastSnapshot is null ? null : FindRoomByVnum(_lastSnapshot.Vnum);

    private bool ShouldMoveKnownRoomToTargetArea(MapRoom room) =>
        MoveKnownRoomsToTargetArea
        && _targetAreaId is { } targetAreaId
        && room.AreaId != targetAreaId
        && room.Id != _mappingStartRoomId;

    private string GetAreaName(int areaId) =>
        Document.Areas.First(area => area.Id == areaId).Name ?? $"#{areaId}";

    private void ClearMissingTargetArea()
    {
        if (_targetAreaId is { } targetAreaId && Document.Areas.All(area => area.Id != targetAreaId))
        {
            _targetAreaId = null;
            MoveKnownRoomsToTargetArea = false;
        }
    }

    private static MapDocument AddRoom(MapDocument document, MapRoom room) => new()
    {
        AnonymousAreaName = document.AnonymousAreaName,
        Areas = document.Areas.Select(area => area.Id == room.AreaId
            ? new MapArea { Id = area.Id, Name = area.Name, Rooms = [.. area.Rooms, room], Labels = area.Labels }
            : area).ToArray(),
    };

    private static MapDocument MoveRoomToArea(MapDocument document, MapRoom movedRoom) => new()
    {
        AnonymousAreaName = document.AnonymousAreaName,
        Areas = document.Areas.Select(area => new MapArea
        {
            Id = area.Id,
            Name = area.Name,
            Rooms = area.Id == movedRoom.AreaId
                ? [.. area.Rooms.Where(room => room.Id != movedRoom.Id), movedRoom]
                : area.Rooms.Where(room => room.Id != movedRoom.Id).ToArray(),
            Labels = area.Labels,
        }).ToArray(),
    };

    private static MapDocument ReplaceRoom(MapDocument document, MapRoom replacement) => new()
    {
        AnonymousAreaName = document.AnonymousAreaName,
        Areas = document.Areas.Select(area => area.Id == replacement.AreaId
            ? new MapArea
            {
                Id = area.Id,
                Name = area.Name,
                Rooms = area.Rooms.Select(room => room.Id == replacement.Id ? replacement : room).ToArray(),
                Labels = area.Labels,
            }
            : area).ToArray(),
    };

    private static MapDocument ReplaceArea(MapDocument document, MapArea replacement) => new()
    {
        AnonymousAreaName = document.AnonymousAreaName,
        Areas = document.Areas.Select(area => area.Id == replacement.Id ? replacement : area).ToArray(),
    };

    private static MapRoom CloneRoom(
        MapRoom room,
        string? name = null,
        IReadOnlyList<MapExit>? exits = null,
        IReadOnlyDictionary<string, JsonElement>? userData = null,
        MapCoordinates? coordinates = null,
        double? weight = null,
        int? areaId = null) => new()
    {
        Id = room.Id,
        AreaId = areaId ?? room.AreaId,
        Name = name ?? room.Name,
        Coordinates = coordinates ?? room.Coordinates,
        Environment = room.Environment,
        Weight = weight ?? room.Weight,
        Symbol = room.Symbol,
        Exits = exits ?? room.Exits,
        UserData = userData ?? room.UserData,
    };

    private static MapRoom CloneRoomWithSymbol(MapRoom room, string? symbol) => new()
    {
        Id = room.Id,
        AreaId = room.AreaId,
        Name = room.Name,
        Coordinates = room.Coordinates,
        Environment = room.Environment,
        Weight = room.Weight,
        Symbol = symbol,
        Exits = room.Exits,
        UserData = room.UserData,
    };

    private static Dictionary<string, JsonElement> CloneUserData(IReadOnlyDictionary<string, JsonElement>? source)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (source is not null)
        {
            foreach (var pair in source)
            {
                result[pair.Key] = pair.Value.Clone();
            }
        }

        return result;
    }

    private void RecordUndo(MapDocument document)
    {
        _undo.Push(document);
        _redo.Clear();
    }

    private static bool RoomMetadataEquals(MapRoom left, MapRoom right) =>
        string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
        string.Equals(left.Vnum, right.Vnum, StringComparison.Ordinal) &&
        string.Equals(left.Sector, right.Sector, StringComparison.Ordinal);

    private static bool ExitMatches(RoomSnapshotExit exit, string normalizedCommand)
    {
        var direction = CanonicalShort(exit.Direction);
        return Normalize(exit.Command) == normalizedCommand ||
               (direction is not null &&
                (Normalize(direction) == normalizedCommand || Normalize(FullDirections[direction]) == normalizedCommand));
    }

    private static string? CanonicalShort(string direction) =>
        DirectionAliases.GetValueOrDefault(Normalize(direction));

    private static bool IsSafeNonMovementCommand(string command)
    {
        var normalized = Normalize(command);
        return normalized is "l" or "look" or "map" or "who" or "redit" or "rplist" or "rpdump" or
                   "oplist" or "opdump" or "mplist" or "mpdump" or "purge" or "stat room" or "stat mob" ||
               normalized.StartsWith("open ", StringComparison.Ordinal) ||
               normalized.StartsWith("close ", StringComparison.Ordinal) ||
               normalized.StartsWith("say ", StringComparison.Ordinal) ||
               normalized.StartsWith("tell ", StringComparison.Ordinal) ||
               normalized.StartsWith("reply ", StringComparison.Ordinal) ||
               normalized.StartsWith("imm ", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var formD = value.Trim().ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);
        foreach (var character in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record PendingMovement(
        int OriginRoomId,
        string OriginVnum,
        string Direction,
        string Command,
        bool HasDoor,
        bool IsClosed);

    private sealed record MappingConflict(PendingMovement Movement, RoomSnapshot Snapshot);
}
