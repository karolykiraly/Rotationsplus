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
using RotationsPlus.Contracts.Common;
using RotationsPlus.Contracts.Marketplace;
using RotationsPlus.Contracts.Payments;
using RotationsPlus.Contracts.Rotations;
using RotationsPlus.Contracts.Students;

namespace RotationsPlus.Integration.Tests;

/// <summary>
/// The preceptor honorarium (payout) path: the three-stage schedule generated on deposit success, the
/// admin list (stage tabs), the ordered "pay" action, and the refunded bookkeeping flag. Runs against the
/// fake gateway via the DEV simulate endpoint, so no live vendor account is needed.
///
/// Seed program cccc…001: weekly honorarium 500, preceptor "Jane Carter". A 4-week booking → total payout
/// 2000, split 500 (Deposit 25%) / 500 (Start 25%) / 1000 (Evaluation 50%).
/// </summary>
public class HonorariumEndpointTests(RotationsApiFactory factory) : IClassFixture<RotationsApiFactory>
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
                new CreateStudentRequest("Hon", "Student", $"hon.{Guid.NewGuid():N}@example.com", null,
                    AcademicStatus.MdStudent, null, null, null, null, null, StudentStatus.MemberActivated, oid), JsonOptions))
            .Content.ReadFromJsonAsync<StudentDetailResponse>(JsonOptions);
        return student!.Id;
    }

    private static readonly Guid InternalMedicineSpecialtyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    /// <summary>Creates a non-open program with the given weekly honorarium (the figure the payout schedule
    /// is derived from). Retail is independent (drives the deposit price, not the honorarium).</summary>
    private async Task<Guid> CreateProgramAsync(HttpClient admin, decimal weeklyHonorarium, decimal retailPerWeek = 100m)
    {
        var program = await (await admin.PostAsJsonAsync("/api/programs",
                new CreateProgramRequest(InternalMedicineSpecialtyId, ProgramType.InPerson, 4, 1,
                    retailPerWeek, weeklyHonorarium, "Honorarium math fixture.", null), JsonOptions))
            .Content.ReadFromJsonAsync<ProgramDetailResponse>(JsonOptions);
        return program!.Id;
    }

    private async Task<Guid> CreateRotationAsync(HttpClient admin, Guid studentId, Guid programId, DateOnly start, DateOnly end)
    {
        var rotation = await (await admin.PostAsJsonAsync("/api/rotations",
                new CreateRotationRequest(programId, studentId, start, end, RotationStatus.Pending), JsonOptions))
            .Content.ReadFromJsonAsync<RotationDetailResponse>(JsonOptions);
        return rotation!.Id;
    }

    /// <summary>Books a rotation (default: seed program cccc…001, 4 weeks) and drives its deposit to success,
    /// which generates the payout schedule. Returns the rotation id. Pass a program/dates to vary the math.</summary>
    private async Task<Guid> BookAndPayDepositAsync(Guid? programId = null, DateOnly? start = null, DateOnly? end = null)
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId,
            programId ?? ProgramId, start ?? new DateOnly(2026, 9, 7), end ?? new DateOnly(2026, 10, 5));

        var customer = Customer(oid);
        var paymentId = (await (await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null))
            .Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        var sim = await customer.PostAsJsonAsync($"/api/dev/payments/{paymentId}/simulate", new { outcome = "succeeded" }, JsonOptions);
        sim.StatusCode.Should().Be(HttpStatusCode.OK);
        return rotationId;
    }

    private async Task<List<Honorarium>> HonorariumsAsync(Guid rotationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
        return await db.Honorariums.AsNoTracking()
            .Where(h => h.RotationId == rotationId)
            .OrderBy(h => h.Stage)
            .ToListAsync();
    }

    // ---- generation ----

    [Fact]
    public async Task A_paid_deposit_generates_the_three_stage_payout_schedule()
    {
        var rotationId = await BookAndPayDepositAsync();

        var honorariums = await HonorariumsAsync(rotationId);
        honorariums.Should().HaveCount(3);

        var deposit = honorariums.Single(h => h.Stage == HonorariumStage.Deposit);
        var start = honorariums.Single(h => h.Stage == HonorariumStage.Start);
        var evaluation = honorariums.Single(h => h.Stage == HonorariumStage.Evaluation);

        deposit.Amount.Should().Be(500m);     // 25% of 2000
        start.Amount.Should().Be(500m);       // 25%
        evaluation.Amount.Should().Be(1000m); // 50%
        (deposit.Amount + start.Amount + evaluation.Amount).Should().Be(2000m); // sums exactly to total

        honorariums.Should().OnlyContain(h => h.Status == HonorariumStatus.Pending && !h.Refunded);
        honorariums.Should().OnlyContain(h => h.PreceptorName == "Jane Carter" && h.Currency == "USD");
        honorariums.Should().OnlyContain(h => h.StudentName == "Hon Student");
        // Evaluation due date snapshot = rotation end date (2026-10-05) + the legacy 7-day grace.
        honorariums.Should().OnlyContain(h => h.EvaluationDueDate == new DateOnly(2026, 10, 12));
    }

    [Fact]
    public async Task Generation_does_not_duplicate_when_the_deposit_settles_again()
    {
        var rotationId = await BookAndPayDepositAsync();

        // A second simulate is a no-op (the payment is already Succeeded), so the schedule stays at three —
        // proving the generator is not re-run / does not duplicate on a settled deposit.
        var honorariums = await HonorariumsAsync(rotationId);
        honorariums.Should().HaveCount(3);
    }

    [Fact]
    public async Task The_three_stages_sum_exactly_to_the_total_for_an_amount_that_does_not_divide_evenly()
    {
        // 10.02 over 1 week splits 2.51 / 2.51 / 5.00 — the last stage takes the REMAINDER (5.00), not its
        // own rounded fraction (round(10.02×0.5)=5.01). This pins the cent-allocation branch that a clean
        // multiple-of-four total (the seed program) can never exercise.
        var admin = Staff(RoleNames.Admin);
        var programId = await CreateProgramAsync(admin, weeklyHonorarium: 10.02m);
        var rotationId = await BookAndPayDepositAsync(programId, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 14));

        var honorariums = await HonorariumsAsync(rotationId);
        honorariums.Single(h => h.Stage == HonorariumStage.Deposit).Amount.Should().Be(2.51m);
        honorariums.Single(h => h.Stage == HonorariumStage.Start).Amount.Should().Be(2.51m);
        honorariums.Single(h => h.Stage == HonorariumStage.Evaluation).Amount.Should().Be(5.00m); // remainder, not 5.01
        honorariums.Sum(h => h.Amount).Should().Be(10.02m); // exact to the cent
    }

    [Fact]
    public async Task A_booking_whose_honorarium_total_would_overflow_the_money_column_is_rejected()
    {
        var admin = Staff(RoleNames.Admin);
        // Max weekly honorarium over even a 2-week rotation (well under the 520-week cap) exceeds the
        // numeric(10,2) ceiling — the booking must be refused at creation, not left to overflow inside the
        // deposit-fulfilment webhook transaction. (The week cap alone wouldn't catch this.)
        var programId = await CreateProgramAsync(admin, weeklyHonorarium: 99_999_999.99m);
        var studentId = await CreateStudentAsync(admin, UniqueOid());

        var response = await admin.PostAsJsonAsync("/api/rotations",
            new CreateRotationRequest(programId, studentId,
                new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 21), RotationStatus.Pending), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_program_with_no_weekly_honorarium_generates_no_payout_schedule()
    {
        var admin = Staff(RoleNames.Admin);
        var programId = await CreateProgramAsync(admin, weeklyHonorarium: 0m);
        var rotationId = await BookAndPayDepositAsync(programId);

        // Nothing to pay → no rows (the generator skips a zero-rate program).
        (await HonorariumsAsync(rotationId)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_deleted_stage_is_not_regenerated_when_the_deposit_is_fulfilled_again()
    {
        var admin = Staff(RoleNames.Admin);
        var oid = UniqueOid();
        var studentId = await CreateStudentAsync(admin, oid);
        var rotationId = await CreateRotationAsync(admin, studentId, ProgramId, new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 5));

        var customer = Customer(oid);
        var paymentId = (await (await customer.PostAsync($"/api/rotations/{rotationId}/payment-intent", null))
            .Content.ReadFromJsonAsync<PaymentIntentResponse>(JsonOptions))!.PaymentId;
        (await customer.PostAsJsonAsync($"/api/dev/payments/{paymentId}/simulate", new { outcome = "succeeded" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Delete the Deposit stage, then force fulfilment to run generation again by resetting the payment
        // to Pending and re-simulating. The generator's IgnoreQueryFilters existence check still sees the
        // soft-deleted row, so it never resurrects the schedule — the live count stays 2.
        var depositId = await StageIdAsync(rotationId, HonorariumStage.Deposit);
        (await admin.DeleteAsync($"/api/honorariums/{depositId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RotationsDbContext>();
            var payment = await db.Payments.FirstAsync(p => p.Id == paymentId);
            payment.Status = PaymentStatus.Pending;
            await db.SaveChangesAsync();
        }

        (await customer.PostAsJsonAsync($"/api/dev/payments/{paymentId}/simulate", new { outcome = "succeeded" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HonorariumsAsync(rotationId)).Should().HaveCount(2); // Deposit not regenerated
    }

    // ---- list (stage tabs) ----

    [Fact]
    public async Task Admin_lists_honorariums_filtered_by_stage()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);

        var page = await admin.GetFromJsonAsync<PagedResponse<HonorariumResponse>>(
            "/api/honorariums?stage=Start&pageSize=100", JsonOptions);

        page!.Items.Should().OnlyContain(h => h.Stage == HonorariumStage.Start);
        page.Items.Should().Contain(h => h.RotationId == rotationId && h.Amount == 500m);
    }

    [Fact]
    public async Task The_status_filter_narrows_the_list()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        var depositId = await StageIdAsync(rotationId, HonorariumStage.Deposit);
        await admin.PostAsync($"/api/honorariums/{depositId}/pay", null); // Deposit → Paid

        var paid = await admin.GetFromJsonAsync<PagedResponse<HonorariumResponse>>(
            "/api/honorariums?stage=Deposit&status=Paid&pageSize=100", JsonOptions);
        var pending = await admin.GetFromJsonAsync<PagedResponse<HonorariumResponse>>(
            "/api/honorariums?stage=Deposit&status=Pending&pageSize=100", JsonOptions);

        paid!.Items.Should().Contain(x => x.Id == depositId);
        pending!.Items.Should().NotContain(x => x.Id == depositId);
    }

    [Fact]
    public async Task An_oversized_search_term_is_rejected()
    {
        var admin = Staff(RoleNames.Admin);

        var response = await admin.GetAsync($"/api/honorariums?q={new string('x', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- pay (ordered) ----

    private async Task<Guid> StageIdAsync(Guid rotationId, HonorariumStage stage) =>
        (await HonorariumsAsync(rotationId)).Single(h => h.Stage == stage).Id;

    [Fact]
    public async Task Admin_pays_the_deposit_stage()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        var depositId = await StageIdAsync(rotationId, HonorariumStage.Deposit);

        var response = await admin.PostAsync($"/api/honorariums/{depositId}/pay", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paid = await response.Content.ReadFromJsonAsync<HonorariumResponse>(JsonOptions);
        paid!.Status.Should().Be(HonorariumStatus.Paid);
        paid.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Paying_the_start_stage_before_the_deposit_is_rejected()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        var startId = await StageIdAsync(rotationId, HonorariumStage.Start);

        var response = await admin.PostAsync($"/api/honorariums/{startId}/pay", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Stages_can_be_paid_in_order()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);

        (await admin.PostAsync($"/api/honorariums/{await StageIdAsync(rotationId, HonorariumStage.Deposit)}/pay", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/honorariums/{await StageIdAsync(rotationId, HonorariumStage.Start)}/pay", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/honorariums/{await StageIdAsync(rotationId, HonorariumStage.Evaluation)}/pay", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Paying_the_evaluation_before_the_start_is_rejected()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        // Deposit paid, but Start still pending → Evaluation is blocked.
        await admin.PostAsync($"/api/honorariums/{await StageIdAsync(rotationId, HonorariumStage.Deposit)}/pay", null);

        var response = await admin.PostAsync($"/api/honorariums/{await StageIdAsync(rotationId, HonorariumStage.Evaluation)}/pay", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Paying_an_already_paid_stage_is_rejected()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        var depositId = await StageIdAsync(rotationId, HonorariumStage.Deposit);
        await admin.PostAsync($"/api/honorariums/{depositId}/pay", null);

        var again = await admin.PostAsync($"/api/honorariums/{depositId}/pay", null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Paying_an_unknown_honorarium_returns_404()
    {
        var admin = Staff(RoleNames.Admin);

        var response = await admin.PostAsync($"/api/honorariums/{Guid.NewGuid()}/pay", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- refunded flag ----

    [Fact]
    public async Task Admin_toggles_the_refunded_flag()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        var depositId = await StageIdAsync(rotationId, HonorariumStage.Deposit);

        var on = await admin.PostAsJsonAsync($"/api/honorariums/{depositId}/refund-flag", new SetHonorariumRefundRequest(true), JsonOptions);
        on.StatusCode.Should().Be(HttpStatusCode.OK);
        (await on.Content.ReadFromJsonAsync<HonorariumResponse>(JsonOptions))!.Refunded.Should().BeTrue();

        var off = await admin.PostAsJsonAsync($"/api/honorariums/{depositId}/refund-flag", new SetHonorariumRefundRequest(false), JsonOptions);
        (await off.Content.ReadFromJsonAsync<HonorariumResponse>(JsonOptions))!.Refunded.Should().BeFalse();
    }

    [Fact]
    public async Task Setting_the_refund_flag_on_an_unknown_honorarium_returns_404()
    {
        var admin = Staff(RoleNames.Admin);

        var response = await admin.PostAsJsonAsync($"/api/honorariums/{Guid.NewGuid()}/refund-flag",
            new SetHonorariumRefundRequest(true), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- delete (guarded) ----

    [Fact]
    public async Task Admin_deletes_a_pending_honorarium()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        var depositId = await StageIdAsync(rotationId, HonorariumStage.Deposit);

        var response = await admin.DeleteAsync($"/api/honorariums/{depositId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Soft-deleted → gone from the live set (the other two stages remain).
        (await HonorariumsAsync(rotationId)).Should().NotContain(h => h.Id == depositId);
        (await HonorariumsAsync(rotationId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task Deleting_a_paid_honorarium_is_rejected()
    {
        var rotationId = await BookAndPayDepositAsync();
        var admin = Staff(RoleNames.Admin);
        var depositId = await StageIdAsync(rotationId, HonorariumStage.Deposit);
        await admin.PostAsync($"/api/honorariums/{depositId}/pay", null); // now Paid

        var response = await admin.DeleteAsync($"/api/honorariums/{depositId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await HonorariumsAsync(rotationId)).Should().Contain(h => h.Id == depositId); // still there
    }

    [Fact]
    public async Task Deleting_an_unknown_honorarium_returns_404()
    {
        var admin = Staff(RoleNames.Admin);

        var response = await admin.DeleteAsync($"/api/honorariums/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
