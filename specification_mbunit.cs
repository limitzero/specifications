using MbUnit.Framework;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
[TestFixture]
public abstract class specification : specification_context
{
    [Test]
    public void execute()
    {
        base.execute_context();
    }

    public override void fail_context()
    {
        Assert.Fail();
    }
}