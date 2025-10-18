using System.IO;
using System.Text;

namespace InterfaceExtractor.Tests.Helpers
{
    /// <summary>
    /// Helper methods for creating test scenarios
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Creates a temporary C# file with the given source code
        /// </summary>
        public static string CreateTempCSharpFile(string sourceCode, string directory)
        {
            var fileName = $"Test_{System.Guid.NewGuid()}.cs";
            var filePath = Path.Combine(directory, fileName);
            File.WriteAllText(filePath, sourceCode);
            return filePath;
        }

        /// <summary>
        /// Sample source code for testing
        /// </summary>
        public static class SampleCode
        {
            public static string SimpleClass => @"
using System;

namespace TestNamespace
{
    public class SimpleClass
    {
        public string Name { get; set; }
        public int Age { get; set; }

        public void DoSomething()
        {
            Console.WriteLine(""Hello"");
        }
    }
}";

            public static string ClassWithDocumentation => @"
using System;

namespace TestNamespace
{
    /// <summary>
    /// A test class with documentation
    /// </summary>
    public class DocumentedClass
    {
        /// <summary>
        /// Gets or sets the name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Performs an action
        /// </summary>
        /// <param name=""value"">The value to process</param>
        /// <returns>True if successful</returns>
        public bool DoSomething(string value)
        {
            return true;
        }
    }
}";

            public static string GenericClass => @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class GenericRepository
    {
        public T GetById<T>(int id) where T : class, new()
        {
            return null;
        }

        public List<T> GetAll<T>() where T : IEntity
        {
            return null;
        }

        public void Update<T>(T entity) where T : class, IEntity
        {
        }
    }

    public interface IEntity
    {
        int Id { get; }
    }
}";

            public static string ClassWithMultipleMemberTypes => @"
using System;

namespace TestNamespace
{
    public class CompleteClass
    {
        // Properties
        public string Name { get; set; }
        public int Count { get; }
        public DateTime LastUpdated { get; private set; }

        // Methods
        public void DoSomething() { }
        public string GetValue() { return """"; }

        // Events
        public event EventHandler Changed;
        public event EventHandler<DataEventArgs> DataChanged;

        // Indexers
        public string this[int index] { get { return null; } set { } }

        // These should be excluded
        private string PrivateProperty { get; set; }
        private void PrivateMethod() { }
        public static string StaticProperty { get; set; }
        public static void StaticMethod() { }
    }

    public class DataEventArgs : EventArgs
    {
        public string Data { get; set; }
    }
}";

            public static string MultipleClasses => @"
using System;

namespace TestNamespace
{
    public class FirstClass
    {
        public string FirstName { get; set; }
        public void FirstMethod() { }
    }

    public class SecondClass
    {
        public string SecondName { get; set; }
        public void SecondMethod() { }
    }

    internal class InternalClass
    {
        public string Name { get; set; }
    }
}";

            public static string ClassWithReadOnlyProperties => @"
using System;

namespace TestNamespace
{
    public class ReadOnlyClass
    {
        public string ReadOnly { get; }
        public string ReadWrite { get; set; }
        public string ExpressionBodied => ""Value"";
        public string PropertyWithPrivateSetter { get; private set; }

        public ReadOnlyClass()
        {
            ReadOnly = ""Initialized"";
        }
    }
}";

            public static string ClassWithEvents => @"
using System;

namespace TestNamespace
{
    public class EventClass
    {
        public event EventHandler SimpleEvent;
        public event EventHandler<CustomEventArgs> GenericEvent;

        private event EventHandler PrivateEvent;
        public static event EventHandler StaticEvent;
    }

    public class CustomEventArgs : EventArgs
    {
        public string Message { get; set; }
    }
}";

            public static string ClassWithIndexers => @"
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    public class IndexerClass
    {
        private List<string> items = new List<string>();

        public string this[int index]
        {
            get { return items[index]; }
            set { items[index] = value; }
        }

        public string this[string key]
        {
            get { return null; }
        }

        private string this[long index]
        {
            get { return null; }
        }
    }
}";

            public static string ClassInheritingInterface => @"
using System;

namespace TestNamespace
{
    public class ImplementingClass : IExistingInterface
    {
        public string Name { get; set; }
        public void DoSomething() { }
    }

    public interface IExistingInterface
    {
        string Name { get; set; }
    }
}";

            public static string EmptyClass => @"
namespace TestNamespace
{
    public class EmptyClass
    {
    }
}";

            public static string ClassWithOnlyPrivateMembers => @"
namespace TestNamespace
{
    public class PrivateOnlyClass
    {
        private string name;
        private void DoSomething() { }
        private string Name { get; set; }
    }
}";

            public static string FileScopedNamespaceClass => @"
using System;

namespace TestNamespace;

public class FileScopedClass
{
    public string Name { get; set; }
    public void DoSomething() { }
}";

            public static string NestedClass => @"
using System;

namespace TestNamespace
{
    public class OuterClass
    {
        public string OuterProperty { get; set; }

        public class NestedClass
        {
            public string NestedProperty { get; set; }
        }
    }
}";
        }

        /// <summary>
        /// Expected interface outputs for validation
        /// </summary>
        public static class ExpectedInterfaces
        {
            public static string SimpleInterface => @"namespace TestNamespace.Interfaces
{
    public interface ISimpleClass
    {
        string Name { get; set; };

        int Age { get; set; };

        void DoSomething();
    }
}";

            public static string InterfaceWithDocumentation => @"namespace TestNamespace.Interfaces
{
    /// <summary>
    /// A test class with documentation
    /// </summary>
    public interface IDocumentedClass
    {
        /// <summary>
        /// Gets or sets the name
        /// </summary>
        string Name { get; set; };

        /// <summary>
        /// Performs an action
        /// </summary>
        /// <param name=""value"">The value to process</param>
        /// <returns>True if successful</returns>
        bool DoSomething(string value);
    }
}";
        }

        /// <summary>
        /// Normalizes whitespace in strings for comparison
        /// </summary>
        public static string NormalizeWhitespace(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var lines = input.Split(['\r', '\n'], System.StringSplitOptions.RemoveEmptyEntries);
            var normalized = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    normalized.AppendLine(trimmed);
                }
            }

            return normalized.ToString().Trim();
        }

        /// <summary>
        /// Compares two code strings ignoring whitespace differences
        /// </summary>
        public static bool CodeEquals(string expected, string actual)
        {
            return NormalizeWhitespace(expected) == NormalizeWhitespace(actual);
        }
    }
}