using MMS.Office.Outlook;

namespace OutlookImport.Tests;

[TestClass]
public sealed class ImportTests
{
    [TestMethod]
    public void NullFiltersReadAllWithoutResolvingAddressesAndReleaseEveryMessage()
    {
        var one = new Message("one"); var two = new Message("two");
        using var folder = new Folder("store", "inbox", one, two);
        var reader = Reader(); reader.Read(folder);
        CollectionAssert.AreEqual(new[] { "one", "two" }, reader.Result.ToArray());
        Assert.AreEqual(0, one.AddressReads); Assert.AreEqual(1, one.Disposals); Assert.AreEqual(1, two.Disposals);
        Assert.IsFalse(one.IncludedAttachments); Assert.IsTrue(folder.MessagesEnumeratorClosed);
        Assert.AreEqual(0, folder.Disposals, "The caller owns the root.");
    }

    [TestMethod]
    public void ExplicitEmptyFilterDoesNotReadOrExposeAnyMail()
    {
        using var folder = new Folder("store", "inbox", new Message("private"));
        var reader = Reader([]); reader.Read(folder);
        Assert.IsEmpty(reader.Result); Assert.AreEqual(0, folder.MessageReads);
    }

    [TestMethod]
    public void MatchingIsExactCaseInsensitiveAndMaterializesOnceWithFirstMatchingTag()
    {
        var hit = new Message("hit") { Sender = " CUSTOMER@example.test ", Recipients = ["customer@example.test", "other@example.test"] };
        var other = new Message("other") { Sender = "prefix-customer@example.test" };
        using var folder = new Folder("store", "inbox", hit, other);
        var reader = Reader([new("customer@example.test", MailAddressType.Sender, "sender"),
            new("CUSTOMER@example.test", MailAddressType.Sender, "duplicate"), new("customer@example.test", MailAddressType.Receiver, "recipient")], attachments: true);
        reader.Read(folder);
        CollectionAssert.AreEqual(new[] { "hit" }, reader.Result.ToArray());
        Assert.AreEqual(1, hit.Materializations); Assert.AreEqual("sender", hit.Tag); Assert.IsTrue(hit.IncludedAttachments);
        Assert.AreEqual(0, other.Materializations); Assert.AreEqual(1, other.Disposals);
    }

    [TestMethod]
    public void RecipientOnlyMatchesOnceAndRetainsFirstMatchingFilterTag()
    {
        var mail = new Message("mail") { Recipients = ["FIRST@example.test", "second@example.test"] };
        using var folder = new Folder("store", "sent", mail);
        var reader = Reader([new("first@example.test", MailAddressType.Receiver, "first"), new("second@example.test", MailAddressType.Receiver, "second")]);
        reader.Read(folder); Assert.HasCount(1, reader.Result); Assert.AreEqual("first", mail.Tag); Assert.AreEqual(1, mail.Materializations);
    }

    [TestMethod]
    public void OverlappingFoldersAndRepeatedEntriesAreDeduplicatedWithinTheirStore()
    {
        var first = new Message("same"); var duplicate = new Message("same"); var otherStore = new Message("same");
        using var root = new Folder("A", "root", first);
        var child = new Folder("A", "child", duplicate); root.Children.Add(child);
        using var overlapping = new Folder("A", "child", new Message("unreachable"));
        using var second = new Folder("B", "root", otherStore);
        var reader = Reader(); reader.Read(root); reader.Read(overlapping); reader.Read(second);
        Assert.HasCount(2, reader.Result); Assert.AreEqual(0, duplicate.Materializations); Assert.AreEqual(1, duplicate.Disposals);
        Assert.AreEqual(0, overlapping.MessageReads); Assert.AreEqual(1, child.Disposals); Assert.AreEqual(1, otherStore.Materializations);
    }

    [TestMethod]
    public void NonrecursiveReadDoesNotOpenChildren()
    {
        using var root = new Folder("A", "root", new Message("root-mail")); root.Children.Add(new("A", "child", new Message("child-mail")));
        var reader = Reader(recursive: false); reader.Read(root);
        Assert.HasCount(1, reader.Result); Assert.AreEqual(0, root.ChildrenReads);
    }

    [TestMethod]
    public void FolderCyclesDoNotLoop()
    {
        using var root = new Folder("A", "root", new Message("one"));
        var cycle = new Folder("A", "root", new Message("two")); root.Children.Add(cycle);
        var reader = Reader(); reader.Read(root);
        Assert.HasCount(1, reader.Result); Assert.AreEqual(0, cycle.MessageReads); Assert.AreEqual(1, cycle.Disposals);
    }

