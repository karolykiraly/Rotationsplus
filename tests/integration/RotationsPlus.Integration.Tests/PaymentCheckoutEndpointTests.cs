using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RotationsPlus.Api.Infrastructure;
using RotationsPlus.Api.Modules.Payments;
using RotationsPlus.Common.Authorization;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// The deposit money path: POST /api/rotations/{id}/payment-intent (customer opens a deposit for their
/// own rotation) and POST /api/webhooks/stripe (the signature-verified, idempotent fulfilment). Runs
/// against the fake gateway, so no live vendor account is needed.
/// </summary>
public class PaymentCheckoutEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
    // Non-open program: 1500/wk, min 4 weeks. A 4-week booking → total 6000, deposit 600, outstanding 5400.
    private static readonly Guid ProgramId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Staff(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", $"oid-{role}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private HttpClient Customer(string oid, string role = RoleNames.Student)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", oid);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        return client;
    }

    private static string UniqueOid() => $"ciam-{Guid.NewGuid():N}";

    private async Task<Guid> CreateStudentAsync(HttpClient admin, string oid)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Pay", "Student", $"pay.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, Guid studentId, RotationStatus status)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(ProgramId, studentId,
                    new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5), status), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }

    /// <summary>Creates an oid-linked student + a Pending rotation and returns (oid, rotationId).</summary>
    private async Task<(string Oid, Guid RotationId)> BookAsync(RotationStatus status = RotationStatus.Pending)
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId, status);
        return (oid, rotationId);
    }

    private async Task<Payment?> GetPaymentAsync(Guid rotationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.RotationId == rotationId);
    }

    private async Task<RotationStatus> GetRotationStatusAsync(Guid rotationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.Rotations.AsNoTracking().Where(r => r.Id == rotationId).Select(r => r.Status).FirstAsync();
    }

    /// <summary>Flips a rotation's status directly in the DB to set up a non-Pending state (an admin
    /// cancelling/approving out of band), bypassing the state-machine the API endpoints enforce.</summary>
    private async Task SetRotationStatusAsync(Guid rotationId, RotationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var rotation = await db.Rotations.FirstAsync(r => r.Id == rotationId);
        rotation.Status = status;
        await db.SaveChangesAsync();
    }

    private static string WebhookPayload(string eventId, string type, string intentId) =>
        JsonSerializer.Serialize(new { id = eventId, type, data = new { @object = new { id = intentId } } });

    private async Task<HttpResponseMessage> PostWebhookAsync(string eventId, string type, string intentId)
    {
        var payload = WebhookPayload(eventId, type, intentId);
        var signature = FakePaymentGateway.Sign(payload, PaymentsOptions.DefaultWebhookSecret);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", signature);
        return await factory.CreateClient().SendAsync(request);
    }

    // ---- payment-intent ----

    [Fact]
    public async Task Customer_opens_a_deposit_for_their_own_rotation()
    {
        var (oid, rotationId) = await BookAsync();

        var response = await Customer(oid).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var intent = await response.Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions);
        intent!.Amount.Should().Be(600m);           // 10% of 6000
        intent.TotalAmount.Should().Be(6000m);
        intent.OutstandingAmount.Should().Be(5400m);
        intent.Currency.Should().Be("USD");
        intent.Status.Should().Be(PaymentStatus.Pending);
        intent.ClientSecret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_customer_cannot_open_a_deposit_for_someone_elses_rotation()
    {
        var (_, rotationId) = await BookAsync();

        // A different signed-in student → indistinguishable from a missing rotation.
        var response = await Customer(UniqueOid()).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Opening_a_deposit_twice_reuses_the_single_pending_payment()
    {
        var (oid, rotationId) = await BookAsync();
        var customer = Customer(oid);

        var first = await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var second = await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.OK); // reused, not a second charge
        var firstId = (await first.Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        var secondId = (await second.Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        secondId.Should().Be(firstId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        (await db.Payments.CountAsync(p => p.RotationId == rotationId)).Should().Be(1);
    }

    [Fact]
    public async Task Staff_cannot_open_a_customer_deposit()
    {
        var (_, rotationId) = await BookAsync();

        var response = await Staff(RoleNames.Admin).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_cannot_open_a_deposit()
    {
        var (_, rotationId) = await BookAsync();

        var response = await factory.CreateClient().PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- webhook fulfilment ----

    [Fact]
    public async Task A_succeeded_webhook_marks_the_deposit_paid_and_approves_the_rotation()
    {
        var (oid, rotationId) = await BookAsync(RotationStatus.Pending);
        await Customer(oid).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var intentId = (await GetPaymentAsync(rotationId))!.ProviderPaymentIntentId!;

        var webhook = await PostWebhookAsync($"evt_{Guid.NewGuid():N}", PaymentWebhookEventTypes.PaymentSucceeded, intentId);

        webhook.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Succeeded);
        (await GetRotationStatusAsync(rotationId)).Should().Be(RotationStatus.NotStarted); // "Approved"
    }

    [Fact]
    public async Task A_redelivered_succeeded_webhook_is_idempotent()
    {
        var (oid, rotationId) = await BookAsync(RotationStatus.Pending);
        await Customer(oid).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var intentId = (await GetPaymentAsync(rotationId))!.ProviderPaymentIntentId!;
        var eventId = $"evt_{Guid.NewGuid():N}";

        var first = await PostWebhookAsync(eventId, PaymentWebhookEventTypes.PaymentSucceeded, intentId);
        var second = await PostWebhookAsync(eventId, PaymentWebhookEventTypes.PaymentSucceeded, intentId);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Succeeded);
        (await GetRotationStatusAsync(rotationId)).Should().Be(RotationStatus.NotStarted);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        (await db.ProcessedWebhookEvents.CountAsync(e => e.Id == eventId)).Should().Be(1);
    }

    [Fact]
    public async Task A_webhook_with_a_bad_signature_is_rejected_and_does_not_fulfil()
    {
        var (oid, rotationId) = await BookAsync(RotationStatus.Pending);
        await Customer(oid).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var intentId = (await GetPaymentAsync(rotationId))!.ProviderPaymentIntentId!;

        var payload = WebhookPayload("evt_x", PaymentWebhookEventTypes.PaymentSucceeded, intentId);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "forged-signature");
        var response = await factory.CreateClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Pending); // untouched
    }

    [Fact]
    public async Task A_failed_webhook_marks_the_deposit_failed_and_leaves_the_rotation_pending()
    {
        var (oid, rotationId) = await BookAsync(RotationStatus.Pending);
        await Customer(oid).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var intentId = (await GetPaymentAsync(rotationId))!.ProviderPaymentIntentId!;

        var webhook = await PostWebhookAsync($"evt_{Guid.NewGuid():N}", PaymentWebhookEventTypes.PaymentFailed, intentId);

        webhook.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Failed);
        (await GetRotationStatusAsync(rotationId)).Should().Be(RotationStatus.Pending);
    }

    [Fact]
    public async Task A_webhook_for_an_unknown_intent_is_acknowledged_without_effect()
    {
        var response = await PostWebhookAsync($"evt_{Guid.NewGuid():N}", PaymentWebhookEventTypes.PaymentSucceeded, "pi_does_not_exist");

        response.StatusCode.Should().Be(HttpStatusCode.OK); // ack so the provider stops retrying
    }

    // ---- concurrency & eligibility guards ----

    [Fact]
    public async Task Two_concurrent_opens_create_exactly_one_payment()
    {
        var (oid, rotationId) = await BookAsync();

        // Two parallel "open a deposit" calls race for the rotation's single active-deposit slot. The
        // unique index lets only one row in; the loser must fall back to the existing intent, never a
        // second charge. Both succeed (2xx) and there is exactly one payment row.
        var customer = Customer(oid);
        var a = customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var b = customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var responses = await Task.WhenAll(a, b);

        responses.Should().OnlyContain(r => r.IsSuccessStatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        (await db.Payments.CountAsync(p => p.RotationId == rotationId)).Should().Be(1);
    }

    [Fact]
    public async Task A_deposit_cannot_be_opened_for_a_rotation_that_is_not_pending()
    {
        var (oid, rotationId) = await BookAsync();
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled); // no longer awaiting a deposit

        var response = await Customer(oid).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetPaymentAsync(rotationId)).Should().BeNull(); // no money path opened
    }

    [Fact]
    public async Task A_preceptor_cannot_open_a_deposit()
    {
        var (_, rotationId) = await BookAsync();

        // A Preceptor passes CustomerOnly but owns no student, so the ownership match fails → 404
        // (indistinguishable from a missing rotation, same as any non-owner).
        var response = await Customer(UniqueOid(), RoleNames.Preceptor)
            .PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Opening_a_deposit_after_it_is_already_paid_is_rejected()
    {
        var (oid, rotationId) = await BookAsync(RotationStatus.Pending);
        var customer = Customer(oid);
        await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var intentId = (await GetPaymentAsync(rotationId))!.ProviderPaymentIntentId!;
        await PostWebhookAsync($"evt_{Guid.NewGuid():N}", PaymentWebhookEventTypes.PaymentSucceeded, intentId);

        // The deposit is final; reopening returns 409 rather than a fresh intent.
        var response = await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_succeeded_webhook_for_a_cancelled_rotation_records_payment_but_leaves_it_cancelled()
    {
        var (oid, rotationId) = await BookAsync(RotationStatus.Pending);
        await Customer(oid).PostAsync($"/api/rotations/{rotationId}/payment-intent", null);
        var intentId = (await GetPaymentAsync(rotationId))!.ProviderPaymentIntentId!;
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled); // admin cancels before the deposit lands

        var webhook = await PostWebhookAsync($"evt_{Guid.NewGuid():N}", PaymentWebhookEventTypes.PaymentSucceeded, intentId);

        // The payment is recorded as Succeeded (money changed hands — never black-holed), but the
        // illegal Cancelled→NotStarted transition is refused, leaving a paid-but-cancelled row for a
        // manual refund rather than silently re-approving a cancelled booking.
        webhook.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Succeeded);
        (await GetRotationStatusAsync(rotationId)).Should().Be(RotationStatus.Cancelled);
    }
}
