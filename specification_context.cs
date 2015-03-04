    /*
    How to use the specification runner for unit tests with NUnit:
    ==============================================
    1. Create an abstract class called "specifications"
    2. Decorate the class with the [TestFixture] attribute
    3. Create a public void method called "execute"
    4. Decorate the method with the [Test] attribute
    5. Make the new "specification" class inherit from specification_context
    6. Inside of the "execute" method, call the base class method "execute_context"
     
     Ex: 
     
     [TestFixture]
     public class specification : specification_context
     {  
        [Test]
        public void execute()
        {
            execute_context()
        }
     }

     How to use the specification runner for unit tests with xUnit:
    ==============================================
    1. Create an abstract class called "specifications"
    2. Create a public void method called "execute"
    3. Decorate the method with the [Fact] attribute
    4. Make the new "specification" class inherit from specification_context
    5. Inside of the "execute" method, call the base class method "execute_context"
     
     Ex: 
     
     public class specification : specification_context
     {  
        [Fact]
        public void execute()
        {
            execute_context()
        }
     }
     
     
    // subject under test:
    public class Calculator
    {
        public int Add(int first, int second)
        {
            return first + second;
        }

        public decimal Divide(decimal first, int second)
        {
            return first/second;
        }
    }

    This is the specifications class, it will have methods describing particular scenarios or examples 
    for inspection along with test conditions for one particular scenario or "subject":
	
    public class calculator_specifications : specification
    {
        private Calculator _calculator;

        // This is a concern around addition, all tests should go 
        // here for anything regarding addition for the calculator
        public void when_adding_two_non_negative_numbers()
        {
            var result = 0;

            establish = () => _calculator = new Calculator();

            because = () =>
            {
                result = _calculator.Add(1, 2);
            };

            it["should return the results as a positive number"] = () =>
            {
                Assert.AreEqual(3, result);
                Assert.True(result > 0);
            };
        }

        // This is a concern around division, all tests should go 
        // here for anything regarding division for the calculator
        public void when_dividing_two_positive_numbers()
        {
            var result = decimal.Zero;

            establish = () => _calculator = new Calculator();

            // this is one test
            it["should throw a divide by zero exception when the denominator is zero"] = () =>
                Assert.Throws<DivideByZeroException>(() => _calculator.Divide(1, 0));

            // and another can be put here...
            it["should return a positive number when the numerator is greater than the denominator"] = () =>
            {
                result = _calculator.Divide(4, 2);
                Assert.AreEqual(2, result);
                Assert.True(result > 0);
            };

            // and one more can be here...
        }
    }
	
    Output:
	
    calculator specifications
        when adding two non negative numbers
            it should return the results as a positive number : passed

        when dividing two positive numbers
            it should throw a divide by zero exception when the denominator is zero : passed
            it should return a positive number when the numerator is greater than the denominator : passed
	
    */

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
            verbalizer.AppendFormat("\t{0}", specification_context.normalize(Name)).AppendLine();

            PreConditions.ForEach(condition => condition());

            foreach ( var condition in Conditions )
            {
                string message = string.Empty;

                if ( condition.Action == specification_context.todo )
                {
                    message = string.Format("{0} : pending", condition);
                    verbalizer.AppendFormat("\t\t{0}", message).AppendLine();
                    continue;
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
                    verbalizer.AppendFormat("\t\t{0}", message).AppendLine();
                }
            }

            verbalizer.AppendLine();

            PostConditions.ForEach(condition => condition());
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
            builder.AppendFormat("it {0}", Name);
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
            "before_each",
            "given_"
        };

        private readonly IEnumerable<string> TeardownMethodPrefixes = new List<string>
        {
            "after_each"
        };

        private readonly IEnumerable<string> TestExampleMethodPrefixes = new List<string>
        {
            "when_",
            "it_", 
            "should_"
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

		public virtual void fail_specification()
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
                fail_specification();
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

            _teardownMethods = GetType()
                .GetMethods(bindings)
                .Where(m => m.GetParameters().Length == 0)
                .Where(m => m.ReturnType == typeof(void))
                .Where(m => TeardownMethodPrefixes.Any(tdm => m.Name.StartsWith(tdm)))
                .Select(m => m)
                .ToList();

            var examples = GetType()
                .GetMethods(bindings)
                .Where(m => m.GetParameters().Length == 0)
                .Where(m => m.ReturnType == typeof(void))
                .Where(m => TestExampleMethodPrefixes.Any(tem => m.Name.StartsWith(tem)))
                .Select(m => m)
                .ToList();

            foreach ( var example in examples )
            {
                var testExample = new test_example { Name = example.Name };
                example.Invoke(this, null);

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
    }