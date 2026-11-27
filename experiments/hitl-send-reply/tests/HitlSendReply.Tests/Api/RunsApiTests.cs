using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using HitlSendReply.Contracts;

namespace HitlSendReply.Tests.Api;

/// <summary>
/// The seven scenarios end to end over HTTP, each started by an inbound email: the person answers in a separate request, and the
/// guarantees are the same as in process. No model key is ever configured here.
/// </summary>
public class RunsApiTests(RunsApiTests.ApiFactory factory) : IClassFixture<RunsApiTests.ApiFactory>
{
    public sealed class ApiFactory : WebApplicationFactory<HitlSendReply.Api.ApiMarker>
    {
        /// <summary>Who plays the agent in the next run; without one, the scripted demo for the ticket.</summary>
        public ScenarioSessionFactory Scenarios { get; } = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.ConfigureServices(services =>
            {
                // Last registration wins: never a real model in tests.
                services.AddSingleton(new LiveModels(null));
                services.AddSingleton<IRunSessionFactory>(sp => Scenarios.Next is null
                    ? new RunSessionFactory(new LiveModels(null))
                    : Scenarios);
            });
    }

    private readonly HttpClient _http = factory.CreateClient();

    // A run starts the way it does in production: an email arrives on the webhook.
    private async Task<RunDto> StartAsync(int number, string lang)
    {
        var scenario = ScenarioCatalog.For(lang == "es" ? Language.Es : Language.En).Single(s => s.Number == number);
        factory.Scenarios.Next = scenario;
        var response = await _http.PostAsJsonAsync("/api/inbound-emails", new InboundEmailRequest(
            Guid.NewGuid().ToString("n"), scenario.Ticket.CustomerAddress, "Support", scenario.CustomerMessage, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var runId = (await response.Content.ReadFromJsonAsync<InboundEmailResult>())!.RunId;

        RunDto? run = null;
        for (var i = 0; i < 100 && run?.Status is null or RunStatus.Starting; i++)
        {
            run = await _http.GetFromJsonAsync<RunDto>($"/api/runs/{runId}");
            await Task.Delay(10);
        }

        return run!;
    }

    private async Task<HttpResponseMessage> ReviewAsync(RunDto run, string callId, bool approved, string? reason = null) =>
        await _http.PostAsJsonAsync($"/api/runs/{run.RunId}/reviews/{callId}", new ReviewRequest(approved, reason, "reviewer@test"));

    [Theory]
    [InlineData(1, "en")] [InlineData(2, "en")] [InlineData(3, "en")] [InlineData(4, "en")] [InlineData(5, "en")] [InlineData(6, "en")] [InlineData(7, "en")]
    [InlineData(1, "es")] [InlineData(2, "es")] [InlineData(3, "es")] [InlineData(4, "es")] [InlineData(5, "es")] [InlineData(6, "es")] [InlineData(7, "es")]
    public async Task Each_scenario_reaches_its_expected_first_decision_and_only_reviewed_text_is_sent(int number, string lang)
    {
        var scenario = ScenarioCatalog.For(lang == "es" ? Language.Es : Language.En).Single(s => s.Number == number);

        var run = await StartAsync(number, lang);

        switch (scenario.ExpectedFirstDecision.ToString())
        {
            case "AutoSend":
                Assert.Equal(RunStatus.Completed, run.Status);
                Assert.Contains(run.Audit, a => a.Event == "sent" && a.Actor == "policy");
                break;
            case "Block":
                Assert.Equal(RunStatus.Completed, run.Status);
                Assert.DoesNotContain(run.Audit, a => a.Event == "sent" && a.CallId != "earlier-run");
                break;
            default:
                Assert.Equal(RunStatus.AwaitingReview, run.Status);
                var review = Assert.Single(run.PendingReviews);
                Assert.Equal("NeedsHuman", review.Decision.Outcome);
                Assert.Empty(run.Sent);

                var reviewed = review.Text;
                var response = await ReviewAsync(run, review.CallId, approved: true);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                run = (await response.Content.ReadFromJsonAsync<RunDto>())!;

                Assert.Equal(RunStatus.Completed, run.Status);
                Assert.All(run.Sent, s => Assert.Equal(reviewed, s.Text));
                break;
        }

        if (scenario.TamperedText.Length > 0)
        {
            Assert.DoesNotContain(run.Sent, s => s.Text == scenario.TamperedText);
        }
    }

    [Fact]
    public async Task A_rejection_sends_nothing_and_the_reason_goes_back_to_the_agent()
    {
        var run = await StartAsync(2, "en");

        var response = await ReviewAsync(run, run.PendingReviews[0].CallId, approved: false, "Do not promise refunds.");
        run = (await response.Content.ReadFromJsonAsync<RunDto>())!;

        Assert.Empty(run.Sent);
        Assert.Contains(run.Audit, a => a.Event == "human-rejected" && a.Reason == "Do not promise refunds.");
    }

    [Fact]
    public async Task An_edited_reply_is_sent_as_the_reviewer_wrote_it()
    {
        const string mine = "Your order #A-1077 was delivered on November 7. A teammate will review the damage report and write back.";
        var run = await StartAsync(2, "en");

        var response = await _http.PostAsJsonAsync($"/api/runs/{run.RunId}/reviews/{run.PendingReviews[0].CallId}",
            new ReviewRequest(true, null, "reviewer@test", mine));
        run = (await response.Content.ReadFromJsonAsync<RunDto>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mine, Assert.Single(run.Sent).Text);
        Assert.Contains(run.Audit, a => a.Event == "human-edited" && a.Actor == "reviewer@test");
    }

    [Fact]
    public async Task An_edited_reply_with_a_wrong_order_is_refused_and_the_review_stays_open()
    {
        var run = await StartAsync(2, "en");
        var callId = run.PendingReviews[0].CallId;

        var response = await _http.PostAsJsonAsync($"/api/runs/{run.RunId}/reviews/{callId}",
            new ReviewRequest(true, null, "reviewer@test", "Your order #A-9999 was delivered on November 7."));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var state = await _http.GetFromJsonAsync<RunDto>($"/api/runs/{run.RunId}");
        Assert.Equal(callId, Assert.Single(state!.PendingReviews).CallId);
        Assert.Empty(state.Sent);
    }

    [Fact]
    public async Task A_rejection_without_a_reason_is_refused()
    {
        var run = await StartAsync(2, "en");

        var response = await ReviewAsync(run, run.PendingReviews[0].CallId, approved: false, reason: " ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_review_answered_twice_is_a_conflict_and_sends_once()
    {
        var run = await StartAsync(2, "en");
        var callId = run.PendingReviews[0].CallId;
        await ReviewAsync(run, callId, approved: true);

        var second = await ReviewAsync(run, callId, approved: true);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var state = await _http.GetFromJsonAsync<RunDto>($"/api/runs/{run.RunId}");
        Assert.Single(state!.Sent);
    }

    [Fact]
    public async Task An_unknown_run_is_not_found()
    {
        var response = await _http.GetAsync("/api/runs/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_catalog_lists_five_tickets_per_language()
    {
        var tickets = await _http.GetFromJsonAsync<List<TicketDto>>("/api/tickets?lang=es");

        Assert.Equal(5, tickets!.Count);
        Assert.All(tickets, t => Assert.Equal("es", t.Language));
    }
}
