#Welcome to specifications testing

This file is designed to include all of the basic things needed for specification testing on the .Net framework with the feel of [NSpec](http://nspec.org) but in a lighter one-file footprint. This single-file is designed to run with the standard testing frameworks of NUnit, xUnit and MSTest.  


## Why should you use it
The all unit test frameworks have a convention on the way it picks up test methods and/or test fixtures (i.e. classes) for executing your tests. If you are wanting to design your tests in such a way as to describe "how" your application should behave under certain conditions, you usually 1) augment your exisiting tests for a third-party test-runner or 2) re-structure your tests in the existing runner to a semi-English description of what is expected to happen. 

The main reason that this file was created is that you can use any test runner that you want to execute the specifications and the specifications themselves will not need to change.  

Usually, we try to have specification tests written like this:

```csharp
public abstract class calculator_context
{
    protected Calculator Calculator {get; private set;}
    
    protected calculator_context()
    {
        Calculator = new Calculator();
    }
}

// using NUnit...

[TestFixture]
public when_a_calculator_is_used_for_addition_for_two_positive_numbers : calculator_context
{
  [Test]
  public void it_should_return_a_positive_number()
  {
    var result = Calculator.Add(1,2);
    Assert.Equal(3, result); 
    Assert.True(result > 0);
  }
}
```

This usually covers the basics of specification testing but there is one thing that stands out in this example, the conditions around the behavior of the calculator are not self-contained. One way of thinking about this is that for every operation on the calculator, a new test fixture will have to be developed to cover the expected and unexpected behavior.



## Usage
###NUnit
>For users who like the NUnit test framework, their is a simple way to extend the functionality into your classes
>> * Create an abstract class in your test project called "specification"
>> * Decorate the class with the [TestFixture] attribute
>> * Create a public void method called "execute"
>> * Inside of the "execute" method, call the base class method "execute_context"
>> * Decorate the new method with the [Test] attribute
>> * Override the method "fail_context" inside of the new class and type "Assert.Fail()" (this alerts NUnit that the specification has failed)
>> * Make the new "specification" class inherit from specification_context
> 

Example: NUnit
```csharp
[TestFixture]
public class specification : specification_context
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
```
###xUnit
>For users who like the xUnit test framework, their is a simple way to extend the functionality into your classes
>> * Create an abstract class in your test project called "specification"
>> * Create a public void method called "execute"
>> * Inside of the "execute" method, call the base class method "execute_context"
>> * Decorate the new method with the [Fact] attribute
>> * Override the method "fail_context" inside of the new class and type "Assert.True(false)" (this alerts xUnit that the specification has failed)
>> * Make the new "specification" class inherit from specification_context
> 

Example: xUnit
```csharp
public class specification : specification_context
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
```


