# Service Bus consumers

Event-driven work (consuming `rotation-events`, `payment-events`, `document-events`,
`notification-requests`) lives here. Each consumer follows the SkyLimit `BackgroundService` +
`ServiceBusProcessor` pattern with dead-letter handling (see `Docs/Plan_Architecture.md §3.4`).

Not wired in P1: the lean DEV footprint omits Service Bus until the first event-driven module lands.
At that point: add `Azure.Messaging.ServiceBus`, provision the namespace + topics in Bicep, and register
the consumer hosted services here.
