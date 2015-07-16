using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Linq;
/*
 * Notes:
 * On rasing events from Moq, please check the verification condition to be Times.AtLeastOnce() instead of Times.Once.
 */

/// <summary>
/// Attribute to skip a specification for testing.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class SkipAttribute : Attribute
{
}

/// <summary>
/// Attribute to tag test example method as the only one to test in a specification.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class TagAttribute : Attribute
{
    public string Name { get; private set; }

    public TagAttribute()
        : this(string.Empty)
    {
    }

    /// <summary>
    /// Sets the name of the tagged case for display in testing.
    /// </summary>
    /// <param name="name">Name to give to the tagged test example method for identification</param>
    public TagAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Represents an action that can only be executed once.
/// </summary>
public class invokable_action
{
    private readonly Action _action;
    public bool IsInvoked { get; private set; }

    public invokable_action(Action action)
    {
        _action = action;
    }

    public void Invoke()
    {
        if ( IsInvoked )
            return;

        if ( _action != null )
        {
            _action.DynamicInvoke();
        }

        IsInvoked = true;
    }

    public bool IsDefined()
    {
        return _action != null;
    }

    public bool IsDefinedBy(Action comparision)
    {
        return _action == comparision;
    }
}

// Resharper disable InconsistentNaming
/// <summary>
/// Represents a top-level class that holds all of the specifications for a given concern.
/// </summary>
public class test_scenario
{
    public string Name { get; set; }
    public List<test_example> Examples { get; set; }

    public test_scenario()
    {
        Examples = new List<test_example>();
    }
}

// Resharper disable InconsistentNaming
/// <summary>
/// Represents a method in the specifications that contains all of the invocations and assertions for specified conditions.
/// </summary>
public class test_example
{
    private readonly specification_context _context;
    private readonly MethodInfo _method;
    private readonly StringBuilder _verbalizer;
    public string Name { get; set; }
    public string Tag { get; set; }
    public bool IsSkipped { get; set; }
    public test_condition ExampleMethodAsTestCondition { get; set; }
    public List<MethodInfo> ActMethods { get; set; }
    public List<test_condition> Conditions { get; set; }
    public List<invokable_action> PreConditions { get; set; }
    public List<invokable_action> PostConditions { get; set; }

    public test_example(specification_context context, MethodInfo method, StringBuilder verbalizer)
    {
        _context = context;
        _method = method;
        _verbalizer = verbalizer;
        PreConditions = new List<invokable_action>();
        Conditions = new List<test_condition>();
        PostConditions = new List<invokable_action>();
    }

    public void execute(StringBuilder verbalizer)
    {
        evaluate_example();
        execute_example();
    }

    private void evaluate_example()
    {
        // clean pre and post actions for the example before inspection:
        _context.establish = null;
        _context.because = null;
        _context.cleanup = null;

        var testCaseName = specification_context.normalize(Name);

        try
        {
            _method.Invoke(_context, null);
        }
        catch
        {
            // there are some variables that are being examined that are not wrapped in a test 
            // condition or verify block (i.e. object null exceptions), send notice for all blocks to be
            // wrapped either in test condition or verify block for runner to properly examine the test case:
            var message = string.Format("The test case example method '{0}' has code blocks that are " +
                "not wrapped in the 'it' or 'verify' blocks where variables are being examined before the runner " +
                "can evaluate all conditions. Please enclose those code areas in either the 'it' named test condition block " +
                "or the 'verify' lambda block.", testCaseName);
            throw new InvalidOperationException(message);
        }

        if ( _context.verify == null )
        {
            // method used as test condition, record it as a test condition on the test example:
            var method_name_as_test_condition = new test_condition();
            method_name_as_test_condition[testCaseName] = () => _method.Invoke(_context, null);
            //this.ExampleMethodAsTestCondition = method_name_as_test_condition;
        }
        else
        {
            // method is the test example and the 'verify' lambda is the test condition:
            var verify_lambda_as_test_condition = new test_condition();
            verify_lambda_as_test_condition[testCaseName] = () => _context.verify();
            this.ExampleMethodAsTestCondition = verify_lambda_as_test_condition;
        }

        if ( _context.establish != null )
            PreConditions.Add(new invokable_action(new Action(_context.establish)));

        if ( _context.because != null )
            PreConditions.Add(new invokable_action(new Action(_context.because)));

        if ( _context.cleanup != null )
            PostConditions.Add(new invokable_action(new Action(_context.cleanup)));

        // gather any supporting named test conditions for the current test case example method:
        Conditions = new List<test_condition>(_context.get_test_conditions());

        _context.reset_test_example_conditions();
    }

    private void execute_example()
    {
        // check for both 'verify' and named test conditions present:
        if ( ExampleMethodAsTestCondition != null & Conditions.Any() )
        {
            // this is a no-op, should not construct test cases this way...
            var invalidTestStructureMessage =
                string.Format(
                    "For the current test example method '{0}', the testing scenario should not include a 'verify' and named test conditions (i.e. it[\"..\"])." +
                    " Please restructure the test scenario to use named test condition(s) or 'verify'.",
                    specification_context.normalize(Name));
            throw new InvalidOperationException(invalidTestStructureMessage);
        }

        PreConditions.ForEach(condition => condition.Invoke());

        if ( ActMethods != null && ActMethods.Any() )
            ActMethods.ForEach(am => am.Invoke(_context, null));

        // 'verify' used with no named test conditions:
        if ( ExampleMethodAsTestCondition != null )
        {
            examine_for_pass_or_failure(ExampleMethodAsTestCondition, _verbalizer, 1);
        }

        // just named test conditions, no 'verify'
        if ( Conditions.Any() )
        {
            var statement = specification_context.normalize(Name);
            _verbalizer.AppendFormat("\t{0}", statement).AppendLine();

            foreach ( var condition in Conditions )
            {
                examine_for_pass_or_failure(condition, _verbalizer);
            }
        }

        PostConditions.ForEach(condition => condition.Invoke());
    }

    private void examine_for_pass_or_failure(
        test_condition condition,
        StringBuilder verbalizer,
        int indentLevel = 2)
    {
        string message = string.Empty;
        string indent = string.Empty;

        Enumerable.Range(1, indentLevel)
            .ToList()
            .ForEach(i => indent += "\t");

        if ( IsSkipped )
        {
            message = string.Format("{0} : skipped", condition);
            verbalizer.AppendFormat("{0}{1}", indent, message).AppendLine();
            return;
        }

        if ( condition.IsActionDefinedBy(specification_context.todo) )
        {
            message = string.Format("{0} : pending", condition);
            verbalizer.AppendFormat("{0}{1}", indent, message).AppendLine();
            return;
        }

        try
        {
            if ( string.IsNullOrEmpty(condition.ToString()) == false )
                message = string.Format("{0} : passed", condition);
            condition.Invoke();
        }
        catch ( Exception testConditionFailureException )
        {
            if ( string.IsNullOrEmpty(condition.ToString()) == false )
                message = string.Format("{0} : failed", condition);
            condition.Failed(testConditionFailureException);
        }
        finally
        {
            if ( string.IsNullOrEmpty(message) == false )
                verbalizer.AppendFormat("{0}{1}", indent, message).AppendLine();
        }
    }
}

// Resharper disable InconsistentNaming
/// <summary>
/// Represents the individual test condition with invocation logic for determining the success or failure of a condition.
/// </summary>
public class test_condition
{
    private invokable_action _action;
    public string Name { get; private set; }
    public bool IsInvoked { get; private set; }
    public Exception Exception { get; private set; }

    public void Failed(Exception exception)
    {
        Exception = exception;
    }

    public bool IsActionDefined()
    {
        return _action.IsDefined();
    }

    public bool IsActionDefinedBy(Action comparision)
    {
        return _action.IsDefinedBy(comparision);
    }

    public void Invoke()
    {
        if ( IsInvoked )
            return;

        if ( _action != null )
        {
            _action.Invoke();
        }

        IsInvoked = true;
    }

    public Action this[string name]
    {
        set
        {
            Name = name;
            _action = new invokable_action(value);
        }
    }

    public override string ToString()
    {
        var builder = new StringBuilder();

        if ( Name.StartsWith("it") == false )
            Name = string.Format("it {0}", Name);

        builder.AppendFormat("{0}", Name);
        return builder.ToString();
    }
}

// Resharper disable InconsistentNaming
/// <summary>
/// Base class for creating test scenarios around a central theme or context
/// </summary>
public abstract class specification_context
{
    private const int BannerCharacterCount = 10;
    private const string MethodForTestConsiderationCharacter = "_";

    public static readonly IEnumerable<string> ArrangeMethodPrefixes = new List<string>
    {
        "before_",
        "given_", 
        "arrange_"
    };

    public static readonly IEnumerable<string> ActMethodPrefixes = new List<string>
    {
        "act_",
        "do_", 
    };

    public static readonly IEnumerable<string> TeardownMethodPrefixes = new List<string>
    {
        "after_",
        "finally_"
    };

    public static readonly IEnumerable<string> TestExampleMethodPrefixes = new List<string>
    {
        "when_",
        "it_",
        "should_",
        "then_", 
        "assert_"
    };

    private HashSet<test_example> _examples = new HashSet<test_example>();
    private HashSet<test_condition> _conditions = new HashSet<test_condition>();
    private StringBuilder _verbalizer = new StringBuilder();
    private List<MethodInfo> _arrangeMethods;
    private List<MethodInfo> _actMethods;
    private List<MethodInfo> _teardownMethods;
    private List<string> _tags = new List<string>();
    private static readonly object _execute_lock = new object();

    /// <summary>
    /// Action to setup the initial context for a test condition
    /// </summary>
    public Action establish { get; set; }

    /// <summary>
    /// Action to execute for inspection against a set of test conditions.
    /// </summary>
    public Action because { get; set; }

    /// <summary>
    /// Action to execute to verify the contents of a test condition without a name attached.
    /// </summary>
    public Action verify { get; set; }

    /// <summary>
    /// Action for closing the text context and restoring any items that may have been affected during the test.
    /// </summary>
    public Action cleanup { get; set; }

    /// <summary>
    /// Marker for test conditions that will need to be considered at a later time when more information is gathered.
    /// </summary>
    public static readonly Action todo = () => { };

    /// <summary>
    /// Marker for a named test condition with an action associated with it for inspection.
    /// </summary>
    public test_condition it
    {
        get
        {
            var condition = new test_condition();
            _conditions.Add(condition);
            return condition;
        }
    }

    protected specification_context()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
        reset_context();
        setup_test_conditions_from_examples();
    }

    protected void execute_context()
    {
        lock ( _execute_lock )
        {
            display_tagged_methods(_verbalizer);

            var specification_under_test = normalize(GetType().Name);

            _verbalizer.AppendLine(IsSkipped(GetType())
                ? string.Format("{0} (skipped)", specification_under_test)
                : specification_under_test);

            if ( _arrangeMethods != null )
                _arrangeMethods.ForEach(m => m.Invoke(this, null));

            var examples = _examples
                .Where(e => e.IsSkipped == false)
                .ToList();

            foreach ( var example in examples )
            {
                example.ActMethods = _actMethods;
                example.execute(_verbalizer);
            }

            if ( _teardownMethods != null )
                _teardownMethods.ForEach(m => m.Invoke(this, null));

            verbalize();

            reset_context();
        }
    }

    public virtual void tear_down_context(bool teardown = true)
    {
        verbalize();
        establish = null;
        because = null;
        cleanup = null;
    }

    public virtual void fail_context()
    {
        Trace.WriteLine("Specification Failed");
    }

    public List<test_condition> get_test_conditions()
    {
        return _conditions.Distinct().ToList();
    }

    public void reset_test_example_conditions()
    {
        _conditions.Clear();
    }

    public static string normalize(string text)
    {
        return text.Replace(MethodForTestConsiderationCharacter, " ");
    }

    private void reset_context()
    {
        _examples = new HashSet<test_example>();
        _conditions = new HashSet<test_condition>();
        _arrangeMethods = new List<MethodInfo>();
        _actMethods = new List<MethodInfo>();
        _teardownMethods = new List<MethodInfo>();
        _tags = new List<string>();
        _verbalizer = new StringBuilder();
    }

    private void verbalize()
    {
        var failedConditions = _examples
            .SelectMany(ex => ex.Conditions.Where(con => con.Exception != null))
            .ToList();

        var failedExampleMethods = _examples
            .Where(ex => ex.ExampleMethodAsTestCondition != null && ex.ExampleMethodAsTestCondition.Exception != null)
            .Select(ex => ex.ExampleMethodAsTestCondition)
            .ToList();

        failedConditions.AddRange(failedExampleMethods);

        if ( failedConditions.Any() )
        {
            var failed = new StringBuilder();
            failed.AppendLine(string.Format("{0} FAILURES {0}", new string('*', BannerCharacterCount)));

            failedConditions.ForEach(condition =>
                failed.AppendLine(String.Format(">> {0} - FAILED\n{1}",
                    condition.ToString(), clean_test_condition_exception(condition.Exception))));

            _verbalizer.AppendLine(failed.ToString());
        }

        Trace.WriteLine(_verbalizer.ToString());

        if ( failedConditions.Any() )
        {
            fail_context();
        }

        _verbalizer.Clear();
    }

    private string clean_test_condition_exception(Exception testConditionException)
    {
        var builder = new StringBuilder();

        var triggeredConditionException = testConditionException.InnerException;
        if ( triggeredConditionException == null )
            return string.Empty;

        var message = triggeredConditionException.Message
            .Split(new string[] { Environment.NewLine },
            StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m).ToList();

        builder.AppendLine(string.Join(Environment.NewLine, message));
        builder.AppendLine(triggeredConditionException.StackTrace);

        return builder.ToString();
    }

    private void setup_test_conditions_from_examples()
    {
        const BindingFlags bindings = BindingFlags.Instance | BindingFlags.Public;

        _arrangeMethods = GetType()
            .GetMethods(bindings)
            .Where(ItIsAMethodForConsideration)
            .Where(m => ArrangeMethodPrefixes.Any(sem => m.Name.StartsWith(sem)))
            .Select(m => m)
            .Distinct()
            .ToList();

        _arrangeMethods = preserve_inheritance_chain_on_methods(_arrangeMethods);

        _teardownMethods = GetType()
            .GetMethods(bindings)
            .Where(ItIsAMethodForConsideration)
            .Where(m => TeardownMethodPrefixes.Any(tdm => m.Name.StartsWith(tdm)))
            .Select(m => m)
            .Distinct()
            .ToList();

        _teardownMethods = preserve_inheritance_chain_on_methods(_teardownMethods);

        _actMethods = GetType()
            .GetMethods(bindings)
            .Where(ItIsAMethodForConsideration)
            .Where(m => IsSkipped(m.DeclaringType) == false)
            .Where(m => ActMethodPrefixes.Any(sem => m.Name.StartsWith(sem)))
            .Select(m => m)
            .Distinct()
            .ToList();

        _actMethods = preserve_inheritance_chain_on_methods(_actMethods);

        var examples = GetType()
            .GetMethods(bindings)
            .Where(ItIsAMethodForConsideration)
            .Where(m => TestExampleMethodPrefixes.Any(tem => m.Name.StartsWith(tem)))
            .Select(m => m)
            .Distinct()
            .ToList();

        examples = preserve_inheritance_chain_on_methods(examples);

        examples = get_tagged_methods(examples);

        compile_all_examples_for_testing(examples);
    }

    private void compile_all_examples_for_testing(IEnumerable<MethodInfo> examples)
    {
        foreach ( var example in examples )
        {
            var tag = get_tagged_method_name(example);
            if ( string.IsNullOrEmpty(tag) == false )
                _tags.Add(tag);

            var testExample = new test_example(this, example, _verbalizer)
            {
                Name = example.Name,
                IsSkipped = IsSkipped(example.DeclaringType)
            };

            // --- moved to test example for execution ----

            _examples.Add(testExample);
        }

        _examples = new HashSet<test_example>(_examples.Reverse());
    }

    private static bool ItIsAMethodForConsideration(MethodInfo method)
    {
        var result = method.ReturnType == typeof(void)
                     && method.Name.Contains(MethodForTestConsiderationCharacter)
                     && method.GetParameters().Length == 0;
        return result;
    }

    private static bool IsSkipped(Type specificationType)
    {
        var result = ( ( specificationType.IsClass || specificationType.IsAbstract )
               & specificationType.GetCustomAttributes(typeof(SkipAttribute), true).Length == 1 );
        return result;
    }

    private static List<MethodInfo> get_tagged_methods(List<MethodInfo> methods)
    {
        var tagged = methods
            .Where(m => m.GetCustomAttributes(typeof(TagAttribute), true).Length > 0)
            .Select(m => m)
            .Distinct()
            .ToList();

        if ( !tagged.Any() )
        {
            tagged = methods;
        }

        return tagged;
    }

    private string get_tagged_method_name(MethodInfo taggedMethod)
    {
        var attr = taggedMethod
            .GetCustomAttributes(typeof(TagAttribute), true)
            .Select(a => a)
            .Cast<TagAttribute>()
            .FirstOrDefault();

        if ( attr == null )
            return string.Empty;

        return attr.Name;

    }

    private void display_tagged_methods(StringBuilder verbalizer)
    {
        var builder = new StringBuilder();
        if ( _tags.Any() )
        {
            builder.AppendLine("Tag(s):");
            _tags.ForEach(tag => builder.AppendLine(tag));
            verbalizer.AppendLine(builder.ToString());
        }
    }

    private List<MethodInfo> preserve_inheritance_chain_on_methods(List<MethodInfo> methods)
    {
        var preservedInheritanceChainMethods = methods;

        if ( this.GetType().BaseType != typeof(specification_context) & methods.Any() )
        {
            var foundMethods = methods.ToArray();
            Array.Reverse(foundMethods);
            preservedInheritanceChainMethods = new List<MethodInfo>(foundMethods);
        }

        return preservedInheritanceChainMethods;
    }
}