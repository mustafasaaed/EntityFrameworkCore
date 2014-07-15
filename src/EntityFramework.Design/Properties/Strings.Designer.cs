// <auto-generated />
namespace Microsoft.Data.Entity.Design
{
    using System.Globalization;
    using System.Reflection;
    using System.Resources;

    internal static class Strings
    {
        private static readonly ResourceManager _resourceManager
            = new ResourceManager("EntityFramework.Design.Strings", typeof(Strings).GetTypeInfo().Assembly);

        /// <summary>
        /// The string argument '{argumentName}' cannot be empty.
        /// </summary>
        internal static string ArgumentIsEmpty
        {
            get { return GetString("ArgumentIsEmpty"); }
        }

        /// <summary>
        /// The string argument '{argumentName}' cannot be empty.
        /// </summary>
        internal static string FormatArgumentIsEmpty(object argumentName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("ArgumentIsEmpty", "argumentName"), argumentName);
        }

        /// <summary>
        /// The value provided for argument '{argumentName}' must be a valid value of enum type '{enumType}'.
        /// </summary>
        internal static string InvalidEnumValue
        {
            get { return GetString("InvalidEnumValue"); }
        }

        /// <summary>
        /// The value provided for argument '{argumentName}' must be a valid value of enum type '{enumType}'.
        /// </summary>
        internal static string FormatInvalidEnumValue(object argumentName, object enumType)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("InvalidEnumValue", "argumentName", "enumType"), argumentName, enumType);
        }

        /// <summary>
        /// The name '{migrationName}' is used by an existing migration.
        /// </summary>
        internal static string DuplicateMigrationName
        {
            get { return GetString("DuplicateMigrationName"); }
        }

        /// <summary>
        /// The name '{migrationName}' is used by an existing migration.
        /// </summary>
        internal static string FormatDuplicateMigrationName(object migrationName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("DuplicateMigrationName", "migrationName"), migrationName);
        }

        /// <summary>
        /// Please specify an assembly that contains a class that derives from DbContext, using the ContextAssembly option.
        /// </summary>
        internal static string ContextAssemblyNotSpecified
        {
            get { return GetString("ContextAssemblyNotSpecified"); }
        }

        /// <summary>
        /// Please specify an assembly that contains a class that derives from DbContext, using the ContextAssembly option.
        /// </summary>
        internal static string FormatContextAssemblyNotSpecified()
        {
            return GetString("ContextAssemblyNotSpecified");
        }

        /// <summary>
        /// The assembly '{contextAssemblyName}' does not contain the type '{contextTypeName}'.
        /// </summary>
        internal static string AssemblyDoesNotContainType
        {
            get { return GetString("AssemblyDoesNotContainType"); }
        }

        /// <summary>
        /// The assembly '{contextAssemblyName}' does not contain the type '{contextTypeName}'.
        /// </summary>
        internal static string FormatAssemblyDoesNotContainType(object contextAssemblyName, object contextTypeName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("AssemblyDoesNotContainType", "contextAssemblyName", "contextTypeName"), contextAssemblyName, contextTypeName);
        }

        /// <summary>
        /// The type '{contextTypeName}' does not derive from DbContext.
        /// </summary>
        internal static string TypeIsNotDbContext
        {
            get { return GetString("TypeIsNotDbContext"); }
        }

        /// <summary>
        /// The type '{contextTypeName}' does not derive from DbContext.
        /// </summary>
        internal static string FormatTypeIsNotDbContext(object contextTypeName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("TypeIsNotDbContext", "contextTypeName"), contextTypeName);
        }

        /// <summary>
        /// The assembly '{contextAssemblyName}' does not contain a class that derives from DbContext.
        /// </summary>
        internal static string AssemblyDoesNotContainDbContext
        {
            get { return GetString("AssemblyDoesNotContainDbContext"); }
        }

        /// <summary>
        /// The assembly '{contextAssemblyName}' does not contain a class that derives from DbContext.
        /// </summary>
        internal static string FormatAssemblyDoesNotContainDbContext(object contextAssemblyName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("AssemblyDoesNotContainDbContext", "contextAssemblyName"), contextAssemblyName);
        }

        /// <summary>
        /// The assembly '{contextAssemblyName}' contains multiple classes that derive from DbContext. Please specify the context type using the ContextType option.
        /// </summary>
        internal static string AssemblyContainsMultipleDbContext
        {
            get { return GetString("AssemblyContainsMultipleDbContext"); }
        }

        /// <summary>
        /// The assembly '{contextAssemblyName}' contains multiple classes that derive from DbContext. Please specify the context type using the ContextType option.
        /// </summary>
        internal static string FormatAssemblyContainsMultipleDbContext(object contextAssemblyName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("AssemblyContainsMultipleDbContext", "contextAssemblyName"), contextAssemblyName);
        }

        /// <summary>
        /// Please specify a migration name using the MigrationName option.
        /// </summary>
        internal static string MigrationNameNotSpecified
        {
            get { return GetString("MigrationNameNotSpecified"); }
        }

        /// <summary>
        /// Please specify a migration name using the MigrationName option.
        /// </summary>
        internal static string FormatMigrationNameNotSpecified()
        {
            return GetString("MigrationNameNotSpecified");
        }

        /// <summary>
        /// The source of migrations specified using the Source option must be one of the following: database, local, pending.
        /// </summary>
        internal static string InvalidMigrationSource
        {
            get { return GetString("InvalidMigrationSource"); }
        }

        /// <summary>
        /// The source of migrations specified using the Source option must be one of the following: database, local, pending.
        /// </summary>
        internal static string FormatInvalidMigrationSource()
        {
            return GetString("InvalidMigrationSource");
        }

        /// <summary>
        /// 
        /// Usage: 
        /// 
        ///   &lt;runner&gt; command --option=value
        /// 
        /// Commands:
        /// 
        ///   config    Writes configuration
        ///   create    Scaffolds a new migration
        ///   list      Lists migrations
        ///   script    Generates SQL script
        ///   apply     Updates the database
        /// 
        /// Options:
        /// 
        ///   config
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        ///     [--ConfigFile=&lt;file&gt;]
        /// 
        ///   create
        ///     --MigrationName=&lt;name&gt;
        ///     [--ConfigFile=&lt;file&gt;]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///   list
        ///     [--ConfigFile=&lt;file&gt;]  
        ///     [--MigrationSource=Database|Local|Pending]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///   script
        ///     [--ConfigFile=&lt;file&gt;]
        ///     [--TargetMigration=&lt;name&gt;]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///   apply
        ///     [--ConfigFile=&lt;file&gt;]
        ///     [--TargetMigration=&lt;name&gt;]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///     
        /// </summary>
        internal static string ToolUsage
        {
            get { return GetString("ToolUsage"); }
        }

        /// <summary>
        /// 
        /// Usage: 
        /// 
        ///   &lt;runner&gt; command --option=value
        /// 
        /// Commands:
        /// 
        ///   config    Writes configuration
        ///   create    Scaffolds a new migration
        ///   list      Lists migrations
        ///   script    Generates SQL script
        ///   apply     Updates the database
        /// 
        /// Options:
        /// 
        ///   config
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        ///     [--ConfigFile=&lt;file&gt;]
        /// 
        ///   create
        ///     --MigrationName=&lt;name&gt;
        ///     [--ConfigFile=&lt;file&gt;]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///   list
        ///     [--ConfigFile=&lt;file&gt;]  
        ///     [--MigrationSource=Database|Local|Pending]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///   script
        ///     [--ConfigFile=&lt;file&gt;]
        ///     [--TargetMigration=&lt;name&gt;]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///   apply
        ///     [--ConfigFile=&lt;file&gt;]
        ///     [--TargetMigration=&lt;name&gt;]
        ///     [--ContextAssembly=&lt;assembly&gt;] [--ContextType=&lt;type&gt;]
        ///     [--MigrationAssembly=&lt;assembly&gt;] [--MigrationNamespace=&lt;namespace&gt;] [--MigrationDirectory=&lt;directory&gt;]
        ///     [--References=&lt;assembly[;...n]&gt;]
        /// 
        ///     
        /// </summary>
        internal static string FormatToolUsage()
        {
            return GetString("ToolUsage");
        }

        /// <summary>
        /// No class named '{contextName}' that derives from DbContext was found.
        /// </summary>
        internal static string SpecifiedContextNotFound
        {
            get { return GetString("SpecifiedContextNotFound"); }
        }

        /// <summary>
        /// No class named '{contextName}' that derives from DbContext was found.
        /// </summary>
        internal static string FormatSpecifiedContextNotFound(object contextName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("SpecifiedContextNotFound", "contextName"), contextName);
        }

        /// <summary>
        /// More than one class named '{contextName}' that derives from DbContext was found. Please specify the fully qualified name of the one to use.
        /// </summary>
        internal static string MultipleContextsFound
        {
            get { return GetString("MultipleContextsFound"); }
        }

        /// <summary>
        /// More than one class named '{contextName}' that derives from DbContext was found. Please specify the fully qualified name of the one to use.
        /// </summary>
        internal static string FormatMultipleContextsFound(object contextName)
        {
            return string.Format(CultureInfo.CurrentCulture, GetString("MultipleContextsFound", "contextName"), contextName);
        }

        private static string GetString(string name, params string[] formatterNames)
        {
            var value = _resourceManager.GetString(name);

            System.Diagnostics.Debug.Assert(value != null);

            if (formatterNames != null)
            {
                for (var i = 0; i < formatterNames.Length; i++)
                {
                    value = value.Replace("{" + formatterNames[i] + "}", "{" + i + "}");
                }
            }

            return value;
        }
    }
}
