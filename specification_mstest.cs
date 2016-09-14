using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;


// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming
[TestClass]
public abstract class specification : specification_context
{
    [TestMethod]
    public void execute()
    {
        base.execute_context();
    }

    public override void fail_context()
    {
        Assert.Fail();
    }
}