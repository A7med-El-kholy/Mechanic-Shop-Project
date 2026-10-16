
using AwesomeAssertions;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.UnitTests.Results;

public sealed class ResultTests
{
    // 1. A successful result should preserve its value and contain no errors.
    [Fact]
    public void Success_returns_value()
    {
        // Arrange
        Result<int> result = 5;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Value.Should().Be(5);
    }

    // 2. A failed result should preserve the error's code and kind.
    [Fact]
    public void Failure_from_error_reports_kind_and_code()
    {
        // Arrange
        Result<string> result = Error.Validation(
            "Vehicle.InvalidVin",
            "Invalid VIN.");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsError.Should().BeTrue();
        result.TopError.Code.Should().Be("Vehicle.InvalidVin");
        result.TopError.Type.Should().Be(ErrorKind.Validation);
    }

    // 3. For the current implementation, a failed string result returns null.
    [Fact]
    public void Failure_value_is_default()
    {
        // Arrange
        Result<string> result = Error.Conflict(
            "Vehicle.DuplicateVin",
            "A vehicle with this VIN already exists.");

        // Assert
        result.Value.Should().BeNull();
    }

    // 4. Multiple errors should be retained in their original order.
    [Fact]
    public void Multiple_errors_are_preserved()
    {
        // Arrange
        var errors = new List<Error>
        {
            Error.Conflict(
                "Vehicle.DuplicateVin",
                "VIN already exists."),
            Error.Validation(
                "Vehicle.InvalidVin",
                "VIN format is invalid.")
        };

        // Act
        Result<string> result = errors;

        // Assert
        result.IsError.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors[0].Code.Should().Be("Vehicle.DuplicateVin");
        result.Errors[1].Code.Should().Be("Vehicle.InvalidVin");
    }

    // 5. A failed result cannot be created from an empty error collection.
    [Fact]
    public void Empty_error_list_throws()
    {
        // Arrange
        var errors = new List<Error>();

        // Act
        var act = () => CreateFailedResult(errors);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private static Result<int> CreateFailedResult(List<Error> errors)
        => errors;

    // 6. A successful result cannot contain a null value.
    [Fact]
    public void Null_value_throws()
    {
        // Arrange
        string? value = null;

        // Act
        var act = () => CreateSuccessfulResult(value);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static Result<string> CreateSuccessfulResult(string? value)
        => value!;

    // 7. Match should invoke the appropriate callback for each outcome.
    [Fact]
    public void Match_routes_to_correct_branch()
    {
        // Arrange
        Result<int> success = 42;
        Result<int> failure = Error.NotFound(
            "Vehicle.NotFound",
            "Vehicle was not found.");

        // Act
        var successResult = success.Match(
            value => $"Value: {value}",
            _ => "Error");

        var failureResult = failure.Match(
            value => $"Value: {value}",
            errors => $"Error: {errors[0].Code}");

        // Assert
        successResult.Should().Be("Value: 42");
        failureResult.Should().Be("Error: Vehicle.NotFound");
    }

    // 8. Each error factory should assign its corresponding ErrorKind.
    [Theory]
    [InlineData(ErrorKind.Failure)]
    [InlineData(ErrorKind.Unexpected)]
    [InlineData(ErrorKind.Validation)]
    [InlineData(ErrorKind.Conflict)]
    [InlineData(ErrorKind.NotFound)]
    [InlineData(ErrorKind.Unauthorized)]
    [InlineData(ErrorKind.Forbidden)]
    public void Error_factories_set_type(ErrorKind expectedKind)
    {
        // Arrange
        Error error = CreateError(expectedKind);

        // Assert
        error.Type.Should().Be(expectedKind);
    }

    private static Error CreateError(ErrorKind kind)
        => kind switch
        {
            ErrorKind.Failure => Error.Failure(),
            ErrorKind.Unexpected => Error.Unexpected(),
            ErrorKind.Validation => Error.Validation(),
            ErrorKind.Conflict => Error.Conflict(),
            ErrorKind.NotFound => Error.NotFound(),
            ErrorKind.Unauthorized => Error.Unauthorized(),
            ErrorKind.Forbidden => Error.Forbidden(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
