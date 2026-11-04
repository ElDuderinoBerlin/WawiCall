using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperCallRepository(DatabaseRepositoryContext context) : ICallRepository
{
    public Task<StoredCall?> GetAsync(Guid callId, CancellationToken cancellationToken = default)
    {
        var id = DatabaseId.Encode(callId);
        return context.RunAsync(false, db => LoadAsync(db, id), cancellationToken);
    }

    public Task<Page<CallSnapshot>> GetPageAsync(CallHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query); ArgumentNullException.ThrowIfNull(query.Page);
        if (query.Company is { } company) StorageContract.CompanyKey(company);
        if (query.Party is { } party) StorageContract.PartyKey(party);
        if (query.Company is not null && query.Party is not null && query.Company != query.Party.Company) throw new ArgumentException("Contradictory company filters.", nameof(query));
        RepositoryData.Bounds(query.StartedFromUtc, query.StartedToUtc);
        if (query.WorkstationId is { } workstation) _ = DatabaseId.Encode(workstation);
        StorageContract.Text(query.MachineName, 256, nameof(query.MachineName), required: query.MachineName is not null);
        StorageContract.Text(query.WindowsUserName, 512, nameof(query.WindowsUserName), required: query.WindowsUserName is not null);
        return context.RunAsync(false, async db =>
        {
            var where = new List<string> { "1=1" };
            string? companyId = null;
            if (query.Company is not null)
            {
                var owner = await RepositoryData.FindCompanyAsync(db, query.Company).ConfigureAwait(false);
                if (owner is null) return new Page<CallSnapshot>([], false);
                companyId = owner.Text("id"); where.Add("c.companyid=@company");
            }
            if (query.Party is not null) where.Add($"EXISTS(SELECT 1 FROM {db.T("callparties")} p WHERE p.callid=c.id AND p.partykeyhash=@hash)");
            if (query.StartedFromUtc is not null) where.Add("c.startedatutc>=@from");
            if (query.StartedToUtc is not null) where.Add("c.startedatutc<@to");
            if (query.WorkstationId is not null) where.Add("c.workstationid=@workstation");
            if (query.MachineName is not null) where.Add(db.Ordinal("c.machinename", "@machine"));
            if (query.WindowsUserName is not null) where.Add(db.Ordinal("c.windowsusername", "@user"));
            var rows = await db.RowsAsync(db.PageSql($"SELECT c.* FROM {db.T("phonecalls")} c WHERE {string.Join(" AND ", where)}", "c.startedatutc DESC,c.id ASC", query.Page),
                new { company = companyId, hash = query.Party is null ? null : SourceIdentityHash.Party(query.Party), from = query.StartedFromUtc?.Ticks,
                    to = query.StartedToUtc?.Ticks, workstation = query.WorkstationId is { } key ? DatabaseId.Encode(key) : null, machine = query.MachineName, user = query.WindowsUserName }).ConfigureAwait(false);
            var result = new List<CallSnapshot>();
            foreach (var row in rows)
            {
                if (query.Party is not null)
                {
                    var candidates = await CandidatesAsync(db, row.Text("id")).ConfigureAwait(false);
                    if (!candidates.Any(candidate => candidate.Party == query.Party)) RepositoryData.Collision();
                }
                result.Add(await SnapshotAsync(db, row).ConfigureAwait(false));
            }
            return RepositorySession.Page(result, query.Page);
        }, cancellationToken);
    }

    public Task<StoredCall> WriteAsync(CallWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db =>
        {
            var call = write.Call; var id = DatabaseId.Encode(call.Id);
            var row = await db.OneAsync($"SELECT * FROM {db.T("phonecalls")} WHERE id=@id", new { id }).ConfigureAwait(false);
            RepositorySession.Expected(row, write.ExpectedVersion);
            var old = row is null ? null : await LoadAsync(db, id).ConfigureAwait(false);
            if (old is not null) ValidateChange(old.History.Call, call, write.Events.Where(entry => !old.History.Events.Any(previous => previous.Id == entry.Id)).ToImmutableArray());
            var candidates = MergeCandidates(old?.History.Parties ?? [], write.Parties);
            StorageContract.Call(call, candidates, write.Events);
            var lookup = write.Lookup ?? old?.History.Lookup;
            if (old?.History.Lookup is { } previous && lookup is not null &&
                RepositoryData.EncodeLookup(previous with { SelectionReason = lookup.SelectionReason }) != RepositoryData.EncodeLookup(lookup))
                throw new RepositoryConflictException(RepositoryConflictKind.AppendConflict);
            var company = call.Company is null ? null : await RepositoryData.RequireCompanyAsync(db, call.Company).ConfigureAwait(false);
            var version = row is null ? 1 : RepositorySession.Next(row.Long("writeversion"));
            var values = Values(call, company?.Text("id"), version);
            // Remove selection references before changing the assigned company; the entire operation is one transaction.
            if (row is not null) await db.ExecuteAsync($"DELETE FROM {db.T("callparties")} WHERE callid=@id", new { id }).ConfigureAwait(false);
            if (row is null) { values.Add("id", id); await db.InsertAsync("phonecalls", values).ConfigureAwait(false); }
            else await db.UpdateAsync("phonecalls", "id", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
            var position = 0;
            foreach (var candidate in candidates)
            {
                var owner = await RepositoryData.RequireCompanyAsync(db, candidate.Party.Company).ConfigureAwait(false);
                await db.InsertAsync("callparties", new()
                {
                    ["id"] = DatabaseId.Encode(Guid.NewGuid()), ["callid"] = id, ["companyid"] = owner.Text("id"),
                    ["assignedcompanyid"] = candidate.IsSelected ? owner.Text("id") : null, ["partykeyhash"] = SourceIdentityHash.Party(candidate.Party),
                    ["kind"] = (int)candidate.Party.Kind, ["externalid"] = candidate.Party.ExternalId, ["ownerexternalid"] = candidate.Party.OwnerExternalId,
                    ["displayname"] = candidate.DisplayName, ["number"] = candidate.Number, ["companydisplayname"] = candidate.CompanyDisplayName,
                    ["isselected"] = candidate.IsSelected, ["position"] = position++, ["matchkind"] = candidate.Match is null ? 0 : (int)candidate.Match.Kind,
                    ["startwidth"] = candidate.Match?.StartWidth, ["apparatusnumber"] = candidate.Match?.ApparatusNumber, ["evidencejson"] = RepositoryData.EncodeMatch(candidate.Match)
                }).ConfigureAwait(false);
            }
            await AppendEventsAsync(db, id, old?.History.Events ?? [], write.Events).ConfigureAwait(false);
            if (lookup is not null)
            {
                await db.ExecuteAsync($"DELETE FROM {db.T("calllookups")} WHERE callid=@id", new { id }).ConfigureAwait(false);
                await db.InsertAsync("calllookups", new()
                {
                    ["callid"] = id, ["detailsjson"] = RepositoryData.EncodeLookup(lookup), ["formatversion"] = 1, ["parserversion"] = lookup.ParserVersion,
                    ["iscomplete"] = lookup.IsComplete, ["searchedatutc"] = DatabaseUtc.Encode(lookup.SearchedAtUtc), ["selectionreason"] = lookup.SelectionReason
                }).ConfigureAwait(false);
            }
            return (await LoadAsync(db, id).ConfigureAwait(false))!;
        }, cancellationToken);
    }

    private static ImmutableArray<CallPartySnapshot> MergeCandidates(ImmutableArray<CallPartySnapshot> previous, ImmutableArray<CallPartySnapshot> incoming)
    {
        var wanted = incoming.ToDictionary(candidate => candidate.Party);
        var result = ImmutableArray.CreateBuilder<CallPartySnapshot>();
        foreach (var candidate in previous)
        {
            if (wanted.Remove(candidate.Party, out var update))
            {
                if (candidate with { IsSelected = update.IsSelected } != update) throw new RepositoryConflictException(RepositoryConflictKind.AppendConflict);
                result.Add(update);
            }
            else result.Add(candidate with { IsSelected = false });
        }
        result.AddRange(incoming.Where(candidate => wanted.ContainsKey(candidate.Party)));
        return result.ToImmutable();
    }

    private static void ValidateChange(CallSnapshot old, CallSnapshot next, ImmutableArray<CallEvent> events)
    {
        if (old.Origin != next.Origin || old.Direction != next.Direction || old.StartedAtUtc != next.StartedAtUtc)
            throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
        if (next.Revision < old.Revision) throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
        if (next.Revision == old.Revision)
        {
            if (old with { Company = next.Company } != next) throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
            return;
        }
        var machine = new CallStateMachine(old);
        // A persistence batch can contain several accepted transitions. Event sequence and domain revision remain separate.
        foreach (var entry in events.Where(entry => entry.OccurredAtUtc >= old.StateChangedAtUtc && entry.OccurredAtUtc < next.StateChangedAtUtc && entry.State != machine.Current.State))
        {
            if (machine.Current.Revision >= next.Revision - 1 || machine.Apply(new(machine.Current.Revision + 1, entry.State, entry.OccurredAtUtc)) != CallChangeResult.Applied)
                throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
        }
        if (machine.Apply(new(next.Revision, next.State, next.StateChangedAtUtc, next.EndReason)) != CallChangeResult.Applied ||
            machine.Current with { Company = next.Company, LocalAddress = next.LocalAddress, RemoteAddress = next.RemoteAddress } != next)
            throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
    }

    private static async Task AppendEventsAsync(RepositorySession db, string call, ImmutableArray<CallEvent> existing, ImmutableArray<CallEvent> incoming)
    {
        var sequence = existing.IsEmpty ? 0 : existing[^1].Sequence;
        var time = existing.IsEmpty ? DateTimeOffset.MinValue : existing[^1].OccurredAtUtc;
        foreach (var entry in incoming)
        {
            var id = DatabaseId.Encode(entry.Id);
            var row = await db.OneAsync($"SELECT * FROM {db.T("callevents")} WHERE id=@id", new { id }).ConfigureAwait(false);
            if (row is not null)
            {
                if (Event(row) != entry) throw new RepositoryConflictException(RepositoryConflictKind.AppendConflict);
                continue;
            }
            if (entry.Sequence <= sequence || entry.OccurredAtUtc < time) throw new RepositoryConflictException(RepositoryConflictKind.AppendConflict);
            await db.InsertAsync("callevents", new() { ["id"] = id, ["callid"] = call, ["sequence"] = entry.Sequence, ["occurredatutc"] = DatabaseUtc.Encode(entry.OccurredAtUtc), ["code"] = entry.Code, ["state"] = (int)entry.State }).ConfigureAwait(false);
            sequence = entry.Sequence; time = entry.OccurredAtUtc;
        }
    }

    private static Dictionary<string, object?> Values(CallSnapshot call, string? company, long version) => new()
    {
        ["companyid"] = company, ["writeversion"] = version, ["revision"] = call.Revision, ["workstationid"] = DatabaseId.Encode(call.Origin!.WorkstationId),
        ["machinename"] = call.Origin.MachineName, ["windowsusername"] = call.Origin.WindowsUserName, ["direction"] = (int)call.Direction, ["state"] = (int)call.State,
        ["localpresentation"] = (int)call.LocalAddress.Presentation, ["localoriginalvalue"] = call.LocalAddress.OriginalValue, ["locale164"] = call.LocalAddress.E164, ["localextension"] = call.LocalAddress.Extension,
        ["remotepresentation"] = (int)call.RemoteAddress.Presentation, ["remoteoriginalvalue"] = call.RemoteAddress.OriginalValue, ["remotee164"] = call.RemoteAddress.E164, ["remoteextension"] = call.RemoteAddress.Extension,
        ["startedatutc"] = DatabaseUtc.Encode(call.StartedAtUtc), ["statechangedatutc"] = DatabaseUtc.Encode(call.StateChangedAtUtc),
        ["answeredatutc"] = call.AnsweredAtUtc?.Ticks, ["endedatutc"] = call.EndedAtUtc?.Ticks, ["endreason"] = (int?)call.EndReason
    };

    private static async Task<CallSnapshot> SnapshotAsync(RepositorySession db, RepositoryRow row) => new()
    {
        Id = row.Id("id"), Company = row.OptionalText("companyid") is { } id ? (await RepositoryData.CompanyByIdAsync(db, id).ConfigureAwait(false)).Key : null,
        Revision = row.Long("revision"), Origin = new(row.Id("workstationid"), row.Text("machinename"), row.Text("windowsusername")),
        Direction = (CallDirection)checked((int)row.Long("direction")), State = (CallState)checked((int)row.Long("state")),
        LocalAddress = new((NumberPresentation)checked((int)row.Long("localpresentation")), row.OptionalText("localoriginalvalue"), row.OptionalText("locale164"), row.OptionalText("localextension")),
        RemoteAddress = new((NumberPresentation)checked((int)row.Long("remotepresentation")), row.OptionalText("remoteoriginalvalue"), row.OptionalText("remotee164"), row.OptionalText("remoteextension")),
        StartedAtUtc = row.Utc("startedatutc"), StateChangedAtUtc = row.Utc("statechangedatutc"), AnsweredAtUtc = row.OptionalUtc("answeredatutc"), EndedAtUtc = row.OptionalUtc("endedatutc"), EndReason = (CallEndReason?)row.OptionalLong("endreason")
    };

    private static async Task<ImmutableArray<CallPartySnapshot>> CandidatesAsync(RepositorySession db, string id)
    {
        var rows = await db.RowsAsync($"SELECT * FROM {db.T("callparties")} WHERE callid=@id ORDER BY position", new { id }).ConfigureAwait(false);
        var result = ImmutableArray.CreateBuilder<CallPartySnapshot>();
        foreach (var row in rows)
        {
            var company = await RepositoryData.CompanyByIdAsync(db, row.Text("companyid")).ConfigureAwait(false);
            var key = RepositoryData.PartyKey(row, company.Key);
            if (SourceIdentityHash.Party(key) != row.Text("partykeyhash")) RepositoryData.Collision();
            result.Add(new(key, row.Text("displayname"), row.OptionalText("number"), row.Bool("isselected")) { CompanyDisplayName = row.OptionalText("companydisplayname"), Match = RepositoryData.DecodeMatch(row) });
        }
        return result.ToImmutable();
    }

    private static CallEvent Event(RepositoryRow row) => new(row.Id("id"), row.Id("callid"), row.Long("sequence"), row.Utc("occurredatutc"), row.Text("code"), (CallState)checked((int)row.Long("state")));

    private static async Task<StoredCall?> LoadAsync(RepositorySession db, string id)
    {
        var row = await db.OneAsync($"SELECT * FROM {db.T("phonecalls")} WHERE id=@id", new { id }).ConfigureAwait(false);
        if (row is null) return null;
        var call = await SnapshotAsync(db, row).ConfigureAwait(false);
        var candidates = await CandidatesAsync(db, id).ConfigureAwait(false);
        var events = (await db.RowsAsync($"SELECT * FROM {db.T("callevents")} WHERE callid=@id ORDER BY {db.Q("sequence")}", new { id }).ConfigureAwait(false)).Select(Event).ToImmutableArray();
        StorageContract.Call(call, candidates, events);
        var notes = (await db.RowsAsync($"SELECT * FROM {db.T("callnotes")} WHERE callid=@id ORDER BY createdatutc,id", new { id }).ConfigureAwait(false)).Select(row => DapperCallNoteRepository.Read(row).Note).ToImmutableArray();
        var recordings = (await db.RowsAsync($"SELECT * FROM {db.T("recordings")} WHERE callid=@id ORDER BY id", new { id }).ConfigureAwait(false)).Select(row => DapperRecordingRepository.Read(row).Recording).ToImmutableArray();
        var lookup = await db.OneAsync($"SELECT * FROM {db.T("calllookups")} WHERE callid=@id", new { id }).ConfigureAwait(false);
        return new(new() { Call = call, Parties = candidates, Events = events, Notes = notes, Recordings = recordings, Lookup = lookup is null ? null : RepositoryData.DecodeLookup(lookup) }, row.Long("writeversion"));
    }
}
