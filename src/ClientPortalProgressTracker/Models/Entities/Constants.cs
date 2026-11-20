namespace ClientPortalProgressTracker.Models.Entities;

public static class ProjectStatus
{
    public const string Draft = "Draft";
    public const string AwaitingDeposit = "Awaiting_Deposit";
    public const string Active = "Active";
    public const string CompletedUnpaid = "Completed_Unpaid";
    public const string PaidInFull = "Paid_In_Full";
}

public static class MilestoneStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "In_Progress";
    public const string Completed = "Completed";
}

public static class PaymentMethod
{
    public const string Paystack = "Paystack";
    public const string ManualEft = "Manual_EFT";
}

public static class PaymentType
{
    public const string Deposit = "Deposit";
    public const string FinalBalance = "Final_Balance";
    public const string FullPayment = "Full_Payment";
}

public static class TransactionStatus
{
    public const string PendingVerification = "Pending_Verification";
    public const string Success = "Success";
    public const string Failed = "Failed";
    public const string Rejected = "Rejected";
}