namespace OrderService.Core.Models.Enums;

public enum ReturnStatus
{
    Requested = 1,
    Approved = 2,
    Rejected = 3,
    ItemsReceived = 4,
    Inspecting = 5,
    Completed = 6,
    RefundProcessed = 7
}

public enum ReturnReason
{
    DefectiveItem = 1,
    WrongItem = 2,
    NotAsDescribed = 3,
    NoLongerNeeded = 4,
    BetterPriceFound = 5,
    QualityIssue = 6,
    SizeIssue = 7,
    Other = 8
}
