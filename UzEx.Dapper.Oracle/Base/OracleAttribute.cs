using System;
using System.Data;
using UzEx.Dapper.Oracle.Enums;

namespace UzEx.Dapper.Oracle.Base;

/// <summary>
/// Represents an attribute used to define properties specific to Oracle database operations
/// when mapping .NET properties to Oracle parameters.
/// </summary>
public class OracleAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the Oracle-specific database type used for the parameter or property.
    /// This property maps to the <see cref="OracleMappingType"/> enumeration,
    /// which represents various Oracle database types.
    /// </summary>
    /// <remarks>
    /// Setting this property defines the database type explicitly for Oracle operations,
    /// aiding in type mapping between .NET and Oracle data types.
    /// If not specified, the type may be inferred based on the property value.
    /// </remarks>
    public OracleMappingType DbType { get; set; }

    /// <summary>
    /// Gets or sets the direction of the parameter, specifying how data is passed between
    /// the application and the Oracle database.
    /// </summary>
    /// <remarks>
    /// The direction can be one of the values from the <see cref="System.Data.ParameterDirection"/> enumeration:
    /// Input, Output, InputOutput, or ReturnValue. This property is used to define the direction in which the data flows
    /// for an Oracle database operation, playing a critical role in stored procedures or function calls.
    /// </remarks>
    public ParameterDirection? Direction { get; set; }

    /// <summary>
    /// Gets or sets the size of the Oracle parameter associated with the property.
    /// This specifies the maximum length or size of the value, particularly for types such as
    /// strings, binary data, or arrays.
    /// </summary>
    /// <remarks>
    /// The size property is essential for defining the expected maximum value for certain
    /// Oracle data types. For instance, when dealing with VARCHAR2 or RAW types, this property
    /// limits the length of the data. If not specified, the size may be inferred or defaulted
    /// depending on the parameter's type and value.
    /// </remarks>
    public int? Size { get; set; }

    /// <summary>
    /// Gets or sets a value that indicates whether the parameter or property can accept null values.
    /// </summary>
    /// <remarks>
    /// When set to <c>true</c>, this property specifies that the associated parameter or property
    /// can be assigned a null value during database operations. It helps in mapping nullability of
    /// .NET types to Oracle database parameters.
    /// If not explicitly set, the nullability will be inferred based on the property's data type or other configurations.
    /// </remarks>
    public bool? IsNullable { get; set; }

    /// <summary>
    /// Gets or sets the precision of the Oracle parameter or property.
    /// Precision specifies the maximum number of digits that are allowed in a numeric value.
    /// </summary>
    /// <remarks>
    /// This property is particularly useful when working with numeric data types in Oracle,
    /// such as <c>NUMBER</c>, where precision determines the total number of digits being used.
    /// If not explicitly set, the default precision is determined by the Oracle database.
    /// </remarks>
    public byte? Precision { get; set; }

    /// <summary>
    /// Gets or sets the scale for numeric values when working with Oracle database parameters or properties.
    /// </summary>
    /// <remarks>
    /// The scale defines the number of digits to the right of the decimal point
    /// for Oracle numeric types, such as <see cref="OracleMappingType.Decimal"/>.
    /// This property is primarily used to ensure accurate representation of numeric
    /// values during database interactions.
    /// </remarks>
    public byte? Scale { get; set; }

    /// <summary>
    /// Gets or sets the Oracle collection type for the parameter or property.
    /// This property maps to the <see cref="OracleMappingCollectionType"/> enumeration,
    /// which specifies the type of collection to be used in Oracle database operations,
    /// such as associative arrays for PL/SQL procedures.
    /// </summary>
    /// <remarks>
    /// Use this property to define how a collection should be represented when interacting with Oracle databases.
    /// This is particularly useful when working with PL/SQL associative arrays or other supported collection types.
    /// If not specified, the default value indicates no collection mapping.
    /// </remarks>
    public OracleMappingCollectionType? CollectionType { get; set; }

    /// <summary>
    /// When set to <c>true</c>, indicates that the property value should be serialized to a JSON string
    /// before being passed to the Oracle database.
    /// </summary>
    /// <remarks>
    /// This property is useful for handling complex objects or collections that need to be stored in
    /// a format compatible with a JSON column or parameter in Oracle.
    /// If set to <c>false</c>, the value is passed as-is without serialization.
    /// The serialization process uses standard JSON serialization methods.
    /// </remarks>
    public bool SerializeAsJson { get; set; } = false;

    /// <summary>
    /// Gets or sets the name of the parameter for Oracle database operations.
    /// This property defines the parameter's identifier in the Oracle context.
    /// </summary>
    /// <remarks>
    /// Setting this property ensures that the parameter is mapped to the correct
    /// Oracle database parameter during execution. If not explicitly set, a default
    /// naming convention may be used.
    /// </remarks>
    public string ParameterName { get; set; }
}