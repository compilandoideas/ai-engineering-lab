using System.Net;
using System.Net.Http.Json;
using LabExperiment.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LabExperiment.Tests;

// Sample tests: delete them with the Notes feature. They check the HTTP contract: status codes and ProblemDetails.
public class NotesApiTests(WebApplicationFactory<ApiMarker> factory) : IClassFixture<WebApplicationFactory<ApiMarker>>
{
    private sealed record Created(Guid Id);

    private sealed record NoteDto(Guid Id, string Text);

    [Fact]
    public async Task Post_creates_the_note_and_get_returns_it()
    {
        var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/notes", new { text = "hello" });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<Created>();

        var note = await client.GetFromJsonAsync<NoteDto>(post.Headers.Location);
        Assert.Equal(created!.Id, note!.Id);
        Assert.Equal("hello", note.Text);
    }

    [Fact]
    public async Task An_empty_note_is_a_400_problem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/notes", new { text = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("General.Validation", problem!.Title);
    }

    [Fact]
    public async Task A_missing_note_is_a_404_problem()
    {
        var response = await factory.CreateClient().GetAsync($"/api/notes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