    [TestMethod]
    public void ExactDateBoundsAreCheckedAfterTheMinutePrecisionSourceQuery()
    {
        var start = new DateTime(2026, 11, 14, 10, 0, 20); var end = start.AddSeconds(30);
        using var folder = new Folder("A", "root", new Message("before") { ReceivedTime = start.AddSeconds(-1) },
            new Message("start") { ReceivedTime = start }, new Message("end") { ReceivedTime = end }, new Message("after") { ReceivedTime = end.AddSeconds(1) });
        var reader = Reader(from: start, to: end); reader.Read(folder);
        CollectionAssert.AreEqual(new[] { "start", "end" }, reader.Result.ToArray()); Assert.AreEqual(start, folder.From); Assert.AreEqual(end, folder.To);
    }

    [TestMethod]
    public void UtcBoundsAreConvertedToOutlookLocalTime()
    {
        var start = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        using var folder = new Folder("A", "root", new Message("match") { ReceivedTime = start.ToLocalTime() });
        var reader = Reader(from: start, to: start); reader.Read(folder);
        Assert.HasCount(1, reader.Result); Assert.AreEqual(start.ToLocalTime(), folder.From);
    }

    [TestMethod]
    public void InvalidRequestsFailBeforeReading()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Reader(from: DateTime.Today.AddDays(1), to: DateTime.Today));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Reader(maximum: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Reader([new("a@example.test", (MailAddressType)99)]));
        Assert.ThrowsExactly<ArgumentException>(() => Reader([new(" ", MailAddressType.Sender)]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Reader([null!]));
    }

    [TestMethod]
    public void ConfiguredLimitFailsInsteadOfSilentlyTruncatingAndCountsUniqueMatchesOnly()
    {
        var rejected = new Message("next");
        using var folder = new Folder("A", "root", new Message("one"), new Message("one"), rejected);
        var reader = Reader(maximum: 1);
        Assert.ThrowsExactly<InvalidOperationException>(() => reader.Read(folder));
        Assert.HasCount(1, reader.Result); Assert.AreEqual(0, rejected.Materializations); Assert.AreEqual(1, rejected.Disposals);
        Assert.IsTrue(folder.MessagesEnumeratorClosed);
    }

    [TestMethod]
    public void ExactlyTheLimitIsASuccess()
    {
        using var folder = new Folder("A", "root", new Message("one")); var reader = Reader(maximum: 1); reader.Read(folder);
        Assert.HasCount(1, reader.Result);
    }

    [TestMethod]
    public void CancellationDuringMaterializationDisposesHandlesAndDoesNotPublishLateResult()
    {
        using var cancellation = new CancellationTokenSource();
        var first = new Message("one") { BeforeRead = cancellation.Cancel }; var next = new Message("two");
        using var folder = new Folder("A", "root", first, next); var reader = Reader(token: cancellation.Token);
        Assert.ThrowsExactly<OperationCanceledException>(() => reader.Read(folder));
        Assert.IsEmpty(reader.Result); Assert.AreEqual(1, first.Disposals); Assert.AreEqual(0, next.Materializations);
        Assert.IsTrue(folder.MessagesEnumeratorClosed);
    }

    [TestMethod]
    public void SourceErrorsAreObservableAndChildHandlesAreReleased()
    {
        var mail = new Message("broken") { BeforeRead = () => throw new InvalidOperationException("source-error") };
        using var root = new Folder("A", "root"); var child = new Folder("A", "child", mail); root.Children.Add(child);
        var reader = Reader(); var error = Assert.ThrowsExactly<InvalidOperationException>(() => reader.Read(root));
        Assert.AreEqual("source-error", error.Message); Assert.AreEqual(1, child.Disposals); Assert.AreEqual(1, mail.Disposals);
        Assert.IsTrue(root.ChildrenEnumeratorClosed); Assert.IsTrue(child.MessagesEnumeratorClosed);
    }

    [TestMethod]
    public void PrecancelledReadDoesNotTouchTheSource()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        using var folder = new Folder("A", "root"); var reader = Reader(token: cancellation.Token);
        Assert.ThrowsExactly<OperationCanceledException>(() => reader.Read(folder)); Assert.AreEqual(0, folder.MessageReads);
    }

    [TestMethod]
    public void MissingSourceIdentityFailsRatherThanCombiningUnrelatedMessages()
    {
        using var missingStore = new Folder("", "folder");
        Assert.ThrowsExactly<ArgumentException>(() => Reader().Read(missingStore));
        var invalid = new Message(""); using var missingEntry = new Folder("store", "folder", invalid);
        Assert.ThrowsExactly<ArgumentException>(() => Reader().Read(missingEntry)); Assert.AreEqual(1, invalid.Disposals);
    }

    [TestMethod]
    public void FilterCollectionIsSnapshottedBeforeTheWorkerCanStart()
    {
        var filters = new List<MailFilter> { new("old@example.test", MailAddressType.Sender) };
        var reader = Reader(filters); filters.Clear(); filters.Add(new("other@example.test", MailAddressType.Sender));
        using var root = new Folder("store", "folder", new Message("hit") { Sender = "old@example.test" });
        reader.Read(root); Assert.HasCount(1, reader.Result);
    }

    [TestMethod]
    public async Task WorkersUseStaAndExitBeforeReturningIncludingRepeatedReads()
    {
        for (var index = 0; index < 20; index++)
        {
            Thread? worker = null;
            var result = await MailImportWorker.RunAsync(_ => { worker = Thread.CurrentThread; Assert.AreEqual(ApartmentState.STA, worker.GetApartmentState()); return 42; }, default);
            Assert.AreEqual(42, result); Assert.IsFalse(worker!.IsAlive);
        }
    }

    [TestMethod]
    public async Task WorkerFailureIsObservableAndJoinsTheThread()
    {
        Thread? worker = null;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => MailImportWorker.RunAsync<int>(_ =>
            { worker = Thread.CurrentThread; throw new InvalidOperationException("source-error"); }, default));
        Assert.IsFalse(worker!.IsAlive);
    }

    [TestMethod]
    public async Task WorkerCancellationAwaitsNonCooperativeSourceBeforeReturning()
    {
        using var cancellation = new CancellationTokenSource(); using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); Thread? worker = null;
        var pending = MailImportWorker.RunAsync(_ => { worker = Thread.CurrentThread; entered.SetResult(); release.Wait(TimeSpan.FromSeconds(5)); return 42; }, cancellation.Token);
        try { await entered.Task; cancellation.Cancel(); Assert.IsFalse(pending.IsCompleted); }
        finally { release.Set(); }
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending); Assert.IsTrue(pending.IsCanceled); Assert.IsFalse(worker!.IsAlive);
    }

    [TestMethod]
    public async Task PrecancelledWorkerDoesNotStartWork()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var started = false;
        await Assert.ThrowsAsync<OperationCanceledException>(() => MailImportWorker.RunAsync(_ => { started = true; return 42; }, cancellation.Token));
        Assert.IsFalse(started);
    }

    private static MailImportReader<string> Reader(IEnumerable<MailFilter>? filters = null, bool recursive = true,
        DateTime? from = null, DateTime? to = null, bool attachments = false, int? maximum = null, CancellationToken token = default) =>
        new(filters, recursive, from, to, attachments, maximum, token);

    private sealed class Message(string id) : IMailImportMessage<string>
    {
        public string EntryId => id;
        public DateTime ReceivedTime { get; init; } = DateTime.Today;
        internal string? Sender;
        internal string[] Recipients = [];
        internal Action? BeforeRead;
        internal int Disposals, Materializations, AddressReads;
        internal bool IncludedAttachments;
        internal object? Tag;
        public string? GetSenderAddress() { AddressReads++; return Sender; }
        public IEnumerable<string> GetRecipientAddresses() { AddressReads++; return Recipients; }
        public string Materialize(bool includeAttachments, object? tag)
        { Materializations++; IncludedAttachments = includeAttachments; Tag = tag; BeforeRead?.Invoke(); return id; }
        public void Dispose() => Disposals++;
    }

    private sealed class Folder(string store, string id, params Message[] messages) : IMailImportFolder<string>
    {
        public string StoreId => store;
        public string FolderId => id;
        internal readonly List<Folder> Children = [];
        internal int Disposals, MessageReads, ChildrenReads;
        internal bool MessagesEnumeratorClosed, ChildrenEnumeratorClosed;
        internal DateTime? From, To;
        public IEnumerable<IMailImportFolder<string>> GetChildren()
        { ChildrenReads++; try { foreach (var child in Children) yield return child; } finally { ChildrenEnumeratorClosed = true; } }
        public IEnumerable<IMailImportMessage<string>> GetMessages(DateTime? from, DateTime? to)
        { MessageReads++; From = from; To = to; try { foreach (var mail in messages) yield return mail; } finally { MessagesEnumeratorClosed = true; } }
        public void Dispose() => Disposals++;
    }
}
