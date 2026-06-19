using System.Net;
using System.Net.Http.Json;
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
/// Admin refunds: POST /api/rotations/{id}/refund refunds every captured payment through the gateway and
/// moves the booking to Refunded — but only from a refundable state (Cancelled/Completed) and only when
/// there is a captured payment. Also pins that a direct edit to Refunded via the admin PUT is rejected.
/// </summary>
public class PaymentRefundEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
{
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

    private HttpClient Customer(string oid)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Oid", oid);
        client.DefaultRequestHeaders.Add("X-Test-Roles", RoleNames.Student);
        return client;
    }

    private static string UniqueOid() => $"ciam-{Guid.NewGuid():N}";

    private async Task<Guid> CreateStudentAsync(HttpClient admin, string oid)
    {
        var student = await (await admin.PostAsJsonAsync("/api/students",
                new CreateStudentRequest("Ref", "Student", $"ref.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, Guid studentId)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(ProgramId, studentId,
                    new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 30), RotationStatus.Pending), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }

    /// <summary>Books a rotation and pays its deposit (via the dev simulator) so it has a captured
    /// payment. Returns the rotation id.</summary>
    private async Task<Guid> PaidRotationAsync()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId);

        var customer = Customer(oid);
        var paymentId = (await (await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null))
            .Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        await customer.PostAsJsonAsync($"/api/dev/payments/{paymentId}/simulate", new { outcome = "succeeded" }, JsonOptions);
        return rotationId;
    }

    private async Task SetRotationStatusAsync(Guid rotationId, RotationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        var rotation = await db.Rotations.FirstAsync(r => r.Id == rotationId);
        rotation.Status = status;
        await db.SaveChangesAsync();
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

    [Fact]
    public async Task Refunding_a_cancelled_paid_rotation_refunds_the_payment_and_marks_it_refunded()
    {
        var rotationId = await PaidRotationAsync();
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled); // admin cancelled the paid booking

        var response = await Staff(RoleNames.Admin).PostAsync($"/api/rotations/{rotationId}/refund", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RefundResponse>(JsonOptions);
        result!.Status.Should().Be(RotationStatus.Refunded);
        result.PaymentsRefunded.Should().Be(1);

        var payment = await GetPaymentAsync(rotationId);
        payment!.Status.Should().Be(PaymentStatus.Refunded);
        payment.ProviderRefundId.Should().NotBeNullOrEmpty();
        (await GetRotationStatusAsync(rotationId)).Should().Be(RotationStatus.Refunded);
    }

    [Fact]
    public async Task Refunding_a_rotation_that_is_not_in_a_refundable_state_is_rejected()
    {
        // Paid but still approved (NotStarted) — Refunded isn't a legal transition from there.
        var rotationId = await PaidRotationAsync();

        var response = await Staff(RoleNames.Admin).PostAsync($"/api/rotations/{rotationId}/refund", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Succeeded); // untouched
    }

    [Fact]
    public async Task Refunding_a_cancelled_rotation_with_no_captured_payment_is_rejected()
    {
        var admin = Staff(RoleNames.Admin);
        var studentId = await CreateStudentAsync(admin, UniqueOid());
        var rotationId = await CreateRotationAsync(admin, studentId);
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled); // cancelled, but never paid

        var response = await admin.PostAsync($"/api/rotations/{rotationId}/refund", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Refunding_twice_is_rejected_the_second_time()
    {
        var rotationId = await PaidRotationAsync();
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled);
        var admin = Staff(RoleNames.Admin);

        var first = await admin.PostAsync($"/api/rotations/{rotationId}/refund", null);
        var second = await admin.PostAsync($"/api/rotations/{rotationId}/refund", null);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict); // Refunded is terminal
    }

    [Fact]
    public async Task Two_concurrent_refunds_refund_exactly_once()
    {
        var rotationId = await PaidRotationAsync();
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled);
        var admin = Staff(RoleNames.Admin);

        // Race two refunds: the conditional-claim UPDATE serialises them, so exactly one wins and the
        // money-out (RefundAsync) runs once — never a double refund.
        var a = admin.PostAsync($"/api/rotations/{rotationId}/refund", null);
        var b = admin.PostAsync($"/api/rotations/{rotationId}/refund", null);
        var responses = await Task.WhenAll(a, b);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        var payment = await GetPaymentAsync(rotationId);
        payment!.Status.Should().Be(PaymentStatus.Refunded);
        payment.ProviderRefundId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_completed_paid_rotation_can_be_refunded()
    {
        var rotationId = await PaidRotationAsync();
        // Approved → Active → Completed (the other legal entry edge into Refunded).
        await SetRotationStatusAsync(rotationId, RotationStatus.Completed);

        var response = await Staff(RoleNames.Admin).PostAsync($"/api/rotations/{rotationId}/refund", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetRotationStatusAsync(rotationId)).Should().Be(RotationStatus.Refunded);
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task A_direct_edit_to_refunded_via_the_admin_put_is_rejected()
    {
        var rotationId = await PaidRotationAsync();
        await SetRotationStatusAsync(rotationId, RotationStatus.Cancelled);
        var admin = Staff(RoleNames.Admin);

        // Fetch the editable shape, then try to PUT a status-only move to Refunded.
        var detail = await admin.GetFromJsonAsync<RotationDetailResponse>($"/api/rotations/{rotationId}", JsonOptions);
        var put = await admin.PutAsJsonAsync($"/api/rotations/{rotationId}",
            new UpdateRotationRequest(detail!.ProgramId, detail.StudentId!.Value, detail.StartDate, detail.EndDate, RotationStatus.Refunded),
            JsonOptions);

        put.StatusCode.Should().Be(HttpStatusCode.BadRequest); // must use the refund action
        (await GetRotationStatusAsync(rotationId)).Should().Be(RotationStatus.Cancelled); // unchanged
        (await GetPaymentAsync(rotationId))!.Status.Should().Be(PaymentStatus.Succeeded); // no money moved
    }
}
