public sealed record MedicineLotRequest(
    string LotNumber,
    DateTime? ExpiryDate,
    decimal Quantity = 0
);