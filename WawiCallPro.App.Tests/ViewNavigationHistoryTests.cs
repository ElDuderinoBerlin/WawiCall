using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class ViewNavigationHistoryTests
{
    [TestMethod]
    public void BackAndForwardPreserveTheBranchAndReactivationDoesNotAddEntries()
    {
        var history = new ViewNavigationHistory<object>(); var first = new object(); var second = new object(); var third = new object();
        Assert.IsFalse(history.CanGoBack); Assert.IsFalse(history.CanGoForward); Assert.IsNull(history.GoBack()); Assert.IsNull(history.GoForward());
        history.Record(first); history.Record(second); history.Record(third); Assert.AreSame(second, history.GoBack());
        history.Record(second); Assert.IsTrue(history.CanGoForward); Assert.AreSame(first, history.GoBack());
        history.Record(first); Assert.IsFalse(history.CanGoBack); Assert.AreSame(second, history.GoForward()); Assert.AreSame(third, history.GoForward());
        Assert.IsFalse(history.CanGoForward);
    }

    [TestMethod]
    public void ANewVisitAfterGoingBackDropsOnlyTheForwardBranch()
    {
        var history = new ViewNavigationHistory<object>(); var first = new object(); var second = new object(); var third = new object(); var other = new object();
        history.Record(first); history.Record(second); history.Record(third); history.GoBack(); history.Record(other);
        Assert.IsFalse(history.CanGoForward); Assert.AreSame(second, history.GoBack()); Assert.AreSame(first, history.GoBack());
        Assert.AreSame(second, history.GoForward()); Assert.AreSame(other, history.GoForward());
    }

    [TestMethod]
    public void ClosedViewsAreRemovedEverywhereAndConsecutiveDuplicatesCollapse()
    {
        var history = new ViewNavigationHistory<object>(); var first = new object(); var second = new object();
        history.Record(first); history.Record(second); history.Record(first); history.Record(second); history.Record(first);
        history.Remove(second); Assert.IsFalse(history.CanGoBack); Assert.IsFalse(history.CanGoForward);
        history.Remove(first); Assert.IsNull(history.GoBack()); Assert.IsNull(history.GoForward());
        history.Record(second); Assert.IsFalse(history.CanGoBack);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void NeighborActivationBeforeOrAfterClosingTheCurrentViewLeavesTheSameHistory(bool activatedFirst)
    {
        var history = new ViewNavigationHistory<object>(); var first = new object(); var closing = new object(); var neighbor = new object();
        history.Record(first); history.Record(neighbor); history.Record(closing);
        if (activatedFirst) history.Record(neighbor);
        history.Remove(closing);
        if (!activatedFirst) history.Record(neighbor);
        Assert.AreSame(first, history.GoBack()); Assert.AreSame(neighbor, history.GoForward()); Assert.IsFalse(history.CanGoForward);
    }
}
