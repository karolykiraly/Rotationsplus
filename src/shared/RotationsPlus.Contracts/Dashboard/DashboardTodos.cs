namespace RotationsPlus.Contracts.Dashboard;

/// <summary>The admin "ToDo's" tab: the actionable work queues an admin needs to clear — documents
/// awaiting review, rotations whose deposit hasn't been paid, and preceptors awaiting approval. Each
/// bucket carries the full <c>Count</c> (for the tab badge) plus a capped <c>Items</c> preview list
/// (the SPA shows "+N more" / links into the owning screen when <c>Count</c> exceeds the preview).</summary>
public sealed record DashboardTodosResponse(
    TodoBucket<DocumentTodoItem> DocumentsToReview,
    TodoBucket<PaymentTodoItem> AwaitingPayment,
    TodoBucket<PreceptorTodoItem> PreceptorApprovals);

/// <summary>A to-do queue: the total outstanding <paramref name="Count"/> and a capped preview of the
/// oldest/soonest items.</summary>
public sealed record TodoBucket<T>(int Count, IReadOnlyList<T> Items);

/// <summary>A document submitted by a student and awaiting admin review (DocumentStatus.Submitted).</summary>
public sealed record DocumentTodoItem(
    Guid DocumentId,
    Guid RotationId,
    int RotationNumber,
    Guid? StudentId,
    string StudentName,
    string DocumentTypeName,
    DateOnly DueDate,
    DateTimeOffset? SubmittedAtUtc);

/// <summary>A booked rotation whose deposit hasn't been received yet (RotationStatus.Pending).</summary>
public sealed record PaymentTodoItem(
    Guid RotationId,
    int RotationNumber,
    string StudentName,
    string SpecialtyName,
    DateOnly StartDate);

/// <summary>A preceptor awaiting admin approval (PreceptorStatus.Pending).</summary>
public sealed record PreceptorTodoItem(
    Guid PreceptorId,
    string FullName,
    string SpecialtyName,
    string Email,
    DateTimeOffset CreatedAtUtc);