## Creating your specification(s)
The syntax for the specification follows some of the syntax of [NSpec](http://nspec.org/) but is stripped down to a less intrusive framework that has to be included in your test project.

For the course of this library, a specification is one class that contains all of the scenarios that could happen as expressed by public methods that contain test conditions for a "subject under test" or "context".

The single-class checks for certain "prefixes" on public methods to determine whether or not to:

1. Execute the method to test certain conditions 
2. Execute the method to setup information for the specification as a whole
3. Execute the method to tear down or clean information for the specification as a whole (i.e. "tear down")

Methods that are used to verify test conditions can be prefixed with*:
>
>>* when_
>>* it_
>>* should_
>>* then_
>

Method that are used to initialize the specification can be prefixed with*:
>
>>* before_
>>* given_
>

Method that are used to terminate and clean-up the specification can be prefixed with*:
>
>>* after_
>>* finally_
>

*Please note that these prefixes are closely in-line with the [Gherkin](http://en.wikipedia.org/wiki/Behavior-driven_development) syntax

If we were to design specifications around how a simple calculator should work, we could end up with something like this:

```csharp
public class calculator_specifications : specification
{
    // this is known as a test "example" condition:
    public void when_adding_two_positive_numbers()
    {
        var calculator = new Calculator();
        var result  = calculator.Add(1,2); 
        
        // this is a test condition, inside of this is a lamdba that can be used
        // to verify the statement that is included in quotes:
        it["should return the result as a positive number"] = () => Assert.Equal(result, 3);
    }
}
```

After running this test (using NUnit as the test runner), we see something like this:

```
calculator specifications
    when adding two positive numbers
		it should return the result as a positive number : passed

1 passed, 0 failed, 0 skipped, took 1.08 seconds (NUnit 2.6.2).
```

What you are seeing here is the parsing of the specification into its differing parts for a more English-sounding description of what the calculator should do under certain circumstances. 

### Reducing work in the specification via the "establish" and "because" actions
When you have a specification and need to initialize information before the test condition is examined, you can use the "establish" lambda property to assign an action to execute your setup. Also, the "because" lambda property can excercise the action on the subject under test to further clarify the test conditions just for assertions. This will be similiar to the Arrange-Act-Assert method used for TDD and BDD (also the "verify" lamba can be used instead of the named test condition i.e. "it[...]" for simple assertions).

Due to the way the specifications are evaluated, please include all code in the test example methods in either the "establish", "because", the named test condition (i.e. it["..."]), or the "verify" lambda methods. The most common problem that you will experience if you do not conform to this rule is an exception for null object being evaluated. The evaluation is a two-pass filter, the first is to find all potential test methods and invoke them for determining test conditions (this is where your method is invoked, and most proably where null exceptions will be thrown) and lastly to run the test conditions found from each test method.

Example:

```csharp
public class calculator_specifications : specification
{
    // this is known as a test "example" or "scenario" condition:
    public void when_adding_two_positive_numbers()
    {
        Calculator calculator; 
        int result = 0; 
        
        // this will execute first (if defined) -> arrange:
        establish = () => calculator = new Calculator(); 
        
        // this will execute after the "establish" clause (if defined) -> act:
        because = ()=> result = calculator.Add(1,2); 
        
        
        // this test condition will always execute after the "because" clause  -> assert:
        it["should return the result as a positive number"] = () => Assert.Equal(result, 3);
    }
}

or with the "verify" syntax for the named test condition (i.e it["...."])

public class when_adding_two_positive_numbers : specification
{
    // this is known as a test "example" or "scenario" condition:
    public void it_should_return__the_result_as_a_positive_number()
    {
        Calculator calculator; 
        int result = 0; 
        
        // this will execute first (if defined) -> arrange:
        establish = () => calculator = new Calculator(); 
        
        // this will execute after the "establish" clause (if defined) -> act:
        because = ()=> result = calculator.Add(1,2); 
        
        // this "verify" can be used in place of the named test condition and will always execute after the "because" clause  -> assert:
        verify = () => Assert.Equal(result, 2);
    }
}
```
The net result of running the specification should be the same as the previous version. 


We can also define specification-level "setup" and "tear-down" methods via the reserved keywords listed above

```csharp
public class calculator_specifications : specification
{
    private Calculator _calculator; 
    
    // this is a like a "setup" method for the specification, it is only run once.
    public void given_that_the_calculator_is_present()
    {
        // this will be our "arrange" section:
        _calculator = new Calculator();
        Console.WriteLine("setup: we can put initialization activities here...");
    }
    
    // this is known as a test "example" or "scenario" condition:
    public void when_adding_two_positive_numbers()
    {
        int result = 0; 
                
        //act:
        because = ()=> result = _calculator.Add(1,2); 
        
        
        // this test condition will always execute after the "because" clause  -> assert:
        it["should return the result as a positive number"] = () => Assert.Equal(result, 3);
    }
    
    public void finally_the_calculator_is_released()
    {
        Console.WriteLine("tear-down:we can put clean-up activities here...");
    }
}
```

After running the example, note that the initalization and clean-up activities run first before the scenario is executed:
```
setup: we can put initialization activities here...
tear-down: we can clean-up activities here...

calculator specifications
    when adding two positive numbers
		it should return the result as a positive number : passed

1 passed, 0 failed, 0 skipped, took 1.15 seconds (NUnit 2.6.2).
```


Eat, Drink and Enjoy...


