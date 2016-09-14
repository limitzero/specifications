using System.Diagnostics;
using Xunit;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
// use with version 1.92 and lower as higner versions suppress output to the trace listners (i.e. console)
public abstract class specification : specification_context
{
    // Xunit suppresses console output on testing, need to add listner for text output
    protected specification()
    {
        Debug.Listeners.Add(new DefaultTraceListener());
    }

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