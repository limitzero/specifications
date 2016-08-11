using XUnit;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
public abstract class specification : specification_context
{
    [Fact]
    public void execute()
    {
        base.execute_context();
    }

    public override void fail_context()
    {
        Assert.True(false);
    }
}