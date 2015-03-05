    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using System.Linq;

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

    /// <summary>
    /// Represents a method in the specifications that contains all of the invocations and assertions for specified conditions.
    /// </summary>
    public class test_example
    {
        public string Name { get; set; }
        public test_condition ExampleMethodAsTestCondition { get; set; }
        public List<test_condition> Conditions { get; set; }
        public List<Action> PreConditions { get; set; }
        public List<Action> PostConditions { get; set; }

        public test_example()
        {
            PreConditions = new List<Action>();
            Conditions = new List<test_condition>();
            PostConditions = new List<Action>();
        }

        public void execute(StringBuilder verbalizer)
        {
            PreConditions.ForEach(condition => condition());

            if ( ExampleMethodAsTestCondition != null )
            {
                examine_for_pass_or_failure(ExampleMethodAsTestCondition, verbalizer, 1);
            }
            else
            {
                var statement = specification_context.normalize(Name);
                verbalizer.AppendFormat("\t{0}", statement).AppendLine();

                foreach ( var condition in Conditions )
                {
                    examine_for_pass_or_failure(condition, verbalizer);
                }
            }

            PostConditions.ForEach(condition => condition());
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
                

            if ( condition.Action == specification_context.todo )
            {
                message = string.Format("{0} : pending", condition);
                verbalizer.AppendFormat("{0}{1}", indent, message).AppendLine();
                return;
            }

            try
            {
                message = string.Format("{0} : passed", condition);
                condition.Action();
            }
            catch ( Exception testConditionFailureException )
            {
                message = string.Format("{0} : failed", condition);
                condition.Failed(testConditionFailureException);
            }
            finally
            {
                verbalizer.AppendFormat("{0}{1}", indent, message).AppendLine();
            }
        }
    }

    /// <summary>
    /// Represents the individual test condition with invocation logic for determining the success or failure of a condition.
    /// </summary>
    public class test_condition
    {
        public string Name { get; private set; }
        public Action Action { get; private set; }
        public Exception Exception { get; private set; }

        public void Failed(Exception exception)
        {
            Exception = exception;
        }

        public Action this[string name]
        {
            set
            {
                Name = name;
                Action = value;
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendFormat("{0}", Name);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Base class for creating test scenarios around a central theme or context
    /// </summary>
    public abstract class specification_context
    {
        private const int BannerCharacterCount = 80;

        private readonly IEnumerable<string> SetupMethodsPrefixes = new List<string>
        {
            "before_",
            "given_"
        };

        private readonly IEnumerable<string> TeardownMethodPrefixes = new List<string>
        {
            "after_", 
            "finally_"
        };

        private readonly IEnumerable<string> TestExampleMethodPrefixes = new List<string>
        {
            "when_",
            "it_", 
            "should_", 
            "then_"
        };

        private readonly List<test_example> _examples = new List<test_example>();
        private readonly List<test_condition> _conditions = new List<test_condition>();
        private readonly StringBuilder _verbalizer = new StringBuilder();
        private List<MethodInfo> _setupMethods;
        private List<MethodInfo> _exampleMethods;
        private List<MethodInfo> _teardownMethods;

        /// <summary>
        /// Action to setup the initial context for a test condition
        /// </summary>
        public Action establish { get; set; }

        /// <summary>
        /// Action to execute for inspection against a set of test conditions.
        /// </summary>
        public Action because { get; set; }

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
            setup_test_conditions_from_examples();
        }

        protected void execute_context()
        {
            _verbalizer.AppendLine(normalize(GetType().Name));

            if ( _setupMethods != null )
                _setupMethods.ForEach(m => m.Invoke(this, null));

            foreach ( var example in _examples )
            {
                example.execute(_verbalizer);
            }

            if ( _teardownMethods != null )
                _teardownMethods.ForEach(m => m.Invoke(this, null));

            verbalize();
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
            Console.WriteLine("Specification Failed");
        }

        public static string normalize(string text)
        {
            return text.Replace("_", " ");
        }

        private void verbalize()
        {
            var failedCondtions = _examples
                .SelectMany(ex => ex.Conditions.Where(con => con.Exception != null))
                .ToList();

            if ( failedCondtions.Any() )
            {
                var failed = new StringBuilder();
                failed.AppendLine(new string('=', BannerCharacterCount));

                failedCondtions.ForEach(condition =>
                    failed.AppendLine(String.Format(">> {0} - FAILED\n{1}",
                        condition.ToString(), condition.Exception)));

                _verbalizer.AppendLine(failed.ToString());
            }

            Console.WriteLine();
            Console.WriteLine(_verbalizer.ToString());

            if ( failedCondtions.Any() )
            {
                fail_context();
            }

            _verbalizer.Clear();
        }

        private void setup_test_conditions_from_examples()
        {
            const BindingFlags bindings = BindingFlags.Instance | BindingFlags.Public;

            _setupMethods = GetType()
                .GetMethods(bindings)
                .Where(m => m.GetParameters().Length == 0)
                .Where(m => m.ReturnType == typeof(void))
                .Where(m => SetupMethodsPrefixes.Any(sem => m.Name.StartsWith(sem)))
                .Select(m => m)
                .ToList();

            _setupMethods = preserve_inheritance_chain_on_methods(_setupMethods);

            _teardownMethods = GetType()
                .GetMethods(bindings)
                .Where(m => m.GetParameters().Length == 0)
                .Where(m => m.ReturnType == typeof(void))
                .Where(m => TeardownMethodPrefixes.Any(tdm => m.Name.StartsWith(tdm)))
                .Select(m => m)
                .ToList();

            _teardownMethods = preserve_inheritance_chain_on_methods(_teardownMethods);

            var examples = GetType()
                .GetMethods(bindings)
                .Where(m => m.GetParameters().Length == 0)
                .Where(m => m.ReturnType == typeof(void))
                .Where(m => TestExampleMethodPrefixes.Any(tem => m.Name.StartsWith(tem)))
                .Select(m => m)
                .ToList();

            examples = preserve_inheritance_chain_on_methods(examples);

            foreach ( var example in examples )
            {
                var testExample = new test_example { Name = example.Name };

                try
                {
                    example.Invoke(this, null);
                }
                catch
                {
                    // method used as test condition, record it on the test example:
                    var condition = new test_condition();
                    condition[normalize(example.Name)] = () => example.Invoke(this, null);
                    testExample.ExampleMethodAsTestCondition = condition;
                }

                if ( establish != null )
                    testExample.PreConditions.Add(new Action(establish));

                if ( because != null )
                    testExample.PreConditions.Add(new Action(because));

                if ( cleanup != null )
                    testExample.PostConditions.Add(new Action(cleanup));

                testExample.Conditions = new List<test_condition>(_conditions);

                _conditions.Clear();

                establish = null;
                because = null;
                cleanup = null;

                _examples.Add(testExample);
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