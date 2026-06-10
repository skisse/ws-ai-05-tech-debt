namespace AcmeFinancial.Payments.Models;

public record Payment(
    Guid Id,
    string FromAccountNumber,
    string ToAccountNumber,
    decimal Amount,
    string Currency,
    DateTimeOffset CreatedAt,
    PaymentStatus Status
);

public enum PaymentStatus
{
    Pending,
    Processing,
    Completed,
    Rejected
}

public record CreatePaymentRequest(
    string FromAccountNumber,
    string ToAccountNumber,
    decimal Amount,
    string Currency
);

public record Result<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };
    public static Result<T> Fail(string error) => new() { Success = false, Error = error };
}
