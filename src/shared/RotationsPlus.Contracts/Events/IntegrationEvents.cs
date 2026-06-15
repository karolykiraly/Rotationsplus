namespace RotationsPlus.Contracts.Events;

/// <summary>Service Bus topic names (events, not RPC). See Plan_Architecture.md §3.4.</summary>
public static class Topics
{
    public const string RotationEvents = "rotation-events";
    public const string PaymentEvents = "payment-events";
    public const string DocumentEvents = "document-events";
    public const string NotificationRequests = "notification-requests";
}

/// <summary>Marker for events published by the API and consumed by the Worker.</summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

// P1 placeholders — shape per §3.4; fleshed out when each module lands. Topics are not yet provisioned (lean DEV).
public sealed record RotationStatusChanged(
    Guid EventId, DateTimeOffset OccurredAt, Guid RotationId, string FromStatus, string ToStatus) : IIntegrationEvent;

public sealed record PaymentEventReceived(
    Guid EventId, DateTimeOffset OccurredAt, string StripeEventId, string Kind) : IIntegrationEvent;

public sealed record DocumentValidated(
    Guid EventId, DateTimeOffset OccurredAt, Guid DocumentId, string Status) : IIntegrationEvent;

public sealed record NotificationRequested(
    Guid EventId, DateTimeOffset OccurredAt, string Channel, string TemplateKey, string Recipient) : IIntegrationEvent;
